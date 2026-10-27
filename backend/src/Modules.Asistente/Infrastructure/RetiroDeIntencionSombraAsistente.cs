using ArsDocendi.Shared.Persistencia;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Drops the retired <c>intencion_sombra</c> column from the operational record, on an
/// already-migrated base.
/// </summary>
public static class RetiroDeIntencionSombraAsistente
{
    /// <summary>Logical resource path of the embedded DDL.</summary>
    public const string RecursoSql = "asistente/011_asistente_retirar_intencion_sombra.sql";

    /// <summary>Applies the DDL. Converges on a base that already dropped the column.</summary>
    public static async Task AplicarAsync(NpgsqlConnection conexion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(conexion);

        var sql = RecursosSql.Leer(typeof(RetiroDeIntencionSombraAsistente).Assembly, RecursoSql);

        await using var transaccion = await conexion.BeginTransactionAsync(ct);
        await using var ddl = new NpgsqlCommand(sql, conexion, transaccion);
        await ddl.ExecuteNonQueryAsync(ct);
        await transaccion.CommitAsync(ct);
    }
}
