using System.Text.RegularExpressions;
using ArsDocendi.Shared.Persistencia;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Aplica los privilegios de lectura del asistente sobre una base ya migrada.
/// </summary>
/// <remarks>
/// Es público y recibe la conexión desde afuera a propósito: lo usan el migrador
/// del módulo y la infraestructura de tests, y las dos tienen que ejercitar
/// exactamente el mismo SQL. Una copia del script en los tests probaría la copia.
/// </remarks>
public static partial class PrivilegiosAsistente
{
    /// <summary>Ruta lógica del recurso embebido con el DDL de privilegios.</summary>
    public const string RecursoSql = "asistente/001_asistente_grants.sql";

    /// <summary>
    /// Concede los privilegios declarados en el manifiesto a los dos roles del
    /// asistente. Idempotente: re-ejecutar converge.
    /// </summary>
    /// <remarks>
    /// Los nombres de rol llevan sufijo de ambiente, así que no pueden estar
    /// escritos en el <c>.sql</c>. Viajan como GUC de transacción —no como
    /// interpolación de texto— y del otro lado se citan con <c>format(%I)</c>.
    /// </remarks>
    public static async Task AplicarAsync(
        NpgsqlConnection conexion,
        string rolSoloLectura,
        string rolSoloLecturaPii,
        CancellationToken ct)
    {
        ValidarNombreDeRol(rolSoloLectura, nameof(rolSoloLectura));
        ValidarNombreDeRol(rolSoloLecturaPii, nameof(rolSoloLecturaPii));

        var sql = RecursosSql.Leer(typeof(PrivilegiosAsistente).Assembly, RecursoSql);

        // Una sola transacción: o quedan todos los privilegios del manifiesto o
        // ninguno. Un estado a medias es peor que ninguno, porque el test de
        // manifiesto lo reportaría como deriva sin que nadie haya cambiado nada.
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
    /// Falla temprano si el nombre no tiene forma de identificador sin comillas.
    /// </summary>
    /// <remarks>
    /// La inyección ya está cerrada por el parámetro y por <c>format(%I)</c>. Esto
    /// existe para que un nombre mal armado —una variable de ambiente vacía, un
    /// valor con espacios— falle con un mensaje que dice cuál, en vez de con un
    /// «role does not exist» a mitad del script.
    /// </remarks>
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
