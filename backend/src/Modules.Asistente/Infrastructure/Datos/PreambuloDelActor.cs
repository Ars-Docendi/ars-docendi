using System.Globalization;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Deja una transacción de lectura en las condiciones que el módulo exige antes de tocar datos:
/// solo lectura, con el actor fijado y —cuando el llamador lo pide— con techo de sentencia.
/// </summary>
internal static class PreambuloDelActor
{
    /// <summary>
    /// Ajuste transaction-local donde viaja el actor. Lo leen las funciones
    /// <c>SECURITY DEFINER</c> del schema <c>identity</c>.
    /// </summary>
    private const string AjusteDelActor = "app.asistente_user_id";

    /// <summary>
    /// Aplica el preámbulo. <paramref name="techoDeSentenciaMs"/> en
    /// <c>null</c> deja el techo que traiga la conexión.
    /// </summary>
    public static async Task AplicarAsync(
        NpgsqlConnection conexion,
        NpgsqlTransaction transaccion,
        Guid actor,
        CancellationToken ct,
        int? techoDeSentenciaMs = null)
    {
        await using (var soloLectura = new NpgsqlCommand(
            "SET TRANSACTION READ ONLY", conexion, transaccion))
        {
            await soloLectura.ExecuteNonQueryAsync(ct);
        }

        // Comandos separados de la lectura y no una sola expresión con AND: PostgreSQL no garantiza
        // el orden de evaluación de los operandos, así que fijar el ajuste y leerlo en la misma
        // expresión podría leerlo antes de escribirlo.
        var sql = techoDeSentenciaMs is null
            ? $"SELECT set_config('{AjusteDelActor}', @actor, true)"
            : $"""
              SELECT set_config('statement_timeout', @techo, true),
                     set_config('{AjusteDelActor}', @actor, true)
              """;

        await using var preambulo = new NpgsqlCommand(sql, conexion, transaccion);
        preambulo.Parameters.AddWithValue("actor", actor.ToString());

        if (techoDeSentenciaMs is not null)
        {
            preambulo.Parameters.AddWithValue(
                "techo", techoDeSentenciaMs.Value.ToString(CultureInfo.InvariantCulture));
        }

        await preambulo.ExecuteNonQueryAsync(ct);
    }
}
