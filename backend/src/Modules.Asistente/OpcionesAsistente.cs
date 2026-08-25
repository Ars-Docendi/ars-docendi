namespace Modules.Asistente;

/// <summary>
/// Configuración del módulo del asistente, por ambiente.
/// </summary>
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
    /// Proveedor del modelo de lenguaje. El default es el simulado: usar uno real
    /// exige configuración explícita.
    /// </summary>
    /// <remarks>
    /// El default vale sobre todo para los ambientes efímeros de PR, que no pueden
    /// tener clave real — su workflow ejecuta un script que viene del propio pull
    /// request en un job con los secrets del environment.
    /// </remarks>
    public string Proveedor { get; set; } = "simulado";

    /// <summary>
    /// Techo de llamadas al modelo por turno (RNF-10). Cuatro sin reintentos:
    /// reescritor, generación, reintento y redacción.
    /// </summary>
    /// <remarks>
    /// Es global del turno, no por capa. Repartido por capa, cada una respeta su
    /// límite y el total se multiplica igual.
    /// </remarks>
    public int MaximoDeLlamadasPorTurno { get; set; } = 4;

    /// <summary>
    /// Intentos de transporte por llamada, el primero incluido (RNF-11).
    /// </summary>
    /// <remarks>
    /// Se multiplica con el techo de arriba: el peor caso de un turno es
    /// <c>MaximoDeLlamadasPorTurno × MaximoDeIntentosDeTransporte</c> requests HTTP.
    /// Con los defaults, 4 × 3 = 12. Las dos cotas explícitas son lo que hace que
    /// ese número se pueda decir en voz alta.
    /// </remarks>
    public int MaximoDeIntentosDeTransporte { get; set; } = 3;

    /// <summary>Espera base del backoff exponencial, en milisegundos.</summary>
    public int EsperaBaseMs { get; set; } = 500;

    /// <summary>Tope de una espera individual, en milisegundos.</summary>
    public int EsperaMaximaMs { get; set; } = 8000;

    /// <summary>
    /// Cuántas filas como mucho devuelve una consulta del carril SQL.
    /// </summary>
    /// <remarks>
    /// El ejecutor pide una fila MÁS que este tope, a propósito. Con un límite
    /// exacto, «devolvió N» y «devolvió más de N y se recortó» son
    /// indistinguibles desde el resultado, y la redacción termina afirmando un
    /// total sobre un recorte. La fila sonda se descarta antes de salir.
    /// </remarks>
    public int TopeDeFilas { get; set; } = 200;

    /// <summary>
    /// Timeout de sentencia de la base, en milisegundos, fijado dentro de la
    /// transacción del carril.
    /// </summary>
    /// <remarks>
    /// Es el que corta del lado del servidor: sin él, una consulta generada con
    /// un producto cartesiano ocuparía un backend hasta terminar, mucho después
    /// de que el cliente se haya ido.
    /// </remarks>
    public int TimeoutDeSentenciaMs { get; set; } = 8000;

    /// <summary>Timeout del comando del lado del cliente, en segundos.</summary>
    /// <remarks>
    /// Va por encima del de sentencia para que, cuando los dos apliquen, el que
    /// corte primero sea el del servidor —que además libera el backend—.
    /// </remarks>
    public int TimeoutDeComandoSegundos { get; set; } = 15;

    /// <summary>Cuánto vive un hilo conversacional sin actividad, en minutos.</summary>
    /// <remarks>
    /// El hilo no se persiste: se pierde en cada redespliegue y eso está aceptado.
    /// Persistirlo agregaría tabla, migración y una política de retención de datos
    /// personales indirectos sin mover ninguna métrica del proyecto.
    /// </remarks>
    public int VigenciaDelHiloMinutos { get; set; } = 120;

    /// <summary>
    /// Cuántos turnos del segmento vigente se le muestran al reescritor.
    /// </summary>
    /// <remarks>
    /// Se recorta desde el inicio del SEGMENTO y no desde el turno cero: anclar
    /// para siempre el primer turno arrastra contexto muerto de temas que la
    /// conversación ya soltó.
    /// </remarks>
    public int TopeDeTurnosDelHistorial { get; set; } = 4;

    /// <summary>
    /// Cuánto dura como mucho un turno completo, en segundos (RNF-09).
    /// </summary>
    /// <remarks>
    /// Es la cota punta a punta, no la suma de los timeouts de cada etapa. Cuatro
    /// llamadas de diez segundos son cuarenta segundos de espera y cada una habría
    /// respetado su límite: la cota tiene que estar arriba de todas.
    ///
    /// Cero o menos lo deja sin cota, que es lo que necesitan los tests que miden
    /// otra cosa.
    /// </remarks>
    public int PresupuestoDelTurnoSegundos { get; set; } = 30;

    /// <summary>
    /// Cuánto espera como mucho una llamada al proveedor, en segundos.
    /// </summary>
    /// <remarks>
    /// Hoy hay timeout de sentencia y de comando en la ejecución de SQL, y hasta
    /// esta configuración no había ninguno en las llamadas al modelo: el peor caso
    /// de un turno no tenía cota superior.
    /// </remarks>
    public int TimeoutDeLlamadaSegundos { get; set; } = 20;

    /// <summary>
    /// Cuántas llamadas al modelo puede consumir un actor en una ventana (RF-20).
    /// </summary>
    /// <remarks>
    /// Se mide en llamadas y no en turnos ni en requests: un turno con reescritor
    /// cuesta tres. Con el default, un actor tiene alrededor de quince turnos
    /// completos por ventana.
    ///
    /// Cero desactiva la cuota. Es lo que corresponde en desarrollo y en los
    /// ambientes efímeros, donde el proveedor es el simulado y no cuesta nada.
    /// </remarks>
    public int CupoDeLlamadasPorActor { get; set; } = 60;

    /// <summary>Ventana deslizante de la cuota, en minutos.</summary>
    public int VentanaDeCuotaMinutos { get; set; } = 60;

    /// <summary>
    /// Fallos seguidos del proveedor que abren el corte.
    /// </summary>
    /// <remarks>
    /// Cuenta fallos de transporte y de timeout, nunca rechazos semánticos: un
    /// modelo que devuelve una respuesta que el validador descarta está sano.
    ///
    /// Cero o menos desactiva el breaker.
    /// </remarks>
    public int FallosParaAbrirElBreaker { get; set; } = 5;

    /// <summary>Cuánto se espera antes de volver a probar el proveedor, en segundos.</summary>
    public int EsperaDelBreakerSegundos { get; set; } = 30;

    /// <summary>
    /// Cuánto se conservan los registros del asistente, en días (RNF-19).
    /// </summary>
    /// <remarks>
    /// El marco institucional de protección de datos todavía no está definido, así
    /// que el default es conservador y se ajusta si aparece una política.
    /// </remarks>
    public int RetencionDeRegistrosDias { get; set; } = 90;

    /// <summary>Cada cuánto corre la purga, en horas.</summary>
    public int PeriodoDePurgaHoras { get; set; } = 24;

    /// <summary>
    /// Cuántas veces se reofrece un menú de aclaración antes de abandonarlo.
    /// </summary>
    /// <remarks>
    /// Sin tope, una respuesta que nunca se reconoce deja la aclaración pendiente
    /// para siempre y el hilo deja de aceptar preguntas nuevas.
    /// </remarks>
    public int MaximoDeIntentosDeAclaracion { get; set; } = 2;
}
