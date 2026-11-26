using Modules.Notificaciones.Contracts;

namespace Modules.Notificaciones.Domain;

/// <summary>
/// The modules a user can switch push on or off for. A notice belongs to the module of the entity it
/// points to, so every event type of a module follows the same switch. Mirrors the CHECK of
/// <c>preferencias_push.modulo</c>.
/// </summary>
public static class ModulosPush
{
    public const string Aulas = "AULAS";
    public const string Designaciones = "DESIGNACIONES";
    public const string Tareas = "TAREAS";

    public static IReadOnlyList<string> Todos { get; } = [Aulas, Designaciones, Tareas];

    /// <summary>The module of a notice, or <c>null</c> for an entity kind with no switch (always pushed).</summary>
    public static string? DeEntidad(string tipoEntidad) => tipoEntidad switch
    {
        TiposEntidadNotificacion.SolicitudAula => Aulas,
        TiposEntidadNotificacion.PedidoDesignacion => Designaciones,
        TiposEntidadNotificacion.Tarea => Tareas,
        _ => null,
    };
}
