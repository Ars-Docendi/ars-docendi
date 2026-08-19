namespace ArsDocendi.Shared.Identity.Administracion;

public sealed record AsignacionRolDto(
    Guid Id,
    Guid RolId,
    string Codigo,
    string Nombre,
    string Ambito,
    Guid? MateriaId,
    Guid? CarreraId);

public sealed record UsuarioAdministracionDto(
    Guid Id,
    Guid PersonaId,
    string Nombre,
    string Apellido,
    string Documento,
    string? Legajo,
    string? Cuil,
    DateOnly? FechaNacimiento,
    string? Telefono,
    string Upn,
    bool Activo,
    uint Version,
    IReadOnlyList<AsignacionRolDto> Roles);

public sealed record GuardarAsignacionRolDto(
    Guid RolId,
    Guid? MateriaId = null,
    Guid? CarreraId = null);

public sealed record GuardarUsuarioDto(
    string Nombre,
    string Apellido,
    string Documento,
    string? Legajo,
    string? Cuil,
    DateOnly? FechaNacimiento,
    string? Telefono,
    string Upn,
    IReadOnlyList<GuardarAsignacionRolDto> Roles,
    uint? Version = null);

public sealed record CambiarEstadoUsuarioDto(uint Version);

public sealed record OpcionCatalogoDto(Guid Id, string Codigo, string Nombre);

public sealed record RolCatalogoDto(
    Guid Id,
    string Codigo,
    string Nombre,
    string Ambito,
    bool EsSistema);

public sealed record CatalogosUsuariosDto(
    IReadOnlyList<RolCatalogoDto> Roles,
    IReadOnlyList<OpcionCatalogoDto> Carreras,
    IReadOnlyList<OpcionCatalogoDto> Materias);
