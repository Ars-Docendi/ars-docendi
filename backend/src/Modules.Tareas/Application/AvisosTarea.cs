using Modules.Notificaciones.Contracts;
using Modules.Tareas.Domain;

namespace Modules.Tareas.Application;

/// <summary>
/// Text snapshots of the notifications of a task. They carry no profile (<c>RolDestino</c> is
/// <c>null</c>): a task belongs to the usuario, not to one of their roles.
/// </summary>
internal static class AvisosTarea
{
    private const int MaximoComentario = 90;

    public static AvisoNuevo Asignada(Tarea tarea) => Nuevo(
        TiposEventoNotificacion.TareaAsignada, tarea, "Tarea asignada",
        $"Se te asignó la tarea N° {tarea.Numero}: {tarea.Titulo}.",
        new() { ["numero"] = tarea.Numero, ["titulo"] = tarea.Titulo });

    public static AvisoNuevo EstadoCambiado(Tarea tarea, string actor, string de, string a) => Nuevo(
        TiposEventoNotificacion.TareaEstadoCambiado, tarea, "Cambio de estado en la tarea",
        $"{actor} cambió la tarea N° {tarea.Numero} ({tarea.Titulo}) de {de} a {a}.",
        new() { ["numero"] = tarea.Numero, ["actor"] = actor, ["de"] = de, ["a"] = a });

    public static AvisoNuevo Comentada(Tarea tarea, string actor, string texto) => Nuevo(
        TiposEventoNotificacion.TareaComentada, tarea, "Nuevo comentario en la tarea",
        $"{actor} comentó en la tarea N° {tarea.Numero} ({tarea.Titulo}): “{Recortar(texto)}”",
        new() { ["numero"] = tarea.Numero, ["actor"] = actor });

    private static string Recortar(string texto) =>
        texto.Length > MaximoComentario ? texto[..(MaximoComentario - 1)].TrimEnd() + "…" : texto;

    private static AvisoNuevo Nuevo(
        string tipoEvento, Tarea tarea, string titulo, string mensaje, Dictionary<string, object?> contexto) =>
        new(tipoEvento, TiposEntidadNotificacion.Tarea, tarea.Id, null, titulo, mensaje, contexto);
}
