namespace Modules.Tareas.Domain;

public abstract class ItemCatalogo
{
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
    public string EstadoInicialTarea => EstadosTarea.Single(e => e.EsInicial).Codigo;

    public string EstadoInicialProyecto => EstadosProyecto.Single(e => e.EsInicial).Codigo;

    public EstadoProyectoCatalogo? EstadoProyecto(string codigo) =>
        EstadosProyecto.FirstOrDefault(e => e.Codigo == codigo);
}
