namespace Modules.Asistente.Application;

/// <summary>Una conversación persistida, sin sus turnos.</summary>
public sealed record ConversacionResumen(
    Guid Id, string Titulo, DateTimeOffset CreadoEn, DateTimeOffset UltimaActividad);

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

    /// <summary>Borra una conversación propia (y sus turnos, por cascada).</summary>
    Task<bool> EliminarAsync(Guid actor, Guid hiloId, CancellationToken ct);

    /// <summary>Borra TODAS las conversaciones propias. Devuelve cuántas borró.</summary>
    Task<int> EliminarTodoAsync(Guid actor, CancellationToken ct);

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
