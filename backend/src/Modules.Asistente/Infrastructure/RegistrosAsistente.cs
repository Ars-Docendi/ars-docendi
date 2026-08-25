using ArsDocendi.Shared.Persistencia;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Crea el schema del asistente y sus dos registros sobre una base ya migrada.
/// </summary>
/// <remarks>
/// Es público y recibe la conexión desde afuera por el mismo motivo que
/// <see cref="PrivilegiosAsistente"/>: lo usan el migrador del módulo y la
/// infraestructura de tests, y las dos tienen que ejercitar exactamente el mismo
/// SQL. Una copia del DDL en los tests probaría la copia.
///
/// Corre <b>después</b> de los privilegios y no antes: el archivo revoca el schema
/// nuevo a los dos roles del asistente, y para eso los roles ya tienen que existir.
/// </remarks>
public static class RegistrosAsistente
{
    /// <summary>Ruta lógica del recurso embebido con el DDL de los registros.</summary>
    public const string RecursoSql = "asistente/002_asistente_registros.sql";

    /// <summary>
    /// Aplica el DDL de los registros. Idempotente: re-ejecutar converge.
    /// </summary>
    /// <remarks>
    /// Los nombres de rol llevan sufijo de ambiente, así que viajan como GUC de
    /// transacción —no como interpolación de texto— y del otro lado se citan con
    /// <c>format(%I)</c>.
    /// </remarks>
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
