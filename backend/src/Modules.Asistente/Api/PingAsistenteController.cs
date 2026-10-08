using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Modules.Asistente.Api;

/// <summary>
/// Smoke test del módulo (invariante #3).
/// </summary>
/// <remarks>
/// <b>Vive en su propio controller y eso no es prolijidad.</b> Estuvo junto al
/// endpoint del turno hasta que ése ganó dependencias, y ahí el ping se rompió: para
/// construir el controller, el contenedor tenía que resolver las cadenas de solo
/// lectura, cuya fábrica falla si el ambiente no las configuró. Un ping que necesita
/// configuración de base deja de poder distinguir «el módulo está cargado» de «la
/// base responde», que es exactamente lo que el invariante le pide.
///
/// Sin constructor y sin dependencias: es la única forma de que la separación sea
/// estructural en vez de una intención.
/// </remarks>
[ApiController]
[Route("api/asistente")]
public sealed class PingAsistenteController : ControllerBase
{
    /// <summary>
    /// Responde sin tocar la base ni ningún servicio externo.
    /// </summary>
    /// <remarks>
    /// La forma de la respuesta es la misma que la de los otros módulos: es el
    /// contrato del smoke test, no texto de dominio.
    /// </remarks>
    [AllowAnonymous]
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { module = "asistente", status = "ok" });
}
