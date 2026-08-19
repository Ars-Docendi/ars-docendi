using ArsDocendi.Host.Administracion;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity.Administracion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArsDocendi.Host.Api;

[ApiController]
[Route("api/administracion/docentes")]
[Authorize]
public sealed class DocentesController(ServicioDocentes servicio) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permisos.UsuariosVer)]
    public Task<IReadOnlyList<DocenteAdministracionDto>> Listar(
        [FromQuery] string? busqueda,
        [FromQuery] Guid? materiaId,
        [FromQuery] string? rol,
        [FromQuery] bool? activo,
        CancellationToken ct) => servicio.ListarAsync(busqueda, materiaId, rol, activo, ct);

    [HttpGet("catalogos")]
    [Authorize(Policy = Permisos.UsuariosVer)]
    public Task<CatalogosDocentesDto> ObtenerCatalogos(CancellationToken ct) =>
        servicio.ObtenerCatalogosAsync(ct);

    [HttpGet("{personaId:guid}")]
    [Authorize(Policy = Permisos.UsuariosVer)]
    public Task<DocenteAdministracionDto> Obtener(Guid personaId, CancellationToken ct) =>
        servicio.ObtenerAsync(personaId, ct);

    [HttpPost]
    [Authorize(Policy = Permisos.UsuariosAdministrar)]
    public async Task<ActionResult<DocenteAdministracionDto>> Crear(
        GuardarDocenteDto datos,
        CancellationToken ct)
    {
        var creado = await servicio.GuardarAsync(null, datos, ct);
        return CreatedAtAction(nameof(Obtener), new { personaId = creado.PersonaId }, creado);
    }

    [HttpPut("{personaId:guid}")]
    [Authorize(Policy = Permisos.UsuariosAdministrar)]
    public Task<DocenteAdministracionDto> Editar(
        Guid personaId,
        GuardarDocenteDto datos,
        CancellationToken ct) => servicio.GuardarAsync(personaId, datos, ct);

    [HttpPost("{personaId:guid}/activar")]
    [Authorize(Policy = Permisos.UsuariosAdministrar)]
    public Task<DocenteAdministracionDto> Activar(
        Guid personaId,
        CambiarEstadoUsuarioDto datos,
        CancellationToken ct) => servicio.CambiarEstadoAsync(personaId, true, datos.Version, ct);

    [HttpPost("{personaId:guid}/desactivar")]
    [Authorize(Policy = Permisos.UsuariosAdministrar)]
    public Task<DocenteAdministracionDto> Desactivar(
        Guid personaId,
        CambiarEstadoUsuarioDto datos,
        CancellationToken ct) => servicio.CambiarEstadoAsync(personaId, false, datos.Version, ct);
}
