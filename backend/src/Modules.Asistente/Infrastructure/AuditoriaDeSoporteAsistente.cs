using ArsDocendi.Shared.Persistencia;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Creates <c>asistente.auditoria_acceso_historial</c> on an already-migrated
/// base.
/// </summary>
/// <remarks>
/// Public and takes the connection from the outside, for the same reason as
/// <see cref="HistorialAsistente"/>. Needs no role GUCs: the table lives
/// inside the `asistente` schema, already denied wholesale to both
/// read-only roles, and it needs <c>hilo_historico</c> to already exist only
/// in the loose sense that the column stores that id — there is no foreign
/// key (design.md D10), so ordering relative to <see cref="HistorialAsistente"/>
/// is not load-bearing, but it still runs after it for readability.
/// </remarks>
public static class AuditoriaDeSoporteAsistente
{
    /// <summary>Logical resource path of the embedded DDL.</summary>
    public const string RecursoSql = "asistente/005_asistente_auditoria_soporte.sql";

    /// <summary>
    /// Applies the audit table's DDL. Converges on a base that already has it.
    /// </summary>
    public static async Task AplicarAsync(NpgsqlConnection conexion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(conexion);

        var sql = RecursosSql.Leer(typeof(AuditoriaDeSoporteAsistente).Assembly, RecursoSql);

        await using var transaccion = await conexion.BeginTransactionAsync(ct);
        await using (var ddl = new NpgsqlCommand(sql, conexion, transaccion))
        {
            await ddl.ExecuteNonQueryAsync(ct);
        }

        await transaccion.CommitAsync(ct);
    }
}
