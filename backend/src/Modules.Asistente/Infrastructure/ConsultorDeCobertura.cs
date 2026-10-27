using System.Data;
using ArsDocendi.Shared.Persistencia;
using Modules.Asistente.Application;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>Cuenta cuánta gente cargó cada dato del portal, con el alcance del actor.</summary>
internal sealed class ConsultorDeCobertura(AperturaDeLectura apertura) : IConsultorDeCobertura
{
    public async Task<IReadOnlyList<CoberturaDeUnDato>> ObtenerAsync(
        IReadOnlyList<string> tablas, Guid actor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tablas);

        if (tablas.Count == 0)
        {
            return [];
        }

        foreach (var tabla in tablas)
        {
            if (!CoberturaDelPortal.EsDeclarable(tabla))
            {
                throw new InvalidOperationException(
                    $"'{tabla}' no es una tabla de portal declarable. El nombre se "
                    + "interpola en la consulta, así que sólo puede venir del catálogo.");
            }
        }

        await using var conexion = await apertura.AbrirAsync(ct);

        await using var transaccion = await conexion.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, ct);

        await PreambuloDelActor.AplicarAsync(conexion, transaccion, actor, ct);

        var ramas = tablas.Select(tabla =>
            $"""
            SELECT '{tabla}' AS tabla, count(DISTINCT perfil_id) AS con_dato
              FROM portal.{tabla}
            """);

        await using var comando = new NpgsqlCommand(
            $"""
            SELECT c.tabla, c.con_dato, (SELECT count(*) FROM identity.personas)
              FROM ({string.Join("\nUNION ALL\n", ramas)}) c
            """, conexion, transaccion);

        await using var lector = await comando.ExecuteReaderAsync(ct);

        var coberturas = new List<CoberturaDeUnDato>();
        while (await lector.ReadAsync(ct))
        {
            coberturas.Add(new CoberturaDeUnDato(
                lector.GetString(0), lector.GetInt64(1), lector.GetInt64(2)));
        }

        return coberturas;
    }
}
