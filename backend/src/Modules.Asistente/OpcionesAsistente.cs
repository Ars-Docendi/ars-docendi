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
    /// Credencial del proveedor real. La inyecta el ambiente en runtime.
    /// </summary>
    /// <remarks>
    /// Nunca se escribe al repositorio, no entra en ningún <c>appsettings.json</c> y
    /// no aparece en ningún registro: el registro operativo guarda el nombre del
    /// proveedor, no su clave.
    ///
    /// Vacía por default y eso está bien: el proveedor simulado no la necesita, y
    /// exigirla siempre rompería el arranque en todo ambiente que todavía corra con
    /// el simulado —que hoy son todos—.
    /// </remarks>
    public string ClaveDelProveedor { get; set; } = string.Empty;

    /// <summary>
    /// Modelo a usar con el proveedor real.
    /// </summary>
    /// <remarks>
    /// Es configuración y no una constante a propósito: comparar costo contra
    /// calidad entre modelos es justamente lo que los cuatro ejes de evaluación
    /// miden, y cambiar de modelo para medirlo tiene que ser una variable de
    /// ambiente y no un recompilado.
    ///
    /// <b>Por qué Sonnet y no Opus.</b> El esquema que el modelo tiene que manejar
    /// es chico —catorce tablas, poco más de cien columnas—, que es el factor que
    /// más pesa en traducir preguntas a SQL. Opus cuesta dos veces y media más por
    /// token para un problema de ese tamaño.
    ///
    /// <b>Por qué no uno más chico todavía.</b> Haiku 4.5 no acepta el parámetro de
    /// esfuerzo —cada llamada volvería 400— y su retiro está anunciado para no antes
    /// de octubre de 2026. Para un sistema que queda en producción, elegirlo sería
    /// elegir una migración forzada. Y ahorra poco: lo que separa a Sonnet de Haiku
    /// es una fracción de lo que separaba a Opus de Sonnet.
    ///
    /// El riesgo que este default asume está del lado de la métrica: responder mal
    /// RESTA y abstenerse no, y los modelos más chicos fallan con confianza en vez
    /// de callarse. Es exactamente lo que el eje de capacidad mide, así que la
    /// elección se confirma o se corrige con una corrida, no con una opinión.
    /// </remarks>
    public string Modelo { get; set; } = "claude-sonnet-5";

    /// <summary>
    /// Deliberación pedida al generar la consulta.
    /// </summary>
    /// <remarks>
    /// <b>Son tres y no uno, y el motivo es de latencia.</b> Con un valor global,
    /// la redacción razonaba antes de escribir la primera palabra: para quien
    /// preguntó eso es espera pura, porque las filas ya estaban.
    ///
    /// La generación sí lo aprovecha: elegir el join correcto entre catorce tablas
    /// es exactamente el trabajo que mejora deliberando. Es la llamada donde
    /// equivocarse cuesta una respuesta falsa, que es lo que la métrica castiga.
    ///
    /// Valores: minimo, bajo, medio, alto, maximo.
    /// </remarks>
    public string EsfuerzoDeGeneracion { get; set; } = "medio";

    /// <summary>
    /// Modelo que redacta la respuesta en español. Vacío usa el mismo que genera.
    /// </summary>
    /// <remarks>
    /// <b>Redactar y generar son trabajos distintos.</b> Generar elige el join
    /// correcto entre catorce tablas y decide qué se pregunta; redactar toma filas
    /// ya obtenidas, ya enmascaradas y ya validadas, y las escribe en una oración.
    /// El segundo no necesita el mismo modelo que el primero, y usarlo se paga en
    /// segundos que el usuario mira una pantalla quieta.
    ///
    /// Es una cadena y no un booleano «usar modelo rápido» porque cuál es el modelo
    /// rápido depende del proveedor y del año. El pipeline no sabe qué modelo es:
    /// esto lo decide la composición, no el puerto.
    ///
    /// Si el modelo elegido no delibera —Haiku 4.5 rechaza `output_config` con
    /// 400—, hay que poner <see cref="EsfuerzoDeRedaccion"/> en `minimo`, que es el
    /// valor que omite el campo entero.
    /// </remarks>
    public string ModeloDeRedaccion { get; set; } = string.Empty;

    /// <summary>
    /// Deliberación pedida al redactar la respuesta en español.
    /// </summary>
    /// <remarks>
    /// Baja a propósito. La consulta ya se ejecutó y las filas ya están: esto es
    /// convertir un resultado en una oración, no decidir nada. Cada nivel de más
    /// acá es tiempo que el usuario mira una pantalla quieta.
    /// </remarks>
    public string EsfuerzoDeRedaccion { get; set; } = "bajo";

    /// <summary>
    /// Deliberación pedida al reescribir una pregunta de seguimiento.
    /// </summary>
    /// <remarks>
    /// Baja por el mismo motivo: resolver «¿y el de Pérez?» contra el turno
    /// anterior es una sustitución, no un problema. Y está en el camino crítico de
    /// todo seguimiento, así que lo que tarde acá lo espera el usuario antes de que
    /// el pipeline siquiera empiece.
    /// </remarks>
    public string EsfuerzoDeReescritura { get; set; } = "bajo";

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
    /// <remarks>
    /// Subió de 30 s con la llegada de los modelos que razonan: el razonamiento
    /// ocurre ANTES del primer token de la respuesta, así que una generación que
    /// antes tardaba segundos ahora puede tardar decenas. Treinta segundos
    /// alcanzaban para dos llamadas sin razonamiento y no alcanzan para dos con él.
    ///
    /// Es un techo, no una espera: un turno que resuelve rápido no paga nada por
    /// que el techo sea alto. Lo que sí cuesta es tenerlo bajo, porque el turno se
    /// corta a la mitad y el usuario recibe «no llegué a tiempo» por un límite
    /// nuestro y no por un problema del proveedor.
    /// </remarks>
    public int PresupuestoDelTurnoSegundos { get; set; } = 150;

    /// <summary>
    /// Cuánto espera como mucho una llamada al proveedor, en segundos.
    /// </summary>
    /// <remarks>
    /// Hoy hay timeout de sentencia y de comando en la ejecución de SQL, y hasta
    /// esta configuración no había ninguno en las llamadas al modelo: el peor caso
    /// de un turno no tenía cota superior.
    /// </remarks>
    /// <remarks>
    /// Subió de 20 s por el mismo motivo que el presupuesto del turno: con
    /// <see cref="Esfuerzo"/> configurado, el modelo piensa antes de escribir y esa
    /// pausa entra dentro de esta cota. Con 20 s el corte llegaba antes que la
    /// respuesta, y el turno degradaba como si el proveedor estuviera caído.
    /// </remarks>
    public int TimeoutDeLlamadaSegundos { get; set; } = 60;

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
    /// Cuánto vale una clave de idempotencia, en minutos.
    /// </summary>
    /// <remarks>
    /// Corta a propósito. Lo que este mecanismo tiene que resolver es el doble clic
    /// y el reintento del cliente ante un timeout de red, no la reproducibilidad de
    /// una respuesta a lo largo del día: una ventana larga en memoria acumula
    /// respuestas de turnos que nadie va a volver a pedir.
    /// </remarks>
    public int VigenciaDeIdempotenciaMinutos { get; set; } = 5;

    /// <summary>
    /// Cuántas veces se reofrece un menú de aclaración antes de abandonarlo.
    /// </summary>
    /// <remarks>
    /// Sin tope, una respuesta que nunca se reconoce deja la aclaración pendiente
    /// para siempre y el hilo deja de aceptar preguntas nuevas.
    /// </remarks>
    public int MaximoDeIntentosDeAclaracion { get; set; } = 2;
    /// <summary>
    /// Techo de tokens de la llamada que genera la consulta.
    /// </summary>
    /// <remarks>
    /// <b>El razonamiento sale de este mismo presupuesto.</b> Con <see cref="Esfuerzo"/>
    /// configurado, el modelo piensa antes de escribir y esos tokens cuentan contra
    /// este techo, así que el número que alcanzaba para escribir la consulta puede
    /// no alcanzar para pensarla y escribirla.
    ///
    /// El valor arrancó en 1200, dimensionado sobre la salida sola. Se subió con
    /// margen porque el modo de fallar del techo corto es el peor posible: la
    /// respuesta llega cortada, el JSON queda incompleto y el turno resuelve «no
    /// pude interpretar la pregunta» —el mismo texto que devuelve una pregunta
    /// genuinamente incontestable—. Un presupuesto chico se ve igual que un
    /// asistente prudente.
    ///
    /// Es configurable y no una constante porque el número correcto depende del
    /// esfuerzo y del modelo, y esos se eligen por ambiente. Si en los logs aparece
    /// el aviso de corte por presupuesto, este es el valor a subir.
    ///
    /// El costo de pasarse es acotado: se paga por token generado, no por techo
    /// pedido, así que un techo holgado solo cuesta cuando de verdad se usa.
    /// </remarks>
    public int MaximoDeTokensDeGeneracion { get; set; } = 4000;

    /// <summary>
    /// Techo de tokens de la llamada que redacta la respuesta en español.
    /// </summary>
    /// <remarks>
    /// Más chico que el de generación porque la salida es prosa corta y no hay
    /// formato que se pueda romper a la mitad: una redacción cortada se lee peor,
    /// pero se lee. Aun así el razonamiento también sale de acá.
    /// </remarks>
    public int MaximoDeTokensDeRedaccion { get; set; } = 2000;

    /// <summary>
    /// Techo de tokens de la llamada que reescribe una pregunta de seguimiento.
    /// </summary>
    /// <remarks>
    /// La salida es una sola pregunta reescrita, así que el techo es chico. Se
    /// subió de 200 por el mismo motivo que los otros dos: con razonamiento, 200
    /// tokens pueden agotarse antes de escribir la primera palabra.
    /// </remarks>
    public int MaximoDeTokensDeReescritura { get; set; } = 1000;

    /// <summary>
    /// Directorio donde viven los cassettes del proveedor. Vacío apaga el mecanismo.
    /// </summary>
    /// <remarks>
    /// <b>Vacío es el default y con eso el handler ni siquiera se registra</b>: el
    /// pipeline del cliente HTTP del proveedor queda exactamente como estaba, así
    /// que producción no paga nada y no hay nada que se pueda misconfigurar. La
    /// única forma de encender el mecanismo es escribir una ruta.
    ///
    /// Con una ruta puesta, cada llamada al proveedor se busca en disco por la
    /// huella de la solicitud. Si el cassette está, se sirve; si no está y
    /// <see cref="RegrabarCassettes"/> tampoco, la llamada <b>falla</b> sin salir a
    /// la red (RNF-15, RNF-16).
    ///
    /// Un cassette prueba el <b>parseo</b>, no la calidad de la traducción: congela
    /// una respuesta, no la competencia del modelo. Eso lo mide el evaluador.
    /// </remarks>
    public string DirectorioDeCassettes { get; set; } = string.Empty;

    /// <summary>
    /// Cualquier valor no vacío permite salir a la red a grabar lo que falte.
    /// </summary>
    /// <remarks>
    /// <b>Es la única perilla del módulo que puede gastar plata</b>, y está vacía
    /// por default. Es una cadena y no un booleano porque lo que importa es que
    /// <b>esté puesta</b>, igual que la variable de re-grabación de cualquier suite
    /// de fixtures: se exporta para una corrida y se saca.
    ///
    /// Con el cassette ya presente NO se re-graba aunque esté puesta. Re-grabar es
    /// una operación deliberada sobre las claves que faltan, no un modo en que cada
    /// corrida vuelva a pagar por respuestas que ya están en disco.
    /// </remarks>
    public string RegrabarCassettes { get; set; } = string.Empty;
}

/// <summary>
/// Huella del fixture sintético contra el que se graban los cassettes.
/// </summary>
/// <remarks>
/// La registra quien sabe cuál es —el evaluador, que ya la recalcula en cada
/// corrida para sellar sus reportes—, y no el módulo: el módulo no referencia al
/// núcleo de evaluación ni debe hacerlo.
///
/// <b>Si no está registrada, no se graba ni se sirve ningún cassette.</b> Es lo
/// que hace mecánica la garantía de que ningún cassette lleva filas reales: sin
/// con qué comparar, un cassette es indistinguible de uno grabado contra una base
/// de desarrollo con datos importados.
/// </remarks>
/// <param name="Valor">La huella, en hexadecimal.</param>
public sealed record HuellaDelFixture(string Valor);
