using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Modules.Asistente.Api;

[ApiController]
[Route("api/asistente")]
public sealed class AsistenteController : ControllerBase
{
    /// <summary>
    /// Smoke test del módulo (invariante #3).
    /// </summary>
    /// <remarks>
    /// No toca la base ni ningún servicio externo, y eso es el punto: tiene que
    /// poder distinguir «el módulo está cargado» de «la base responde». Un ping
    /// que consulta la base deja de servir como smoke test del módulo, porque
    /// falla por un motivo que no es el suyo.
    ///
    /// La forma de la respuesta es la misma que la de los otros módulos: es el
    /// contrato del smoke test, no texto de dominio.
    /// </remarks>
    [AllowAnonymous]
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { module = "asistente", status = "ok" });
}
