using System.Text.Json.Serialization;

namespace Modules.Portal.Contracts.Dtos;

public sealed record PerfilInstitucionalDto(string Nombre, string Apellido, string Upn, string Documento, string Legajo, string Cuil);
public sealed record ContactoDto(string? Telefono, string? Mail);
public sealed record CvDto(Guid ArchivoId, string Nombre, DateOnly FechaCarga, long TamanoBytes, string Estado);
public sealed record PeriodoDto(DateOnly Desde, DateOnly? Hasta);
public sealed record ExperienciaDto(Guid Id, string Puesto, string Organizacion, string Descripcion, DateOnly Desde, DateOnly? Hasta);
public sealed record EducacionDto(Guid Id, string Nivel, string Carrera, string Institucion, DateOnly Desde, DateOnly? Hasta);
public sealed record CertificacionDto(Guid Id, string Nombre, string Emisor, DateOnly Fecha, DateOnly? Vencimiento);
public sealed record DocumentoProyectoDto(Guid ArchivoId, string Nombre, DateOnly FechaCarga, long TamanoBytes, string Estado);
public sealed record ProyectoDto(Guid Id, string Nombre, string Rol, string Descripcion, DateOnly Desde, DateOnly? Hasta, DocumentoProyectoDto? Documento, string? Doi);
public sealed record TagDto(string Termino, bool Sugerido);

public sealed record PerfilDocenteDto(
    PerfilInstitucionalDto Institucional,
    ContactoDto Contacto,
    CvDto? Cv,
    IReadOnlyList<ExperienciaDto> Experiencia,
    IReadOnlyList<EducacionDto> Educacion,
    IReadOnlyList<CertificacionDto> Certificaciones,
    IReadOnlyList<ProyectoDto> Proyectos,
    IReadOnlyList<TagDto> Habilidades,
    IReadOnlyList<TagDto> Intereses);

public sealed record GuardarContactoDto(string? Telefono, string? Mail);

public sealed record GuardarCvDto
{
    public Guid ArchivoId { get; init; }
    // Constructor de compatibilidad para código interno previo al flujo de archivos.
    // No forma parte del contrato HTTP nuevo y el servicio no lo acepta como carga real.
    public string? LegacyNombre { get; init; }
    public string? LegacyUri { get; init; }

    [JsonConstructor]
    public GuardarCvDto(Guid archivoId) => ArchivoId = archivoId;

    public GuardarCvDto(string nombre, string? uri)
    {
        ArchivoId = Guid.Empty;
        LegacyNombre = nombre;
        LegacyUri = uri;
    }
}

public sealed record GuardarExperienciaDto(string Puesto, string Organizacion, string Descripcion, DateOnly Desde, DateOnly? Hasta);
public sealed record GuardarEducacionDto(string Nivel, string Carrera, string Institucion, DateOnly Desde, DateOnly? Hasta);
public sealed record GuardarCertificacionDto(string Nombre, string Emisor, DateOnly Fecha, DateOnly? Vencimiento);

public sealed record GuardarProyectoDto
{
    public string Nombre { get; init; } = string.Empty;
    public string Rol { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public DateOnly Desde { get; init; }
    public DateOnly? Hasta { get; init; }
    public string? Doi { get; init; }
    public Guid? DocumentoArchivoId { get; init; }
    public string? DocumentoNombre { get; init; }
    public string? DocumentoUri { get; init; }

    [JsonConstructor]
    public GuardarProyectoDto(string nombre, string rol, string descripcion, DateOnly desde, DateOnly? hasta, string? doi, Guid? documentoArchivoId)
    {
        Nombre = nombre; Rol = rol; Descripcion = descripcion; Desde = desde; Hasta = hasta; Doi = doi; DocumentoArchivoId = documentoArchivoId;
    }

    public GuardarProyectoDto(
        string nombre,
        string rol,
        string descripcion,
        DateOnly desde,
        DateOnly? hasta,
        string? doi,
        string? documentoNombre,
        string? documentoUri) : this(nombre, rol, descripcion, desde, hasta, doi, null)
    {
        DocumentoNombre = documentoNombre;
        DocumentoUri = documentoUri;
    }
}

public sealed record GuardarTagsDto(IReadOnlyList<string> Terminos);
