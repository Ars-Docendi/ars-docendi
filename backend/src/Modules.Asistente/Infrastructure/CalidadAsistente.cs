using ArsDocendi.Shared.Persistencia;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Adds the quality columns to the analytic record (cause, model, replaced flag) and creates the
/// quality-review audit table, on an already-migrated base.
/// </summary>
public static class CalidadAsistente
{
    /// <summary>Logical resource path of the embedded DDL.</summary>
    public const string RecursoSql = "asistente/010_asistente_calidad.sql";

    /// <summary>Applies the DDL and seed. Converges on a base that already has them.</summary>
    public static async Task AplicarAsync(NpgsqlConnection conexion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(conexion);

        var sql = RecursosSql.Leer(typeof(CalidadAsistente).Assembly, RecursoSql);

        await using var transaccion = await conexion.BeginTransactionAsync(ct);
        await using var ddl = new NpgsqlCommand(sql, conexion, transaccion);
        await ddl.ExecuteNonQueryAsync(ct);
        await transaccion.CommitAsync(ct);
    }
}
