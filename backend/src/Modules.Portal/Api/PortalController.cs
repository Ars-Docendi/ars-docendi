using Microsoft.AspNetCore.Mvc;

namespace Modules.Portal.Api;

[ApiController]
[Route("api/portal")]
public sealed class PortalController : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { module = "portal", status = "ok" });
}
