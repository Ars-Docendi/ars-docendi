using Modules.Portal.Contracts.Dtos;

namespace Modules.Portal.Contracts.Administracion;

/// <summary>Archivo del perfil listo para descargar.</summary>
public sealed record DescargaPortalDto(Stream Contenido, string Nombre, string Mime);

/// <summary>
/// Lectura y corrección del perfil de Mi Portal de una persona dada, para la administración de
/// personas. Mismas validaciones, versiones y reglas de archivos que la edición propia; quien
/// llama es responsable de autorizar (permiso y ámbito) antes de invocarlo.
/// </summary>
public interface IAdministracionPortal
{
    Task<PerfilDocenteDto?> ObtenerAsync(Guid personaId, CancellationToken ct);
    Task<ContactoDto> GuardarContactoAsync(Guid personaId, GuardarContactoDto datos, CancellationToken ct);
    Task<CvDto> GuardarCvAsync(Guid personaId, GuardarCvDto datos, CancellationToken ct);
    Task EliminarCvAsync(Guid personaId, uint? version, CancellationToken ct);
    Task<DescargaPortalDto?> DescargarCvAsync(Guid personaId, CancellationToken ct);
    Task<DescargaPortalDto?> DescargarDocumentoAsync(Guid personaId, Guid proyectoId, CancellationToken ct);
    Task ReemplazarTagsAsync(Guid personaId, string tipo, GuardarTagsDto datos, CancellationToken ct);

    Task<ExperienciaDto> CrearExperienciaAsync(Guid personaId, GuardarExperienciaDto datos, CancellationToken ct);
    Task<ExperienciaDto> EditarExperienciaAsync(Guid personaId, Guid id, GuardarExperienciaDto datos, CancellationToken ct);
    Task EliminarExperienciaAsync(Guid personaId, Guid id, uint? version, CancellationToken ct);

    Task<EducacionDto> CrearEducacionAsync(Guid personaId, GuardarEducacionDto datos, CancellationToken ct);
    Task<EducacionDto> EditarEducacionAsync(Guid personaId, Guid id, GuardarEducacionDto datos, CancellationToken ct);
    Task EliminarEducacionAsync(Guid personaId, Guid id, uint? version, CancellationToken ct);

    Task<CertificacionDto> CrearCertificacionAsync(Guid personaId, GuardarCertificacionDto datos, CancellationToken ct);
    Task<CertificacionDto> EditarCertificacionAsync(Guid personaId, Guid id, GuardarCertificacionDto datos, CancellationToken ct);
    Task EliminarCertificacionAsync(Guid personaId, Guid id, uint? version, CancellationToken ct);

    Task<ProyectoDto> CrearProyectoAsync(Guid personaId, GuardarProyectoDto datos, CancellationToken ct);
    Task<ProyectoDto> EditarProyectoAsync(Guid personaId, Guid id, GuardarProyectoDto datos, CancellationToken ct);
    Task EliminarProyectoAsync(Guid personaId, Guid id, uint? version, CancellationToken ct);
}
