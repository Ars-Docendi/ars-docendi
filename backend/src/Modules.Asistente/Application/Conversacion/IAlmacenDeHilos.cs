namespace Modules.Asistente.Application;

/// <summary>Resuelve el hilo conversacional de un turno.</summary>
public interface IAlmacenDeHilos
{
    /// <summary>
    /// Devuelve el hilo pedido, o uno nuevo si no existe o si venció.
    /// </summary>
    /// <param name="hilo">
    /// El identificador que trajo el cliente. Nulo en el primer turno.
    /// </param>
    /// <param name="actor">El usuario autenticado del turno.</param>
    /// <exception cref="HiloAjeno">
    /// Si el hilo existe y pertenece a otro actor. Perder el hilo degrada el
    /// seguimiento y no rompe el turno, pero usar el de otro no es una degradación:
    /// es un error que se quiere ver.
    /// </exception>
    HiloConversacional Resolver(Guid? hilo, Guid actor);

    /// <summary>
    /// Crea un hilo efímero NUEVO, con un id propio y la vigencia normal de
    /// 120 minutos, sembrado con los turnos de una conversación persistida —
    /// para reanudarla (asistente-historial-conversaciones §7, design.md D3).
    /// </summary>
    /// <remarks>
    /// No revive el id efímero viejo: en general no se puede —puede estar
    /// vencido hace mucho, o siempre lo va a estar para algo que valga la
    /// pena «reanudar»—, y además exigiría que el resto de este puerto
    /// soportara un id impuesto desde afuera en lugar de uno que él mismo
    /// mintió.
    /// </remarks>
    /// <param name="actor">El actor al que le pertenece la conversación.</param>
    /// <param name="hiloHistorico">
    /// El id de la conversación persistida (<c>asistente.hilo_historico.id</c>).
    /// El hilo devuelto queda con <see cref="HiloConversacional.HiloHistorico"/>
    /// ya fijado en este valor, así que los turnos siguientes extienden la
    /// MISMA conversación en vez de abrir una nueva.
    /// </param>
    /// <param name="turnos">
    /// Los turnos persistidos, del más viejo al más reciente, tal como
    /// <see cref="HiloConversacional.Agregar"/> ya los recibe.
    /// </param>
    HiloConversacional Sembrar(Guid actor, Guid hiloHistorico, IReadOnlyList<TurnoDelHilo> turnos);
}
