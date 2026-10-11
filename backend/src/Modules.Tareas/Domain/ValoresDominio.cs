namespace Modules.Tareas.Domain;

/// <summary>
/// Códigos de estado que las reglas de una tarea nombran explícitamente (Pausa exige un
/// comentario, Resuelta exige Solución, Cancelar es exclusivo de la autoridad creadora).
/// La lista de estados válidos y cuál es el inicial NO viven acá: salen del catálogo
/// <c>tareas.estados_tarea</c>.
/// </summary>
/// <summary>
/// Estados de proyecto que tienen semántica propia en las reglas de transición. El resto
/// (lista, inicial, admite tareas) sale del catálogo <c>tareas.estados_proyecto</c>.
/// </summary>
public static class EstadosProyecto
{
    public const string Finalizado = "finalizado";
    public const string Cancelado = "cancelado";
}

public static class EstadosTarea
{
    public const string Pausa = "pausa";
    public const string Resuelta = "resuelta";
    public const string Cancelada = "cancelada";

    public static bool EsTerminal(string estado) => estado is Resuelta or Cancelada;
}

public static class AccionesHistorial
{
    public const string Crear = "crear";
    public const string CambiarEstado = "cambiar_estado";
    public const string EditarAvance = "editar_avance";
    public const string Editar = "editar";
}
