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
    [ProducesResponseType<Host.Administracion.EstadoBaseDatosDto>(StatusCodes.Status200OK)]
    public Task<Host.Administracion.EstadoBaseDatosDto> ObtenerEstado(CancellationToken ct) =>
        servicio.ObtenerEstadoAsync(ct);
}
