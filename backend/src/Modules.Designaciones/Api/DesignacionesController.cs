using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Designaciones.Services;

namespace Modules.Designaciones.Api;

[ApiController]
[Route("api/designaciones")]
public sealed class DesignacionesController : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { module = "designaciones", status = "ok" });

    /// <summary>Designaciones vigentes de quien consulta; sin persona vinculada, lista vacía.</summary>
    [Authorize]
    [HttpGet("vigentes/mias")]
    public Task<IReadOnlyList<DesignacionPropiaDto>> VigentesPropias(
        IServicioDesignacionesPropias servicio, CancellationToken ct) => servicio.ListarVigentesAsync(ct);
}
