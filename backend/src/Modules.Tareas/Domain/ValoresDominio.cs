namespace Modules.Tareas.Domain;

/// <summary>Estados de una tarea. Los códigos son los mismos que persiste la base y consume el frontend.</summary>
public static class EstadosTarea
{
    public const string Pendiente = "pendiente";
    public const string EnCurso = "en_curso";
    public const string Pausa = "pausa";
    public const string Resuelta = "resuelta";
    public const string Cancelada = "cancelada";

    public static readonly string[] Todos = [Pendiente, EnCurso, Pausa, Resuelta, Cancelada];

    public static bool EsTerminal(string estado) => estado is Resuelta or Cancelada;
}

public static class Prioridades
{
    public static readonly string[] Todas = ["alta", "media", "baja"];
}

public static class TiposTarea
{
    public static readonly string[] Todos =
        ["extension", "administrativa", "posgrado", "investigacion", "academica", "decanato"];
}

public static class EstadosProyecto
{
    public const string Abierto = "abierto";
    public const string Finalizado = "finalizado";
    public const string Cancelado = "cancelado";

    public static readonly string[] Todos = [Abierto, Finalizado, Cancelado];
}

public static class AccionesHistorial
{
    public const string Crear = "crear";
    public const string CambiarEstado = "cambiar_estado";
    public const string EditarAvance = "editar_avance";
    public const string Editar = "editar";
}
