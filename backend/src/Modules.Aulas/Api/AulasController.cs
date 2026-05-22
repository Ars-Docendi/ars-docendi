using Microsoft.AspNetCore.Mvc;

namespace Modules.Aulas.Api;

[ApiController]
[Route("api/aulas")]
public sealed class AulasController : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { module = "aulas", status = "ok" });
}
