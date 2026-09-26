using ArsDocendi.Shared.Persistencia;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Creates <c>asistente.hilo_historico</c> and <c>asistente.turno_historico</c>
/// on an already-migrated base.
/// </summary>
/// <remarks>
/// Public and takes the connection from the outside, for the same reason as
/// <see cref="RegistrosAsistente"/> and <see cref="RetroalimentacionAsistente"/>:
/// the module's migrator and the test infrastructure have to run the exact
/// same SQL. A copy of the DDL in tests would only prove the copy.
///
/// Needs no role GUCs, same as <see cref="RetroalimentacionAsistente"/>: the
/// two new tables live inside the `asistente` schema, already denied
/// wholesale to both read-only roles, so no REVOKE is issued here.
/// </remarks>
public static class HistorialAsistente
{
    /// <summary>Logical resource path of the embedded DDL.</summary>
    public const string RecursoSql = "asistente/004_asistente_historial.sql";

    /// <summary>
    /// Applies the history tables' DDL. Converges on a base that already has them.
    /// </summary>
    public static async Task AplicarAsync(NpgsqlConnection conexion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(conexion);

        var sql = RecursosSql.Leer(typeof(HistorialAsistente).Assembly, RecursoSql);

        await using var transaccion = await conexion.BeginTransactionAsync(ct);
        await using (var ddl = new NpgsqlCommand(sql, conexion, transaccion))
        {
            await ddl.ExecuteNonQueryAsync(ct);
        }

        await transaccion.CommitAsync(ct);
    }
}
