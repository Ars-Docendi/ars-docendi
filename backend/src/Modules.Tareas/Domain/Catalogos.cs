namespace Modules.Tareas.Domain;

public abstract class ItemCatalogo
{
    /// <summary>Clave numérica: es lo que referencian las tablas de negocio.</summary>
    public short Id { get; set; }
    /// <summary>Identificador estable para la API y el código; no se guarda en las tablas de negocio.</summary>
    public required string Codigo { get; set; }
    public required string Nombre { get; set; }
    public short Orden { get; set; }
}

public sealed class EstadoProyectoCatalogo : ItemCatalogo
{
    /// <summary>Acción que lleva a este estado ("Finalizar", "Cancelar", "Reabrir").</summary>
    public required string Verbo { get; set; }
    public bool EsInicial { get; set; }
    /// <summary>Un proyecto en este estado acepta tareas nuevas y genera cuadro en la pantalla inicial.</summary>
    public bool AdmiteTareas { get; set; }
}

public sealed class EstadoTareaCatalogo : ItemCatalogo
{
    public bool EsInicial { get; set; }
}

public sealed class PrioridadCatalogo : ItemCatalogo;

public sealed class TipoTareaCatalogo : ItemCatalogo;

/// <summary>Catálogos de Tareas leídos de la base: fuente de los valores válidos y de los estados iniciales.</summary>
public sealed record CatalogosTareas(
    IReadOnlyList<EstadoProyectoCatalogo> EstadosProyecto,
    IReadOnlyList<EstadoTareaCatalogo> EstadosTarea,
    IReadOnlyList<PrioridadCatalogo> Prioridades,
    IReadOnlyList<TipoTareaCatalogo> Tipos)
{
    public EstadoTareaCatalogo EstadoInicialTarea => EstadosTarea.Single(e => e.EsInicial);

    public EstadoProyectoCatalogo EstadoInicialProyecto => EstadosProyecto.Single(e => e.EsInicial);

    public EstadoProyectoCatalogo? EstadoProyecto(string codigo) =>
        EstadosProyecto.FirstOrDefault(e => e.Codigo == codigo);

    public EstadoProyectoCatalogo? EstadoProyecto(short id) => EstadosProyecto.FirstOrDefault(e => e.Id == id);

    public EstadoTareaCatalogo? EstadoTarea(string codigo) => EstadosTarea.FirstOrDefault(e => e.Codigo == codigo);

    public EstadoTareaCatalogo? EstadoTarea(short id) => EstadosTarea.FirstOrDefault(e => e.Id == id);

    public PrioridadCatalogo? Prioridad(string codigo) => Prioridades.FirstOrDefault(p => p.Codigo == codigo);

    public PrioridadCatalogo? Prioridad(short id) => Prioridades.FirstOrDefault(p => p.Id == id);

    public TipoTareaCatalogo? Tipo(string codigo) => Tipos.FirstOrDefault(t => t.Codigo == codigo);

    public TipoTareaCatalogo? Tipo(short id) => Tipos.FirstOrDefault(t => t.Id == id);
}
