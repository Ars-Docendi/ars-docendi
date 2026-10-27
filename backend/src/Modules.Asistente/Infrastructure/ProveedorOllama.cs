using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Modules.Asistente.Application;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Adaptador de <see cref="IProveedorDeModelo"/> contra la API nativa de Ollama (<c>POST
/// /api/chat</c>) del servicio de inferencia compartido (asistente-proveedor-ollama, design.md D1).
/// </summary>
internal sealed class ProveedorOllama : IProveedorDeModelo
{
    /// <summary>Nombre de configuración de este proveedor.</summary>
    public const string Clave = "ollama";

    private readonly HttpClient _transporte;
    private readonly OpcionesInferencia _servicio;
    private readonly string _modelo;
    private readonly int _contexto;
    private readonly ILogger<ProveedorOllama> _log;

    public ProveedorOllama(
        HttpClient transporte,
        OpcionesInferencia servicio,
        string modelo,
        int contexto,
        ILogger<ProveedorOllama> log)
    {
        ArgumentNullException.ThrowIfNull(transporte);
        ArgumentNullException.ThrowIfNull(servicio);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelo);

        if (!servicio.Habilitada)
        {
            throw new InvalidOperationException(
                $"El proveedor '{Clave}' necesita '{OpcionesInferencia.Seccion}:Url' en la configuración del ambiente.");
        }

        _transporte = transporte;
        _servicio = servicio;
        _modelo = modelo;
        _contexto = contexto;
        _log = log;
    }

    /// <summary>
    /// <c>ollama/{modelo}</c>: el mismo formato <c>proveedor/modelo</c> que parte
    /// <c>CalculadoraDeCosto</c>.
    /// </summary>
    public string Nombre => $"{Clave}/{_modelo}";

    public bool EsSimulado => false;

    public async Task<RespuestaDelModelo> CompletarAsync(
        SolicitudAlModelo solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        using var pedido = new HttpRequestMessage(HttpMethod.Post, _servicio.Ruta("api/chat"))
        {
            Content = new StringContent(
                Cuerpo(solicitud).ToJsonString(), Encoding.UTF8, "application/json"),
        };
        _servicio.Autenticar(pedido);

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
            throw new HttpRequestException(
                "El servicio de inferencia no sirvió la llamada.", excepcion, excepcion.StatusCode);
        }

        using (respuesta)
        {
            if (!respuesta.IsSuccessStatusCode)
            {
                throw Rechazo(respuesta.StatusCode, await respuesta.Content.ReadAsStringAsync(ct));
            }

            return enFlujo
                ? await LeerFlujoAsync(respuesta, solicitud, ct)
                : Traducir(await respuesta.Content.ReadAsStringAsync(ct), solicitud);
        }
    }

    /// <summary>Arma el cuerpo del request.</summary>
    internal JsonObject Cuerpo(SolicitudAlModelo solicitud)
    {
        var opciones = new JsonObject
        {
            ["temperature"] = (double)solicitud.Temperatura,
            ["num_predict"] = solicitud.MaximoDeTokens,
        };

        // Fijo por configuración y el mismo en todos los pedidos: un num_ctx distinto del que tiene
        // cargado el servidor lo hace recargar el modelo.
        if (_contexto > 0)
        {
            opciones["num_ctx"] = _contexto;
        }

        var cuerpo = new JsonObject
        {
            ["model"] = _modelo,
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "system", ["content"] = solicitud.PrefijoEstable },
                new JsonObject { ["role"] = "user", ["content"] = solicitud.Mensaje },
            },
            ["stream"] = solicitud.AlRecibirTexto is not null,

            // EL RAZONAMIENTO SE APAGA SALVO QUE SE LO PIDA EXPLÍCITAMENTE, igual que en
            // ProveedorLocal: en una GPU compartida, pensar multiplica la salida y es latencia para
            // todos los que esperan.
            ["think"] = solicitud.Esfuerzo is EsfuerzoDelModelo.Alto or EsfuerzoDelModelo.Maximo,

            // Un prompt más largo que el contexto es un error y no un recorte: por
            // defecto Ollama descarta el PRINCIPIO del prompt —el esquema— y
            // responde igual, que es una respuesta falsa sin ningún aviso.
            ["truncate"] = false,
            ["options"] = opciones,
        };

        if (solicitud.EsquemaDeSalidaJson is { } esquema)
        {
            // Salida restringida por esquema (structured outputs de Ollama). No
            // reemplaza al validador de SQL.
            cuerpo["format"] = JsonNode.Parse(esquema);
        }

        return cuerpo;
    }

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
                "El servicio de inferencia devolvió algo que no es JSON.", excepcion);
        }

        return Armar(Cadena(raiz?["message"]?["content"]) ?? string.Empty, raiz, solicitud);
    }

    /// <summary>
    /// Lee una respuesta NDJSON —un objeto por línea, el último con <c>done: true</c> y los
    /// conteos— y le pasa a <see cref="SolicitudAlModelo.AlRecibirTexto"/> el texto visible a
    /// medida que llega.
    /// </summary>
    private async Task<RespuestaDelModelo> LeerFlujoAsync(
        HttpResponseMessage respuesta, SolicitudAlModelo solicitud, CancellationToken ct)
    {
        var alRecibir = solicitud.AlRecibirTexto!;
        var acumulado = new StringBuilder();
        var emitidos = 0;
        JsonNode? final = null;

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
                    "El servicio de inferencia cortó la respuesta a mitad.", excepcion);
            }

            if (linea is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(linea))
            {
                continue;
            }

            JsonNode? evento;
            try
            {
                evento = JsonNode.Parse(linea);
            }
            catch (JsonException excepcion)
            {
                throw new HttpRequestException(
                    "El servicio de inferencia devolvió un fragmento que no es JSON.", excepcion);
            }

            if (Cadena(evento?["error"]) is { } error)
            {
                _log.LogWarning("El servicio de inferencia cortó el flujo con un error: {Inicio}", Recortar(error));
                throw new HttpRequestException("El servicio de inferencia cortó el flujo con un error.");
            }

            if (Cadena(evento?["message"]?["content"]) is { Length: > 0 } fragmento)
            {
                acumulado.Append(fragmento);
                var visible = ProveedorLocal.VisibleHastaAhora(acumulado.ToString());
                if (visible.Length > emitidos)
                {
                    await alRecibir(visible[emitidos..], ct);
                    emitidos = visible.Length;
                }
            }

            if (evento?["done"] is JsonValue hecho && hecho.TryGetValue<bool>(out var termino) && termino)
            {
                final = evento;
                break;
            }
        }

        if (final is null)
        {
            throw new HttpRequestException("El servicio de inferencia terminó el flujo sin cerrarlo.");
        }

        return Armar(acumulado.ToString(), final, solicitud);
    }

    /// <summary>
    /// Arma la respuesta del puerto con el texto y el objeto final. Sin él, los tokens de caché
    /// quedan en cero, que el puerto define como «no informa».
    /// </summary>
    private RespuestaDelModelo Armar(string contenido, JsonNode? final, SolicitudAlModelo solicitud)
    {
        var seQuedoSinTokens = Cadena(final?["done_reason"]) == "length";
        if (seQuedoSinTokens)
        {
            _log.LogWarning(
                "La respuesta del servicio de inferencia se cortó al agotar los {MaximoDeTokens} tokens "
                + "de presupuesto. Si el turno abstiene sin motivo aparente, subí el techo de esta "
                + "llamada o revisá que el razonamiento esté apagado.",
                solicitud.MaximoDeTokens);
        }

        var entrada = Entero(final?["prompt_eval_count"]);

        return new RespuestaDelModelo(
            ProveedorLocal.SinRazonamiento(contenido),
            entrada,
            Entero(final?["eval_count"]),
            EsSimulada: false,
            seQuedoSinTokens,
            Math.Min(entrada, Entero(final?["prompt_eval_cached_count"])));
    }

    /// <summary>
    /// Traduce un estado no exitoso al único vocabulario de falla que el pipeline
    /// conoce, nombrando la causa en el log sin el prompt ni credenciales.
    /// </summary>
    private HttpRequestException Rechazo(HttpStatusCode estado, string cuerpo)
    {
        var codigo = (int)estado;

        if (estado is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _log.LogError(
                "El servicio de inferencia rechazó la credencial ({Estado}). Revisá Inferencia__Usuario, "
                + "Inferencia__Contrasena y, fuera de prod, el Service Token de Cloudflare Access y que la "
                + "IP de salida esté en su política.",
                codigo);
        }
        else if (estado is HttpStatusCode.NotFound)
        {
            _log.LogError(
                "El servicio de inferencia no encontró el modelo o la ruta ({Estado}). Revisá que "
                + "Asistente__Modelo esté descargado en el servidor (ollama list).",
                codigo);
        }
        else if (estado is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
        {
            // 429 lo pone Traefik (tasa o pedidos en vuelo); 503 lo pone Ollama con la cola llena.
            _log.LogWarning(
                "El servicio de inferencia está saturado ({Estado}): límite de pedidos o cola llena.",
                codigo);
            throw new ProveedorSaturado(
                TimeSpan.Zero, $"El servicio de inferencia está saturado ({codigo}): límite de pedidos o cola llena.");
        }
        else if (codigo is 400 or 422)
        {
            _log.LogError(
                "El servicio de inferencia rechazó el request por mal armado ({Estado}): {Inicio}. "
                + "Si dice que el prompt excede el contexto, Asistente__ContextoDelModelo (o el "
                + "OLLAMA_CONTEXT_LENGTH del servidor) es chico para el prefijo más el techo de la llamada.",
                codigo,
                Recortar(cuerpo));
        }
        else
        {
            _log.LogWarning("El servicio de inferencia no sirvió la llamada ({Estado}).", codigo);
        }

        return new HttpRequestException(
            $"El servicio de inferencia respondió {codigo.ToString(CultureInfo.InvariantCulture)}.",
            inner: null,
            estado);
    }

    /// <summary>Lo justo del cuerpo de error para diagnosticar, sin el prompt.</summary>
    private static string Recortar(string cuerpo) =>
        cuerpo.Length <= 200 ? cuerpo : cuerpo[..200] + "…";

    private static string? Cadena(JsonNode? nodo) =>
        nodo is JsonValue valor && valor.TryGetValue<string>(out var cadena) ? cadena : null;

    /// <summary>Un conteo de tokens del JSON, acotado a 32 bits como lo guarda el registro.</summary>
    private static int Entero(JsonNode? nodo)
    {
        if (nodo is not JsonValue valor || !valor.TryGetValue<long>(out var numero))
        {
            return 0;
        }

        return numero <= 0 ? 0 : numero >= int.MaxValue ? int.MaxValue : (int)numero;
    }
}
