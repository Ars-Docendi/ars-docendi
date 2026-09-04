using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using Modules.Portal.Contracts.Dtos;
using Modules.Portal.Contracts.Queries;
using Modules.Portal.Domain;
using Modules.Portal.Repositories;

namespace Modules.Portal.Application;

public sealed class ServicioPortal(
    IRepositorioPortal repositorio,
    IConsultasIdentity identity,
    ICurrentUser usuario) : IPortalQueries
{
    public async Task<PerfilDocenteDto?> ObtenerPerfilAsync(Guid personaId, CancellationToken ct)
    {
        var persona = await identity.ObtenerPersonaAsync(personaId, ct);
        if (persona is null) return null;
        return Mapear(persona, await repositorio.ObtenerAsync(personaId, ct));
    }

    public async Task<PerfilDocenteDto> ObtenerPropioAsync(CancellationToken ct)
    {
        var persona = await PersonaActualAsync(ct);
        return Mapear(persona, await repositorio.ObtenerAsync(persona.Id, ct));
    }

    public async Task<ContactoDto> GuardarContactoAsync(GuardarContactoDto datos, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(datos.Mail))
        {
            try { if (!new System.Net.Mail.MailAddress(datos.Mail).Address.Equals(datos.Mail.Trim(), StringComparison.OrdinalIgnoreCase)) throw Error("mail", "El mail no es válido."); }
            catch (FormatException) { throw Error("mail", "El mail no es válido."); }
        }
        var perfil = await PerfilActualAsync(ct);
        perfil.Contacto ??= new Contacto { Id = Guid.NewGuid(), PerfilId = perfil.Id };
        perfil.Contacto.Telefono = datos.Telefono?.Trim();
        perfil.Contacto.Mail = datos.Mail?.Trim();
        await repositorio.GuardarAsync(ct);
        return new(perfil.Contacto.Telefono, perfil.Contacto.Mail);
    }

    public async Task<CvDto> GuardarCvAsync(GuardarCvDto datos, CancellationToken ct)
    {
        ValidarPdf(datos.Nombre);
        var perfil = await PerfilActualAsync(ct);
        perfil.Cv ??= new Cv { Id = Guid.NewGuid(), PerfilId = perfil.Id, Nombre = datos.Nombre };
        perfil.Cv.Nombre = datos.Nombre.Trim(); perfil.Cv.Uri = datos.Uri; perfil.Cv.FechaCarga = DateTimeOffset.UtcNow;
        await repositorio.GuardarAsync(ct);
        return new(perfil.Cv.Nombre, DateOnly.FromDateTime(perfil.Cv.FechaCarga.UtcDateTime));
    }

    public async Task EliminarCvAsync(CancellationToken ct)
    {
        var perfil = await PerfilActualAsync(ct);
        if (perfil.Cv is not null) repositorio.Eliminar(perfil.Cv);
        await repositorio.GuardarAsync(ct);
    }

    public Task<ExperienciaDto> CrearAsync(GuardarExperienciaDto d, CancellationToken ct) { ValidarPeriodo(d.Puesto, d.Desde, d.Hasta, "puesto"); return GuardarItemAsync(new Experiencia { Id = Guid.NewGuid(), Puesto = d.Puesto, Organizacion = d.Organizacion, Descripcion = d.Descripcion, Desde = d.Desde, Hasta = d.Hasta }, ct, Mapear); }
    public Task<EducacionDto> CrearAsync(GuardarEducacionDto d, CancellationToken ct) { ValidarPeriodo(d.Carrera, d.Desde, d.Hasta, "carrera"); return GuardarItemAsync(new Educacion { Id = Guid.NewGuid(), Nivel = d.Nivel, Carrera = d.Carrera, Institucion = d.Institucion, Desde = d.Desde, Hasta = d.Hasta }, ct, Mapear); }
    public Task<CertificacionDto> CrearAsync(GuardarCertificacionDto d, CancellationToken ct) { ValidarTexto(d.Nombre, "nombre"); if (d.Vencimiento < d.Fecha) throw Error("vencimiento", "No puede ser anterior a la fecha."); return GuardarItemAsync(new Certificacion { Id = Guid.NewGuid(), Nombre = d.Nombre, Emisor = d.Emisor, Fecha = d.Fecha, Vencimiento = d.Vencimiento }, ct, Mapear); }
    public Task<ProyectoDto> CrearAsync(GuardarProyectoDto d, CancellationToken ct) { ValidarPeriodo(d.Nombre, d.Desde, d.Hasta, "nombre"); if (d.DocumentoNombre is not null) ValidarPdf(d.DocumentoNombre); return GuardarItemAsync(CrearProyecto(d), ct, Mapear); }

    public Task<ExperienciaDto> EditarAsync(Guid id, GuardarExperienciaDto d, CancellationToken ct) => EditarItemAsync(id, new Experiencia { Puesto = d.Puesto, Organizacion = d.Organizacion, Descripcion = d.Descripcion, Desde = d.Desde, Hasta = d.Hasta }, ct, Mapear);
    public Task<EducacionDto> EditarAsync(Guid id, GuardarEducacionDto d, CancellationToken ct) => EditarItemAsync(id, new Educacion { Nivel = d.Nivel, Carrera = d.Carrera, Institucion = d.Institucion, Desde = d.Desde, Hasta = d.Hasta }, ct, Mapear);
    public Task<CertificacionDto> EditarAsync(Guid id, GuardarCertificacionDto d, CancellationToken ct) => EditarItemAsync(id, new Certificacion { Nombre = d.Nombre, Emisor = d.Emisor, Fecha = d.Fecha, Vencimiento = d.Vencimiento }, ct, Mapear);
    public Task<ProyectoDto> EditarAsync(Guid id, GuardarProyectoDto d, CancellationToken ct) => EditarItemAsync(id, CrearProyecto(d), ct, Mapear);

    public async Task EliminarAsync<T>(Guid id, CancellationToken ct) where T : class
    {
        var persona = await PersonaActualAsync(ct);
        var item = await repositorio.ObtenerItemAsync<T>(id, persona.Id, ct) ?? throw NoEncontrado();
        repositorio.Eliminar(item); await repositorio.GuardarAsync(ct);
    }

    public async Task ReemplazarTagsAsync(string tipo, GuardarTagsDto datos, CancellationToken ct)
    {
        if (tipo is not ("habilidad" or "interes")) throw NoEncontrado();
        await repositorio.ReemplazarTagsAsync(await PerfilActualAsync(ct), tipo, datos.Terminos, ct);
    }

    private async Task<TDto> GuardarItemAsync<T, TDto>(T item, CancellationToken ct, Func<T, TDto> map) where T : class
    { item.GetType().GetProperty("PerfilId")!.SetValue(item, (await PerfilActualAsync(ct)).Id); repositorio.Agregar(item); await repositorio.GuardarAsync(ct); return map(item); }
    private async Task<TDto> EditarItemAsync<T, TDto>(Guid id, T valores, CancellationToken ct, Func<T, TDto> map) where T : class
    {
        var persona = await PersonaActualAsync(ct);
        var actual = await repositorio.ObtenerItemAsync<T>(id, persona.Id, ct) ?? throw NoEncontrado();
        foreach (var propiedad in typeof(T).GetProperties().Where(x => x.CanWrite && x.Name is not ("Id" or "PerfilId" or "CreadoEn")))
            propiedad.SetValue(actual, propiedad.GetValue(valores));
        await repositorio.GuardarAsync(ct);
        return map(actual);
    }
    private Proyecto CrearProyecto(GuardarProyectoDto d) => new() { Id = Guid.NewGuid(), Nombre = d.Nombre, Rol = d.Rol, Descripcion = d.Descripcion, Desde = d.Desde, Hasta = d.Hasta, Doi = d.Doi, Documento = string.IsNullOrWhiteSpace(d.DocumentoNombre) ? null : new DocumentoProyecto { Id = Guid.NewGuid(), Nombre = d.DocumentoNombre, Uri = d.DocumentoUri } };
    private async Task<Persona> PersonaActualAsync(CancellationToken ct) => Guid.TryParse(usuario.UserId, out var uid) ? (await identity.ListarUsuariosAsync(ct)).FirstOrDefault(x => x.Id == uid)?.Persona ?? throw NoEncontrado() : throw new ExcepcionAplicacion(TipoErrorAplicacion.NoAutenticado, "unauthenticated", "Se requiere autenticación.");
    private async Task<Perfil> PerfilActualAsync(CancellationToken ct) => await repositorio.ObtenerOCrearAsync((await PersonaActualAsync(ct)).Id, ct);
    private static void ValidarPdf(string nombre) { if (string.IsNullOrWhiteSpace(nombre) || !nombre.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) throw Error("nombre", "El archivo debe ser PDF."); }
    private static void ValidarTexto(string texto, string campo) { if (string.IsNullOrWhiteSpace(texto)) throw Error(campo, "Campo obligatorio."); }
    private static void ValidarPeriodo(string texto, DateOnly desde, DateOnly? hasta, string campo) { ValidarTexto(texto, campo); if (hasta < desde) throw Error("hasta", "Debe ser posterior o igual a desde."); }
    private static ExcepcionAplicacion Error(string campo, string mensaje) => new(TipoErrorAplicacion.Validacion, "validation", mensaje, new Dictionary<string, string[]> { [campo] = [mensaje] });
    private static ExcepcionAplicacion NoEncontrado() => new(TipoErrorAplicacion.NoEncontrado, "resource-not-found", "No se encontró el recurso solicitado.");

    private static PerfilDocenteDto Mapear(Persona p, Perfil? x) => new(new(p.Nombre, p.Apellido, p.Usuario?.Upn ?? string.Empty, p.Documento, p.Legajo ?? string.Empty, p.Cuil ?? string.Empty), new(x?.Contacto?.Telefono, x?.Contacto?.Mail), x?.Cv is { } cv ? new(cv.Nombre, DateOnly.FromDateTime(cv.FechaCarga.UtcDateTime)) : null, x?.Experiencias.Select(Mapear).ToArray() ?? [], x?.Educaciones.Select(Mapear).ToArray() ?? [], x?.Certificaciones.Select(Mapear).ToArray() ?? [], x?.Proyectos.Select(Mapear).ToArray() ?? [], x?.Habilidades.Where(h => h.Tipo == "habilidad").Select(h => new TagDto(h.Habilidad!.Termino, h.Habilidad.Sugerido)).ToArray() ?? [], x?.Habilidades.Where(h => h.Tipo == "interes").Select(h => new TagDto(h.Habilidad!.Termino, h.Habilidad.Sugerido)).ToArray() ?? []);
    private static ExperienciaDto Mapear(Experiencia x) => new(x.Id, x.Puesto, x.Organizacion, x.Descripcion, x.Desde, x.Hasta);
    private static EducacionDto Mapear(Educacion x) => new(x.Id, x.Nivel, x.Carrera, x.Institucion, x.Desde, x.Hasta);
    private static CertificacionDto Mapear(Certificacion x) => new(x.Id, x.Nombre, x.Emisor, x.Fecha, x.Vencimiento);
    private static ProyectoDto Mapear(Proyecto x) => new(x.Id, x.Nombre, x.Rol, x.Descripcion, x.Desde, x.Hasta, x.Documento is { } d ? new(d.Nombre) : null, x.Doi);
}
