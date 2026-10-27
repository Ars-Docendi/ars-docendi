namespace Modules.Asistente.Application;

/// <summary>Una conversación persistida, sin sus turnos.</summary>
public sealed record ConversacionResumen(
    Guid Id,
    string Titulo,
    DateTimeOffset CreadoEn,
    DateTimeOffset UltimaActividad,
    bool Archivada,
    bool PendienteDeBorrado = false);

/// <summary>Un turno persistido, tal como el historial lo guarda.</summary>
public sealed record TurnoDeHistorial(
    Guid Id,
    string Pregunta,
    string? SqlResuelto,
    EstadoDelTurno Estado,
    DateTimeOffset OcurrioEn,
    IReadOnlyDictionary<string, (TipoDeMencion Tipo, Guid Id)>? Referencias = null);

/// <summary>Una conversación persistida, con todos sus turnos.</summary>
public sealed record ConversacionDetalle(
    Guid Id,
    string Titulo,
    DateTimeOffset CreadoEn,
    DateTimeOffset UltimaActividad,
    IReadOnlyList<TurnoDeHistorial> Turnos);

/// <summary>Lo que necesita la re-ejecución de un turno ya respondido.</summary>
public sealed record TurnoParaReejecutar(
    EstadoDelTurno Estado,
    string? SqlResuelto,
    IReadOnlyDictionary<string, (TipoDeMencion Tipo, Guid Id)>? Referencias = null);

/// <summary>
/// El lado de LECTURA propia del historial: listar, buscar, ver el detalle de una conversación,
/// renombrar, borrar (una o todas) y el dato que necesita la re-ejecución.
/// </summary>
public interface IConsultasDeHistorial
{
    /// <summary>Las conversaciones propias, de la más reciente a la más vieja.</summary>
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
    /// Marca una conversación propia como pendiente de borrado (design.md D4): no la borra todavía,
    /// así que sigue siendo <c>UNDO</c>able dentro de su ventana.
    /// </summary>
    Task<Guid?> EliminarAsync(Guid actor, Guid hiloId, CancellationToken ct);

    /// <summary>
    /// Marca TODAS las conversaciones propias (archivadas incluidas) como pendientes de borrado,
    /// con un lote nuevo.
    /// </summary>
    Task<Guid> EliminarTodoAsync(Guid actor, CancellationToken ct);

    /// <summary>
    /// Deshace un lote de borrado propio, dentro de su ventana
    /// (<c>OpcionesAsistente.VentanaDeDeshacerSegundos</c>).
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
