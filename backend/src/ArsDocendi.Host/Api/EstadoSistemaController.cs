using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArsDocendi.Host.Api;

[ApiController]
[Route("api/administracion/sistema")]
[Authorize]
public sealed class EstadoSistemaController(Host.Administracion.ServicioEstadoSistema servicio) : ControllerBase
{
    [HttpGet("estado")]
    [Authorize(Policy = Permisos.SistemaEstadoVer)]
    [ProducesResponseType<Host.Administracion.EstadoSistemaDto>(StatusCodes.Status200OK)]
    public Task<Host.Administracion.EstadoSistemaDto> ObtenerEstado(CancellationToken ct) =>
        servicio.ObtenerEstadoAsync(ct);
}
