namespace Modules.Designaciones.Api;

public sealed record PeriodoDto(
    Guid Id,
    string Nombre,
    DateOnly CargaDesde,
    DateOnly CargaHasta,
    DateOnly ImpactoDesde,
    DateOnly ImpactoHasta,
    bool Activo,
    uint Version);

public sealed record GuardarPeriodoDto(
    string Nombre,
    DateOnly CargaDesde,
    DateOnly CargaHasta,
    DateOnly ImpactoDesde,
    DateOnly ImpactoHasta,
    bool Activo,
    uint? Version = null);

public sealed record CambiarEstadoPeriodoDto(uint Version);
