namespace ArsDocendi.Shared.Identity.Administracion;

public sealed record AsignacionRolDto(
    Guid Id,
    Guid RolId,
    string Codigo,
    string Nombre,
    string Ambito,
    Guid? MateriaId,
    Guid? CarreraId);

public sealed record RolResumenDto(Guid Id, string Codigo, string Nombre);

/// <summary>
/// Membresía a guardar. Qué campo lleva depende del rol: el docente manda
/// <see cref="MateriaId"/> y <see cref="CarreraId"/> juntos, el jefe de cátedra sólo
/// <see cref="MateriaId"/> y el coordinador sólo <see cref="CarreraId"/>.
/// </summary>
public sealed record GuardarAsignacionRolDto(
    Guid RolId,
    Guid? MateriaId = null,
    Guid? CarreraId = null);

public sealed record CambiarEstadoUsuarioDto(uint Version);

public sealed record OpcionCatalogoDto(
    Guid Id,
    string Codigo,
    string Nombre,
    Guid? CarreraId = null,
    string? CarreraNombre = null,
    string? CarreraCodigo = null);

public sealed record RolCatalogoDto(
    Guid Id,
    string Codigo,
    string Nombre,
    string Ambito,
    bool EsSistema);
