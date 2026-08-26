using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Logging;
using Modules.Asistente.Application;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Adaptador de <see cref="IProveedorDeModelo"/> contra la API de Anthropic.
/// </summary>
/// <remarks>
/// Es el primer adaptador real del puerto y su única responsabilidad es
/// <b>traducir</b>: del contrato del puerto al del SDK a la ida, y del SDK al
/// vocabulario de fallas del módulo a la vuelta.
///
/// Todo lo demás ya existía antes que él y no lo toca: el techo de llamadas del
/// turno, el corte al proveedor, el timeout por llamada, el reintento de
/// transporte y el presupuesto punta a punta viven en decoradores y handlers que
/// no saben qué proveedor hay adentro.
///
/// Es el ÚNICO archivo del módulo que nombra a Anthropic. Un adaptador para otro
/// proveedor —o para un modelo propio, que es la migración que el proyecto
/// contempla— es otra clase al lado de ésta y otro brazo del <c>switch</c> de
/// <c>ModuleExtensions</c>; ninguno de los dos desplaza al otro y el pipeline no
/// se entera.
/// </remarks>
internal sealed class ProveedorAnthropic : IProveedorDeModelo, IDisposable
{
    /// <summary>Nombre de configuración de este proveedor.</summary>
    public const string Clave = "anthropic";

    private readonly AnthropicClient _cliente;
    private readonly string _modelo;
    private readonly Effort _esfuerzo;
    private readonly ILogger<ProveedorAnthropic> _log;

    public ProveedorAnthropic(
        HttpClient transporte,
        string clave,
        string modelo,
        string esfuerzo,
        ILogger<ProveedorAnthropic> log)
    {
        ArgumentNullException.ThrowIfNull(transporte);
        ArgumentException.ThrowIfNullOrWhiteSpace(clave);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelo);

        _cliente = new AnthropicClient
        {
            ApiKey = clave,

            // El transporte llega de afuera y ya trae el reintento del módulo en su
            // pipeline. Que el adaptador no lo construya es lo que permite probarlo
            // sin clave real ni red.
            HttpClient = transporte,

            // Cero, y no es una preferencia.
            //
            // El SDK reintenta por su cuenta ante errores de conexión, 408, 409, 429
            // y 5xx. El módulo YA reintenta, en ReintentoDeTransporte, y
            // OpcionesAsistente.MaximoDeIntentosDeTransporte documenta el peor caso
            // de un turno como MaximoDeLlamadasPorTurno × MaximoDeIntentosDeTransporte
            // —4 × 3 = 12 requests— diciendo que «las dos cotas explícitas son lo que
            // hace que ese número se pueda decir en voz alta».
            //
            // Con el reintento del SDK encendido ese número pasa a 4 × 3 × 3 = 36 y
            // NADA falla: el sistema hace el triple de requests que su propia
            // documentación declara, en silencio. Un solo lugar reintenta.
            MaxRetries = 0,
        };

        _modelo = modelo;
        _esfuerzo = EsfuerzoDe(esfuerzo);
        _log = log;
    }

    /// <summary>
    /// Traduce el esfuerzo de la configuración al del SDK.
    /// </summary>
    /// <remarks>
    /// Vive acá y no en la composición del módulo para que <c>Effort</c> —un tipo
    /// del SDK— no se filtre fuera de este archivo. Es lo que sostiene que el
    /// adaptador se pueda sacar borrando una clase.
    ///
    /// Un valor desconocido falla en vez de caer al default en silencio: alguien
    /// que escribió <c>alto</c> en vez de <c>high</c> quiere enterarse, no correr
    /// un mes con un esfuerzo que no eligió.
    /// </remarks>
    private static Effort EsfuerzoDe(string configurado) =>
        configurado.Trim().ToLowerInvariant() switch
        {
            "low" => Effort.Low,
            "medium" => Effort.Medium,
            "high" => Effort.High,
            "xhigh" => Effort.Xhigh,
            "max" => Effort.Max,
            _ => throw new InvalidOperationException(
                $"Esfuerzo '{configurado}' desconocido en la configuración del asistente. "
                + "Los valores aceptados son: low, medium, high, xhigh, max."),
        };

    /// <summary>
    /// Incluye el modelo, no solo el proveedor.
    /// </summary>
    /// <remarks>
    /// <see cref="Clave"/> es lo que la configuración elige; esto es lo que se
    /// cuenta después. Comparar costo contra calidad entre modelos es justamente
    /// para lo que existe el evaluador, y un reporte que solo dice «anthropic» no
    /// permite saber cuál de los dos corrió.
    /// </remarks>
    public string Nombre => $"{Clave}/{_modelo}";

    public bool EsSimulado => false;

    public async Task<RespuestaDelModelo> CompletarAsync(
        SolicitudAlModelo solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        // La temperatura NO viaja: los modelos Claude actuales la rechazan con 400.
        // El puerto la conserva porque otros proveedores —y un modelo propio— sí la
        // usan, y amoldar el puerto a las carencias de un adaptador convertiría al
        // puerto en la forma de ese adaptador. El determinismo que el carril SQL
        // necesita se pide por instrucción dentro del prefijo y por el esfuerzo.
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
            OutputConfig = new OutputConfig { Effort = _esfuerzo },
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
            // El SDK envolvió nuestra propia cancelación en un tipo suyo. Se la
            // devuelve al decorador como cancelación, que es lo único que él sabe
            // reconocer.
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
            // Límite de tasa, 5xx, E/S y cualquier otra falla del SDK. Todas
            // significan lo mismo para el pipeline: el proveedor no sirvió la
            // llamada.
            throw Transporte(excepcion);
        }

        return Traducir(respuesta);
    }

    public void Dispose() => _cliente.Dispose();

    /// <summary>
    /// Convierte la respuesta del SDK al contrato del puerto.
    /// </summary>
    /// <remarks>
    /// Una respuesta sin bloque de texto NO es una caída. El modelo puede haberse
    /// rehusado, o haber pensado sin escribir. Devolver texto vacío deja que el
    /// pipeline haga lo correcto —el validador rechaza una SQL vacía y el turno
    /// abstiene—, mientras que tratarlo como falla de transporte abriría el corte
    /// por algo que no es una falla de servicio y le contestaría al usuario
    /// «servicio degradado» cuando el servicio anduvo.
    /// </remarks>
    private RespuestaDelModelo Traducir(Message respuesta)
    {
        if (respuesta.StopReason == "refusal")
        {
            // Warning y no Error: el proveedor funcionó. Saber que se rehusó —y no
            // que devolvió algo inválido— cambia qué se investiga.
            _log.LogWarning(
                "El modelo se rehusó a responder ({Categoria}).",
                respuesta.StopDetails?.Category);
        }

        var texto = string.Concat(
            respuesta.Content.Select(bloque => bloque.Value).OfType<TextBlock>().Select(b => b.Text));

        // Los tokens servidos desde caché SUMAN a los de entrada. Son tokens de
        // prompt reales; lo único distinto es lo que cuestan. Contar solo
        // InputTokens haría que, el día que la caché empiece a funcionar, el
        // prefijo del esquema —el bloque más grande del prompt— desapareciera del
        // registro y pareciera que los prompts se achicaron a la mitad.
        //
        // El registro operativo mide tamaño de prompt, no factura; su propia
        // documentación lo dice.
        return new RespuestaDelModelo(
            texto,
            Acotar(respuesta.Usage.InputTokens + (respuesta.Usage.CacheReadInputTokens ?? 0)),
            Acotar(respuesta.Usage.OutputTokens),
            EsSimulada: false);
    }

    /// <summary>
    /// La credencial no sirve. Se degrada igual, pero se grita.
    /// </summary>
    /// <remarks>
    /// Hay dos formas de tratar esto y las dos tienen un costo. Como falla de
    /// transporte a secas, el usuario recibe la degradación que el contrato
    /// promete pero en los logs parece intermitencia normal y una clave mal
    /// cargada puede quedar así días. Como excepción propia que atraviesa el
    /// pipeline, es imposible de ignorar pero cada request termina en 500 — y el
    /// módulo tiene un contrato de cuatro estados justamente para que un problema
    /// del proveedor nunca sea un 500.
    ///
    /// Se toman las dos mitades buenas: se degrada, y se registra en Error con la
    /// causa nombrada.
    /// </remarks>
    private HttpRequestException Credencial(AnthropicException excepcion)
    {
        _log.LogError(
            excepcion,
            "El proveedor del modelo rechazó la credencial. Revisá Asistente__ClaveDelProveedor "
            + "en este ambiente: mientras siga así, todos los turnos van a responder degradados.");

        return Transporte(excepcion);
    }

    /// <summary>El request salió mal armado de acá. Ningún reintento lo arregla.</summary>
    private HttpRequestException Armado(AnthropicException excepcion)
    {
        _log.LogError(
            excepcion,
            "El proveedor del modelo rechazó el request por mal armado. Es un defecto del "
            + "adaptador, no una falla del proveedor: reintentar no lo corrige.");

        return Transporte(excepcion);
    }

    /// <summary>
    /// Traduce al único vocabulario de falla que el pipeline conoce.
    /// </summary>
    /// <remarks>
    /// <c>ProveedorConBreaker</c> cuenta exactamente dos formas de fallo: la
    /// cancelación de su propio token y <see cref="HttpRequestException"/>.
    /// Cualquier otra excepción lo ATRAVIESA sin contarse, así que dejar escapar
    /// un tipo del SDK haría que el corte no abriera nunca: un proveedor caído al
    /// cien por ciento seguiría recibiendo llamadas turno tras turno.
    ///
    /// Ensanchar el <c>catch</c> del breaker está descartado: acoplaría un
    /// decorador genérico a un adaptador concreto, y el adaptador siguiente
    /// traería sus propios tipos.
    ///
    /// El mensaje del proveedor no se propaga al usuario: <c>CarrilSql</c> lo
    /// resuelve como degradado y responde con su texto propio.
    /// </remarks>
    private static HttpRequestException Transporte(AnthropicException excepcion) =>
        new("El proveedor del modelo no sirvió la llamada.", excepcion);

    /// <summary>
    /// Los conteos del SDK son de 64 bits y el registro operativo los guarda en 32.
    /// </summary>
    /// <remarks>
    /// Acota en vez de castear: un <c>checked</c> que explota le rompería el turno
    /// al usuario por un número de telemetría, y un cast sin acotar guardaría un
    /// negativo. Ninguna respuesta real se acerca al tope.
    /// </remarks>
    private static int Acotar(long tokens) =>
        tokens <= 0 ? 0 : tokens >= int.MaxValue ? int.MaxValue : (int)tokens;
}
