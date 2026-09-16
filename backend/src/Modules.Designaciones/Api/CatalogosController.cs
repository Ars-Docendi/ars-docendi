using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Designaciones.Services;

namespace Modules.Designaciones.Api;

[ApiController]
[Route("api/designaciones/catalogos")]
[Authorize(Policy = Permisos.DesignacionesVer)]
public sealed class CatalogosController(ServicioCatalogosDesignaciones servicio) : ControllerBase
{
    [HttpGet]
    public Task<CatalogosDesignacionesDto> Obtener(CancellationToken ct) => servicio.ObtenerAsync(ct);
}
