using System.Text.RegularExpressions;
using ArsDocendi.Shared.Persistencia;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>Aplica los privilegios de lectura del asistente sobre una base ya migrada.</summary>
public static partial class PrivilegiosAsistente
{
    /// <summary>Ruta lógica del recurso embebido con el DDL de privilegios.</summary>
    public const string RecursoSql = "asistente/001_asistente_grants.sql";

    /// <summary>
    /// Concede los privilegios declarados en el manifiesto a los dos roles del asistente.
    /// </summary>
    public static async Task AplicarAsync(
        NpgsqlConnection conexion,
        string rolSoloLectura,
        string rolSoloLecturaPii,
        CancellationToken ct)
    {
        ValidarNombreDeRol(rolSoloLectura, nameof(rolSoloLectura));
        ValidarNombreDeRol(rolSoloLecturaPii, nameof(rolSoloLecturaPii));

        var sql = RecursosSql.Leer(typeof(PrivilegiosAsistente).Assembly, RecursoSql);

        // Una sola transacción: o quedan todos los privilegios del manifiesto o ninguno.
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

        await using (var grants = new NpgsqlCommand(sql, conexion, transaccion))
        {
            await grants.ExecuteNonQueryAsync(ct);
        }

        await transaccion.CommitAsync(ct);
    }

    /// <summary>
    /// Falla temprano si el nombre no tiene forma de identificador sin comillas. La inyección ya
    /// está cerrada por el parámetro y por <c>format(%I)</c>.
    /// </summary>
    private static void ValidarNombreDeRol(string valor, string parametro)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new ArgumentException(
                $"El nombre del rol '{parametro}' está vacío. Lo aporta la configuración del ambiente.",
                parametro);
        }

        if (valor.Length > 63)
        {
            throw new ArgumentException(
                $"El nombre del rol '{parametro}' tiene {valor.Length} caracteres; PostgreSQL trunca los identificadores en 63.",
                parametro);
        }

        if (!FormaDeIdentificador().IsMatch(valor))
        {
            throw new ArgumentException(
                $"El nombre del rol '{parametro}' ('{valor}') no tiene forma de identificador sin comillas.",
                parametro);
        }
    }

    [GeneratedRegex("^[a-z_][a-z0-9_]*$")]
    private static partial Regex FormaDeIdentificador();
}
