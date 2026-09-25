namespace Modules.Tareas.Domain;

public sealed class Proyecto
{
    public Guid Id { get; set; }
    public int Numero { get; set; }
    public required string Nombre { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public short EstadoId { get; set; }
    public Guid ResponsableId { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}

public sealed class Tarea
{
    public Guid Id { get; set; }
    public int Numero { get; set; }
    public required string Titulo { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public short PrioridadId { get; set; }
    public short TipoId { get; set; }
    public short EstadoId { get; set; }
    public short PorcentajeAvance { get; set; }
    public string? Solucion { get; set; }
    public Guid ResponsableId { get; set; }
    public Guid CreadoPorId { get; set; }
    public Guid? ProyectoId { get; set; }
    public Guid? TareaPadreId { get; set; }
    public DateTimeOffset CreadoEn { get; set; }

    public List<ComentarioTarea> Comentarios { get; set; } = [];
    public List<EventoTarea> Historial { get; set; } = [];
}

/// <summary>Vínculo simple entre dos tareas. Una fila por par, con <c>TareaId &lt; RelacionadaId</c>.</summary>
public sealed class RelacionTarea
{
    public Guid TareaId { get; set; }
    public Guid RelacionadaId { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}

public sealed class ComentarioTarea
{
    public Guid Id { get; set; }
    public Guid TareaId { get; set; }
    public Guid AutorId { get; set; }
    public required string AutorRol { get; set; }
    public required string Texto { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}

public sealed class EventoTarea
{
    public Guid Id { get; set; }
    public Guid TareaId { get; set; }
    public required string Accion { get; set; }
    public Guid ActorId { get; set; }
    public required string ActorRol { get; set; }
    public short EstadoId { get; set; }
    public string? Detalle { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}
