using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Modules.Asistente.Api;

/// <summary>
/// Smoke test del módulo (invariante #3). Un ping que necesita configuración de base deja de poder
/// distinguir «el módulo está cargado» de «la base responde», que es exactamente lo que el
/// invariante le pide.
/// </summary>
[ApiController]
[Route("api/asistente")]
public sealed class PingAsistenteController : ControllerBase
{
    /// <summary>Responde sin tocar la base ni ningún servicio externo.</summary>
    [AllowAnonymous]
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { module = "asistente", status = "ok" });
}
