using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Logging;
using Modules.Asistente.Application;

namespace Modules.Asistente.Infrastructure;

/// <summary>Adaptador de <see cref="IProveedorDeModelo"/> contra la API de Anthropic.</summary>
internal sealed class ProveedorAnthropic : IProveedorDeModelo, IDisposable
{
    /// <summary>Nombre de configuración de este proveedor.</summary>
    public const string Clave = "anthropic";

    private readonly AnthropicClient _cliente;
    private readonly string _modelo;
    private readonly ILogger<ProveedorAnthropic> _log;

    public ProveedorAnthropic(
        HttpClient transporte,
        string clave,
        string modelo,
        ILogger<ProveedorAnthropic> log)
    {
        ArgumentNullException.ThrowIfNull(transporte);
        ArgumentException.ThrowIfNullOrWhiteSpace(clave);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelo);

        _cliente = new AnthropicClient
        {
            ApiKey = clave,

            // El transporte llega de afuera y ya trae el reintento del módulo en su pipeline.
            HttpClient = transporte,

            // Cero, y no es una preferencia. El SDK reintenta por su cuenta ante errores de
            // conexión, 408, 409, 429 y 5xx.
            MaxRetries = 0,
        };

        _modelo = modelo;
        _log = log;
    }

    /// <summary>Traduce el esfuerzo del puerto al del SDK.</summary>
    private static Effort EsfuerzoDe(EsfuerzoDelModelo esfuerzo) => esfuerzo switch
    {
        // Mínimo no llega acá: se resuelve omitiendo el campo entero, que es la
        // única forma de hablarle a un modelo que no delibera.
        EsfuerzoDelModelo.Bajo => Effort.Low,
        EsfuerzoDelModelo.Medio => Effort.Medium,
        EsfuerzoDelModelo.Alto => Effort.High,
        EsfuerzoDelModelo.Maximo => Effort.Max,
        _ => throw new InvalidOperationException(
            $"Esfuerzo del puerto no mapeado en el adaptador: {esfuerzo}."),
    };

    /// <summary>Incluye el modelo, no solo el proveedor.</summary>
    public string Nombre => $"{Clave}/{_modelo}";

    public bool EsSimulado => false;

    public async Task<RespuestaDelModelo> CompletarAsync(
        SolicitudAlModelo solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        // La temperatura NO viaja: los modelos Claude actuales la rechazan con 400.
        var parametros = new MessageCreateParams
        {
            Model = _modelo,
            MaxTokens = solicitud.MaximoDeTokens,

            // El prefijo va como bloque de sistema MARCADO PARA CACHEAR. Sin la
            // marca el diseño sigue siendo correcto y el ahorro simplemente no
            // ocurre, sin que nada falle ni se note salvo en la factura: el esquema
            // es el bloque más grande del prompt y se repite idéntico turno a turno.
            System = new List<TextBlockParam>
            {
                new()
                {
                    Text = solicitud.PrefijoEstable,
                    CacheControl = new CacheControlEphemeral(),
                },
            },

            Messages = [new() { Role = Role.User, Content = solicitud.Mensaje }],
            // MÍNIMO NO MANDA EL CAMPO, y no es una optimización: hay modelos que no deliberan y
            // rechazan `output_config` con 400 —Haiku 4.5 es uno—.
            OutputConfig = solicitud.Esfuerzo == EsfuerzoDelModelo.Minimo
                ? null
                : new OutputConfig { Effort = EsfuerzoDe(solicitud.Esfuerzo) },
        };

        Message respuesta;
        try
        {
            respuesta = await _cliente.Messages.Create(parametros, ct);
        }
        catch (OperationCanceledException)
        {
            // Del token del breaker o del request. El decorador la reconoce y la
            // convierte en TimeoutDelProveedor; atraparla acá le sacaría el fallo
            // de las manos y el corte nunca abriría por timeout.
            throw;
        }
        catch (Exception excepcion) when (ct.IsCancellationRequested)
        {
            // El SDK envolvió nuestra propia cancelación en un tipo suyo. Se la devuelve al
            // decorador como cancelación, que es lo único que él sabe reconocer.
            throw new OperationCanceledException(
                "La llamada al proveedor del modelo se canceló.", excepcion, ct);
        }
        catch (AnthropicUnauthorizedException excepcion)
        {
            throw Credencial(excepcion);
        }
        catch (AnthropicForbiddenException excepcion)
        {
            throw Credencial(excepcion);
        }
        catch (AnthropicBadRequestException excepcion)
        {
            throw Armado(excepcion);
        }
        catch (AnthropicUnprocessableEntityException excepcion)
        {
            throw Armado(excepcion);
        }
        catch (AnthropicException excepcion)
        {
            // Límite de tasa, 5xx, E/S y cualquier otra falla del SDK. Todas significan lo mismo
            // para el pipeline: el proveedor no sirvió la llamada.
            throw Transporte(excepcion);
        }

        return Traducir(respuesta, solicitud);
    }

    private bool _dispuesto;

    /// <summary>Libera el cliente del SDK. Idempotente.</summary>
    public void Dispose()
    {
        if (_dispuesto)
        {
            return;
        }

        _dispuesto = true;
        _cliente.Dispose();
    }

    /// <summary>Convierte la respuesta del SDK al contrato del puerto.</summary>
    private RespuestaDelModelo Traducir(Message respuesta, SolicitudAlModelo solicitud)
    {
        if (respuesta.StopReason == "refusal")
        {
            // Warning y no Error: el proveedor funcionó. Saber que se rehusó —y no
            // que devolvió algo inválido— cambia qué se investiga.
            _log.LogWarning(
                "El modelo se rehusó a responder ({Categoria}).",
                respuesta.StopDetails?.Category);
        }

        // EL CORTE POR PRESUPUESTO SE GRITA, y es la única forma de enterarse. Una respuesta
        // cortada por MaxTokens deja un JSON incompleto; el generador no lo puede interpretar y el
        // turno resuelve «no pude interpretar la pregunta», que es palabra por palabra lo que
        // responde una pregunta genuinamente incontestable.
        var seQuedoSinTokens = respuesta.StopReason == "max_tokens";

        if (seQuedoSinTokens)
        {
            _log.LogWarning(
                "La respuesta del modelo se cortó al agotar los {MaximoDeTokens} tokens de "
                + "presupuesto (esfuerzo {Esfuerzo}). Con esfuerzo configurado el razonamiento "
                + "sale de ese mismo techo. Si el turno abstiene sin motivo aparente, esta es "
                + "la causa: subí el presupuesto de la llamada.",
                solicitud.MaximoDeTokens,
                solicitud.Esfuerzo);
        }

        var texto = string.Concat(
            respuesta.Content.Select(bloque => bloque.Value).OfType<TextBlock>().Select(b => b.Text));

        // Los tokens servidos desde caché SUMAN a los de entrada. Son tokens de prompt reales; lo
        // único distinto es lo que cuestan.
        var deCache = Acotar(respuesta.Usage.CacheReadInputTokens ?? 0);

        return new RespuestaDelModelo(
            texto,
            Acotar(respuesta.Usage.InputTokens) + deCache,
            Acotar(respuesta.Usage.OutputTokens),
            EsSimulada: false,
            seQuedoSinTokens,
            deCache);
    }

    /// <summary>La credencial no sirve. Se degrada igual, pero se grita.</summary>
    private HttpRequestException Credencial(AnthropicException excepcion)
    {
        _log.LogError(
            excepcion,
            "El proveedor del modelo rechazó la credencial. Revisá Asistente__ClaveDelProveedor "
            + "en este ambiente: mientras siga así, todos los turnos van a responder degradados.");

        // Keeps the status so the turn's cause is credencial_rechazada and not a
        // generic provider error (asistente-metricas-de-calidad, D2).
        return new HttpRequestException(
            "El proveedor del modelo no sirvió la llamada.", excepcion, System.Net.HttpStatusCode.Unauthorized);
    }

    /// <summary>El request salió mal armado de acá. Ningún reintento lo arregla.</summary>
    private HttpRequestException Armado(AnthropicException excepcion)
    {
        if (SinCredito(excepcion.Message))
        {
            _log.LogError(
                excepcion,
                "La cuenta del proveedor del modelo no tiene crédito. NO es un defecto del "
                + "código y reintentar no lo corrige: hay que cargar saldo. Mientras tanto "
                + "todos los turnos van a responder degradados.");
        }
        else
        {
            _log.LogError(
                excepcion,
                "El proveedor del modelo rechazó el request por mal armado. Es un defecto del "
                + "adaptador, no una falla del proveedor: reintentar no lo corrige.");
        }

        return Transporte(excepcion);
    }

    /// <summary>Si el 400 del proveedor es en realidad falta de saldo.</summary>
    private static bool SinCredito(string mensaje) =>
        mensaje.Contains("credit balance", StringComparison.OrdinalIgnoreCase)
        || mensaje.Contains("billing", StringComparison.OrdinalIgnoreCase)
        || mensaje.Contains("purchase credits", StringComparison.OrdinalIgnoreCase);

    /// <summary>Traduce al único vocabulario de falla que el pipeline conoce.</summary>
    private static HttpRequestException Transporte(AnthropicException excepcion) =>
        new("El proveedor del modelo no sirvió la llamada.", excepcion);

    /// <summary>
    /// Los conteos del SDK son de 64 bits y el registro operativo los guarda en 32.
    /// </summary>
    private static int Acotar(long tokens) =>
        tokens <= 0 ? 0 : tokens >= int.MaxValue ? int.MaxValue : (int)tokens;
}
