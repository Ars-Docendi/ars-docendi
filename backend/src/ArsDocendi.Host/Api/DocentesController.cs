using System.Security.Claims;
using ArsDocendi.Host.Administracion;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity.Administracion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArsDocendi.Host.Api;

[ApiController]
[Route("api/administracion/docentes")]
[Authorize]
public sealed class DocentesController(
    ServicioDocentes servicio,
    ResolutorAlcanceDocentes resolutorAlcance) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Politicas.DocentesVer)]
    public async Task<IReadOnlyList<DocenteAdministracionDto>> Listar(
        [FromQuery] string? busqueda,
        [FromQuery] Guid? materiaId,
        [FromQuery] string? rol,
        [FromQuery] bool? activo,
        CancellationToken ct) => await servicio.ListarAsync(
            busqueda, materiaId, rol, activo, await ObtenerMateriasVisiblesAsync(ct), ct);

    [HttpGet("catalogos")]
    [Authorize(Policy = Politicas.DocentesVer)]
    public async Task<CatalogosDocentesDto> ObtenerCatalogos(CancellationToken ct) =>
        await servicio.ObtenerCatalogosAsync(await ObtenerMateriasVisiblesAsync(ct), ct);

    [HttpGet("{personaId:guid}")]
    [Authorize(Policy = Politicas.DocentesVer)]
    public async Task<DocenteAdministracionDto> Obtener(Guid personaId, CancellationToken ct) =>
        await servicio.ObtenerAsync(personaId, await ObtenerMateriasVisiblesAsync(ct), ct);

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

    private async Task<IReadOnlySet<Guid>?> ObtenerMateriasVisiblesAsync(CancellationToken ct)
    {
        if (User.HasClaim(Permisos.Claim, Permisos.UsuariosVer)) return null;

        var usuarioId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return await resolutorAlcance.ObtenerMateriasDeJefaturaAsync(usuarioId, ct);
    }
}
