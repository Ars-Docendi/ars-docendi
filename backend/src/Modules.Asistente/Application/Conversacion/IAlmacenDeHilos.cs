namespace Modules.Asistente.Application;

/// <summary>Resuelve el hilo conversacional de un turno.</summary>
public interface IAlmacenDeHilos
{
    /// <summary>Devuelve el hilo pedido, o uno nuevo si no existe o si venció.</summary>
    HiloConversacional Resolver(Guid? hilo, Guid actor);

    /// <summary>
    /// Crea un hilo efímero NUEVO, con un id propio y la vigencia normal de 120 minutos, sembrado
    /// con los turnos de una conversación persistida — para reanudarla
    /// (asistente-historial-conversaciones §7, design.md D3).
    /// </summary>
    HiloConversacional Sembrar(Guid actor, Guid hiloHistorico, IReadOnlyList<TurnoDelHilo> turnos);
}
