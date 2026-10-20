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

public sealed record PerfilDocenteDto(bool EsDocente, int CantidadMaterias);

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
    IReadOnlyList<RolResumenDto> Roles,
    IReadOnlyList<AsignacionRolDto> Membresias,
    PerfilDocenteDto PerfilDocente);

/// <summary>
/// Membresía a guardar. Qué campo lleva depende del rol: el docente manda
/// <see cref="MateriaId"/> y <see cref="CarreraId"/> juntos, el jefe de cátedra sólo
/// <see cref="MateriaId"/> y el coordinador sólo <see cref="CarreraId"/>.
/// </summary>
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
    IReadOnlyList<GuardarAsignacionRolDto> Membresias,
    uint? Version = null);

public sealed record CambiarEstadoUsuarioDto(uint Version);

public sealed record OpcionCatalogoDto(
    Guid Id,
    string Codigo,
    string Nombre,
    Guid? CarreraId = null,
    string? CarreraNombre = null);

public sealed record RolCatalogoDto(
    Guid Id,
    string Codigo,
    string Nombre,
    string Ambito,
    bool EsSistema);

/// <param name="Materias">Materias canónicas (membresía de Jefe de Cátedra).</param>
/// <param name="MateriasPlan">
/// Pares materia–carrera informativos (catálogo materia–plan deduplicado): la membresía de
/// Docente manda <c>MateriaId</c> y <c>CarreraId</c> por separado, y este catálogo sólo ayuda
/// a ofrecer combinaciones válidas en el selector.
/// </param>
public sealed record CatalogosUsuariosDto(
    IReadOnlyList<RolCatalogoDto> Roles,
    IReadOnlyList<OpcionCatalogoDto> Carreras,
    IReadOnlyList<OpcionCatalogoDto> Materias,
    IReadOnlyList<OpcionCatalogoDto> MateriasPlan);
