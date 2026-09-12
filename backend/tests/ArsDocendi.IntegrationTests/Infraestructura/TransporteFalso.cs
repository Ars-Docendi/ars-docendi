using System.Net;
using System.Text;

namespace ArsDocendi.IntegrationTests.Infraestructura;

/// <summary>
/// Intercepta el HTTP del proveedor: guarda lo que se mandó y devuelve lo que se le diga.
/// </summary>
/// <remarks>
/// Se prueba contra el <b>cable</b> y no contra un doble del SDK, y el motivo es
/// concreto: lo que el adaptador tiene que garantizar —que el prefijo viaje marcado
/// para cachear, que la temperatura no viaje— son propiedades del JSON que sale, no
/// de los objetos que se arman. Un doble del cliente pasaría igual si el SDK
/// serializara distinto de lo que se supone.
///
/// Sirve además para probar el adaptador real <b>sin clave y sin red</b>, que es lo
/// que permite que estos tests corran en CI.
/// </remarks>
public sealed class TransporteFalso(Func<int, HttpResponseMessage> responder) : HttpMessageHandler
{
    private readonly List<string> _cuerpos = [];
    private readonly Lock _candado = new();

    /// <summary>Cuántas veces se llamó al transporte.</summary>
    public int Intentos
    {
        get
        {
            lock (_candado)
            {
                return _cuerpos.Count;
            }
        }
    }

    /// <summary>Los cuerpos que se mandaron, en orden.</summary>
    public IReadOnlyList<string> Cuerpos
    {
        get
        {
            lock (_candado)
            {
                return [.. _cuerpos];
            }
        }
    }

    /// <summary>Un transporte que siempre devuelve el mismo estado, sin cuerpo útil.</summary>
    public static TransporteFalso QueFalla(HttpStatusCode estado) =>
        new(_ => new HttpResponseMessage(estado)
        {
            Content = new StringContent(
                """{"type":"error","error":{"type":"api_error","message":"falla simulada"}}""",
                Encoding.UTF8,
                "application/json"),
        });

    /// <summary>Un transporte que siempre devuelve la misma respuesta exitosa.</summary>
    public static TransporteFalso QueResponde(
        string texto = "SELECT 1",
        int tokensDeEntrada = 120,
        int tokensDeSalida = 8,
        int? tokensDeCache = null,
        string motivoDeCorte = "end_turn") =>
        new(_ => Exito(texto, tokensDeEntrada, tokensDeSalida, tokensDeCache, motivoDeCorte));

    /// <summary>Una respuesta con la forma que devuelve la API de mensajes.</summary>
    public static HttpResponseMessage Exito(
        string? texto,
        int tokensDeEntrada = 120,
        int tokensDeSalida = 8,
        int? tokensDeCache = null,
        string motivoDeCorte = "end_turn")
    {
        var bloques = texto is null
            ? "[]"
            : $$"""[{"type":"text","text":{{Json(texto)}}}]""";

        var cache = tokensDeCache is { } cuantos
            ? $""","cache_read_input_tokens":{cuantos}"""
            : string.Empty;

        var cuerpo = $$"""
            {
              "id": "msg_falso",
              "type": "message",
              "role": "assistant",
              "model": "claude-opus-5",
              "content": {{bloques}},
              "stop_reason": {{Json(motivoDeCorte)}},
              "stop_sequence": null,
              "usage": { "input_tokens": {{tokensDeEntrada}}, "output_tokens": {{tokensDeSalida}}{{cache}} }
            }
            """;

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(cuerpo, Encoding.UTF8, "application/json"),
        };
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage solicitud, CancellationToken ct)
    {
        var cuerpo = solicitud.Content is null
            ? string.Empty
            : await solicitud.Content.ReadAsStringAsync(ct);

        int cual;
        lock (_candado)
        {
            _cuerpos.Add(cuerpo);
            cual = _cuerpos.Count;
        }

        return responder(cual);
    }

    private static string Json(string valor) =>
        System.Text.Json.JsonSerializer.Serialize(valor);
}
