namespace Modules.Designaciones.Api;

/// <summary>Designación vigente de la persona autenticada, para Mi Portal.</summary>
public sealed record DesignacionPropiaDto(
    Guid Id,
    string Materia,
    string Carrera,
    string Cargo,
    string? Dedicacion,
    int Horas,
    int? HorasInvestigacion,
    int? HorasExternas,
    DateOnly VigenteDesde);
