using System.Net;
using Microsoft.Extensions.Logging;
using Modules.Notificaciones.Domain;
using WebPush;

namespace Modules.Notificaciones.Services;

public enum ResultadoEnvioPush
{
    Enviado,

    /// <summary>The push service says the subscription no longer exists (404/410): delete it.</summary>
    Caducada,

    /// <summary>Any other failure: kept for the next notice.</summary>
    Fallo,
}

/// <summary>
/// The external boundary: the only thing that talks to the push services over HTTPS. Implementations
/// never throw for a delivery failure and never log the payload.
/// </summary>
public interface IEnviadorPush
{
    Task<ResultadoEnvioPush> EnviarAsync(SuscripcionPush destino, string carga, CancellationToken ct);
}

internal sealed class EnviadorWebPush(
    ConfiguracionWebPush configuracion, WebPushClient cliente, ILogger<EnviadorWebPush> logger) : IEnviadorPush
{
    private const int MaximoMotivo = 120;

    // A notice that could not be delivered in a day is stale: do not let the push service queue it for weeks.
    private const int TtlSegundos = 86400;

    public async Task<ResultadoEnvioPush> EnviarAsync(SuscripcionPush destino, string carga, CancellationToken ct)
    {
        if (configuracion.Vapid is not { } vapid) return ResultadoEnvioPush.Fallo;
        var opciones = new Dictionary<string, object> { ["vapidDetails"] = vapid, ["TTL"] = TtlSegundos };
        try
        {
            await cliente.SendNotificationAsync(
                new PushSubscription(destino.Endpoint, destino.P256dh, destino.Auth), carga, opciones, ct);
            return ResultadoEnvioPush.Enviado;
        }
        catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return ResultadoEnvioPush.Caducada;
        }
        catch (WebPushException ex)
        {
            // Only the status and the service's reason (e.g. Apple's {"reason":"BadJwtToken"}); the
            // payload and the endpoint, which identifies a device, are never logged.
            var motivo = ex.HttpResponseMessage is { } respuesta
                ? await LeerMotivoAsync(respuesta)
                : string.Empty;
            logger.LogWarning("Push rechazado por el servicio ({Estado}): {Motivo}", (int)ex.StatusCode, motivo);
            return ResultadoEnvioPush.Fallo;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Push no enviado: el servicio no respondió a tiempo");
            return ResultadoEnvioPush.Fallo;
        }
        catch (Exception ex)
        {
            logger.LogWarning("Push no enviado ({TipoError})", ex.GetType().Name);
            return ResultadoEnvioPush.Fallo;
        }
    }

    private static async Task<string> LeerMotivoAsync(HttpResponseMessage respuesta)
    {
        try
        {
            var texto = await respuesta.Content.ReadAsStringAsync(CancellationToken.None);
            return texto.Length > MaximoMotivo ? texto[..MaximoMotivo] : texto;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
