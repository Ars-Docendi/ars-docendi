using System.Globalization;
using Npgsql;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Deja una transacción de lectura en las condiciones que el módulo exige antes
/// de tocar datos: solo lectura, con el actor fijado y —cuando el llamador lo
/// pide— con techo de sentencia.
/// </summary>
/// <remarks>
/// <b>Es la única declaración del ajuste del actor en todo el módulo</b>, y ese
/// es el punto. Estaba escrito cuatro veces, y el modo de falla de la quinta
/// copia —la que alguien escribe sin acordarse del preámbulo— es <b>silencioso</b>:
/// sin el ajuste, <c>identity.asistente_actor()</c> devuelve <c>NULL</c>, la
/// policy de RLS da falso, y el asistente contesta «no hay datos» en vez de «no
/// podés verlos». Eso no es un error visible: es una respuesta falsa producida
/// por el sistema, que es exactamente lo que mide la métrica primaria del
/// proyecto.
///
/// El tercer parámetro de <c>set_config</c> en verdadero es lo que hace el ajuste
/// <b>transaction-local</b>. Con una variante de sesión, el ajuste sobreviviría
/// al <c>COMMIT</c> y a la devolución de la conexión al pool, y el turno
/// siguiente que tomara esa conexión física heredaría el actor del anterior.
///
/// <c>SET TRANSACTION READ ONLY</c> va primero porque PostgreSQL no lo admite una
/// vez que la transacción tocó datos. Que viva acá adentro es lo que hace cierta
/// la frase «toda transacción que fija el actor es de solo lectura»: no queda
/// forma de fijar el actor sin declararla.
///
/// Recibe la conexión y la transacción desde afuera, igual que
/// <see cref="PrivilegiosAsistente"/>: quién abre la conexión y con qué rol es
/// decisión del llamador, y meterla acá adentro convertiría este preámbulo en un
/// segundo lugar donde se elige el rol.
/// </remarks>
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

        // Comandos separados de la lectura y no una sola expresión con AND:
        // PostgreSQL no garantiza el orden de evaluación de los operandos, así que
        // fijar el ajuste y leerlo en la misma expresión podría leerlo antes de
        // escribirlo.
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
