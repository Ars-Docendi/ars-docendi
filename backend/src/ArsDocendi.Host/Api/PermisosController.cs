using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity.Administracion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArsDocendi.Host.Api;

[ApiController]
[Route("api/administracion/permisos")]
[Authorize(Policy = Permisos.RolesVer)]
public sealed class PermisosController(ServicioRoles servicio) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<PermisoAdministracionDto>> Listar(CancellationToken ct) =>
        servicio.ListarPermisosAsync(ct);
}
