namespace Modules.Asistente.Application;

/// <summary>
/// Los cuatro estados en que puede terminar un turno (RF-14), más el que no sale.
/// </summary>
/// <remarks>
/// El contrato tiene cuatro y sólo cuatro. <see cref="Fallo"/> es del registro: un
/// turno que revienta no produce cuerpo HTTP, así que nunca necesita nombre público
/// y el mapeo del contrato revienta si se le pide uno.
/// </remarks>
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

    /// <summary>
    /// El turno terminó en una excepción no prevista. <b>Sólo para el registro</b>:
    /// nunca viaja al cliente, que ve la excepción y no un cuerpo.
    /// </summary>
    Fallo,
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
/// <param name="Sugerencias">
/// Qué otra cosa probar, cuando el turno terminó en un rechazo.
/// </param>
/// <remarks>
/// <c>Opciones</c> y <c>Sugerencias</c> son campos <b>distintos</b>, y colapsarlos
/// borraría el tercer estado del sistema. Las opciones <b>bloquean</b>: el turno
/// espera una elección para poder seguir. Las sugerencias no bloquean nada: el
/// turno ya terminó, y son próximos pasos.
///
/// Con un solo campo, la interfaz tendría que adivinar cuál de las dos cosas le
/// llegó mirando el estado — y el día que un turno respondido quiera sugerir algo,
/// la distinción se pierde del todo.
/// </remarks>
/// <param name="Sql">
/// La consulta que se ejecutó. Presente <b>solo</b> si el actor tiene el permiso
/// de verla; nula en cualquier otro caso.
/// </param>
/// <remarks>
/// No es transparencia gratuita: un <c>WHERE</c> puede llevar un documento. Por eso
/// va detrás de un permiso propio, que no se concede a ningún rol por omisión.
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
    IReadOnlyList<OpcionDeAclaracion>? Opciones = null,
    IReadOnlyList<string>? Sugerencias = null,
    string? Sql = null);
