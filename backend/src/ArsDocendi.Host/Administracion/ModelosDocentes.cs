using Modules.Designaciones.Contracts.Administracion;

namespace ArsDocendi.Host.Administracion;

public sealed record AsignacionDocenteDto(
    Guid Id,
    Guid MateriaId,
    string MateriaCodigo,
    string MateriaNombre,
    Guid CargoId,
    string CargoNombre,
    string CargoAbreviatura,
    string? Dedicacion,
    int Horas);

public sealed record DocenteAdministracionDto(
    Guid PersonaId,
    Guid? UsuarioId,
    string Nombre,
    string Apellido,
    string Documento,
    string? Legajo,
    string? Cuil,
    DateOnly? FechaNacimiento,
    string? Telefono,
    string? Upn,
    bool TieneCuenta,
    bool Activo,
    uint? Version,
    IReadOnlyList<string> Roles,
    IReadOnlyList<AsignacionDocenteDto> Asignaciones);

public sealed record GuardarDocenteDto(
    Guid? PersonaId,
    string Nombre,
    string Apellido,
    string Documento,
    string? Legajo,
    string? Cuil,
    DateOnly? FechaNacimiento,
    string? Telefono,
    string Upn,
    IReadOnlyList<string> Roles,
    IReadOnlyList<GuardarDesignacionVigenteDto> Designaciones,
    uint? Version = null);

public sealed record PersonaElegibleDto(
    Guid Id,
    string Nombre,
    string Apellido,
    string Documento,
    string? Legajo,
    string? Cuil,
    DateOnly? FechaNacimiento,
    string? Telefono,
    string? Upn,
    uint? Version);

public sealed record CatalogosDocentesDto(
    IReadOnlyList<ArsDocendi.Shared.Identity.Administracion.OpcionCatalogoDto> Roles,
    IReadOnlyList<ArsDocendi.Shared.Identity.Administracion.OpcionCatalogoDto> Materias,
    IReadOnlyList<CargoAdministracionDto> Cargos,
    IReadOnlyList<PersonaElegibleDto> PersonasElegibles);
