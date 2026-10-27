using ArsDocendi.Shared.Persistencia;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>Creates <c>asistente.retroalimentacion_turno</c> on an already-migrated base.</summary>
public static class RetroalimentacionAsistente
{
    /// <summary>Logical resource path of the embedded DDL.</summary>
    public const string RecursoSql = "asistente/003_asistente_retroalimentacion.sql";

    /// <summary>Applies the feedback table DDL. Converges on a base that already has it.</summary>
    public static async Task AplicarAsync(NpgsqlConnection conexion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(conexion);

        var sql = RecursosSql.Leer(typeof(RetroalimentacionAsistente).Assembly, RecursoSql);

        await using var transaccion = await conexion.BeginTransactionAsync(ct);
        await using (var ddl = new NpgsqlCommand(sql, conexion, transaccion))
        {
            await ddl.ExecuteNonQueryAsync(ct);
        }

        await transaccion.CommitAsync(ct);
    }
}
