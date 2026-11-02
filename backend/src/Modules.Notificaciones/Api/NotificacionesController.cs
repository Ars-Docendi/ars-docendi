using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Notificaciones.Services;

namespace Modules.Notificaciones.Api;

[ApiController]
[Route("api/notificaciones")]
public sealed class NotificacionesController(IServicioNotificaciones servicio) : ControllerBase
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
}
