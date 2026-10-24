using ArsDocendi.Shared.Identity.Administracion;
using Modules.Designaciones.Contracts.Administracion;

namespace ArsDocendi.Host.Administracion;

/// <summary>Designación vigente de la persona.</summary>
public sealed record AsignacionDocenteDto(
    Guid Id,
    Guid MateriaId,
    string MateriaCodigo,
    string MateriaNombre,
    Guid CarreraId,
    Guid CargoId,
    string CargoNombre,
    string CargoAbreviatura,
    string? Dedicacion,
    int Horas,
    Guid? DedicacionId = null,
    int? HorasInvestigacion = null,
    int? HorasExternas = null);

/// <summary>
/// Una persona con su cuenta, sus roles con ámbito y sus designaciones vigentes: lo que antes
/// se repartía entre Usuarios y Docentes.
/// </summary>
public sealed record PersonaAdministracionDto(
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
    IReadOnlyList<RolResumenDto> Roles,
    IReadOnlyList<AsignacionRolDto> Membresias,
    IReadOnlyList<AsignacionDocenteDto> Asignaciones);

public sealed record GuardarPersonaDto(
    string Nombre,
    string Apellido,
    string Documento,
    string? Legajo,
    string? Cuil,
    DateOnly? FechaNacimiento,
    string? Telefono,
    string Upn,
    IReadOnlyList<GuardarAsignacionRolDto> Membresias,
    IReadOnlyList<GuardarDesignacionVigenteDto> Designaciones,
    uint? Version = null);

/// <summary>Resultado de la verificación de DNI: lo mínimo para avisar quién lo tiene.</summary>
public sealed record DocumentoRegistradoDto(
    bool Existe,
    Guid? PersonaId = null,
    string? Nombre = null,
    string? Legajo = null,
    bool? Activo = null);

/// <param name="Materias">Materias canónicas (Jefe de Cátedra y roles de ámbito materia).</param>
/// <param name="MateriasPlan">Pares materia–carrera informativos (Docente y designaciones).</param>
/// <param name="Carreras">Carreras activas (Coordinador de Carrera y roles de ámbito carrera).</param>
public sealed record CatalogosPersonasDto(
    IReadOnlyList<RolCatalogoDto> Roles,
    IReadOnlyList<OpcionCatalogoDto> Materias,
    IReadOnlyList<OpcionCatalogoDto> MateriasPlan,
    IReadOnlyList<OpcionCatalogoDto> Carreras,
    IReadOnlyList<CargoAdministracionDto> Cargos,
    IReadOnlyList<DedicacionAdministracionDto> Dedicaciones);
