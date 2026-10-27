using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Modules.Asistente.Application;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Adaptador de <see cref="IProveedorDeModelo"/> contra un modelo propio servido por un servidor
/// OpenAI-compatible: vLLM, llama.cpp (<c>llama-server</c>) o SGLang (asistente-proveedor-local,
/// design.md D2).
/// </summary>
internal sealed partial class ProveedorLocal : IProveedorDeModelo
{
    /// <summary>Nombre de configuración de este proveedor.</summary>
    public const string Clave = "local";

    private readonly HttpClient _transporte;
    private readonly Uri _completaciones;
    private readonly string? _clave;
    private readonly string _modelo;
    private readonly ILogger<ProveedorLocal> _log;

    public ProveedorLocal(
        HttpClient transporte,
        string url,
        string? clave,
        string modelo,
        ILogger<ProveedorLocal> log)
    {
        ArgumentNullException.ThrowIfNull(transporte);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelo);

        _transporte = transporte;
        _completaciones = new Uri(url.TrimEnd('/') + "/chat/completions", UriKind.Absolute);
        _clave = string.IsNullOrWhiteSpace(clave) ? null : clave;
        _modelo = modelo;
        _log = log;
    }

    /// <summary>
    /// <c>local/{modelo}</c>: el mismo formato <c>proveedor/modelo</c> que parte
    /// <c>CalculadoraDeCosto</c>.
    /// </summary>
    public string Nombre => $"{Clave}/{_modelo}";

    public bool EsSimulado => false;

    public async Task<RespuestaDelModelo> CompletarAsync(
        SolicitudAlModelo solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        using var pedido = new HttpRequestMessage(HttpMethod.Post, _completaciones)
        {
            Content = new StringContent(
                Cuerpo(solicitud).ToJsonString(), Encoding.UTF8, "application/json"),
        };

        if (_clave is not null)
        {
            pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _clave);
        }

        // Por fragmentos, la respuesta se lee a medida que llega: con el default
        // (`ResponseContentRead`) el cliente HTTP esperaría el cuerpo entero.
        var enFlujo = solicitud.AlRecibirTexto is not null;

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await _transporte.SendAsync(
                pedido,
                enFlujo ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead,
                ct);
        }
        catch (OperationCanceledException)
        {
            // Del token del breaker o del request: el decorador la reconoce y la
            // convierte en TimeoutDelProveedor.
            throw;
        }
        catch (HttpRequestException excepcion)
        {
            throw Transporte(excepcion);
        }

        using (respuesta)
        {
            if (!respuesta.IsSuccessStatusCode)
            {
                throw Rechazo(respuesta.StatusCode, await respuesta.Content.ReadAsStringAsync(ct));
            }

            if (enFlujo)
            {
                return await LeerFlujoAsync(respuesta, solicitud, ct);
            }

            return Traducir(await respuesta.Content.ReadAsStringAsync(ct), solicitud);
        }
    }

    /// <summary>Arma el cuerpo del request.</summary>
    internal JsonObject Cuerpo(SolicitudAlModelo solicitud)
    {
        var cuerpo = new JsonObject
        {
            ["model"] = _modelo,
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "system", ["content"] = solicitud.PrefijoEstable },
                new JsonObject { ["role"] = "user", ["content"] = solicitud.Mensaje },
            },
            // La temperatura SÍ viaja: el puerto la conservó para esto.
            ["temperature"] = (double)solicitud.Temperatura,
            ["max_tokens"] = solicitud.MaximoDeTokens,
            ["stream"] = solicitud.AlRecibirTexto is not null,

            // EL RAZONAMIENTO SE APAGA SALVO QUE SE LO PIDA EXPLÍCITAMENTE (D2). En un modelo local
            // pensar multiplica la salida 5–20×, y en una GPU compartida eso es latencia para todos
            // los que esperan.
            ["chat_template_kwargs"] = new JsonObject
            {
                ["enable_thinking"] = solicitud.Esfuerzo is EsfuerzoDelModelo.Alto or EsfuerzoDelModelo.Maximo,
            },
        };

        if (solicitud.AlRecibirTexto is not null)
        {
            // Sin esto vLLM no informa tokens en un flujo, y el registro operativo
            // anotaría cero.
            cuerpo["stream_options"] = new JsonObject { ["include_usage"] = true };
        }

        if (solicitud.EsquemaDeSalidaJson is { } esquema)
        {
            // Decodificación restringida: el servidor sólo puede emitir tokens que mantienen válido
            // el esquema (xgrammar en vLLM/SGLang, GBNF en llama-server).
            cuerpo["response_format"] = new JsonObject
            {
                ["type"] = "json_schema",
                ["json_schema"] = new JsonObject
                {
                    ["name"] = "respuesta",
                    ["strict"] = true,
                    ["schema"] = JsonNode.Parse(esquema),
                },
            };
        }

        return cuerpo;
    }

    /// <summary>Convierte la respuesta del servidor al contrato del puerto.</summary>
    private RespuestaDelModelo Traducir(string json, SolicitudAlModelo solicitud)
    {
        JsonNode? raiz;
        try
        {
            raiz = JsonNode.Parse(json);
        }
        catch (JsonException excepcion)
        {
            throw new HttpRequestException(
                "El servidor del modelo local devolvió algo que no es JSON.", excepcion);
        }

        var eleccion = PrimeraEleccion(raiz);

        return Armar(
            Cadena(eleccion?["message"]?["content"]) ?? string.Empty,
            Cadena(eleccion?["finish_reason"]),
            raiz?["usage"],
            raiz?["timings"],
            solicitud);
    }

    /// <summary>
    /// Lee una respuesta <c>text/event-stream</c> y le pasa a <see
    /// cref="SolicitudAlModelo.AlRecibirTexto"/> el texto visible a medida que llega
    /// (asistente-optimizaciones-modelo-local, design.md D9).
    /// </summary>
    private async Task<RespuestaDelModelo> LeerFlujoAsync(
        HttpResponseMessage respuesta, SolicitudAlModelo solicitud, CancellationToken ct)
    {
        var alRecibir = solicitud.AlRecibirTexto!;
        var acumulado = new StringBuilder();
        var emitidos = 0;
        string? motivoDeFin = null;
        JsonNode? uso = null;
        JsonNode? tiempos = null;

        await using var flujo = await respuesta.Content.ReadAsStreamAsync(ct);
        using var lector = new StreamReader(flujo, Encoding.UTF8);

        while (true)
        {
            string? linea;
            try
            {
                linea = await lector.ReadLineAsync(ct);
            }
            catch (IOException excepcion)
            {
                throw new HttpRequestException(
                    "El servidor del modelo local cortó la respuesta a mitad.", excepcion);
            }

            if (linea is null)
            {
                break;
            }

            if (!linea.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var dato = linea["data:".Length..].Trim();
            if (dato == "[DONE]")
            {
                break;
            }

            JsonNode? evento;
            try
            {
                evento = JsonNode.Parse(dato);
            }
            catch (JsonException excepcion)
            {
                throw new HttpRequestException(
                    "El servidor del modelo local devolvió un evento que no es JSON.", excepcion);
            }

            var eleccion = PrimeraEleccion(evento);
            motivoDeFin = Cadena(eleccion?["finish_reason"]) ?? motivoDeFin;
            uso = evento?["usage"] is JsonObject conUso ? conUso : uso;
            tiempos = evento?["timings"] is JsonObject conTiempos ? conTiempos : tiempos;

            if (Cadena(eleccion?["delta"]?["content"]) is not { Length: > 0 } fragmento)
            {
                continue;
            }

            acumulado.Append(fragmento);
            var visible = VisibleHastaAhora(acumulado.ToString());
            if (visible.Length > emitidos)
            {
                await alRecibir(visible[emitidos..], ct);
                emitidos = visible.Length;
            }
        }

        return Armar(acumulado.ToString(), motivoDeFin, uso, tiempos, solicitud);
    }

    /// <summary>Lo que se puede mostrar de una respuesta que todavía está llegando.</summary>
    internal static string VisibleHastaAhora(string texto)
    {
        const string Apertura = "<think>";

        var sinBloques = BloqueDeRazonamiento().Replace(texto, string.Empty);
        var abierto = sinBloques.IndexOf(Apertura, StringComparison.Ordinal);
        var visible = abierto >= 0 ? sinBloques[..abierto] : sinBloques;

        for (var largo = Math.Min(Apertura.Length - 1, visible.Length); largo > 0; largo--)
        {
            if (Apertura.StartsWith(visible[^largo..], StringComparison.Ordinal))
            {
                visible = visible[..^largo];
                break;
            }
        }

        return visible.TrimStart();
    }

    /// <summary>Arma la respuesta del puerto, venga entera o por fragmentos.</summary>
    private RespuestaDelModelo Armar(
        string contenido,
        string? motivoDeFin,
        JsonNode? uso,
        JsonNode? tiempos,
        SolicitudAlModelo solicitud)
    {
        var seQuedoSinTokens = motivoDeFin == "length";
        if (seQuedoSinTokens)
        {
            _log.LogWarning(
                "La respuesta del modelo local se cortó al agotar los {MaximoDeTokens} tokens de "
                + "presupuesto. Si el turno abstiene sin motivo aparente, subí el techo de esta "
                + "llamada o revisá que el razonamiento esté apagado.",
                solicitud.MaximoDeTokens);
        }

        var entrada = Entero(uso?["prompt_tokens"]);

        // `prompt_tokens_details.cached_tokens` es lo que informa vLLM (con
        // --enable-prompt-tokens-details) y llama-server reciente; `timings.cache_n` es lo que
        // informa llama-server por su cuenta.
        var informados = Entero(uso?["prompt_tokens_details"]?["cached_tokens"]);
        if (informados == 0)
        {
            informados = Entero(tiempos?["cache_n"]);
        }

        var deCache = Math.Min(entrada, informados);

        return new RespuestaDelModelo(
            SinRazonamiento(contenido),
            entrada,
            Entero(uso?["completion_tokens"]),
            EsSimulada: false,
            seQuedoSinTokens,
            deCache);
    }

    /// <summary>
    /// Descarta un bloque <c>&lt;think&gt;…&lt;/think&gt;</c> que el modelo haya emitido igual.
    /// </summary>
    internal static string SinRazonamiento(string texto)
    {
        var sinBloques = BloqueDeRazonamiento().Replace(texto, string.Empty);
        var abierto = sinBloques.IndexOf("<think>", StringComparison.Ordinal);

        return (abierto >= 0 ? sinBloques[..abierto] : sinBloques).Trim();
    }

    [GeneratedRegex(@"<think>.*?</think>", RegexOptions.Singleline)]
    private static partial Regex BloqueDeRazonamiento();

    /// <summary>
    /// Traduce un estado no exitoso al único vocabulario de falla que el pipeline conoce, nombrando
    /// la causa en el log.
    /// </summary>
    private HttpRequestException Rechazo(HttpStatusCode estado, string cuerpo)
    {
        var codigo = (int)estado;

        if (estado is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _log.LogError(
                "El servidor del modelo local rechazó la credencial ({Estado}). Revisá "
                + "Asistente__ClaveDelProveedor contra el --api-key del servidor.",
                codigo);
        }
        else if (estado is HttpStatusCode.NotFound)
        {
            _log.LogError(
                "El servidor del modelo local no encontró el modelo o la ruta ({Estado}). Revisá "
                + "Asistente__Modelo contra el --served-model-name y Asistente__UrlDelProveedorLocal "
                + "(debe terminar en /v1).",
                codigo);
        }
        else if (codigo is 400 or 422)
        {
            _log.LogError(
                "El servidor del modelo local rechazó el request por mal armado ({Estado}): {Inicio}. "
                + "Si dice que el prompt excede el contexto, el --max-model-len del servidor es "
                + "chico para el prefijo del esquema más el techo de tokens de la llamada.",
                codigo,
                Recortar(cuerpo));
        }
        else
        {
            _log.LogWarning(
                "El servidor del modelo local no sirvió la llamada ({Estado}).", codigo);
        }

        return new HttpRequestException(
            $"El servidor del modelo local respondió {codigo.ToString(CultureInfo.InvariantCulture)}.",
            inner: null,
            estado);
    }

    private static HttpRequestException Transporte(HttpRequestException excepcion) =>
        new("El servidor del modelo local no sirvió la llamada.", excepcion, excepcion.StatusCode);

    /// <summary>Lo justo del cuerpo de error para diagnosticar, sin el prompt.</summary>
    private static string Recortar(string cuerpo) =>
        cuerpo.Length <= 200 ? cuerpo : cuerpo[..200] + "…";

    /// <summary>
    /// <c>choices[0]</c>, o nulo si no hay: el último evento de un flujo con
    /// <c>include_usage</c> trae <c>"choices": []</c> y sólo el uso.
    /// </summary>
    private static JsonNode? PrimeraEleccion(JsonNode? raiz) =>
        raiz?["choices"] is JsonArray { Count: > 0 } elecciones ? elecciones[0] : null;

    /// <summary>Una cadena del JSON, o nulo si falta o es de otro tipo.</summary>
    private static string? Cadena(JsonNode? nodo) =>
        nodo is JsonValue valor && valor.TryGetValue<string>(out var cadena) ? cadena : null;

    /// <summary>
    /// Un conteo de tokens del JSON, acotado a 32 bits como lo guarda el registro.
    /// </summary>
    private static int Entero(JsonNode? nodo)
    {
        if (nodo is not JsonValue valor || !valor.TryGetValue<long>(out var numero))
        {
            return 0;
        }

        return numero <= 0 ? 0 : numero >= int.MaxValue ? int.MaxValue : (int)numero;
    }
}
