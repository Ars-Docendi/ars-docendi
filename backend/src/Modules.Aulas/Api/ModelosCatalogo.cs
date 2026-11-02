namespace Modules.Aulas.Api;

public sealed record AulaDto(
    Guid Id,
    string Nombre,
    string Tipo,
    int Capacidad,
    string? Equipamiento,
    bool Activa,
    uint Version);

public sealed record GuardarAulaDto(
    string Nombre,
    string Tipo,
    int Capacidad,
    string? Equipamiento,
    uint? Version = null);

public sealed record CambiarEstadoAulaDto(uint Version);
