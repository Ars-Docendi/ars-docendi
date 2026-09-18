using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Storage.Contracts;
using Modules.Portal.Contracts.Dtos;
using Modules.Portal.Contracts.Queries;
using Modules.Portal.Domain;
using Modules.Portal.Repositories;

namespace Modules.Portal.Application;

public sealed class ServicioPortal(
    RepositorioPortal repositorio,
    IConsultasIdentity identity,
    ICurrentUser usuario,
    IAlmacenamientoArchivos almacenamiento) : IPortalQueries
{
    public async Task<PerfilDocenteDto?> ObtenerPerfilAsync(Guid personaId, CancellationToken ct)
    {
        var persona = await identity.ObtenerPersonaAsync(personaId, ct);
        if (persona is null) return null;
        return await MapearAsync(persona, await repositorio.ObtenerAsync(personaId, ct), ct);
    }

    public async Task<PerfilDocenteDto> ObtenerPropioAsync(CancellationToken ct)
    {
        var persona = await PersonaActualAsync(ct);
        return await MapearAsync(persona, await repositorio.ObtenerAsync(persona.Id, ct), ct);
    }

    public async Task<ContactoDto> GuardarContactoAsync(GuardarContactoDto datos, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(datos.Mail))
        {
            try
            {
                if (!new System.Net.Mail.MailAddress(datos.Mail).Address.Equals(datos.Mail.Trim(), StringComparison.OrdinalIgnoreCase))
                    throw Error("mail", "El mail no es válido.");
            }
            catch (FormatException) { throw Error("mail", "El mail no es válido."); }
        }
        var perfil = await PerfilActualAsync(ct);
        if (perfil.Contacto is null)
        {
            perfil.Contacto = new Contacto { Id = Guid.NewGuid(), PerfilId = perfil.Id };
            repositorio.Agregar(perfil.Contacto);
        }
        perfil.Contacto.Telefono = datos.Telefono?.Trim();
        perfil.Contacto.Mail = datos.Mail?.Trim();
        await repositorio.GuardarAsync(ct);
        return new(perfil.Contacto.Telefono, perfil.Contacto.Mail);
    }

    public async Task<CvDto> GuardarCvAsync(GuardarCvDto datos, CancellationToken ct)
    {
        if (datos.ArchivoId == Guid.Empty && !string.IsNullOrWhiteSpace(datos.LegacyNombre))
        {
            var legado = await PerfilActualAsync(ct);
            if (legado.Cv is null)
            {
                legado.Cv = new Cv { Id = Guid.NewGuid(), PerfilId = legado.Id, Nombre = datos.LegacyNombre.Trim() };
                repositorio.Agregar(legado.Cv);
            }
            legado.Cv.Nombre = datos.LegacyNombre.Trim();
            legado.Cv.Uri = datos.LegacyUri;
            legado.Cv.ArchivoId = null;
            legado.Cv.FechaCarga = DateTimeOffset.UtcNow;
            await repositorio.GuardarAsync(ct);
            return MapearCv(legado.Cv, null);
        }
        var archivo = await RequerirArchivoPropioAsync(datos.ArchivoId, PropositosArchivo.Cv, ct);
        var perfil = await PerfilActualAsync(ct);
        var archivoAnterior = perfil.Cv?.ArchivoId;
        if (perfil.Cv is null)
        {
            perfil.Cv = new Cv { Id = Guid.NewGuid(), PerfilId = perfil.Id, Nombre = archivo.NombreOriginal };
            repositorio.Agregar(perfil.Cv);
        }
        perfil.Cv.Nombre = archivo.NombreOriginal;
        perfil.Cv.ArchivoId = archivo.Id;
        perfil.Cv.Uri = null;
        perfil.Cv.FechaCarga = DateTimeOffset.UtcNow;
        await repositorio.GuardarAsync(ct);
        if (archivoAnterior is { } anterior && anterior != archivo.Id)
            await almacenamiento.EliminarAsync(anterior, UsuarioIdActual(), ct);
        return MapearCv(perfil.Cv, archivo);
    }

    public async Task<DescargaArchivo?> DescargarCvAsync(CancellationToken ct)
    {
        var perfil = await PerfilActualAsync(ct);
        return perfil.Cv?.ArchivoId is { } archivoId
            ? await almacenamiento.AbrirDescargaAsync(archivoId, ct)
            : null;
    }

    public async Task EliminarCvAsync(CancellationToken ct)
    {
        var perfil = await PerfilActualAsync(ct);
        var archivoId = perfil.Cv?.ArchivoId;
        if (perfil.Cv is not null) repositorio.Eliminar(perfil.Cv);
        await repositorio.GuardarAsync(ct);
        if (archivoId is { } id) await almacenamiento.EliminarAsync(id, UsuarioIdActual(), ct);
    }

    public async Task<ExperienciaDto> CrearAsync(GuardarExperienciaDto d, CancellationToken ct)
    {
        Validar(d);
        var item = new Experiencia { Id = Guid.NewGuid(), PerfilId = (await PerfilActualAsync(ct)).Id, Puesto = d.Puesto, Organizacion = d.Organizacion, Descripcion = d.Descripcion, Desde = d.Desde, Hasta = d.Hasta };
        repositorio.Agregar(item); await repositorio.GuardarAsync(ct); return Mapear(item);
    }

    public async Task<EducacionDto> CrearAsync(GuardarEducacionDto d, CancellationToken ct)
    {
        Validar(d);
        var item = new Educacion { Id = Guid.NewGuid(), PerfilId = (await PerfilActualAsync(ct)).Id, Nivel = d.Nivel, Carrera = d.Carrera, Institucion = d.Institucion, Desde = d.Desde, Hasta = d.Hasta };
        repositorio.Agregar(item); await repositorio.GuardarAsync(ct); return Mapear(item);
    }

    public async Task<CertificacionDto> CrearAsync(GuardarCertificacionDto d, CancellationToken ct)
    {
        Validar(d);
        var item = new Certificacion { Id = Guid.NewGuid(), PerfilId = (await PerfilActualAsync(ct)).Id, Nombre = d.Nombre, Emisor = d.Emisor, Fecha = d.Fecha, Vencimiento = d.Vencimiento };
        repositorio.Agregar(item); await repositorio.GuardarAsync(ct); return Mapear(item);
    }

    public async Task<ProyectoDto> CrearAsync(GuardarProyectoDto d, CancellationToken ct)
    {
        Validar(d);
        var archivo = d.DocumentoArchivoId is { } archivoId
            ? await RequerirArchivoPropioAsync(archivoId, PropositosArchivo.DocumentoProyecto, ct)
            : null;
        var item = CrearProyecto(d, archivo);
        item.PerfilId = (await PerfilActualAsync(ct)).Id;
        if (item.Documento is not null) item.Documento.ProyectoId = item.Id;
        repositorio.Agregar(item);
        await repositorio.GuardarAsync(ct);
        return await MapearProyectoAsync(item, ct);
    }

    public async Task<ExperienciaDto> EditarAsync(Guid id, GuardarExperienciaDto d, CancellationToken ct)
    {
        Validar(d);
        var actual = await ObtenerItemActualAsync<Experiencia>(id, ct);
        actual.Puesto = d.Puesto; actual.Organizacion = d.Organizacion; actual.Descripcion = d.Descripcion; actual.Desde = d.Desde; actual.Hasta = d.Hasta;
        await repositorio.GuardarAsync(ct); return Mapear(actual);
    }

    public async Task<EducacionDto> EditarAsync(Guid id, GuardarEducacionDto d, CancellationToken ct)
    {
        Validar(d);
        var actual = await ObtenerItemActualAsync<Educacion>(id, ct);
        actual.Nivel = d.Nivel; actual.Carrera = d.Carrera; actual.Institucion = d.Institucion; actual.Desde = d.Desde; actual.Hasta = d.Hasta;
        await repositorio.GuardarAsync(ct); return Mapear(actual);
    }

    public async Task<CertificacionDto> EditarAsync(Guid id, GuardarCertificacionDto d, CancellationToken ct)
    {
        Validar(d);
        var actual = await ObtenerItemActualAsync<Certificacion>(id, ct);
        actual.Nombre = d.Nombre; actual.Emisor = d.Emisor; actual.Fecha = d.Fecha; actual.Vencimiento = d.Vencimiento;
        await repositorio.GuardarAsync(ct); return Mapear(actual);
    }

    public async Task<ProyectoDto> EditarAsync(Guid id, GuardarProyectoDto d, CancellationToken ct)
    {
        Validar(d);
        var persona = await PersonaActualAsync(ct);
        var actual = await repositorio.ObtenerItemAsync<Proyecto>(id, persona.Id, ct) ?? throw NoEncontrado();
        var archivoAnterior = actual.Documento?.ArchivoId;
        var archivo = d.DocumentoArchivoId is { } archivoId
            ? await RequerirArchivoPropioAsync(archivoId, PropositosArchivo.DocumentoProyecto, ct)
            : null;
        actual.Nombre = d.Nombre;
        actual.Rol = d.Rol;
        actual.Descripcion = d.Descripcion;
        actual.Desde = d.Desde;
        actual.Hasta = d.Hasta;
        actual.Doi = d.Doi;
        var documentoLegacy = d.DocumentoArchivoId is null && !string.IsNullOrWhiteSpace(d.DocumentoNombre);
        if (archivo is null && !documentoLegacy)
        {
            if (actual.Documento is not null) repositorio.Eliminar(actual.Documento);
            actual.Documento = null;
        }
        else if (actual.Documento is null)
        {
            actual.Documento = new DocumentoProyecto
            {
                Id = Guid.NewGuid(), ProyectoId = id,
                Nombre = archivo?.NombreOriginal ?? d.DocumentoNombre!,
                ArchivoId = archivo?.Id, Uri = archivo is null ? d.DocumentoUri : null,
                FechaCarga = DateTimeOffset.UtcNow,
            };
            repositorio.Agregar(actual.Documento);
        }
        else
        {
            actual.Documento.Nombre = archivo?.NombreOriginal ?? d.DocumentoNombre!;
            actual.Documento.ArchivoId = archivo?.Id;
            actual.Documento.Uri = archivo is null ? d.DocumentoUri : null;
            actual.Documento.FechaCarga = DateTimeOffset.UtcNow;
        }
        await repositorio.GuardarAsync(ct);
        if (archivoAnterior is { } anterior && archivo?.Id != anterior)
            await almacenamiento.EliminarAsync(anterior, UsuarioIdActual(), ct);
        return await MapearProyectoAsync(actual, ct);
    }

    public async Task<DescargaArchivo?> DescargarDocumentoAsync(Guid id, CancellationToken ct)
    {
        var persona = await PersonaActualAsync(ct);
        var proyecto = await repositorio.ObtenerItemAsync<Proyecto>(id, persona.Id, ct);
        return proyecto?.Documento?.ArchivoId is { } archivoId
            ? await almacenamiento.AbrirDescargaAsync(archivoId, ct)
            : null;
    }

    public async Task EliminarAsync<T>(Guid id, CancellationToken ct) where T : class
    {
        var persona = await PersonaActualAsync(ct);
        var item = await repositorio.ObtenerItemAsync<T>(id, persona.Id, ct) ?? throw NoEncontrado();
        var archivoId = (item as Proyecto)?.Documento?.ArchivoId;
        repositorio.Eliminar(item); await repositorio.GuardarAsync(ct);
        if (archivoId is { } idArchivo)
            await almacenamiento.EliminarAsync(idArchivo, UsuarioIdActual(), ct);
    }

    public async Task ReemplazarTagsAsync(string tipo, GuardarTagsDto datos, CancellationToken ct)
    {
        if (tipo is not ("habilidad" or "interes")) throw NoEncontrado();
        await repositorio.ReemplazarTagsAsync(await PerfilActualAsync(ct), tipo, datos.Terminos, ct);
    }

    private async Task<T> ObtenerItemActualAsync<T>(Guid id, CancellationToken ct) where T : class =>
        await repositorio.ObtenerItemAsync<T>(id, (await PersonaActualAsync(ct)).Id, ct) ?? throw NoEncontrado();

    private async Task<ArchivoDto> RequerirArchivoPropioAsync(Guid id, string proposito, CancellationToken ct) =>
        await almacenamiento.RequerirDisponibleDePropietarioAsync(id, proposito, UsuarioIdActual(), ct);

    private Proyecto CrearProyecto(GuardarProyectoDto d, ArchivoDto? archivo) => new()
    {
        Id = Guid.NewGuid(), Nombre = d.Nombre, Rol = d.Rol, Descripcion = d.Descripcion,
        Desde = d.Desde, Hasta = d.Hasta, Doi = d.Doi,
        Documento = archivo is null && string.IsNullOrWhiteSpace(d.DocumentoNombre) ? null : new DocumentoProyecto
        {
            Id = Guid.NewGuid(), ProyectoId = Guid.Empty, Nombre = archivo?.NombreOriginal ?? d.DocumentoNombre!,
            ArchivoId = archivo?.Id, Uri = archivo is null ? d.DocumentoUri : null, FechaCarga = DateTimeOffset.UtcNow,
        },
    };

    private async Task<Persona> PersonaActualAsync(CancellationToken ct) =>
        Guid.TryParse(usuario.UserId, out var uid)
            ? (await identity.ListarUsuariosAsync(ct)).FirstOrDefault(x => x.Id == uid)?.Persona ?? throw NoEncontrado()
            : throw new ExcepcionAplicacion(TipoErrorAplicacion.NoAutenticado, "unauthenticated", "Se requiere autenticación.");

    private Guid UsuarioIdActual() => Guid.TryParse(usuario.UserId, out var uid)
        ? uid
        : throw new ExcepcionAplicacion(TipoErrorAplicacion.NoAutenticado, "unauthenticated", "Se requiere autenticación.");

    private async Task<Perfil> PerfilActualAsync(CancellationToken ct) => await repositorio.ObtenerOCrearAsync((await PersonaActualAsync(ct)).Id, ct);

    private async Task<PerfilDocenteDto> MapearAsync(Persona p, Perfil? x, CancellationToken ct)
    {
        var cvArchivo = x?.Cv?.ArchivoId is { } cvId ? await almacenamiento.ObtenerAsync(cvId, ct) : null;
        var proyectos = new List<ProyectoDto>();
        foreach (var proyecto in x?.Proyectos ?? []) proyectos.Add(await MapearProyectoAsync(proyecto, ct));
        return new(
            new(p.Nombre, p.Apellido, p.Usuario?.Upn ?? string.Empty, p.Documento, p.Legajo ?? string.Empty, p.Cuil ?? string.Empty),
            new(x?.Contacto?.Telefono, x?.Contacto?.Mail),
            x?.Cv is { } cv ? MapearCv(cv, cvArchivo) : null,
            x?.Experiencias.Select(Mapear).ToArray() ?? [],
            x?.Educaciones.Select(Mapear).ToArray() ?? [],
            x?.Certificaciones.Select(Mapear).ToArray() ?? [],
            proyectos,
            x?.Habilidades.Where(h => h.Tipo == "habilidad").Select(h => new TagDto(h.Habilidad!.Termino, h.Habilidad.Sugerido)).ToArray() ?? [],
            x?.Habilidades.Where(h => h.Tipo == "interes").Select(h => new TagDto(h.Habilidad!.Termino, h.Habilidad.Sugerido)).ToArray() ?? []);
    }

    private async Task<ProyectoDto> MapearProyectoAsync(Proyecto x, CancellationToken ct)
    {
        var archivo = x.Documento?.ArchivoId is { } id ? await almacenamiento.ObtenerAsync(id, ct) : null;
        var documento = x.Documento is null ? null : new DocumentoProyectoDto(
            x.Documento.ArchivoId ?? Guid.Empty,
            x.Documento.Nombre,
            DateOnly.FromDateTime(x.Documento.FechaCarga.UtcDateTime),
            archivo?.TamanoBytes ?? 0,
            archivo?.Estado ?? "legacy");
        return new(x.Id, x.Nombre, x.Rol, x.Descripcion, x.Desde, x.Hasta, documento, x.Doi);
    }

    private static CvDto MapearCv(Cv cv, ArchivoDto? archivo) => new(
        cv.ArchivoId ?? Guid.Empty, cv.Nombre, DateOnly.FromDateTime(cv.FechaCarga.UtcDateTime),
        archivo?.TamanoBytes ?? 0, archivo?.Estado ?? "legacy");

    private static ExperienciaDto Mapear(Experiencia x) => new(x.Id, x.Puesto, x.Organizacion, x.Descripcion, x.Desde, x.Hasta);
    private static EducacionDto Mapear(Educacion x) => new(x.Id, x.Nivel, x.Carrera, x.Institucion, x.Desde, x.Hasta);
    private static CertificacionDto Mapear(Certificacion x) => new(x.Id, x.Nombre, x.Emisor, x.Fecha, x.Vencimiento);

    private static void ValidarPdf(string nombre) =>
        throw new NotSupportedException("La validación del PDF se realiza sobre archivoId confirmado.");
    private static void ValidarTexto(string texto, string campo) { if (string.IsNullOrWhiteSpace(texto)) throw Error(campo, "Campo obligatorio."); }
    private static void ValidarPeriodo(string texto, DateOnly desde, DateOnly? hasta, string campo) { ValidarTexto(texto, campo); if (hasta < desde) throw Error("hasta", "Debe ser posterior o igual a desde."); }
    private static void Validar(GuardarExperienciaDto d) { ValidarPeriodo(d.Puesto, d.Desde, d.Hasta, "puesto"); ValidarTexto(d.Organizacion, "organizacion"); ValidarTexto(d.Descripcion, "descripcion"); }
    private static void Validar(GuardarEducacionDto d) { ValidarPeriodo(d.Carrera, d.Desde, d.Hasta, "carrera"); if (d.Nivel is not ("Grado" or "Especialización" or "Maestría" or "Doctorado")) throw Error("nivel", "El nivel no es válido."); ValidarTexto(d.Institucion, "institucion"); }
    private static void Validar(GuardarCertificacionDto d) { ValidarTexto(d.Nombre, "nombre"); ValidarTexto(d.Emisor, "emisor"); if (d.Vencimiento < d.Fecha) throw Error("vencimiento", "No puede ser anterior a la fecha."); }
    private static void Validar(GuardarProyectoDto d) { ValidarPeriodo(d.Nombre, d.Desde, d.Hasta, "nombre"); ValidarTexto(d.Rol, "rol"); ValidarTexto(d.Descripcion, "descripcion"); }
    private static ExcepcionAplicacion Error(string campo, string mensaje) => new(TipoErrorAplicacion.Validacion, "validation", mensaje, new Dictionary<string, string[]> { [campo] = [mensaje] });
    private static ExcepcionAplicacion NoEncontrado() => new(TipoErrorAplicacion.NoEncontrado, "resource-not-found", "No se encontró el recurso solicitado.");
}
