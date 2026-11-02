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

/// <param name="Hash">Hash de cabeza en hexadecimal; es el valor que imprime el lote exportado.</param>
public sealed record IntegridadPeriodoDto(
    bool Integro,
    long Secuencia,
    string Hash,
    IReadOnlyList<HallazgoIntegridadDto> Hallazgos);

public sealed record HallazgoIntegridadDto(string Categoria, Guid Id);
