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
public sealed record GeneracionDeSql(
    bool EsContestable,
    string? Sql,
    string Razonamiento,
    string Categoria)
{
    /// <summary>Generación que declara la pregunta fuera de alcance.</summary>
    public static GeneracionDeSql NoContestable(string razonamiento) =>
        new(false, null, razonamiento, CategoriaNoContestable);

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
    ILogger<GeneradorDeSql> log)
{

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
    public async Task<GeneracionDeSql> GenerarAsync(
        string pregunta, bool conDatosPersonales, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pregunta);

        var prefijo = await esquema.ObtenerAsync(conDatosPersonales, ct);
        var elegidos = ejemplos.Elegir(pregunta);

        var respuesta = await modelo.CompletarAsync(
            new SolicitudAlModelo
            {
                PrefijoEstable = prefijo.Prefijo,
                Mensaje = ArmarMensaje(pregunta, elegidos, fecha.Hoy()),
                Temperatura = 0.0m,
                // La llamada que MÁS se beneficia de deliberar: elegir el join
                // correcto entre catorce tablas es el trabajo que mejora pensando, y
                // es donde equivocarse produce una respuesta falsa.
                Esfuerzo = EsfuerzoConfigurado.Interpretar(
                    opciones.Value.EsfuerzoDeGeneracion,
                    nameof(OpcionesAsistente.EsfuerzoDeGeneracion)),
                MaximoDeTokens = opciones.Value.MaximoDeTokensDeGeneracion,
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
    internal static string ArmarMensaje(
        string pregunta, IReadOnlyList<EjemploSql> elegidos, DateOnly hoy)
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

        mensaje.Append(CultureInfo.InvariantCulture, $"\nPregunta del usuario:\n{pregunta}\n");

        return mensaje.ToString();
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

        // Contestable sin consulta es una contradicción del modelo: se resuelve
        // como abstención en lugar de seguir con una consulta vacía.
        if (!interpretada.EsContestable || string.IsNullOrWhiteSpace(interpretada.Sql))
        {
            return GeneracionDeSql.NoContestable(razonamiento);
        }

        var categoria = string.IsNullOrWhiteSpace(interpretada.Categoria)
            ? "consulta_simple"
            : interpretada.Categoria.Trim();

        return new GeneracionDeSql(true, interpretada.Sql.Trim(), razonamiento, categoria);
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

    private sealed class RespuestaDeGeneracion
    {
        [JsonPropertyName("es_contestable")]
        public bool EsContestable { get; init; }

        [JsonPropertyName("sql")]
        public string? Sql { get; init; }

        [JsonPropertyName("razonamiento")]
        public string? Razonamiento { get; init; }

        [JsonPropertyName("categoria")]
        public string? Categoria { get; init; }
    }
}
