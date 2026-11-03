using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Notificaciones.Services;

namespace Modules.Notificaciones.Api;

[ApiController]
[Route("api/notificaciones")]
public sealed class NotificacionesController(IServicioNotificaciones servicio, IServicioPush push) : ControllerBase
{
    [HttpGet("ping")]
    [AllowAnonymous]
    public IActionResult Ping() => Ok(new { module = "notificaciones", status = "ok" });

    /// <summary>Unread notifications of the session user under the active profile.</summary>
    [HttpGet("no-leidas/cantidad")]
    [Authorize]
    public Task<CantidadNoLeidasDto> ContarNoLeidas(CancellationToken ct) => servicio.ContarNoLeidasAsync(ct);

    [HttpGet]
    [Authorize]
    public Task<PaginaNotificacionesDto> Listar(
        [FromQuery] string? pagina, [FromQuery] string? tamanio, [FromQuery] string? estado, CancellationToken ct) =>
        servicio.ListarAsync(pagina, tamanio, estado, ct);

    /// <summary>Idempotent. 404 for a missing, foreign or other-profile notification.</summary>
    [HttpPatch("{id:guid}/leida")]
    [Authorize]
    public Task<NotificacionDto> MarcarLeida(Guid id, CancellationToken ct) => servicio.MarcarLeidaAsync(id, ct);

    /// <summary>Exactly one of <c>{ ids }</c> or <c>{ todas: true, hasta? }</c>.</summary>
    [HttpPatch("leidas")]
    [Authorize]
    public Task<CantidadActualizadaDto> MarcarLeidas([FromBody] JsonElement cuerpo, CancellationToken ct) =>
        servicio.MarcarLeidasAsync(cuerpo, ct);

    /// <summary>VAPID public key for <c>pushManager.subscribe</c>; 404 when Web Push is disabled.</summary>
    [HttpGet("push/clave-publica")]
    [Authorize]
    public ActionResult<ClavePublicaPushDto> ClavePublicaPush() =>
        push.ClavePublica is { } clave ? new ClavePublicaPushDto(clave) : NotFound();

    /// <summary>Upsert by endpoint: a device already held by another user is reassigned to the session user.</summary>
    [HttpPost("push/suscripciones")]
    [Authorize]
    public async Task<IActionResult> Suscribir([FromBody] SuscripcionPushDto dto, CancellationToken ct)
    {
        if (push.ClavePublica is null) return NotFound();
        await push.SuscribirAsync(dto, ct);
        return NoContent();
    }

    /// <summary>Removes the subscription of that endpoint, only when it belongs to the session user.</summary>
    [HttpDelete("push/suscripciones")]
    [Authorize]
    public async Task<IActionResult> CancelarSuscripcion([FromBody] CancelarSuscripcionPushDto dto, CancellationToken ct)
    {
        await push.CancelarAsync(dto.Endpoint, ct);
        return NoContent();
    }

    /// <summary>Per-module push switches of the session user; every module, enabled by default.</summary>
    [HttpGet("push/preferencias")]
    [Authorize]
    public Task<PreferenciasPushDto> PreferenciasPush(CancellationToken ct) => push.ObtenerPreferenciasAsync(ct);

    [HttpPut("push/preferencias")]
    [Authorize]
    public Task<PreferenciasPushDto> GuardarPreferenciasPush([FromBody] PreferenciasPushDto dto, CancellationToken ct) =>
        push.GuardarPreferenciasAsync(dto, ct);
}
