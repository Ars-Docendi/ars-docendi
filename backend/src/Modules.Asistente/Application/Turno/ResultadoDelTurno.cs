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
/// <see cref="EstadoDelTurno.NecesitaAclaracion"/>. BLOQUEAN el turno: el hilo
/// espera una elección para poder seguir. No hay un campo equivalente para
/// después de un rechazo o una respuesta: el asistente ya no sugiere próximos
/// pasos fuera de la bienvenida (design.md D12 de asistente-rediseno-v3,
/// ARS-149) — el catálogo de capacidades sigue siendo la única fuente de
/// ejemplos clicables, y vive en <c>GET /capacidades</c>, no en el turno.
/// </param>
/// <param name="Sql">
/// La consulta que se ejecutó. Presente <b>solo</b> si el actor tiene el permiso
/// de verla; nula en cualquier otro caso.
/// </param>
/// <remarks>
/// No es transparencia gratuita: un <c>WHERE</c> puede llevar un documento. Por eso
/// va detrás de un permiso propio, que no se concede a ningún rol por omisión.
/// </remarks>
/// <param name="SqlEjecutado">
/// La consulta que respondió, para que el hilo la arrastre al turno siguiente.
/// Nula si el turno no devolvió filas.
/// </param>
/// <remarks>
/// <b>ES OTRO CAMPO QUE <c>Sql</c>, Y NO UNA DUPLICACIÓN.</b> Los dos llevan el
/// mismo texto cuando el actor tiene el permiso, pero responden a preguntas
/// distintas: <c>Sql</c> es «¿esto se le puede MOSTRAR?» y depende de
/// <c>asistente.ver_consulta</c>; éste es «¿esto sirve para continuar la
/// conversación?» y no depende de ningún permiso, porque nunca sale del servidor.
///
/// Reusar <c>Sql</c> para las dos cosas ataría el seguimiento a un permiso que no
/// tiene nada que ver: un actor sin él perdería el arrastre sin que nada lo
/// explique. <b>Este campo no se mapea al DTO de la API</b>, y hay un test que lo
/// verifica.
/// </remarks>
/// <param name="Vinculos">
/// Las celdas del resultado que identifican algo que el actor puede abrir, con qué
/// clase de cosa es y con qué identificador. Vacío o nulo cuando no hay ninguna.
/// </param>
/// <param name="ClaveDeRetroalimentacion">
/// The feedback token: the exact id of this turn's analytic row
/// (asistente.registro_analitico.id), handed to the client once so it can later
/// submit feedback for this specific turn. Populated only when
/// <see cref="Estado"/> is <see cref="EstadoDelTurno.Respondida"/>, and null for
/// every other state — see asistente-retroalimentacion's spec for why: rating a
/// turn that never produced an answer has nothing to rate. It is never derived
/// from, and never travels next to, any actor identifier: see design.md D1/D2 of
/// the asistente-feedback-export-seguimiento change and TD-012.
/// </param>
/// <remarks>
/// <b>Los resuelve una autoridad ajena al carril</b>, y por eso llegan acá y no se
/// calculan adentro: quién puede abrir un trámite lo decide el módulo dueño, no la
/// policy que dejó pasar la fila. Ver <see cref="IResolutorDeVinculos"/>.
/// </remarks>
/// <param name="CupoRestante">
/// El cupo diario del actor INMEDIATAMENTE DESPUÉS de que este turno se
/// cobrara (asistente-cupo-visible, tarea 7.2) — nunca el valor de antes.
/// <see cref="CapaConversacional.ResponderAsync"/> lo adjunta afuera de su
/// <c>try/finally</c>, una vez que el cobro ya ocurrió.
/// </param>
/// <param name="Conversacion">
/// <see cref="HiloConversacional.HiloHistorico"/> DESPUÉS de que
/// <see cref="CapaConversacional.RegistrarAsync"/> intentó escribir el
/// historial de este turno — nunca el valor de antes. Nulo si esa escritura
/// falló o el turno no se registró (design.md D13 de
/// asistente-rediseno-v3): el cliente lo usa para resaltar y titular la fila
/// del rail, y nunca viaja junto a <see cref="ClaveDeRetroalimentacion"/> ni
/// al registro analítico — ninguno de los dos lo necesita.
/// </param>
/// <param name="ReferenciasEjecutadas">
/// Los marcadores <c>$refN</c> —con su tipo e id— que <see cref="SqlEjecutado"/>
/// usa, heredados del segmento más los nuevos de este turno (design.md D11 de
/// asistente-rediseno-v3). <b>Nunca se manda al cliente</b> —no está en
/// <c>RespuestaDelAsistente</c>, mismo criterio que <c>SqlEjecutado</c>—: sólo
/// sirve para que <c>CapaConversacional</c> lo guarde en
/// <see cref="TurnoDelHilo.Referencias"/> y en <c>asistente.turno_historico.referencias</c>,
/// para que un seguimiento, «Volver a consultar» o «Reanudar» puedan volver a
/// ligarlos. Nulo si <see cref="SqlEjecutado"/> también lo es, o si el turno no
/// usó ninguna mención.
/// </param>
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
    string? Sql = null,
    string? SqlEjecutado = null,
    IReadOnlyList<VinculoDelResultado>? Vinculos = null,
    Guid? ClaveDeRetroalimentacion = null,
    int? CupoRestante = null,
    Guid? Conversacion = null,
    IReadOnlyDictionary<string, (TipoDeMencion Tipo, Guid Id)>? ReferenciasEjecutadas = null);
