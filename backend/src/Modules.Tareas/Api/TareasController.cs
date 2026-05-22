using Microsoft.AspNetCore.Mvc;

namespace Modules.Tareas.Api;

[ApiController]
[Route("api/tareas")]
public sealed class TareasController : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { module = "tareas", status = "ok" });
}
