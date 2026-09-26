using ArsDocendi.Shared.Persistencia;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Creates the seven administration-of-use tables (budgets, org cap, price
/// table, maintenance flag, admin audit trail) on an already-migrated base.
/// </summary>
/// <remarks>
/// Public and takes the connection from the outside, for the same reason as
/// <see cref="HistorialAsistente"/> and <see cref="AuditoriaDeSoporteAsistente"/>:
/// the module's migrator and the test infrastructure have to run the exact
/// same SQL. Needs no role GUCs: every new table lives inside the
/// <c>asistente</c> schema, already denied wholesale to both read-only roles
/// by <see cref="RegistrosAsistente"/>.
/// </remarks>
public static class AdministracionAsistente
{
    /// <summary>Logical resource path of the embedded DDL.</summary>
    public const string RecursoSql = "asistente/006_asistente_administracion.sql";

    /// <summary>
    /// Applies the administration-of-use tables' DDL. Converges on a base
    /// that already has them.
    /// </summary>
    public static async Task AplicarAsync(NpgsqlConnection conexion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(conexion);

        var sql = RecursosSql.Leer(typeof(AdministracionAsistente).Assembly, RecursoSql);

        await using var transaccion = await conexion.BeginTransactionAsync(ct);
        await using (var ddl = new NpgsqlCommand(sql, conexion, transaccion))
        {
            await ddl.ExecuteNonQueryAsync(ct);
        }

        await transaccion.CommitAsync(ct);
    }
}
