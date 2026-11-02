namespace Modules.Designaciones.Api;

/// <summary>Par materia–carrera: una materia dictada en más de una carrera aparece una vez por carrera.</summary>
public sealed record MateriaDesignacionesDto(
    Guid MateriaId,
    string Codigo,
    string Nombre,
    Guid CarreraId,
    string CarreraNombre);

public sealed record PersonaDesignacionesDto(
    Guid Id,
    string Nombre,
    string Apellido,
    string Documento,
    string? Legajo,
    IReadOnlyList<DesignacionVigenteCatalogoDto> DesignacionesVigentes);

public sealed record DesignacionVigenteCatalogoDto(
    Guid MateriaId,
    string MateriaNombre,
    Guid CarreraId,
    Guid CargoId,
    string CargoNombre,
    string? Dedicacion,
    int Horas,
    Guid? DedicacionId = null,
    int? HorasInvestigacion = null,
    int? HorasExternas = null);

public sealed record CargoDesignacionesDto(
    Guid Id,
    string Codigo,
    string Nombre,
    string Abreviatura,
    short Orden);

public sealed record DedicacionDesignacionesDto(Guid Id, short Codigo, string Nombre, short Orden);

public sealed record CatalogosDesignacionesDto(
    PeriodoDto? PeriodoActivo,
    IReadOnlyList<PeriodoDto> Periodos,
    IReadOnlyList<MateriaDesignacionesDto> Materias,
    IReadOnlyList<PersonaDesignacionesDto> Personas,
    IReadOnlyList<CargoDesignacionesDto> Cargos,
    IReadOnlyList<DedicacionDesignacionesDto> Dedicaciones,
    IReadOnlyList<string> TiposBaja,
    IReadOnlyList<string> Novedades);
