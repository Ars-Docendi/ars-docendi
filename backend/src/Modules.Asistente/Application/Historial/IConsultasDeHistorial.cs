namespace Modules.Asistente.Application;

/// <summary>Una conversación persistida, sin sus turnos.</summary>
/// <param name="Archivada">
/// Si está archivada (design.md D3 de asistente-historial-conversaciones).
/// Archivar no toca <see cref="UltimaActividad"/>: la retención sigue
/// contando igual, archivada o no.
/// </param>
/// <param name="PendienteDeBorrado">
/// Si un borrado la marcó y su ventana todavía no venció (design.md D4).
/// SIEMPRE falso en el lado propio del historial —<see cref="IConsultasDeHistorial"/>
/// nunca devuelve una conversación pendiente, ni al actor ni a nadie—; sólo
/// <c>IConsultasDeAuditoriaDeSoporte</c> lo pone en verdadero, porque support
/// SÍ ve una conversación pendiente, marcada, hasta que la ventana cierra.
/// </param>
public sealed record ConversacionResumen(
    Guid Id,
    string Titulo,
    DateTimeOffset CreadoEn,
    DateTimeOffset UltimaActividad,
    bool Archivada,
    bool PendienteDeBorrado = false);

/// <summary>Un turno persistido, tal como el historial lo guarda.</summary>
/// <remarks>
/// <c>SqlResuelto</c> viaja siempre desde acá: la visibilidad por
/// <c>asistente.ver_consulta</c> (design.md D9 de
/// asistente-historial-conversaciones) se decide en el borde HTTP, no en esta
/// consulta — el mismo criterio que <c>CarrilSql.LaConsulta</c> ya aplica
/// para el turno en vivo.
/// </remarks>
public sealed record TurnoDeHistorial(
    Guid Id, string Pregunta, string? SqlResuelto, EstadoDelTurno Estado, DateTimeOffset OcurrioEn);

/// <summary>Una conversación persistida, con todos sus turnos.</summary>
public sealed record ConversacionDetalle(
    Guid Id,
    string Titulo,
    DateTimeOffset CreadoEn,
    DateTimeOffset UltimaActividad,
    IReadOnlyList<TurnoDeHistorial> Turnos);

/// <summary>Lo que necesita la re-ejecución de un turno ya respondido.</summary>
public sealed record TurnoParaReejecutar(EstadoDelTurno Estado, string? SqlResuelto);

/// <summary>
/// El lado de LECTURA propia del historial: listar, buscar, ver el detalle de
/// una conversación, renombrar, borrar (una o todas) y el dato que necesita
/// la re-ejecución.
/// </summary>
/// <remarks>
/// Todo escrito acotado al <paramref name="actor"/> que llama — nunca hay
/// forma de operar sobre la conversación de otro por esta interfaz (para eso
/// está <c>asistente-acceso-de-soporte-al-historial</c>, con su propio
/// permiso y su propia auditoría). Un id que no existe o que pertenece a otro
/// actor se trata IGUAL — <c>null</c>/<c>false</c>/sin filas — para no filtrar
/// cuál de los dos casos fue.
/// </remarks>
public interface IConsultasDeHistorial
{
    /// <summary>
    /// Las conversaciones propias, de la más reciente a la más vieja.
    /// </summary>
    /// <param name="busqueda">
    /// Texto a buscar en las preguntas propias (búsqueda de texto completo,
    /// design.md D6), o <c>null</c>/vacío para listar todo.
    /// </param>
    Task<IReadOnlyList<ConversacionResumen>> ListarAsync(
        Guid actor, string? busqueda, CancellationToken ct);

    /// <summary>Una conversación propia, con sus turnos. <c>null</c> si no es propia.</summary>
    Task<ConversacionDetalle?> ObtenerAsync(Guid actor, Guid hiloId, CancellationToken ct);

    /// <summary>Renombra una conversación propia. <c>false</c> si no es propia.</summary>
    Task<bool> RenombrarAsync(Guid actor, Guid hiloId, string nuevoTitulo, CancellationToken ct);

    /// <summary>Archiva una conversación propia. <c>false</c> si no es propia.</summary>
    Task<bool> ArchivarAsync(Guid actor, Guid hiloId, CancellationToken ct);

    /// <summary>Desarchiva una conversación propia. <c>false</c> si no es propia.</summary>
    Task<bool> DesarchivarAsync(Guid actor, Guid hiloId, CancellationToken ct);

    /// <summary>
    /// Marca una conversación propia como pendiente de borrado (design.md D4):
    /// no la borra todavía, así que sigue siendo <c>UNDO</c>able dentro de su
    /// ventana. Devuelve el id del lote de borrado, o <c>null</c> si el hilo
    /// no existe o no es propio.
    /// </summary>
    Task<Guid?> EliminarAsync(Guid actor, Guid hiloId, CancellationToken ct);

    /// <summary>
    /// Marca TODAS las conversaciones propias (archivadas incluidas) como
    /// pendientes de borrado, con un lote nuevo. Siempre devuelve un lote,
    /// aunque no haya ninguna conversación que marcar.
    /// </summary>
    Task<Guid> EliminarTodoAsync(Guid actor, CancellationToken ct);

    /// <summary>
    /// Deshace un lote de borrado propio, dentro de su ventana
    /// (<c>OpcionesAsistente.VentanaDeDeshacerSegundos</c>). <c>false</c>
    /// si el lote no existe, no es propio, o venció.
    /// </summary>
    Task<bool> DeshacerBorradoAsync(Guid actor, Guid lote, CancellationToken ct);

    /// <summary>
    /// Los turnos de una conversación propia, tal como el historial los
    /// guarda — para <c>Reanudar</c> (asistente-historial-conversaciones §7).
    /// </summary>
    Task<IReadOnlyList<TurnoDeHistorial>?> ObtenerTurnosAsync(Guid actor, Guid hiloId, CancellationToken ct);

    /// <summary>
    /// El estado y la SQL de un turno propio, para la re-ejecución (§8).
    /// <c>null</c> si el turno no existe o no es de este actor.
    /// </summary>
    Task<TurnoParaReejecutar?> ObtenerTurnoParaReejecutarAsync(Guid actor, Guid turnoId, CancellationToken ct);
}
