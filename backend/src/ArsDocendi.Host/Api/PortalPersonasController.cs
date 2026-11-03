using ArsDocendi.Host.Administracion;
using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modules.Portal.Contracts.Administracion;
using Modules.Portal.Contracts.Dtos;

namespace ArsDocendi.Host.Api;

/// <summary>
/// Perfil de Mi Portal de una persona, desde la administración de personas. Leer y descargar
/// requiere poder ver a la persona; corregir, además, <c>portal.administrar</c>. En ambos casos
/// la persona tiene que estar en el ámbito del actor (fuera de él, 404).
/// </summary>
[ApiController]
[Route("api/administracion/personas/{personaId:guid}/portal")]
[Authorize]
public sealed class PortalPersonasController(
    IAdministracionPortal portal,
    ServicioAdministracionPersonas personas,
    AlcancePersonas alcance) : ControllerBase
{
    [HttpGet]
    public async Task<PerfilDocenteDto> Obtener(Guid personaId, CancellationToken ct)
    {
        await RequerirEnAmbitoAsync(personaId, ct);
        return await portal.ObtenerAsync(personaId, ct) ?? throw NoEncontrada();
    }

    [HttpGet("cv/descarga")]
    public async Task<IActionResult> DescargarCv(Guid personaId, CancellationToken ct)
    {
        await RequerirEnAmbitoAsync(personaId, ct);
        return Archivo(await portal.DescargarCvAsync(personaId, ct));
    }

    [HttpGet("proyectos/{id:guid}/documento")]
    public async Task<IActionResult> DescargarDocumento(Guid personaId, Guid id, CancellationToken ct)
    {
        await RequerirEnAmbitoAsync(personaId, ct);
        return Archivo(await portal.DescargarDocumentoAsync(personaId, id, ct));
    }

    [HttpPut("contacto"), Authorize(Policy = Permisos.PortalAdministrar)]
    public async Task<ContactoDto> Contacto(Guid personaId, GuardarContactoDto datos, CancellationToken ct)
    {
        await RequerirEnAmbitoAsync(personaId, ct);
        return await portal.GuardarContactoAsync(personaId, datos, ct);
    }

    [HttpPut("cv"), Authorize(Policy = Permisos.PortalAdministrar)]
    public async Task<CvDto> Cv(Guid personaId, GuardarCvDto datos, CancellationToken ct)
    {
        await RequerirEnAmbitoAsync(personaId, ct);
        return await portal.GuardarCvAsync(personaId, datos, ct);
    }

    [HttpDelete("cv"), Authorize(Policy = Permisos.PortalAdministrar)]
    public async Task<IActionResult> EliminarCv(Guid personaId, [FromQuery] uint? version, CancellationToken ct)
    {
        await RequerirEnAmbitoAsync(personaId, ct);
        await portal.EliminarCvAsync(personaId, version, ct);
        return NoContent();
    }

    [HttpPut("habilidades"), Authorize(Policy = Permisos.PortalAdministrar)]
    public Task<IActionResult> Habilidades(Guid personaId, GuardarTagsDto datos, CancellationToken ct) =>
        Tags(personaId, "habilidad", datos, ct);

    [HttpPut("intereses"), Authorize(Policy = Permisos.PortalAdministrar)]
    public Task<IActionResult> Intereses(Guid personaId, GuardarTagsDto datos, CancellationToken ct) =>
        Tags(personaId, "interes", datos, ct);

    [HttpPost("experiencia"), Authorize(Policy = Permisos.PortalAdministrar)]
    public async Task<ExperienciaDto> CrearExperiencia(Guid personaId, GuardarExperienciaDto datos, CancellationToken ct)
    {
        await RequerirEnAmbitoAsync(personaId, ct);
        return await portal.CrearExperienciaAsync(personaId, datos, ct);
    }

    [HttpPut("experiencia/{id:guid}"), Authorize(Policy = Permisos.PortalAdministrar)]
    public async Task<ExperienciaDto> EditarExperiencia(Guid personaId, Guid id, GuardarExperienciaDto datos, CancellationToken ct)
    {
        await RequerirEnAmbitoAsync(personaId, ct);
        return await portal.EditarExperienciaAsync(personaId, id, datos, ct);
    }

    [HttpDelete("experiencia/{id:guid}"), Authorize(Policy = Permisos.PortalAdministrar)]
    public Task<IActionResult> EliminarExperiencia(Guid personaId, Guid id, [FromQuery] uint? version, CancellationToken ct) =>
        Eliminar(personaId, () => portal.EliminarExperienciaAsync(personaId, id, version, ct), ct);

    [HttpPost("educacion"), Authorize(Policy = Permisos.PortalAdministrar)]
    public async Task<EducacionDto> CrearEducacion(Guid personaId, GuardarEducacionDto datos, CancellationToken ct)
    {
        await RequerirEnAmbitoAsync(personaId, ct);
        return await portal.CrearEducacionAsync(personaId, datos, ct);
    }

    [HttpPut("educacion/{id:guid}"), Authorize(Policy = Permisos.PortalAdministrar)]
    public async Task<EducacionDto> EditarEducacion(Guid personaId, Guid id, GuardarEducacionDto datos, CancellationToken ct)
    {
        await RequerirEnAmbitoAsync(personaId, ct);
        return await portal.EditarEducacionAsync(personaId, id, datos, ct);
    }

    [HttpDelete("educacion/{id:guid}"), Authorize(Policy = Permisos.PortalAdministrar)]
    public Task<IActionResult> EliminarEducacion(Guid personaId, Guid id, [FromQuery] uint? version, CancellationToken ct) =>
        Eliminar(personaId, () => portal.EliminarEducacionAsync(personaId, id, version, ct), ct);

    [HttpPost("certificaciones"), Authorize(Policy = Permisos.PortalAdministrar)]
    public async Task<CertificacionDto> CrearCertificacion(Guid personaId, GuardarCertificacionDto datos, CancellationToken ct)
    {
        await RequerirEnAmbitoAsync(personaId, ct);
        return await portal.CrearCertificacionAsync(personaId, datos, ct);
    }

    [HttpPut("certificaciones/{id:guid}"), Authorize(Policy = Permisos.PortalAdministrar)]
    public async Task<CertificacionDto> EditarCertificacion(Guid personaId, Guid id, GuardarCertificacionDto datos, CancellationToken ct)
    {
        await RequerirEnAmbitoAsync(personaId, ct);
        return await portal.EditarCertificacionAsync(personaId, id, datos, ct);
    }

    [HttpDelete("certificaciones/{id:guid}"), Authorize(Policy = Permisos.PortalAdministrar)]
    public Task<IActionResult> EliminarCertificacion(Guid personaId, Guid id, [FromQuery] uint? version, CancellationToken ct) =>
        Eliminar(personaId, () => portal.EliminarCertificacionAsync(personaId, id, version, ct), ct);

    [HttpPost("proyectos"), Authorize(Policy = Permisos.PortalAdministrar)]
    public async Task<ProyectoDto> CrearProyecto(Guid personaId, GuardarProyectoDto datos, CancellationToken ct)
    {
        await RequerirEnAmbitoAsync(personaId, ct);
        return await portal.CrearProyectoAsync(personaId, datos, ct);
    }

    [HttpPut("proyectos/{id:guid}"), Authorize(Policy = Permisos.PortalAdministrar)]
    public async Task<ProyectoDto> EditarProyecto(Guid personaId, Guid id, GuardarProyectoDto datos, CancellationToken ct)
    {
        await RequerirEnAmbitoAsync(personaId, ct);
        return await portal.EditarProyectoAsync(personaId, id, datos, ct);
    }

    [HttpDelete("proyectos/{id:guid}"), Authorize(Policy = Permisos.PortalAdministrar)]
    public Task<IActionResult> EliminarProyecto(Guid personaId, Guid id, [FromQuery] uint? version, CancellationToken ct) =>
        Eliminar(personaId, () => portal.EliminarProyectoAsync(personaId, id, version, ct), ct);

    private async Task<IActionResult> Tags(Guid personaId, string tipo, GuardarTagsDto datos, CancellationToken ct)
    {
        await RequerirEnAmbitoAsync(personaId, ct);
        await portal.ReemplazarTagsAsync(personaId, tipo, datos, ct);
        return NoContent();
    }

    private async Task<IActionResult> Eliminar(Guid personaId, Func<Task> eliminar, CancellationToken ct)
    {
        await RequerirEnAmbitoAsync(personaId, ct);
        await eliminar();
        return NoContent();
    }

    // Exige poder ver personas (403 si no) y que esta esté en el ámbito del actor (404 si no).
    private async Task RequerirEnAmbitoAsync(Guid personaId, CancellationToken ct)
    {
        var materias = await alcance.MateriasVisiblesAsync(User, ct);
        if (!await personas.EstaEnAmbitoAsync(personaId, materias, ct)) throw NoEncontrada();
    }

    private IActionResult Archivo(DescargaPortalDto? descarga) => descarga is null
        ? NotFound()
        : File(descarga.Contenido, descarga.Mime, descarga.Nombre, enableRangeProcessing: true);

    private static ExcepcionAplicacion NoEncontrada() => new(
        TipoErrorAplicacion.NoEncontrado, "resource-not-found", "No se encontró la persona solicitada.");
}
