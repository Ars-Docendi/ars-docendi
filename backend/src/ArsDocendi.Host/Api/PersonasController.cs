using System.Security.Claims;
using ArsDocendi.Host.Administracion;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity.Administracion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArsDocendi.Host.Api;

/// <summary>
/// Administración unificada de personas. Ver: <c>personas.ver</c> (todas) o
/// <c>personas.ver_ambito</c> (personas designadas en las materias de jefatura del actor);
/// mutar: <c>personas.administrar</c>.
/// </summary>
[ApiController]
[Route("api/administracion/personas")]
[Authorize]
public sealed class PersonasController(
    ServicioAdministracionPersonas servicio,
    AlcancePersonas alcance) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<PersonaAdministracionDto>> Listar(CancellationToken ct) =>
        await servicio.ListarAsync(await alcance.MateriasVisiblesAsync(User, ct), ct);

    [HttpGet("catalogos")]
    public async Task<CatalogosPersonasDto> ObtenerCatalogos(CancellationToken ct)
    {
        await alcance.MateriasVisiblesAsync(User, ct);
        return await servicio.ObtenerCatalogosAsync(ct);
    }

    /// <summary>El DNI viaja en el body para que no quede en rutas ni logs de acceso.</summary>
    [HttpPost("documento/verificar")]
    [Authorize(Policy = Permisos.PersonasAdministrar)]
    public Task<DocumentoRegistradoDto> VerificarDocumento(VerificarDocumentoDto datos, CancellationToken ct) =>
        servicio.VerificarDocumentoAsync(datos.Documento, datos.ExceptoPersonaId, ct);

    [HttpGet("{personaId:guid}")]
    public async Task<PersonaAdministracionDto> Obtener(Guid personaId, CancellationToken ct) =>
        await servicio.ObtenerAsync(personaId, await alcance.MateriasVisiblesAsync(User, ct), ct);

    [HttpPost]
    [Authorize(Policy = Permisos.PersonasAdministrar)]
    public async Task<ActionResult<PersonaAdministracionDto>> Crear(GuardarPersonaDto datos, CancellationToken ct)
    {
        var creada = await servicio.GuardarAsync(null, datos, ct);
        return CreatedAtAction(nameof(Obtener), new { personaId = creada.PersonaId }, creada);
    }

    [HttpPut("{personaId:guid}")]
    [Authorize(Policy = Permisos.PersonasAdministrar)]
    public Task<PersonaAdministracionDto> Editar(Guid personaId, GuardarPersonaDto datos, CancellationToken ct) =>
        servicio.GuardarAsync(personaId, datos, ct);

    [HttpPost("{personaId:guid}/activar")]
    [Authorize(Policy = Permisos.PersonasAdministrar)]
    public Task<PersonaAdministracionDto> Activar(Guid personaId, CambiarEstadoUsuarioDto datos, CancellationToken ct) =>
        servicio.CambiarEstadoAsync(personaId, true, datos.Version, ct);

    [HttpPost("{personaId:guid}/desactivar")]
    [Authorize(Policy = Permisos.PersonasAdministrar)]
    public Task<PersonaAdministracionDto> Desactivar(Guid personaId, CambiarEstadoUsuarioDto datos, CancellationToken ct) =>
        servicio.CambiarEstadoAsync(personaId, false, datos.Version, ct);
}
