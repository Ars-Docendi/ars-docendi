using ArsDocendi.Shared.Identity.Administracion;
using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArsDocendi.Host.Api;

[ApiController]
[Route("api/administracion/catalogos")]
[Authorize(Policy = Permisos.UsuariosVer)]
public sealed class CatalogosAdministracionController(ServicioUsuarios servicio) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<CatalogosUsuariosDto>(StatusCodes.Status200OK)]
    public Task<CatalogosUsuariosDto> Obtener(CancellationToken ct) =>
        servicio.ObtenerCatalogosAsync(ct);
}
