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
/// Adaptador de <see cref="IProveedorDeModelo"/> contra un modelo propio servido
/// por un servidor OpenAI-compatible: vLLM, llama.cpp (<c>llama-server</c>) o
/// SGLang (asistente-proveedor-local, design.md D2).
/// </summary>
/// <remarks>
/// Igual que <c>ProveedorAnthropic</c>, su única responsabilidad es
/// <b>traducir</b>. El techo del turno, la compuerta de concurrencia, el corte,
/// el timeout por llamada y el reintento de transporte viven en decoradores y
/// handlers que no saben qué proveedor hay adentro.
///
/// No usa ningún SDK: la API es <c>POST {url}/chat/completions</c> con JSON
/// plano, y los tres servidores la hablan igual. Un SDK agregaría una
/// dependencia para serializar cinco campos.
/// </remarks>
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
    /// <c>CalculadoraDeCosto</c>. Sin fila en <c>tabla_de_precios</c> esos turnos
    /// se informan «sin precio», nunca como costo cero.
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

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await _transporte.SendAsync(pedido, ct);
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
            var texto = await respuesta.Content.ReadAsStringAsync(ct);

            if (!respuesta.IsSuccessStatusCode)
            {
                throw Rechazo(respuesta.StatusCode, texto);
            }

            return Traducir(texto, solicitud);
        }
    }

    /// <summary>Arma el cuerpo del request.</summary>
    /// <remarks>
    /// <b>El orden de los mensajes es la caché.</b> El servidor cachea por
    /// prefijo de tokens: el sistema va primero y es byte a byte igual entre
    /// turnos, y todo lo variable va en el mensaje de usuario, detrás.
    /// </remarks>
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
            ["stream"] = false,

            // EL RAZONAMIENTO SE APAGA SALVO QUE SE LO PIDA EXPLÍCITAMENTE (D2).
            // En un modelo local pensar multiplica la salida 5–20×, y en una GPU
            // compartida eso es latencia para todos los que esperan. `medio` —el
            // default de la generación— no piensa: con Claude «medio» era una
            // perilla de costo, acá es una de capacidad.
            ["chat_template_kwargs"] = new JsonObject
            {
                ["enable_thinking"] = solicitud.Esfuerzo is EsfuerzoDelModelo.Alto or EsfuerzoDelModelo.Maximo,
            },
        };

        if (solicitud.EsquemaDeSalidaJson is { } esquema)
        {
            // Decodificación restringida: el servidor sólo puede emitir tokens que
            // mantienen válido el esquema (xgrammar en vLLM/SGLang, GBNF en
            // llama-server). No reemplaza al validador de SQL.
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
    /// <remarks>
    /// Una respuesta sin texto NO es una caída, por el mismo motivo que en
    /// <c>ProveedorAnthropic</c>: el validador rechaza una SQL vacía y el turno
    /// abstiene, en vez de abrir el corte por algo que no es una falla de servicio.
    /// </remarks>
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

        var eleccion = raiz?["choices"]?[0];
        var contenido = Cadena(eleccion?["message"]?["content"]) ?? string.Empty;
        var motivoDeFin = Cadena(eleccion?["finish_reason"]);
        var uso = raiz?["usage"];

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
        // --enable-prompt-tokens-details) y llama-server reciente; `timings.cache_n`
        // es lo que informa llama-server por su cuenta. Cero significa «no informa»
        // o «no acertó», igual que con Anthropic.
        var informados = Entero(uso?["prompt_tokens_details"]?["cached_tokens"]);
        if (informados == 0)
        {
            informados = Entero(raiz?["timings"]?["cache_n"]);
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
    /// Descarta un bloque <c>&lt;think&gt;…&lt;/think&gt;</c> que el modelo haya
    /// emitido igual.
    /// </summary>
    /// <remarks>
    /// Hubo versiones de servidores que no respetaban <c>enable_thinking</c>. Lo
    /// que viene antes del cierre es razonamiento, no respuesta: dejarlo rompería
    /// el JSON de la generación —que se busca de la primera llave a la última— y
    /// le mostraría al usuario un borrador. Un bloque abierto que nunca cierra es
    /// una respuesta cortada a mitad del razonamiento: no hay respuesta.
    /// </remarks>
    internal static string SinRazonamiento(string texto)
    {
        var sinBloques = BloqueDeRazonamiento().Replace(texto, string.Empty);
        var abierto = sinBloques.IndexOf("<think>", StringComparison.Ordinal);

        return (abierto >= 0 ? sinBloques[..abierto] : sinBloques).Trim();
    }

    [GeneratedRegex(@"<think>.*?</think>", RegexOptions.Singleline)]
    private static partial Regex BloqueDeRazonamiento();

    /// <summary>
    /// Traduce un estado no exitoso al único vocabulario de falla que el pipeline
    /// conoce, nombrando la causa en el log.
    /// </summary>
    /// <remarks>
    /// Todas terminan en <see cref="HttpRequestException"/>, igual que en
    /// <c>ProveedorAnthropic</c>: el breaker cuenta exactamente esa excepción, y
    /// el usuario recibe la degradación del contrato y nunca un 500. Lo que cambia
    /// es lo que dice el log, porque la reacción de quien lo lee no se parece. El
    /// cuerpo del servidor NO se loguea entero: puede repetir el prompt, y el
    /// prompt lleva la pregunta del usuario.
    /// </remarks>
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
