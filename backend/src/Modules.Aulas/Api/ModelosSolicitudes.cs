namespace Modules.Aulas.Api;

public sealed record DocenteSolicitudDto(Guid Id, string Nombre, string Apellido, string? Legajo);

public sealed record MateriaOpcionDto(Guid Id, string Codigo, string Nombre);

public sealed record SolicitudReservaAulaDto(
    Guid Id,
    DateOnly Dia,
    TimeOnly HorarioDesde,
    TimeOnly HorarioHasta,
    int CantidadAlumnosAprox,
    MateriaOpcionDto Materia,
    string Comision,
    string Estado,
    string? AulaAsignada,
    string? MotivoRechazo,
    DateTimeOffset CreadoEn,
    DocenteSolicitudDto? Docente = null);

public sealed record CrearSolicitudReservaAulaDto(
    DateOnly Dia,
    TimeOnly HorarioDesde,
    TimeOnly HorarioHasta,
    int CantidadAlumnosAprox,
    Guid MateriaId,
    string Comision);

public sealed record AsignarAulaDto(string AulaAsignada);

public sealed record RechazarSolicitudDto(string Motivo);
