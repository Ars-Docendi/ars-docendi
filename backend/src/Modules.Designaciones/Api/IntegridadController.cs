using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Designaciones.Services.Integridad;

namespace Modules.Designaciones.Api;

/// <summary>Verificación de la cadena de sellos de un período [BR-designaciones-019].</summary>
[ApiController]
[Route("api/designaciones/periodos")]
[Authorize(Policy = Permisos.AuditoriaVer)]
public sealed class IntegridadController(VerificadorIntegridadPedidos verificador) : ControllerBase
{
    [HttpGet("{id:guid}/integridad")]
    public Task<IntegridadPeriodoDto> Consultar(Guid id, CancellationToken ct) =>
        verificador.ConsultarAsync(id, ct);
}
