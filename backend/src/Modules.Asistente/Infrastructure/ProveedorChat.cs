using System.Net;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Modules.Asistente.Application;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// What differs per cloud company around an <see cref="IChatClient"/>: how the
/// request is built and how a provider failure is recognised
/// (asistente-modelo-en-caliente, D10).
/// </summary>
/// <remarks>
/// The spike showed that two things pass only through provider-specific raw types
/// (Anthropic's <c>cache_control</c> and <c>output_config.effort</c>), so the
/// request cannot be built portably. The implementations live in the one file that
/// names the SDKs (<c>ClientesDeChat.cs</c>), which keeps this class free of them.
/// </remarks>
internal interface IConstructorDeChat
{
    /// <summary>The messages and options for one call.</summary>
    (IList<ChatMessage> Mensajes, ChatOptions Opciones) Armar(SolicitudAlModelo solicitud);

    /// <summary>
    /// Whether <paramref name="excepcion"/> is a provider failure of this company's
    /// SDK, and the HTTP status it carried (null for a transport failure).
    /// </summary>
    bool EsFallaDelProveedor(Exception excepcion, out HttpStatusCode? estado);
}

/// <summary>
/// <see cref="IProveedorDeModelo"/> for the four cloud companies over a
/// Microsoft.Extensions.AI <see cref="IChatClient"/> (asistente-modelo-en-caliente,
/// D1/D2). <c>Nombre</c> is <c>"{empresa}/{modelo}"</c>.
/// </summary>
/// <remarks>
/// One attempt per call: the module's only retry authority is
/// <see cref="ReintentoDeTransporte"/> in the shared transport, and the SDK retries
/// are zeroed when the client is built.
///
/// It owns failure translation. Neither SDK throws <see cref="HttpRequestException"/>,
/// and <see cref="ProveedorConBreaker"/> counts exactly two shapes of failure: a
/// cancellation of its own token and <see cref="HttpRequestException"/>. So a
/// provider failure leaves here as <see cref="HttpRequestException"/> with the
/// status preserved, and with NO inner exception and no provider text: a provider
/// can echo part of the key in a 401, and the carril logs the exception it catches.
/// </remarks>
internal sealed class ProveedorChat : IProveedorDeModelo, IDisposable
{
    private const string ConteoDeCreacionDeCache = "CacheCreationInputTokens";

    /// <summary>
    /// Key of <see cref="Exception.Data"/> set (to <c>true</c>) on a failure whose provider
    /// text says the account is out of credit. The text itself never leaves this class:
    /// only this flag does, for the administrator's connection test.
    /// </summary>
    internal const string IndicioDeSinCredito = "ars.sin-credito";

    // Provider wordings for "no credit": Anthropic 400 "credit balance is too low",
    // OpenAI 429 "insufficient_quota" / "exceeded your current quota", and the like.
    private static readonly string[] MarcasDeSinCredito =
        ["credit balance", "insufficient_quota", "exceeded your current quota", "billing", "insufficient credit"];

    private readonly string _empresa;
    private readonly string _modelo;
    private readonly IChatClient _cliente;
    private readonly IConstructorDeChat _constructor;
    private readonly ILogger<ProveedorChat> _log;
    private readonly IDisposable? _dueno;
    private bool _dispuesto;

    public ProveedorChat(
        string empresa,
        string modelo,
        IChatClient cliente,
        IConstructorDeChat constructor,
        ILogger<ProveedorChat> log,
        IDisposable? dueno = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(empresa);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelo);
        ArgumentNullException.ThrowIfNull(cliente);
        ArgumentNullException.ThrowIfNull(constructor);

        _empresa = empresa;
        _modelo = modelo;
        _cliente = cliente;
        _constructor = constructor;
        _log = log;
        _dueno = dueno;
    }

    public string Nombre => $"{_empresa}/{_modelo}";

    public bool EsSimulado => false;

    public async Task<RespuestaDelModelo> CompletarAsync(
        SolicitudAlModelo solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var (mensajes, opciones) = _constructor.Armar(solicitud);

        ChatResponse respuesta;
        try
        {
            respuesta = await _cliente.GetResponseAsync(mensajes, opciones, ct);
        }
        catch (OperationCanceledException)
        {
            // From the breaker's token or the request's: the decorator recognises it.
            throw;
        }
        catch (Exception excepcion) when (ct.IsCancellationRequested)
        {
            // The SDK wrapped our own cancellation in a type of its own.
            throw new OperationCanceledException(
                "La llamada al proveedor del modelo se canceló.", excepcion, ct);
        }
        catch (Exception excepcion) when (_constructor.EsFallaDelProveedor(excepcion, out var estado))
        {
            // Only the company and the status are logged: the provider's own text
            // may carry part of the key.
            if (estado is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                _log.LogError(
                    "El proveedor {Empresa} rechazó la credencial (HTTP {Estado}). Revisá la clave guardada "
                    + "de esa empresa: mientras siga así, todos los turnos van a responder degradados.",
                    _empresa,
                    (int)estado);
            }
            else
            {
                _log.LogWarning(
                    "El proveedor {Empresa} no sirvió la llamada (HTTP {Estado}).",
                    _empresa,
                    estado is { } e ? (int)e : 0);
            }

            var falla = new HttpRequestException("El proveedor del modelo no sirvió la llamada.", null, estado);
            if (IndicaFaltaDeCredito(excepcion.Message))
            {
                falla.Data[IndicioDeSinCredito] = true;
            }

            throw falla;
        }

        return Traducir(respuesta, solicitud);
    }

    public void Dispose()
    {
        if (_dispuesto)
        {
            return;
        }

        _dispuesto = true;
        _cliente.Dispose();
        _dueno?.Dispose();
    }

    private RespuestaDelModelo Traducir(ChatResponse respuesta, SolicitudAlModelo solicitud)
    {
        if (respuesta.FinishReason == ChatFinishReason.ContentFilter)
        {
            _log.LogWarning("El modelo {Nombre} se rehusó a responder.", Nombre);
        }

        // Same as ProveedorAnthropic: a cut-off answer is shouted, because it looks
        // exactly like a prudent assistant otherwise.
        var seQuedoSinTokens = respuesta.FinishReason == ChatFinishReason.Length;
        if (seQuedoSinTokens)
        {
            _log.LogWarning(
                "La respuesta del modelo se cortó al agotar los {MaximoDeTokens} tokens de presupuesto "
                + "(esfuerzo {Esfuerzo}). Con esfuerzo configurado el razonamiento sale de ese mismo techo.",
                solicitud.MaximoDeTokens,
                solicitud.Esfuerzo);
        }

        var uso = respuesta.Usage;
        var deCache = Acotar(uso?.CachedInputTokenCount ?? 0);

        // Anthropic's adapter sums input + cache read + cache creation into
        // InputTokenCount. The module reports input + cache read, so the creation
        // count is taken out. Other companies expose no such count.
        long creacion = 0;
        _ = uso?.AdditionalCounts?.TryGetValue(ConteoDeCreacionDeCache, out creacion);

        return new RespuestaDelModelo(
            respuesta.Text ?? string.Empty,
            Acotar((uso?.InputTokenCount ?? 0) - creacion),
            Acotar(uso?.OutputTokenCount ?? 0),
            EsSimulada: false,
            seQuedoSinTokens,
            deCache);
    }

    private static bool IndicaFaltaDeCredito(string? texto) =>
        texto is not null
        && MarcasDeSinCredito.Any(marca => texto.Contains(marca, StringComparison.OrdinalIgnoreCase));

    private static int Acotar(long tokens) =>
        tokens <= 0 ? 0 : tokens >= int.MaxValue ? int.MaxValue : (int)tokens;
}
