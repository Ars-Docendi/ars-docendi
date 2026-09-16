namespace Modules.Portal.Contracts.Dtos;

public sealed record PerfilInstitucionalDto(string Nombre, string Apellido, string Upn, string Documento, string Legajo, string Cuil);
public sealed record ContactoDto(string? Telefono, string? Mail);
public sealed record CvDto(string Nombre, DateOnly FechaCarga);
public sealed record PeriodoDto(DateOnly Desde, DateOnly? Hasta);
public sealed record ExperienciaDto(Guid Id, string Puesto, string Organizacion, string Descripcion, DateOnly Desde, DateOnly? Hasta);
public sealed record EducacionDto(Guid Id, string Nivel, string Carrera, string Institucion, DateOnly Desde, DateOnly? Hasta);
public sealed record CertificacionDto(Guid Id, string Nombre, string Emisor, DateOnly Fecha, DateOnly? Vencimiento);
public sealed record DocumentoProyectoDto(string Nombre);
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
public sealed record GuardarCvDto(string Nombre, string? Uri);
public sealed record GuardarExperienciaDto(string Puesto, string Organizacion, string Descripcion, DateOnly Desde, DateOnly? Hasta);
public sealed record GuardarEducacionDto(string Nivel, string Carrera, string Institucion, DateOnly Desde, DateOnly? Hasta);
public sealed record GuardarCertificacionDto(string Nombre, string Emisor, DateOnly Fecha, DateOnly? Vencimiento);
public sealed record GuardarProyectoDto(string Nombre, string Rol, string Descripcion, DateOnly Desde, DateOnly? Hasta, string? Doi, string? DocumentoNombre, string? DocumentoUri);
public sealed record GuardarTagsDto(IReadOnlyList<string> Terminos);
