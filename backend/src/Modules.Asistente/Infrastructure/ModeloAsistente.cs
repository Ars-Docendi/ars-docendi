using ArsDocendi.Shared.Persistencia;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Creates the three runtime-model tables (per-company credential, current selection, model
/// catalog) on an already-migrated base.
/// </summary>
public static class ModeloAsistente
{
    /// <summary>Logical resource path of the embedded DDL.</summary>
    public const string RecursoSql = "asistente/009_asistente_modelo.sql";

    /// <summary>Applies the DDL and seed. Converges on a base that already has them.</summary>
    public static async Task AplicarAsync(NpgsqlConnection conexion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(conexion);

        var sql = RecursosSql.Leer(typeof(ModeloAsistente).Assembly, RecursoSql);

        await using var transaccion = await conexion.BeginTransactionAsync(ct);
        await using var ddl = new NpgsqlCommand(sql, conexion, transaccion);
        await ddl.ExecuteNonQueryAsync(ct);
        await transaccion.CommitAsync(ct);
    }
}
