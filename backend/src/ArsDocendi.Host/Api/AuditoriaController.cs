using ArsDocendi.Host.Administracion;
using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArsDocendi.Host.Api;

[ApiController]
[Route("api/administracion/auditoria")]
[Authorize]
public sealed class AuditoriaController(ServicioAuditoria servicio) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permisos.AuditoriaVer)]
    [ProducesResponseType<PaginaAuditoriaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PaginaAuditoriaDto> Listar([FromQuery] ConsultaAuditoriaDto filtros, CancellationToken ct) =>
        servicio.ListarAsync(filtros, ct);
}
