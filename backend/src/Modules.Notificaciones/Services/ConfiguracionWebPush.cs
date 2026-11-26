using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebPush;

namespace Modules.Notificaciones.Services;

/// <summary>
/// Validated VAPID settings, computed once when the host starts (the push worker needs it). An
/// invalid configuration stops the host in Production, like the sealing keys; elsewhere it disables
/// push and logs why, so a developer notices without losing the rest of the app.
/// </summary>
public sealed class ConfiguracionWebPush
{
    public ConfiguracionWebPush(
        IOptions<OpcionesWebPush> opciones, IHostEnvironment ambiente, ILogger<ConfiguracionWebPush> logger)
    {
        var valores = opciones.Value;
        if (valores.SinClaves) return;

        var errores = valores.Errores();
        if (errores.Count > 0)
        {
            var detalle = string.Join(" ", errores);
            if (ambiente.IsProduction()) throw new InvalidOperationException(detalle);
            logger.LogError("Web Push deshabilitado por configuración inválida. {Detalle}", detalle);
            return;
        }

        if (valores.SubjectEnDominioReservado())
        {
            var mensaje = $"{OpcionesWebPush.Seccion}:Subject usa un dominio reservado (.local, .test, example.com...): "
                + "Apple lo rechaza con 403 BadJwtToken. Usá un mailto: o https: real.";
            if (ambiente.IsProduction()) throw new InvalidOperationException(mensaje);
            logger.LogWarning("{Mensaje}", mensaje);
        }

        ClavePublica = valores.PublicKey!.Trim();
        Vapid = new VapidDetails(valores.Subject!.Trim(), ClavePublica, valores.PrivateKey!.Trim());
    }

    public bool Habilitada => Vapid is not null;

    /// <summary>Public VAPID key for <c>pushManager.subscribe</c>; <c>null</c> when disabled.</summary>
    public string? ClavePublica { get; }

    public VapidDetails? Vapid { get; }
}
