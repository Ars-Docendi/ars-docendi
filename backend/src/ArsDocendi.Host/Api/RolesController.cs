using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity.Administracion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArsDocendi.Host.Api;

[ApiController]
[Route("api/administracion/roles")]
[Authorize]
public sealed class RolesController(ServicioRoles servicio) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permisos.RolesVer)]
    public Task<IReadOnlyList<RolAdministracionDto>> Listar(CancellationToken ct) =>
        servicio.ListarAsync(ct);

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permisos.RolesVer)]
    public Task<RolAdministracionDto> Obtener(Guid id, CancellationToken ct) =>
        servicio.ObtenerAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = Permisos.RolesAdministrar)]
    public async Task<ActionResult<RolAdministracionDto>> Crear(
        CrearRolDto datos,
        CancellationToken ct)
    {
        // Asignar permisos en el alta es gestionar membresía: exige el mismo permiso que el PUT.
        if (datos.PermisoIds is { Count: > 0 } && !User.HasClaim(Permisos.Claim, Permisos.RolesGestionarMembresia))
        {
            return Forbid();
        }
        var creado = await servicio.CrearAsync(datos, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permisos.RolesAdministrar)]
    public Task<RolAdministracionDto> Editar(
        Guid id,
        EditarRolDto datos,
        CancellationToken ct) => servicio.EditarAsync(id, datos, ct);

    [HttpPost("{id:guid}/desactivar")]
    [Authorize(Policy = Permisos.RolesAdministrar)]
    public Task<RolAdministracionDto> Desactivar(
        Guid id,
        CambiarEstadoRolDto datos,
        CancellationToken ct) => servicio.CambiarEstadoAsync(id, false, datos, ct);

    [HttpPost("{id:guid}/activar")]
    [Authorize(Policy = Permisos.RolesAdministrar)]
    public Task<RolAdministracionDto> Activar(
        Guid id,
        CambiarEstadoRolDto datos,
        CancellationToken ct) => servicio.CambiarEstadoAsync(id, true, datos, ct);

    [HttpGet("{id:guid}/permisos")]
    [Authorize(Policy = Permisos.RolesVer)]
    public async Task<IReadOnlyList<PermisoAdministracionDto>> ObtenerPermisos(
        Guid id,
        CancellationToken ct) => (await servicio.ObtenerAsync(id, ct)).Permisos;

    [HttpPut("{id:guid}/permisos")]
    [Authorize(Policy = Permisos.RolesGestionarMembresia)]
    public Task<IReadOnlyList<PermisoAdministracionDto>> ReemplazarPermisos(
        Guid id,
        ReemplazarPermisosDto datos,
        CancellationToken ct) => servicio.ReemplazarPermisosAsync(id, datos, ct);
}
