namespace Modules.Tareas.Application;

/// <summary>Persona de una tarea o proyecto: id de usuario, nombre para mostrar y rol de mayor jerarquía.</summary>
public sealed record PersonaTareaDto(Guid Id, string Nombre, string Rol);

public sealed record ComentarioTareaDto(Guid Id, string Autor, string RolAutor, string Texto, DateTimeOffset Fecha);

public sealed record EventoTareaDto(
    Guid Id, string Accion, string PorRol, string PorNombre, string Estado, string? Detalle, DateTimeOffset Fecha);

/// <summary>
/// Tarea tal como la consume el frontend. En el listado <c>Comentarios</c> e <c>Historial</c>
/// vienen vacíos (la tabla no los usa); el detalle los trae completos.
/// </summary>
public sealed record TareaDto(
    Guid Id,
    int Numero,
    string Titulo,
    string Descripcion,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    string Prioridad,
    string Tipo,
    string Estado,
    int PorcentajeAvance,
    string? Solucion,
    PersonaTareaDto Responsable,
    PersonaTareaDto CreadoPor,
    IReadOnlyList<ComentarioTareaDto> Comentarios,
    IReadOnlyList<EventoTareaDto> Historial,
    Guid? ProyectoId,
    Guid? TareaPadreId,
    IReadOnlyList<Guid> TareasRelacionadasIds);

public sealed record ProyectoTareasDto(
    Guid Id,
    int Numero,
    string Nombre,
    string Descripcion,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    string Estado,
    PersonaTareaDto Responsable);

public sealed record CrearTareaRequest(
    string Titulo,
    string? Descripcion,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    string Prioridad,
    string Tipo,
    Guid ResponsableId,
    Guid? ProyectoId,
    Guid? TareaPadreId);

public sealed record EditarTareaRequest(
    string Titulo,
    string? Descripcion,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    string Prioridad,
    string Tipo,
    Guid ResponsableId,
    Guid? ProyectoId);

public sealed record CambiarEstadoTareaRequest(string Estado, string? Comentario, string? Solucion);

public sealed record EditarAvanceRequest(int PorcentajeAvance);

public sealed record ComentarTareaRequest(string Texto);

public sealed record RelacionarTareaRequest(Guid OtraTareaId);

public sealed record CrearProyectoRequest(
    string Nombre,
    string? Descripcion,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    Guid ResponsableId);

public sealed record CambiarEstadoProyectoRequest(string Estado);
