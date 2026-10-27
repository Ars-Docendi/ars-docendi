namespace Modules.Asistente.Application;

/// <summary>
/// Evita que un doble clic se pague dos veces (§4.6). La caché en memoria alcanza para lo que el
/// requisito pide de verdad, que es el doble clic.
/// </summary>
public interface IIdempotencia
{
    /// <summary>Devuelve la respuesta ya calculada para esa clave, o <c>null</c>.</summary>
    ResultadoDelTurno? Recordar(Guid actor, string clave);

    /// <summary>Guarda la respuesta de un turno bajo su clave.</summary>
    void Guardar(Guid actor, string clave, ResultadoDelTurno resultado);
}
