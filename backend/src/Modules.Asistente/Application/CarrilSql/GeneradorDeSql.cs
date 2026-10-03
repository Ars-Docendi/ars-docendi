using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Modules.Asistente.Application;

/// <summary>Lo que devuelve la primera llamada al modelo.</summary>
/// <param name="EsContestable">Si la pregunta se puede responder con el esquema disponible.</param>
/// <param name="Sql">La consulta generada, o nulo si no es contestable.</param>
/// <param name="Razonamiento">
/// Cómo interpretó el modelo la pregunta. Llega a la respuesta como transparencia
/// media (RF-11); no se descarta.
/// </param>
/// <param name="Categoria">Categoría estimada de dificultad.</param>
/// <param name="Motivo">
/// El motivo declarado por el modelo cuando <paramref name="EsContestable"/> es
/// falso (design.md D2 de asistente-rechazos-dinamicos). <c>null</c> significa
/// «el modelo no decidió nada»: una contradicción (contestable sin consulta),
/// una respuesta ininteligible o una generación cortada por el techo de
/// tokens — nunca una abstención declarada con un motivo fuera del conjunto,
/// que resuelve <see cref="MotivoDeRechazo.NoCubierto"/> y no <c>null</c>. Esa
/// distinción es la que mantiene <c>motivo_rechazo</c> limpia de fallas de
/// formato (design.md D7).
/// </param>
/// <param name="TerminoCandidato">
/// El término que el modelo propone para nombrar lo que no pudo responder, sin
/// validar todavía. Sólo <see cref="TerminoDelRechazo.Validar"/> decide si
/// llega al texto, y sólo como un span de lo que el usuario tipeó — nunca esta
/// cadena tal cual.
/// </param>
public sealed record GeneracionDeSql(
    bool EsContestable,
    string? Sql,
    string Razonamiento,
    string Categoria,
    MotivoDeRechazo? Motivo = null,
    string? TerminoCandidato = null,
    string? PreguntaInterpretada = null)
{
    /// <summary>Generación que declara la pregunta fuera de alcance.</summary>
    public static GeneracionDeSql NoContestable(
        string razonamiento, MotivoDeRechazo? motivo = null, string? terminoCandidato = null) =>
        new(false, null, razonamiento, CategoriaNoContestable, motivo, terminoCandidato);

    /// <summary>
    /// Generación que se cortó por el techo de tokens antes de poder decidir.
    /// </summary>
    /// <remarks>
    /// Para el usuario es una abstención más: mismo estado y mismo texto, porque
    /// contarle que hubo un problema de presupuesto es contarle cómo está hecho el
    /// sistema (RNF-18). La diferencia va en la categoría, que es lo que leen el
    /// registro analítico y el evaluador: una abstención es una decisión del modelo
    /// y esto no lo es, y la métrica primaria —corrección con abstención— no puede
    /// acreditar como acierto un turno que no llegó a decidir nada.
    /// </remarks>
    public static GeneracionDeSql Truncada(string razonamiento) =>
        new(false, null, razonamiento, CategoriaTruncada);

    /// <summary>Categoría con que se marca una pregunta fuera de alcance.</summary>
    public const string CategoriaNoContestable = "no_contestable";

    /// <summary>Categoría con que se marca una generación cortada por el techo de tokens.</summary>
    public const string CategoriaTruncada = "truncado_en_generacion";
}

/// <summary>
/// Primera llamada al modelo: traduce la pregunta a SQL (RF-11, RF-18).
/// </summary>
/// <remarks>
/// Temperatura cero y prefijo cacheado sin modificar. Todo lo que varía por turno
/// —los ejemplos, la fecha, la pregunta— viaja en el prompt de usuario: es la
/// línea que separa lo que se cachea de lo que no.
/// </remarks>
public sealed class GeneradorDeSql(
    IProveedorDeEsquema esquema,
    ISelectorDeEjemplos ejemplos,
    IProveedorDeModelo modelo,
    IFechaDeReferencia fecha,
    IOptions<OpcionesAsistente> opciones,
    ILogger<GeneradorDeSql> log,
    CacheDeConsultasGeneradas? cache = null)
{
    /// <summary>
    /// La forma JSON de la respuesta, declarada para que un proveedor que puede
    /// imponerla la imponga (asistente-proveedor-local, design.md D3).
    /// </summary>
    /// <remarks>
    /// Mismas claves y MISMO ORDEN que pide <c>InstruccionesDeGeneracion</c>: con
    /// decodificación restringida el modelo escribe las propiedades en el orden del
    /// esquema, y uno distinto del de las instrucciones lo haría pelear contra su
    /// propio prompt. <c>motivo</c> y <c>termino</c> son opcionales y aceptan nulo,
    /// igual que el intérprete, que los tolera ausentes o de otro tipo.
    ///
    /// El esquema garantiza la FORMA. Que la consulta sea segura y respete el
    /// alcance lo sigue decidiendo <c>ValidadorDeSql</c>.
    /// </remarks>
    internal const string EsquemaDeSalida = """
        {
          "type": "object",
          "properties": {
            "pregunta_interpretada": { "type": ["string", "null"] },
            "es_contestable": { "type": "boolean" },
            "sql": { "type": ["string", "null"] },
            "razonamiento": { "type": "string" },
            "categoria": {
              "type": "string",
              "enum": ["consulta_simple", "filtro_temporal", "cruce_de_tablas", "agregacion", "no_contestable", "ambigua"]
            },
            "motivo": { "type": ["string", "null"] },
            "termino": { "type": ["string", "null"] }
          },
          "required": ["es_contestable", "sql", "razonamiento", "categoria"],
          "additionalProperties": false
        }
        """;

    /// <summary>
    /// Si una consulta que volvió vacía se vuelve a generar
    /// (<see cref="OpcionesAsistente.ReintentarConsultaVacia"/>, D6).
    /// </summary>
    /// <remarks>
    /// Lo expone el generador porque es el dueño de la llamada que se repetiría y
    /// el que ya recibe las opciones: el carril pregunta, no configura.
    /// </remarks>
    internal bool ReintentaConsultaVacia => opciones.Value.ReintentarConsultaVacia;

    /// <summary>Si el reintento por vacío lleva la consulta anterior (D4).</summary>
    internal bool ReintentaConContexto => opciones.Value.ReintentoConContexto;

    /// <summary>Si un rechazo del motor tiene una ronda de reparación (D4).</summary>
    internal bool ReparaConsultaFallida => opciones.Value.RepararConsultaFallida;

    /// <summary>
    /// Una generación ya hecha para la misma pregunta sin contexto, si la caché
    /// está prendida y la tiene (asistente-optimizaciones-modelo-local, D6).
    /// </summary>
    internal GeneracionDeSql? BuscarEnCache(string pregunta, bool conDatosPersonales) =>
        cache is null || opciones.Value.VigenciaDeCacheDeConsultasMinutos <= 0
            ? null
            : cache.Buscar(pregunta, conDatosPersonales, fecha.Hoy());

    /// <summary>Recuerda una generación que validó y trajo filas (D6).</summary>
    internal void Recordar(string pregunta, bool conDatosPersonales, GeneracionDeSql generacion)
    {
        var vigencia = opciones.Value.VigenciaDeCacheDeConsultasMinutos;
        if (cache is not null && vigencia > 0)
        {
            cache.Guardar(pregunta, conDatosPersonales, fecha.Hoy(), generacion, TimeSpan.FromMinutes(vigencia));
        }
    }


    /// <summary>
    /// Razonamiento con que se resuelve una respuesta que no se pudo interpretar.
    /// </summary>
    /// <remarks>
    /// Está escrito para el usuario final: no menciona esquema, ni tablas, ni el
    /// hecho de que hubo un problema de formato (D15).
    /// </remarks>
    private const string RazonamientoIninteligible =
        "No pude interpretar la pregunta con la información disponible.";

    /// <summary>Genera la consulta para una pregunta.</summary>
    /// <param name="menciones">
    /// Las menciones nuevas de este turno, ya numeradas por
    /// <see cref="MarcadoresDeReferencias.Asignar"/>. Vacío o nulo en un turno
    /// sin menciones —el caso mayoritario— deja el mensaje BYTE A BYTE igual que
    /// antes de que existieran (design.md D11): el bloque «Menciones» sólo se
    /// agrega cuando hay algo que agregar.
    /// </param>
    /// <param name="preguntasAnteriores">
    /// Las preguntas anteriores del segmento vigente, sólo con
    /// <see cref="OpcionesAsistente.ReescrituraEnLaGeneracion"/>
    /// (asistente-optimizaciones-modelo-local, D7): la generación resuelve la
    /// anáfora y devuelve la pregunta interpretada, en vez de una llamada aparte
    /// al reescritor.
    /// </param>
    /// <param name="intentoAnterior">
    /// La consulta anterior de ESTE turno y qué pasó con ella —vacía, o rechazada
    /// por el motor con el error saneado— (D4). Nulo en la primera generación.
    /// </param>
    /// <param name="esSegundaGeneracion">
    /// Verdadero en el reintento tras una consulta vacía y en la reparación tras un
    /// rechazo del motor. No se infiere de <paramref name="intentoAnterior"/>: el
    /// reintento sin contexto no lo trae. Con
    /// <see cref="OpcionesAsistente.RazonamientoEnSegundaGeneracion"/> prendida pide
    /// esfuerzo alto y el techo de <see cref="OpcionesAsistente.MaximoDeTokensDeSegundaGeneracion"/>.
    /// </param>
    public async Task<GeneracionDeSql> GenerarAsync(
        string pregunta,
        bool conDatosPersonales,
        CancellationToken ct,
        IReadOnlyList<string>? consultasAnteriores = null,
        IReadOnlyList<(string Marcador, TipoDeMencion Tipo, ResultadoDeMencion Entidad)>? menciones = null,
        IReadOnlyList<string>? preguntasAnteriores = null,
        (string Sql, string Problema)? intentoAnterior = null,
        bool esSegundaGeneracion = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pregunta);

        var valores = opciones.Value;
        var conRazonamiento = esSegundaGeneracion && valores.RazonamientoEnSegundaGeneracion;
        var prefijo = await esquema.ObtenerAsync(conDatosPersonales, ct);

        // EJEMPLOS EN EL PREFIJO (D3): todos al final del prefijo cacheable, y
        // ninguno en el mensaje. Sin la opción, el prefijo y el mensaje son BYTE A
        // BYTE los de siempre.
        var prefijoEstable = valores.EjemplosEnElPrefijo
            ? prefijo.Prefijo + BloqueDeEjemplos()
            : prefijo.Prefijo;
        var elegidos = valores.EjemplosEnElPrefijo ? [] : ejemplos.Elegir(pregunta);

        var respuesta = await modelo.CompletarAsync(
            new SolicitudAlModelo
            {
                PrefijoEstable = prefijoEstable,
                Mensaje = ArmarMensaje(
                    pregunta, elegidos, fecha.Hoy(), consultasAnteriores, menciones,
                    preguntasAnteriores, intentoAnterior),
                Temperatura = 0.0m,
                // La llamada que MÁS se beneficia de deliberar: elegir el join
                // correcto entre catorce tablas es el trabajo que mejora pensando, y
                // es donde equivocarse produce una respuesta falsa.
                // En la segunda generación del turno, con la opción prendida, se paga
                // deliberar: esfuerzo alto y su propio techo (cero usa el de siempre).
                Esfuerzo = conRazonamiento
                    ? EsfuerzoDelModelo.Alto
                    : EsfuerzoConfigurado.Interpretar(
                        valores.EsfuerzoDeGeneracion,
                        nameof(OpcionesAsistente.EsfuerzoDeGeneracion)),
                MaximoDeTokens = conRazonamiento && valores.MaximoDeTokensDeSegundaGeneracion > 0
                    ? valores.MaximoDeTokensDeSegundaGeneracion
                    : valores.MaximoDeTokensDeGeneracion,
                EsquemaDeSalidaJson = EsquemaDeSalida,
            },
            ct);

        var interpretada = InterpretarSiSePuede(respuesta.Texto);

        if (respuesta.SeQuedoSinTokens)
        {
            if (interpretada is null)
            {
                // El modelo gastó el presupuesto antes de cerrar el objeto. NO es
                // una abstención: el modelo no decidió nada, y contarla como tal
                // haría que un techo corto se viera igual que un asistente prudente
                // —en el registro y, peor, en la métrica—.
                return GeneracionDeSql.Truncada(RazonamientoIninteligible);
            }

            // El objeto llegó entero y lo que se cortó fue algo después. Se usa,
            // pero queda dicho: es la señal de que el techo está al límite.
            log.LogWarning(
                "La generación agotó su techo de tokens pero el objeto llegó completo; "
                + "se usa igual. El presupuesto de la llamada está al límite.");
        }

        return interpretada ?? GeneracionDeSql.NoContestable(RazonamientoIninteligible);
    }

    /// <summary>
    /// Arma el prompt de usuario. Todo lo variable del turno está acá y nada de
    /// esto puede filtrarse al prefijo.
    /// </summary>
    /// <param name="consultasAnteriores">
    /// Las consultas del segmento vigente, de la más vieja a la más reciente. Van
    /// ACÁ y no en el prefijo: el prefijo se cachea y su huella sella los reportes
    /// de evaluación, así que meterle algo que cambia por turno lo invalidaría en
    /// cada llamada y pagaría escritura a 1,25× en vez de lectura a 0,1×.
    /// </param>
    /// <param name="menciones">
    /// Las menciones nuevas de este turno (design.md D11). VA DESPUÉS de la
    /// pregunta y no antes: es la última cosa cierta de este turno —la que el
    /// usuario eligió al escribir, no algo que haya que inferir de la pregunta—,
    /// y por el mismo motivo por el que las consultas anteriores van después de
    /// los ejemplos, ponerla antes la dejaría a merced de que el modelo la lea
    /// como un ejemplo más en vez de como una instrucción sobre ESTA pregunta.
    /// </param>
    internal static string ArmarMensaje(
        string pregunta,
        IReadOnlyList<EjemploSql> elegidos,
        DateOnly hoy,
        IReadOnlyList<string>? consultasAnteriores = null,
        IReadOnlyList<(string Marcador, TipoDeMencion Tipo, ResultadoDeMencion Entidad)>? menciones = null,
        IReadOnlyList<string>? preguntasAnteriores = null,
        (string Sql, string Problema)? intentoAnterior = null)
    {
        var mensaje = new StringBuilder();

        mensaje.Append(CultureInfo.InvariantCulture,
            $"Fecha de referencia: {hoy:yyyy-MM-dd}.\n");
        mensaje.Append(
            "Usá esta fecha cuando la pregunta hable del presente. No uses funciones de reloj.\n");

        if (elegidos.Count > 0)
        {
            mensaje.Append("\nEjemplos de preguntas ya resueltas sobre este esquema:\n");

            foreach (var ejemplo in elegidos)
            {
                mensaje.Append(CultureInfo.InvariantCulture,
                    $"\nPregunta: {ejemplo.Pregunta}\nSQL: {ejemplo.Sql}\n");
            }
        }

        // DESPUÉS DE LOS EJEMPLOS Y ANTES DE LA PREGUNTA, a propósito. Los ejemplos
        // enseñan la FORMA del esquema y son intercambiables entre turnos; esto es
        // el estado de ESTA conversación y lo que la pregunta continúa. Ponerlo
        // entre los ejemplos lo dejaría a merced de que el modelo lo lea como uno
        // más y copie su forma en vez de continuarla.
        if (consultasAnteriores is { Count: > 0 })
        {
            mensaje.Append(
                "\nConsultas de los turnos anteriores de esta conversación, de la más "
                + "vieja a la más reciente. Si la pregunta continúa alguna de ellas, "
                + "editala o anidala en lugar de escribir una nueva desde cero:\n");

            foreach (var consulta in consultasAnteriores)
            {
                mensaje.Append(CultureInfo.InvariantCulture, $"\nSQL: {consulta}\n");
            }
        }

        // PREGUNTAS ANTERIORES (D7): sólo con la reescritura dentro de la
        // generación. Van antes de la pregunta por lo mismo que las consultas
        // anteriores: son el estado de esta conversación.
        if (preguntasAnteriores is { Count: > 0 })
        {
            mensaje.Append(
                "\nPreguntas anteriores de esta conversación, de la más vieja a la más reciente:\n");

            foreach (var anterior in preguntasAnteriores)
            {
                mensaje.Append(CultureInfo.InvariantCulture, $"- {anterior}\n");
            }
        }

        mensaje.Append(CultureInfo.InvariantCulture, $"\nPregunta del usuario:\n{pregunta}\n");

        if (preguntasAnteriores is { Count: > 0 })
        {
            mensaje.Append(
                "\nSi la pregunta continúa alguna de las anteriores —«¿y en Sistemas?», «de esos, "
                + "¿cuántos…?»—, agregá al objeto la clave `pregunta_interpretada` con la pregunta "
                + "completa y autocontenida, en español, y escribí la consulta para ESA pregunta. "
                + "Si la pregunta ya se entiende sola, omití la clave.\n");
        }

        // SÓLO SI HAY MENCIONES NUEVAS: con un turno sin ellas —el caso
        // mayoritario, y el único que existía antes de D11— este método devuelve
        // BYTE A BYTE el mismo texto que devolvía antes, así que ningún cassette
        // grabado se invalida (PrefijoDeLosCassettesTests sigue en pie sin
        // regrabar nada).
        if (menciones is { Count: > 0 })
        {
            mensaje.Append(
                "\nMenciones de esta pregunta. Cada una nombra una entidad EXACTA que ya "
                + "eligió el usuario: filtrá por su marcador reservado, NUNCA por nombre, y "
                + "usá cada marcador declarado al menos una vez en la consulta:\n");

            foreach (var (marcador, tipo, entidad) in menciones)
            {
                var fk = tipo == TipoDeMencion.Materia ? "materias.id" : "personas.id";
                var clase = tipo == TipoDeMencion.Materia ? "materia" : "docente";
                var etiqueta = MarcadoresDeReferencias.Etiqueta(tipo, entidad);

                mensaje.Append(CultureInfo.InvariantCulture,
                    $"\n{marcador}: {clase} \"{etiqueta}\" — filtrá por {fk} = {marcador} (o la clave foránea que corresponda), nunca por el nombre.\n");
            }
        }

        // EL INTENTO ANTERIOR VA AL FINAL (D4): es lo último que pasó en este
        // turno y lo que la nueva consulta tiene que corregir.
        if (intentoAnterior is { } intento)
        {
            mensaje.Append(CultureInfo.InvariantCulture,
                $"\nIntento anterior para esta misma pregunta:\nSQL: {intento.Sql}\nQué pasó: {intento.Problema}\n");
            mensaje.Append(
                "Escribí una consulta corregida que responda la pregunta. Si con este esquema no "
                + "se puede responder, devolvé `es_contestable` en false.\n");
        }

        return mensaje.ToString();
    }

    /// <summary>
    /// Todos los ejemplos verificados, en el orden del catálogo, para el final
    /// del prefijo (asistente-optimizaciones-modelo-local, D3).
    /// </summary>
    /// <remarks>
    /// Determinista y memorizado: el catálogo es un recurso embebido y no cambia
    /// mientras el proceso vive, así que el prefijo resultante es byte a byte
    /// igual en cada turno — que es lo que la caché de prefijo necesita.
    /// </remarks>
    private string BloqueDeEjemplos() => _bloqueDeEjemplos ??= ArmarBloqueDeEjemplos(ejemplos.Catalogo);

    private string? _bloqueDeEjemplos;

    internal static string ArmarBloqueDeEjemplos(IReadOnlyList<EjemploSql> catalogo)
    {
        var bloque = new StringBuilder();
        bloque.Append("\nEJEMPLOS VERIFICADOS\n\n");
        bloque.Append(
            "Preguntas ya resueltas sobre este esquema. Imitá su forma; no copies sus "
            + "valores si la pregunta nombra otros.\n");

        foreach (var ejemplo in catalogo)
        {
            bloque.Append(CultureInfo.InvariantCulture,
                $"\nPregunta: {ejemplo.Pregunta}\nSQL: {ejemplo.Sql}\n");
        }

        return bloque.ToString();
    }

    /// <summary>
    /// Interpreta la respuesta del modelo.
    /// </summary>
    /// <remarks>
    /// Una respuesta que no se puede interpretar resuelve <b>no contestable</b>.
    /// La alternativa —buscar algo que parezca SQL dentro del texto— convertiría
    /// un fallo de formato en la ejecución de una consulta que nadie declaró como
    /// tal, que es exactamente el caso que el validador existe para evitar.
    /// </remarks>
    internal static GeneracionDeSql Interpretar(string texto) =>
        InterpretarSiSePuede(texto) ?? GeneracionDeSql.NoContestable(RazonamientoIninteligible);

    /// <summary>
    /// Interpreta la respuesta, o nulo si no tiene la forma esperada.
    /// </summary>
    /// <remarks>
    /// Devuelve nulo en lugar de resolver por abstención porque quien llama necesita
    /// la diferencia: la misma respuesta ininteligible es una abstención si el modelo
    /// terminó de escribir y un corte por presupuesto si no.
    /// </remarks>
    private static GeneracionDeSql? InterpretarSiSePuede(string texto)
    {
        var json = ExtraerObjeto(texto);
        if (json is null)
        {
            return null;
        }

        RespuestaDeGeneracion? interpretada;
        try
        {
            interpretada = JsonSerializer.Deserialize<RespuestaDeGeneracion>(json);
        }
        catch (JsonException)
        {
            return null;
        }

        if (interpretada is null)
        {
            return null;
        }

        var razonamiento = string.IsNullOrWhiteSpace(interpretada.Razonamiento)
            ? RazonamientoIninteligible
            : interpretada.Razonamiento.Trim();

        // EL MODELO DECLARÓ, EXPLÍCITAMENTE, QUE NO ES CONTESTABLE (design.md D2
        // de asistente-rechazos-dinamicos): acá SÍ hubo una decisión, así que el
        // motivo se interpreta —cualquier valor fuera del conjunto cerrado
        // resuelve `NoCubierto`, nunca `null`— y el término candidato queda
        // expuesto sin validar todavía.
        if (!interpretada.EsContestable)
        {
            var motivo = MotivosDeRechazo.Interpretar(ComoCadena(interpretada.Motivo));
            var termino = ComoCadena(interpretada.Termino);

            return GeneracionDeSql.NoContestable(razonamiento, motivo, termino);
        }

        // Contestable sin consulta es una contradicción del modelo: se resuelve
        // como abstención en lugar de seguir con una consulta vacía. El motivo
        // queda `null` a propósito (design.md D2): no fue una decisión del
        // modelo SOBRE EL MOTIVO, fue una contradicción del objeto entero.
        if (string.IsNullOrWhiteSpace(interpretada.Sql))
        {
            return GeneracionDeSql.NoContestable(razonamiento);
        }

        var categoria = string.IsNullOrWhiteSpace(interpretada.Categoria)
            ? "consulta_simple"
            : interpretada.Categoria.Trim();

        return new GeneracionDeSql(
            true,
            interpretada.Sql.Trim(),
            razonamiento,
            categoria,
            PreguntaInterpretada: string.IsNullOrWhiteSpace(interpretada.PreguntaInterpretada)
                ? null
                : interpretada.PreguntaInterpretada.Trim());
    }

    /// <summary>
    /// Recorta el objeto JSON del texto, tolerando delimitadores de bloque de
    /// código y prosa alrededor.
    /// </summary>
    /// <remarks>
    /// Busca desde la primera llave hasta la última: el objeto que se espera es
    /// uno solo y está completo, así que no hace falta equilibrar llaves — y un
    /// texto donde eso no alcance es justamente un texto que no se puede
    /// interpretar, que resuelve por abstención.
    /// </remarks>
    private static string? ExtraerObjeto(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var inicio = texto.IndexOf('{', StringComparison.Ordinal);
        var fin = texto.LastIndexOf('}');

        return inicio >= 0 && fin > inicio ? texto[inicio..(fin + 1)] : null;
    }

    /// <summary>
    /// Lee un <see cref="JsonElement"/> como cadena, o <c>null</c> si no lo es
    /// (design.md D2): un `motivo`/`termino` que el modelo mandó como número o
    /// booleano se ignora en lugar de tirar la generación entera abajo.
    /// </summary>
    private static string? ComoCadena(JsonElement? elemento) =>
        elemento is { ValueKind: JsonValueKind.String } valor ? valor.GetString() : null;

    private sealed class RespuestaDeGeneracion
    {
        // Sólo con la reescritura dentro de la generación (D7); ausente en el
        // resto, y el intérprete la ignora si no es una cadena útil.
        [JsonPropertyName("pregunta_interpretada")]
        public string? PreguntaInterpretada { get; init; }

        [JsonPropertyName("es_contestable")]
        public bool EsContestable { get; init; }

        [JsonPropertyName("sql")]
        public string? Sql { get; init; }

        [JsonPropertyName("razonamiento")]
        public string? Razonamiento { get; init; }

        [JsonPropertyName("categoria")]
        public string? Categoria { get; init; }

        // TOLERANTE A PROPÓSITO (design.md D2): `JsonElement?` en vez de
        // `string?` para que un `motivo`/`termino` que no es cadena —42, un
        // objeto, `true`— se ignore en `ComoCadena` en lugar de que
        // `JsonSerializer.Deserialize` tire `JsonException` y pierda la
        // decisión entera (`es_contestable`, `sql`, `razonamiento`) con ella.
        [JsonPropertyName("motivo")]
        public JsonElement? Motivo { get; init; }

        [JsonPropertyName("termino")]
        public JsonElement? Termino { get; init; }
    }
}
