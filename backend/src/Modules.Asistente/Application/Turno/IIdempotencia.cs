namespace Modules.Asistente.Application;

/// <summary>
/// Evita que un doble clic se pague dos veces (§4.6).
/// </summary>
/// <remarks>
/// No es formalismo: cada turno cuesta dos o tres llamadas al modelo, así que un
/// doble submit se factura completo dos veces.
///
/// <b>Vive en memoria y no persiste nada.</b> No se reusa ni se copia
/// <c>designaciones.idempotencia_comandos</c>, que guarda el <c>response_body</c>
/// completo: eso es exactamente lo que este módulo decidió no persistir, y copiar esa
/// tabla acá metería las filas devueltas —las que el enmascaramiento acaba de
/// proteger— en una tabla sin política de retención, por la puerta de atrás.
///
/// La caché en memoria alcanza para lo que el requisito pide de verdad, que es el
/// doble clic. No sobrevive al redespliegue, y eso es coherente con no persistir ni
/// el hilo conversacional.
/// </remarks>
public interface IIdempotencia
{
    /// <summary>
    /// Devuelve la respuesta ya calculada para esa clave, o <c>null</c>.
    /// </summary>
    /// <remarks>
    /// La clave se acota por actor. Sin eso, la clave de un usuario le devolvería a
    /// otro una respuesta calculada con el alcance del primero: un canal de fuga
    /// trivial de disparar y difícil de notar, porque el segundo usuario recibe algo
    /// que parece una respuesta correcta.
    /// </remarks>
    ResultadoDelTurno? Recordar(Guid actor, string clave);

    /// <summary>Guarda la respuesta de un turno bajo su clave.</summary>
    void Guardar(Guid actor, string clave, ResultadoDelTurno resultado);
}
