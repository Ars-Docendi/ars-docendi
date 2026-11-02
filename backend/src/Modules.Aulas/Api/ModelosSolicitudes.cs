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
    DocenteSolicitudDto? Docente = null,
    string? MotivoCancelacion = null,
    bool EsPropia = false,
    Guid? AulaId = null);

public sealed record CrearSolicitudReservaAulaDto(
    DateOnly Dia,
    TimeOnly HorarioDesde,
    TimeOnly HorarioHasta,
    int CantidadAlumnosAprox,
    Guid MateriaId,
    string Comision);

/// <summary>Se elige el aula del catálogo por id; la respuesta trae su nombre en <c>AulaAsignada</c>.</summary>
public sealed record AsignarAulaDto(Guid AulaId);

public sealed record RechazarSolicitudDto(string Motivo);

public sealed record CancelarReservaDto(string Motivo);
