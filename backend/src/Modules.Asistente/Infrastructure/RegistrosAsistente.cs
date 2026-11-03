using ArsDocendi.Shared.Persistencia;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>Crea el schema del asistente y sus dos registros sobre una base ya migrada.</summary>
public static class RegistrosAsistente
{
    /// <summary>Ruta lógica del recurso embebido con el DDL de los registros.</summary>
    public const string RecursoSql = "asistente/002_asistente_registros.sql";

    /// <summary>
    /// Aplica el DDL de los registros. Converge también sobre una base que ya tenía las tablas: lo
    /// que el <c>CREATE TABLE IF NOT EXISTS</c> no puede agregar lo agrega un <c>ALTER TABLE ...
    /// </summary>
    public static async Task AplicarAsync(
        NpgsqlConnection conexion,
        string rolSoloLectura,
        string rolSoloLecturaPii,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(conexion);

        var sql = RecursosSql.Leer(typeof(RegistrosAsistente).Assembly, RecursoSql);

        await using var transaccion = await conexion.BeginTransactionAsync(ct);

        await using (var guc = new NpgsqlCommand(
            """
            SELECT set_config('app.asistente_rol_basico', @basico, true),
                   set_config('app.asistente_rol_pii', @pii, true)
            """, conexion, transaccion))
        {
            guc.Parameters.AddWithValue("basico", rolSoloLectura);
            guc.Parameters.AddWithValue("pii", rolSoloLecturaPii);
            await guc.ExecuteNonQueryAsync(ct);
        }

        await using (var ddl = new NpgsqlCommand(sql, conexion, transaccion))
        {
            await ddl.ExecuteNonQueryAsync(ct);
        }

        await transaccion.CommitAsync(ct);
    }
}
