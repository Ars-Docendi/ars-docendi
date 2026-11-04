using ArsDocendi.Storage.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Modules.Portal.Contracts.Administracion;
using Modules.Portal.Contracts.Dtos;
using Modules.Portal.Domain;

namespace Modules.Portal.Application;

/// <summary>
/// Implementa <see cref="IAdministracionPortal"/> con la misma lógica de Mi Portal. Cada llamada
/// usa una instancia propia de <see cref="ServicioPortal"/> apuntada a la persona objetivo, para
/// que ese objetivo no afecte otros usos del servicio en el mismo request.
/// </summary>
internal sealed class AdministracionPortal(IServiceProvider servicios) : IAdministracionPortal
{
    private ServicioPortal Para(Guid personaId)
    {
        var servicio = ActivatorUtilities.CreateInstance<ServicioPortal>(servicios);
        servicio.ActuarSobre(personaId);
        return servicio;
    }

    private static DescargaPortalDto? Mapear(DescargaArchivo? d) => d is null ? null : new(d.Contenido, d.Nombre, d.Mime);

    public Task<PerfilDocenteDto?> ObtenerAsync(Guid personaId, CancellationToken ct) =>
        Para(personaId).ObtenerPerfilAsync(personaId, ct);

    public Task<ContactoDto> GuardarContactoAsync(Guid personaId, GuardarContactoDto datos, CancellationToken ct) =>
        Para(personaId).GuardarContactoAsync(datos, ct);

    public Task<CvDto> GuardarCvAsync(Guid personaId, GuardarCvDto datos, CancellationToken ct) =>
        Para(personaId).GuardarCvAsync(datos, ct);

    public Task EliminarCvAsync(Guid personaId, uint? version, CancellationToken ct) =>
        Para(personaId).EliminarCvAsync(ct, version);

    public async Task<DescargaPortalDto?> DescargarCvAsync(Guid personaId, CancellationToken ct) =>
        Mapear(await Para(personaId).DescargarCvAsync(ct));

    public async Task<DescargaPortalDto?> DescargarDocumentoAsync(Guid personaId, Guid proyectoId, CancellationToken ct) =>
        Mapear(await Para(personaId).DescargarDocumentoAsync(proyectoId, ct));

    public Task ReemplazarTagsAsync(Guid personaId, string tipo, GuardarTagsDto datos, CancellationToken ct) =>
        Para(personaId).ReemplazarTagsAsync(tipo, datos, ct);

    public Task<ExperienciaDto> CrearExperienciaAsync(Guid personaId, GuardarExperienciaDto datos, CancellationToken ct) =>
        Para(personaId).CrearAsync(datos, ct);
    public Task<ExperienciaDto> EditarExperienciaAsync(Guid personaId, Guid id, GuardarExperienciaDto datos, CancellationToken ct) =>
        Para(personaId).EditarAsync(id, datos, ct);
    public Task EliminarExperienciaAsync(Guid personaId, Guid id, uint? version, CancellationToken ct) =>
        Para(personaId).EliminarAsync<Experiencia>(id, ct, version);

    public Task<EducacionDto> CrearEducacionAsync(Guid personaId, GuardarEducacionDto datos, CancellationToken ct) =>
        Para(personaId).CrearAsync(datos, ct);
    public Task<EducacionDto> EditarEducacionAsync(Guid personaId, Guid id, GuardarEducacionDto datos, CancellationToken ct) =>
        Para(personaId).EditarAsync(id, datos, ct);
    public Task EliminarEducacionAsync(Guid personaId, Guid id, uint? version, CancellationToken ct) =>
        Para(personaId).EliminarAsync<Educacion>(id, ct, version);

    public Task<CertificacionDto> CrearCertificacionAsync(Guid personaId, GuardarCertificacionDto datos, CancellationToken ct) =>
        Para(personaId).CrearAsync(datos, ct);
    public Task<CertificacionDto> EditarCertificacionAsync(Guid personaId, Guid id, GuardarCertificacionDto datos, CancellationToken ct) =>
        Para(personaId).EditarAsync(id, datos, ct);
    public Task EliminarCertificacionAsync(Guid personaId, Guid id, uint? version, CancellationToken ct) =>
        Para(personaId).EliminarAsync<Certificacion>(id, ct, version);

    public Task<ProyectoDto> CrearProyectoAsync(Guid personaId, GuardarProyectoDto datos, CancellationToken ct) =>
        Para(personaId).CrearAsync(datos, ct);
    public Task<ProyectoDto> EditarProyectoAsync(Guid personaId, Guid id, GuardarProyectoDto datos, CancellationToken ct) =>
        Para(personaId).EditarAsync(id, datos, ct);
    public Task EliminarProyectoAsync(Guid personaId, Guid id, uint? version, CancellationToken ct) =>
        Para(personaId).EliminarAsync<Proyecto>(id, ct, version);
}
