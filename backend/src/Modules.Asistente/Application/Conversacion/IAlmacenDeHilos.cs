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
}
