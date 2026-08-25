namespace Modules.Asistente.Application;

/// <summary>Los cuatro estados en que puede terminar un turno (RF-14).</summary>
public enum EstadoDelTurno
{
    /// <summary>Se respondió con datos.</summary>
    Respondida,

    /// <summary>La pregunta no se puede responder con lo que el asistente ve.</summary>
    NoContestable,

    /// <summary>Falta un dato para poder responder sin adivinar.</summary>
    NecesitaAclaracion,

    /// <summary>El proveedor no está disponible o el turno se quedó sin cupo.</summary>
    ServicioDegradado,
}

/// <summary>
/// Lo que devuelve el carril SQL.
/// </summary>
/// <remarks>
/// No es todavía el contrato de la API: el endpoint, la forma de la respuesta
/// HTTP y la <c>Idempotency-Key</c> llegan con la épica de superficie de usuario.
/// Éste es el valor de retorno del servicio, y existe para que el carril se pueda
/// ejercitar sin inventar dos veces el contrato.
/// </remarks>
/// <param name="Estado">En cuál de los cuatro estados terminó.</param>
/// <param name="Respuesta">El texto que lee el usuario.</param>
/// <param name="Razonamiento">
/// Cómo se interpretó la pregunta, tal como lo devolvió la generación. Se expone
/// sin agregarle ninguna explicación generada aparte (RF-11).
/// </param>
/// <param name="PreguntaInterpretada">
/// Presente solo cuando difiere del mensaje del usuario (RF-10).
/// </param>
/// <param name="Columnas">Nombres de las columnas del resultado, si hubo.</param>
/// <param name="Filas">Las filas devueltas, ya recortadas al tope.</param>
/// <param name="Truncado">
/// Si hubo más filas que el tope. Booleano, nunca un conteo: cuántas quedaron
/// afuera es un canal de inferencia sobre datos que el usuario no puede ver.
/// </param>
/// <param name="Sensibilidad">
/// La clasificación de cada columna, paralela a <paramref name="Columnas"/>. La
/// necesita quien renderiza: con columnas sensibles el modelo redacta el marco y
/// el dato lo muestra la interfaz, así que tiene que saber cuáles son.
/// </param>
/// <param name="Categoria">Categoría estimada por la generación.</param>
/// <param name="LlamadasAlModelo">Cuántas llamadas al modelo consumió el turno.</param>
/// <param name="Hilo">
/// El hilo al que pertenece el turno. El cliente lo devuelve en el turno siguiente
/// para que el seguimiento funcione.
/// </param>
/// <param name="Opciones">
/// Las opciones de la aclaración, cuando el turno terminó en
/// <see cref="EstadoDelTurno.NecesitaAclaracion"/>.
/// </param>
/// <remarks>
/// <c>Opciones</c> es un campo <b>distinto</b> de las sugerencias de un rechazo
/// cooperativo, que llegan con la superficie de usuario. Colapsarlos haría que la
/// interfaz no pueda distinguir «elegí una de estas, y sigo» de «probá con alguna
/// de estas otras preguntas».
/// </remarks>
public sealed record ResultadoDelTurno(
    EstadoDelTurno Estado,
    string Respuesta,
    string Razonamiento,
    string? PreguntaInterpretada,
    IReadOnlyList<string> Columnas,
    IReadOnlyList<IReadOnlyList<object?>> Filas,
    bool Truncado,
    IReadOnlyList<SensibilidadDeColumna> Sensibilidad,
    string Categoria,
    int LlamadasAlModelo,
    Guid Hilo = default,
    IReadOnlyList<OpcionDeAclaracion>? Opciones = null);
