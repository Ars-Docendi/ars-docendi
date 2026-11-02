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
    /// <summary>Con <paramref name="pedidoId"/> (edición), la persona de ese pedido sigue siendo elegible.</summary>
    [HttpGet]
    public Task<CatalogosDesignacionesDto> Obtener([FromQuery] Guid? pedidoId, CancellationToken ct) =>
        servicio.ObtenerAsync(ct, pedidoId);
}
