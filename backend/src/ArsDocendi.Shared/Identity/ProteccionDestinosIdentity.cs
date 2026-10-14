using System.Data;
using System.Data.Common;
using ArsDocendi.Shared.Aplicacion;
using Npgsql;

namespace ArsDocendi.Shared.Identity;

/// <summary>
/// Guard de lectura sobre destinos canónicos, en la misma transacción de la referencia.
/// No abre conexiones, no confirma transacciones y no escribe Identity.
/// El consumidor conserva y libera ambos recursos; debe validar solo referencias nuevas
/// o cambiadas. Leer historia inactiva usa IConsultasIdentity, no este guard.
/// </summary>
public static class ProteccionDestinosIdentity
{
    public static async Task ValidarAsync(
        DbConnection conexion,
        DbTransaction transaccion,
        IReadOnlyCollection<Guid> usuarioIds,
        IReadOnlyCollection<Guid> personaIds,
        CancellationToken ct)
    {
        if (conexion.State != ConnectionState.Open || transaccion.Connection != conexion)
            throw new InvalidOperationException("La protección exige la conexión abierta y transacción de la mutación consumidora.");
        await using var comando = conexion.CreateCommand();
        comando.Transaction = transaccion;
        comando.CommandText = "SELECT identity.validar_destinos_logicos(@usuarios, @personas)";
        var usuarios = comando.CreateParameter();
        usuarios.ParameterName = "usuarios";
        usuarios.Value = usuarioIds.Distinct().Order().ToArray();
        comando.Parameters.Add(usuarios);
        var personas = comando.CreateParameter();
        personas.ParameterName = "personas";
        personas.Value = personaIds.Distinct().Order().ToArray();
        comando.Parameters.Add(personas);
        try
        {
            await comando.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.ForeignKeyViolation
            && error.ConstraintName == "identity_destino_no_elegible")
        {
            throw new ExcepcionAplicacion(TipoErrorAplicacion.Conflicto,
                "identity-destination-conflict", "El destino de la referencia no existe o dejó de estar activo.");
        }
    }
}
