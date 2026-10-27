namespace Modules.Asistente;

/// <summary>Configuración del módulo del asistente, por ambiente.</summary>
public sealed class OpcionesAsistente
{
    /// <summary>Sección de configuración: <c>Asistente</c>.</summary>
    public const string Seccion = "Asistente";

    /// <summary>
    /// Rol de PostgreSQL con el que el asistente lee sin datos personales.
    /// Lleva sufijo de ambiente (<c>asistente_ro_prod</c>, <c>asistente_ro_pr_123</c>);
    /// lo crea <c>infra/scripts/provision-db.sh</c>.
    /// </summary>
    public string RolSoloLectura { get; set; } = string.Empty;

    /// <summary>
    /// Rol de PostgreSQL con el que el asistente lee incluyendo datos personales.
    /// </summary>
    public string RolSoloLecturaPii { get; set; } = string.Empty;

    /// <summary>
    /// Contraseña del rol de solo lectura. La inyecta el ambiente en runtime; nunca
    /// se loguea ni se escribe al repositorio.
    /// </summary>
    public string PasswordSoloLectura { get; set; } = string.Empty;

    /// <summary>Contraseña del rol de solo lectura con datos personales.</summary>
    public string PasswordSoloLecturaPii { get; set; } = string.Empty;

    /// <summary>
    /// Proveedor del modelo de lenguaje. El default es el simulado: usar uno real exige
    /// configuración explícita.
    /// </summary>
    public string Proveedor { get; set; } = "simulado";

    /// <summary>Credencial del proveedor real. La inyecta el ambiente en runtime.</summary>
    public string ClaveDelProveedor { get; set; } = string.Empty;

    /// <summary>Modelo a usar con el proveedor real.</summary>
    public string Modelo { get; set; } = "claude-sonnet-5";

    /// <summary>Deliberación pedida al generar la consulta.</summary>
    public string EsfuerzoDeGeneracion { get; set; } = "medio";

    /// <summary>
    /// Modelo que redacta la respuesta en español. Vacío usa el mismo que genera.
    /// </summary>
    public string ModeloDeRedaccion { get; set; } = string.Empty;

    /// <summary>Deliberación pedida al redactar la respuesta en español.</summary>
    public string EsfuerzoDeRedaccion { get; set; } = "bajo";

    /// <summary>Deliberación pedida al reescribir una pregunta de seguimiento.</summary>
    public string EsfuerzoDeReescritura { get; set; } = "bajo";

    /// <summary>
    /// Techo de llamadas al modelo por turno (RNF-10). Cuatro sin reintentos: reescritor,
    /// generación, reintento y redacción.
    /// </summary>
    public int MaximoDeLlamadasPorTurno { get; set; } = 4;

    /// <summary>Intentos de transporte por llamada, el primero incluido (RNF-11).</summary>
    public int MaximoDeIntentosDeTransporte { get; set; } = 3;

    /// <summary>Espera base del backoff exponencial, en milisegundos.</summary>
    public int EsperaBaseMs { get; set; } = 500;

    /// <summary>Tope de una espera individual, en milisegundos.</summary>
    public int EsperaMaximaMs { get; set; } = 8000;

    /// <summary>Cuántas filas como mucho devuelve una consulta del carril SQL.</summary>
    public int TopeDeFilas { get; set; } = 200;

    /// <summary>
    /// Timeout de sentencia de la base, en milisegundos, fijado dentro de la transacción del
    /// carril.
    /// </summary>
    public int TimeoutDeSentenciaMs { get; set; } = 8000;

    /// <summary>Timeout del comando del lado del cliente, en segundos.</summary>
    public int TimeoutDeComandoSegundos { get; set; } = 15;

    /// <summary>Cuánto vive un hilo conversacional sin actividad, en minutos.</summary>
    public int VigenciaDelHiloMinutos { get; set; } = 120;

    /// <summary>Cuántos turnos del segmento vigente se le muestran al reescritor.</summary>
    public int TopeDeTurnosDelHistorial { get; set; } = 4;

    /// <summary>Cuánto dura como mucho un turno completo, en segundos (RNF-09).</summary>
    public int PresupuestoDelTurnoSegundos { get; set; } = 150;

    /// <summary>Cuánto espera como mucho una llamada al proveedor, en segundos.</summary>
    public int TimeoutDeLlamadaSegundos { get; set; } = 60;

    /// <summary>Fallos seguidos del proveedor que abren el corte.</summary>
    public int FallosParaAbrirElBreaker { get; set; } = 5;

    /// <summary>Cuánto se espera antes de volver a probar el proveedor, en segundos.</summary>
    public int EsperaDelBreakerSegundos { get; set; } = 30;

    /// <summary>
    /// Cuántas llamadas al modelo pueden estar en curso a la vez, en todo el proceso.
    /// </summary>
    public int MaximoDeLlamadasConcurrentes { get; set; }

    /// <summary>
    /// Cuánto espera como mucho una llamada por un lugar en la compuerta, en segundos.
    /// </summary>
    public int EsperaMaximaEnColaSegundos { get; set; } = 30;

    /// <summary>Cuánto se conservan los registros del asistente, en días (RNF-19).</summary>
    public int RetencionDeRegistrosDias { get; set; } = 90;

    /// <summary>
    /// Cuánto se conserva el historial propio de conversaciones (preguntas, SQL resuelta, estado y
    /// momentos), en días, contado desde la última actividad de la conversación y no desde su
    /// creación.
    /// </summary>
    public int RetencionDeHistorialDias { get; set; } = 180;

    /// <summary>
    /// Cuánto se conserva la auditoría de accesos de soporte al historial ajeno, en días.
    /// </summary>
    public int RetencionDeAuditoriaDeSoporteDias { get; set; } = 365;

    /// <summary>
    /// Cuánto se conserva la auditoría de administración (ediciones de presupuesto y toggles de
    /// mantenimiento), en días.
    /// </summary>
    public int RetencionDeAuditoriaDeAdministracionDias { get; set; } = 365;

    /// <summary>
    /// How long the audit trail of reads of the quality problem list is kept, in days
    /// (asistente-metricas-de-calidad, D5).
    /// </summary>
    public int RetencionDeAuditoriaDeCalidadDias { get; set; } = 365;

    /// <summary>Cada cuánto corre la purga, en horas.</summary>
    public int PeriodoDePurgaHoras { get; set; } = 24;

    /// <summary>
    /// Ventana, en segundos, en que un borrado sigue pendiente y se puede
    /// deshacer (asistente-rediseno-v3, design.md D4 de
    /// asistente-historial-conversaciones).
    /// </summary>
    /// <remarks>
    /// Son los 10 s que «Deshacer» se muestra en la interfaz MÁS 5 s de
    /// margen: el timer del aviso arranca cuando llega la respuesta del
    /// `DELETE`, así que un clic a los 9,9 s con una red lenta todavía tiene
    /// que llegar a tiempo. Cada consulta propia filtra
    /// <c>borrado_pendiente_desde IS NULL</c>; cada lectura de soporte
    /// filtra <c>IS NULL OR &gt; ahora - esta ventana</c> — es la misma
    /// ventana la que hace final el borrado en TODOS lados, sin depender de
    /// que el cliente siga abierto.
    /// </remarks>
    public int VentanaDeDeshacerSegundos { get; set; } = 15;

    /// <summary>
    /// Cada cuánto corre el barrido que purga físicamente lo que superó <see
    /// cref="VentanaDeDeshacerSegundos"/>, en segundos.
    /// </summary>
    public int PeriodoDeBarridoDeBorradosSegundos { get; set; } = 60;

    /// <summary>Cuánto vale una clave de idempotencia, en minutos.</summary>
    public int VigenciaDeIdempotenciaMinutos { get; set; } = 5;

    /// <summary>How long a feedback token stays valid after it is minted, in minutes.</summary>
    public int VigenciaDeRetroalimentacionMinutos { get; set; } = 120;

    /// <summary>Cuántas veces se reofrece un menú de aclaración antes de abandonarlo.</summary>
    public int MaximoDeIntentosDeAclaracion { get; set; } = 2;
    /// <summary>Techo de tokens de la llamada que genera la consulta.</summary>
    public int MaximoDeTokensDeGeneracion { get; set; } = 4000;

    /// <summary>Techo de tokens de la llamada que redacta la respuesta en español.</summary>
    public int MaximoDeTokensDeRedaccion { get; set; } = 2000;

    /// <summary>Techo de tokens de la llamada que reescribe una pregunta de seguimiento.</summary>
    public int MaximoDeTokensDeReescritura { get; set; } = 1000;

    /// <summary>
    /// Directorio donde viven los cassettes del proveedor. Vacío apaga el mecanismo.
    /// </summary>
    public string DirectorioDeCassettes { get; set; } = string.Empty;

    /// <summary>
    /// URL base del servidor OpenAI-compatible del modelo propio, terminada en <c>/v1</c> (por
    /// ejemplo <c>http://llm:8000/v1</c>).
    /// </summary>
    public string UrlDelProveedorLocal { get; set; } = string.Empty;

    /// <summary>
    /// Contexto que el proveedor <c>ollama</c> le pide al servidor en cada pedido
    /// (<c>options.num_ctx</c>).
    /// </summary>
    public int ContextoDelModelo { get; set; }

    /// <summary>
    /// Si una consulta que volvió vacía se vuelve a generar una vez. Default <c>true</c>
    /// (asistente-proveedor-local, D6).
    /// </summary>
    public bool ReintentarConsultaVacia { get; set; } = true;

    // ------------------------------------------------------------------
    // asistente-optimizaciones-modelo-local.

    /// <summary>
    /// Renderiza el esquema del prefijo en su forma compacta (D2): tipos abreviados, nulabilidad
    /// como <c>?</c> y claves foráneas en línea.
    /// </summary>
    public bool EsquemaCompacto { get; set; }

    /// <summary>
    /// Pone TODOS los ejemplos verificados al final del prefijo cacheable, en vez
    /// de elegir hasta cuatro por pregunta en el mensaje (D3). Con caché de
    /// prefijo su costo marginal es casi nulo; sin caché son ~2,5k tokens más
    /// por llamada.
    /// </summary>
    public bool EjemplosEnElPrefijo { get; set; }

    /// <summary>
    /// Pone el glosario institucional —el vocabulario del Departamento y el valor exacto que le
    /// corresponde en el esquema— en el prefijo cacheable, después del esquema y antes de los
    /// ejemplos, y suma al bloque «VALORES POSIBLES» el catálogo de dedicaciones
    /// (asistente-glosario-institucional, D1 y D2).
    /// </summary>
    public bool GlosarioEnElPrefijo { get; set; }

    /// <summary>
    /// El reintento por consulta vacía le dice al modelo qué consulta no trajo filas, en vez de
    /// repetir el prompt idéntico (D4).
    /// </summary>
    public bool ReintentoConContexto { get; set; }

    /// <summary>
    /// Si PostgreSQL rechaza la consulta generada —salvo por privilegio o por
    /// timeout—, una sola ronda de corrección con el error saneado (D4).
    /// </summary>
    public bool RepararConsultaFallida { get; set; }

    /// <summary>
    /// La segunda generación de un turno —el reintento tras una consulta vacía, con o sin contexto,
    /// y la reparación tras un rechazo del motor— se pide con esfuerzo alto, o sea con razonamiento
    /// donde el servidor lo permita.
    /// </summary>
    public bool RazonamientoEnSegundaGeneracion { get; set; }

    /// <summary>
    /// Techo de tokens de la segunda generación cuando <see
    /// cref="RazonamientoEnSegundaGeneracion"/> está prendida: razonar gasta tokens de salida antes
    /// de escribir la consulta.
    /// </summary>
    public int MaximoDeTokensDeSegundaGeneracion { get; set; }

    /// <summary>
    /// Redacta sin modelo un resultado trivial —una columna, de una a cinco
    /// filas, alcance completo, sin recorte ni cobertura autodeclarada— (D5).
    /// </summary>
    public bool RedaccionConPlantillas { get; set; }

    /// <summary>
    /// Lets the redaction prompt carry the real values of <c>sensible-valor</c> columns instead of
    /// markers such as «documento 1» (asistente-redaccion-sin-enmascarado-local).
    /// </summary>
    public bool RedaccionSinEnmascarar { get; set; }

    /// <summary>
    /// Whether redaction runs without masking <c>sensible-valor</c>: the option on, a provider on
    /// own hardware, and no cassette directory.
    /// </summary>
    public bool RedaccionSinEnmascararVigente => RedaccionSinEnmascararCon(ProveedorEnHardwarePropio);

    /// <summary>
    /// The same decision for an adapter that is not necessarily the one the environment names: a
    /// runtime selection resolved for the turn (asistente-modelo-en-caliente, D7).
    /// </summary>
    internal bool RedaccionSinEnmascararCon(bool adaptadorEnHardwarePropio) =>
        RedaccionSinEnmascarar
        && adaptadorEnHardwarePropio
        && string.IsNullOrWhiteSpace(DirectorioDeCassettes);

    /// <summary>Whether <see cref="Proveedor"/> runs on the Department's hardware.</summary>
    internal bool ProveedorEnHardwarePropio =>
        string.Equals(Proveedor, Infrastructure.ProveedorLocal.Clave, StringComparison.Ordinal)
        || string.Equals(Proveedor, Infrastructure.ProveedorOllama.Clave, StringComparison.Ordinal);

    /// <summary>
    /// Cuántos minutos se reutiliza la consulta generada para la misma pregunta sin contexto, la
    /// misma variante de rol y la misma fecha (D6).
    /// </summary>
    public int VigenciaDeCacheDeConsultasMinutos { get; set; }

    /// <summary>
    /// En un seguimiento, no llama al reescritor: la generación recibe las preguntas anteriores y
    /// devuelve la pregunta resuelta junto con la consulta (D7).
    /// </summary>
    public bool ReescrituraEnLaGeneracion { get; set; }

    /// <summary>
    /// Ofrece la redacción por fragmentos en <c>POST /api/asistente/consultas/flujo</c> y lo
    /// anuncia en <c>GET /capacidades</c> (D9).
    /// </summary>
    public bool StreamingDeRedaccion { get; set; }

    /// <summary>
    /// Responde las preguntas sobre docentes con designación vigente con un plan tipado que el
    /// código compila a SQL, en vez de dejar que el modelo escriba la consulta (change
    /// <c>asistente-plan-compilado</c>, prototipo).
    /// </summary>
    public bool PlanCompilado { get; set; }

    /// <summary>
    /// Cuántas muestras del plan tienen que coincidir para ejecutarlo (D5 de
    /// <c>asistente-plan-compilado</c>).
    /// </summary>
    public int MuestrasDelPlan { get; set; } = 3;

    /// <summary>
    /// Temperatura de las muestras del plan que siguen a la primera. Un valor demasiado bajo hace
    /// que el acuerdo no pruebe nada; uno demasiado alto, que casi nunca haya acuerdo.
    /// </summary>
    public decimal TemperaturaDeMuestrasDelPlan { get; set; } = 0.6m;

    /// <summary>
    /// Clave maestra (32 bytes en base64) con que se cifran las claves de API por empresa guardadas
    /// en <c>asistente.credencial_de_proveedor</c> (asistente-modelo-en-caliente, D4).
    /// </summary>
    public string ClaveMaestra { get; set; } = string.Empty;

    /// <summary>
    /// Clave maestra anterior, sólo durante una rotación: lo cifrado con ella se descifra y se
    /// recifra con <see cref="ClaveMaestra"/> al leerlo.
    /// </summary>
    public string ClaveMaestraAnterior { get; set; } = string.Empty;

    /// <summary>Cualquier valor no vacío permite salir a la red a grabar lo que falte.</summary>
    public string RegrabarCassettes { get; set; } = string.Empty;
}
