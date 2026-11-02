using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Portal.Application;

namespace Modules.Portal.Api;

[ApiController]
[Route("api/portal/vocabulario")]
[Authorize]
public sealed class VocabularioController(ServicioVocabulario servicio) : ControllerBase
{
    /// <summary>Términos para elegir habilidades e intereses.</summary>
    [HttpGet]
    public Task<IReadOnlyList<string>> Ofrecidos(CancellationToken ct) => servicio.ListarOfrecidosAsync(ct);

    [HttpGet("terminos")]
    [Authorize(Policy = Permisos.PortalVocabularioAdministrar)]
    public Task<IReadOnlyList<TerminoVocabularioDto>> Listar(CancellationToken ct) => servicio.ListarAsync(ct);

    [HttpPost("terminos/{id:guid}/incorporar")]
    [Authorize(Policy = Permisos.PortalVocabularioAdministrar)]
    public async Task<IActionResult> Incorporar(Guid id, CancellationToken ct)
    {
        await servicio.IncorporarAsync(id, ct);
        return NoContent();
    }

    [HttpPut("terminos/{id:guid}")]
    [Authorize(Policy = Permisos.PortalVocabularioAdministrar)]
    public async Task<IActionResult> Renombrar(Guid id, RenombrarTerminoDto datos, CancellationToken ct)
    {
        await servicio.RenombrarAsync(id, datos, ct);
        return NoContent();
    }

    [HttpPost("terminos/{id:guid}/fusionar")]
    [Authorize(Policy = Permisos.PortalVocabularioAdministrar)]
    public async Task<IActionResult> Fusionar(Guid id, FusionarTerminoDto datos, CancellationToken ct)
    {
        await servicio.FusionarAsync(id, datos, ct);
        return NoContent();
    }
}
