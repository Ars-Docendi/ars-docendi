using Microsoft.AspNetCore.Mvc;

namespace Modules.Designaciones.Api;

[ApiController]
[Route("api/designaciones")]
public sealed class DesignacionesController : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { module = "designaciones", status = "ok" });
}
