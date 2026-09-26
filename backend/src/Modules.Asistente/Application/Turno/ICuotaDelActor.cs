namespace Modules.Asistente.Application;

/// <summary>
/// Cupo diario de turnos por actor, persistente (asistente-presupuesto-persistente).
/// </summary>
/// <remarks>
/// Reemplaza al cupo de llamadas en memoria (design.md D2 de
/// asistente-administracion-de-uso): se mide en <b>turnos</b> y no en llamadas al
/// modelo, sobre un <b>día calendario UTC</b> y no una ventana deslizante. Un
/// turno cuenta si y solo si invocó al modelo al menos una vez (D4) — un saludo o
/// un menú de aclaración resuelto sin proveedor no gasta cupo, igual que antes.
///
/// Todos los métodos son asíncronos porque el cupo vive en Postgres
/// (<c>asistente.presupuesto_rol</c>/<c>presupuesto_usuario</c>), a diferencia del
/// cupo en memoria que reemplaza.
/// </remarks>
public interface ICuotaDelActor
{
    /// <summary>Si al actor le queda cupo hoy.</summary>
    Task<bool> HayCupoAsync(Guid actor, CancellationToken ct);

    /// <summary>
    /// Anota que el turno gastó cupo.
    /// </summary>
    /// <remarks>
    /// Se llama exactamente una vez por turno, en el mismo <c>finally</c> que
    /// antes llamaba a <c>Anotar</c> (D4: incondicional, incluso si el turno
    /// falló). La implementación por defecto (<c>CuotaPersistente</c>) no
    /// necesita escribir nada acá: el consumo se deriva contando, al vuelo,
    /// las filas de <c>asistente.registro_operativo</c> del día con
    /// <c>llamadas_al_modelo &gt; 0</c> — la fila que ese mismo turno ya
    /// escribió (vía <c>IRegistroDelTurno</c>, que corre ANTES de este
    /// <c>finally</c>) ya es, ella misma, la anotación. Mantener el método en
    /// la interfaz y en el sitio de llamada documenta la garantía y deja
    /// abierta una implementación futura que sí necesite un escrito propio.
    /// </remarks>
    Task AnotarAsync(Guid actor, CancellationToken ct);

    /// <summary>
    /// Cuándo vuelve a haber cupo, o <c>null</c> si ya hay o si el cupo está
    /// desactivado (0).
    /// </summary>
    Task<DateTimeOffset?> CupoVuelveAAsync(Guid actor, CancellationToken ct);

    /// <summary>
    /// Cuántos turnos le quedan hoy al actor.
    /// </summary>
    /// <remarks>
    /// Con el cupo desactivado (0, en el rol o en el override del actor) no hay
    /// un número de turnos restantes que decir: se devuelve
    /// <see cref="int.MaxValue"/> como centinela de "sin tope", documentado
    /// acá porque la firma del método —heredada de la especificación de esta
    /// tarea— no admite un <c>int?</c>. El llamador (grupo 7, cupo visible al
    /// usuario) es quien decide cómo mostrar ese centinela.
    /// </remarks>
    Task<int> CupoRestanteAsync(Guid actor, CancellationToken ct);
}
