namespace ArsDocendi.Shared.Identity.Desarrollo;

public sealed record AmbitoDesarrolloDto(Guid Id, string Codigo, string Nombre);

public sealed record RolDesarrolloDto(
    string Codigo,
    string Nombre,
    IReadOnlyList<AmbitoDesarrolloDto> Materias,
    IReadOnlyList<AmbitoDesarrolloDto> Carreras);

public sealed record IdentidadDesarrolloDto(
    Guid UsuarioId,
    string NombreParaMostrar,
    string Upn,
    IReadOnlyList<RolDesarrolloDto> Roles);

public sealed record IdentidadAutenticadaDesarrollo(
    Guid UsuarioId,
    string NombreParaMostrar,
    string Upn,
    string RolCodigo,
    IReadOnlyList<string> Permisos);
