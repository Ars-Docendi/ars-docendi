namespace ArsDocendi.Shared.Identity.Administracion;

public sealed record PermisoAdministracionDto(
    Guid Id,
    string Codigo,
    string Nombre,
    string Descripcion);

public sealed record RolAdministracionDto(
    Guid Id,
    string Codigo,
    string Nombre,
    string? Descripcion,
    string Ambito,
    bool EsSistema,
    bool Activo,
    uint Version,
    IReadOnlyList<PermisoAdministracionDto> Permisos);

public sealed record CrearRolDto(
    string Nombre,
    string? Descripcion,
    string Ambito,
    Guid? RolBaseId = null);

public sealed record EditarRolDto(
    string Nombre,
    string? Descripcion,
    string Ambito,
    uint Version);

public sealed record ReemplazarPermisosDto(
    IReadOnlyList<Guid> PermisoIds,
    uint Version);
