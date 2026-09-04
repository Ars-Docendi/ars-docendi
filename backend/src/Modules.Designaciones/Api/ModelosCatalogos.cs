namespace Modules.Designaciones.Api;

public sealed record MateriaDesignacionesDto(
    Guid Id,
    string Codigo,
    string Nombre,
    Guid CarreraId);

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
    Guid CargoId,
    string CargoNombre,
    string? Dedicacion,
    int Horas);

public sealed record CargoDesignacionesDto(
    Guid Id,
    string Codigo,
    string Nombre,
    string Abreviatura,
    short Orden);

public sealed record CatalogosDesignacionesDto(
    PeriodoDto? PeriodoActivo,
    IReadOnlyList<PeriodoDto> Periodos,
    IReadOnlyList<MateriaDesignacionesDto> Materias,
    IReadOnlyList<PersonaDesignacionesDto> Personas,
    IReadOnlyList<CargoDesignacionesDto> Cargos,
    IReadOnlyList<string> Dedicaciones,
    IReadOnlyList<string> TiposBaja,
    IReadOnlyList<string> Novedades);
