using System.Data;
using ArsDocendi.Shared.Persistencia;
using Modules.Asistente.Application;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Cuenta cuánta gente cargó cada dato del portal, con el alcance del actor.
/// </summary>
/// <remarks>
/// Es UNA consulta y no una por tabla: son conteos chicos y agrupados, y abrir una
/// transacción por cada uno multiplicaría la latencia de un turno que ya gastó una
/// llamada al modelo.
///
/// El nombre de la tabla se interpola, y por eso <b>sale de un catálogo cerrado</b>
/// —lo que devuelve <see cref="CoberturaDelPortal.TablasQueToca"/>, que sólo puede
/// producir claves de su propio diccionario— y nunca del texto de la consulta ni de
/// nada que venga del modelo. Un identificador no se puede parametrizar.
/// </remarks>
internal sealed class ConsultorDeCobertura(CadenaSoloLectura cadena) : IConsultorDeCobertura
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

        await using var conexion = new NpgsqlConnection(cadena.Valor);
        await conexion.OpenAsync(ct);

        await using var transaccion = await conexion.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, ct);

        await using (var soloLectura = new NpgsqlCommand(
            "SET TRANSACTION READ ONLY", conexion, transaccion))
        {
            await soloLectura.ExecuteNonQueryAsync(ct);
        }

        await using (var fijarActor = new NpgsqlCommand(
            "SELECT set_config('app.asistente_user_id', @actor, true)", conexion, transaccion))
        {
            fijarActor.Parameters.AddWithValue("actor", actor.ToString());
            await fijarActor.ExecuteNonQueryAsync(ct);
        }

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
