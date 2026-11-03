namespace Modules.Notificaciones.Contracts;

/// <summary>
/// Public entry point to emit in-app notifications. The emitting module says WHO (selectors) and
/// WHAT (an immutable text snapshot); this module resolves which users are meant.
/// <para>
/// Delivery is best effort: an implementation MUST NOT throw into the business flow. Call it only
/// after the business transaction has committed. The authenticated actor is never notified of
/// their own action.
/// </para>
/// </summary>
public interface INotificador
{
    Task NotificarAsync(AvisoNuevo aviso, IReadOnlyCollection<DestinoNotificacion> destinos, CancellationToken ct);
}

/// <summary>Immutable content of a notification. The text is built by the emitting module.</summary>
/// <param name="RolDestino">Profile under which it is visible; <c>null</c> means any profile.</param>
public sealed record AvisoNuevo(
    string TipoEvento,
    string TipoEntidad,
    Guid EntidadId,
    string? RolDestino,
    string Titulo,
    string Mensaje,
    IReadOnlyDictionary<string, object?> Contexto);

/// <summary>Who a notification is meant for. Closed set of selectors.</summary>
public abstract record DestinoNotificacion
{
    /// <summary>A specific usuario.</summary>
    public sealed record Usuario(Guid UsuarioId) : DestinoNotificacion;

    /// <summary>A persona; skipped (and logged) when it has no usuario.</summary>
    public sealed record Persona(Guid PersonaId) : DestinoNotificacion;

    /// <summary>
    /// Every active usuario holding the system role in the scope: a materia, a carrera, or global
    /// when both are <c>null</c>.
    /// </summary>
    public sealed record RolEnAmbito(string CodigoRol, Guid? MateriaId = null, Guid? CarreraId = null)
        : DestinoNotificacion;
}

/// <summary>Entity kinds a notification can point to. Mirrors the CHECK of the table.</summary>
public static class TiposEntidadNotificacion
{
    public const string SolicitudAula = "SOLICITUD_AULA";
    public const string PedidoDesignacion = "PEDIDO_DESIGNACION";
    public const string Tarea = "TAREA";
}

/// <summary>Event types emitted by the modules.</summary>
public static class TiposEventoNotificacion
{
    public const string AulaAsignada = "AULA_ASIGNADA";
    public const string AulaActualizada = "AULA_ACTUALIZADA";
    public const string SolicitudRechazada = "SOLICITUD_RECHAZADA";
    public const string ReservaCancelada = "RESERVA_CANCELADA";
    public const string SolicitudCanceladaDocente = "SOLICITUD_CANCELADA_DOCENTE";
    public const string PedidoParaRevisar = "PEDIDO_PARA_REVISAR";
    public const string PedidoRechazado = "PEDIDO_RECHAZADO";
    public const string PedidoDevuelto = "PEDIDO_DEVUELTO";
    public const string TareaAsignada = "TAREA_ASIGNADA";
    public const string TareaEstadoCambiado = "TAREA_ESTADO_CAMBIADO";
    public const string TareaComentada = "TAREA_COMENTADA";

    /// <summary>Every event type, for the per-type push preferences. A new constant goes here too.</summary>
    public static IReadOnlyList<string> Todos { get; } =
    [
        AulaAsignada, AulaActualizada, SolicitudRechazada, ReservaCancelada, SolicitudCanceladaDocente,
        PedidoParaRevisar, PedidoRechazado, PedidoDevuelto,
        TareaAsignada, TareaEstadoCambiado, TareaComentada,
    ];
}
