using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;

namespace Modules.Asistente.Application;

/// <summary>
/// Convierte un seguimiento elíptico —«¿y en Sistemas?»— en una pregunta
/// autocontenida.
/// </summary>
/// <remarks>
/// Es la <b>única</b> llamada al modelo que agrega la capa conversacional, y solo
/// corre cuando hay historial: un primer turno no la paga.
///
/// Su prompt no está cacheado, así que se lo puede editar sin tocar el prefijo
/// estable del carril SQL.
///
/// Corre <b>antes</b> del detector de ambigüedad: la pregunta original no contiene
/// ninguna entidad ambigua, pero la reescrita sí, y además ya trae el discriminador
/// que la resuelve.
/// </remarks>
public sealed class ReescritorDePreguntas(
    IProveedorDeModelo modelo, IOptions<OpcionesAsistente> opciones)
{
    private const decimal Temperatura = 0.0m;


    /// <summary>
    /// Reescribe el mensaje como pregunta autocontenida.
    /// </summary>
    /// <remarks>
    /// Con historial vacío devuelve el mensaje tal cual, <b>sin llamar al modelo</b>.
    /// Es el mecanismo del cambio de tema: al pivotar, el llamador manda historial
    /// vacío y la reescritura deja de ocurrir, en vez de pedirle al modelo que
    /// ignore lo anterior.
    /// </remarks>
    public async Task<string> ReescribirAsync(
        string mensaje, IReadOnlyList<TurnoDelHilo> historial, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mensaje);
        ArgumentNullException.ThrowIfNull(historial);

        if (historial.Count == 0)
        {
            return mensaje;
        }

        var respuesta = await modelo.CompletarAsync(
            new SolicitudAlModelo
            {
                PrefijoEstable = Instrucciones,
                Mensaje = ArmarMensaje(mensaje, historial),
                Temperatura = Temperatura,
                // Bajo: resolver «¿y el de Pérez?» contra el turno anterior es una
                // sustitución. Y está en el camino crítico de todo seguimiento, antes
                // de que el pipeline siquiera empiece.
                Esfuerzo = EsfuerzoConfigurado.Interpretar(
                    opciones.Value.EsfuerzoDeReescritura,
                    nameof(OpcionesAsistente.EsfuerzoDeReescritura)),
                MaximoDeTokens = opciones.Value.MaximoDeTokensDeReescritura,
            },
            ct);

        return Interpretar(respuesta.Texto, mensaje);
    }

    /// <summary>Instrucciones fijas de la reescritura.</summary>
    /// <remarks>
    /// <b>Soltar una restricción es un resultado legítimo.</b> Una regla que dijera
    /// «conservá todas las restricciones vigentes; si no la menciona, arrastrala»
    /// produce arrastre silencioso: el filtro del turno anterior angosta el
    /// resultado del siguiente y el usuario no se entera. Por eso la regla enumera
    /// los campos del dominio y decide uno por uno.
    ///
    /// Los dos ejemplos son deliberados. Sin el de descarte, la única forma
    /// demostrada de resolver el prompt es arrastrar, y el modelo arrastra siempre.
    /// Ninguno replica un turno del dataset de evaluación: un ejemplo copiado de ahí
    /// estaría entrenando contra la métrica.
    /// </remarks>
    internal const string Instrucciones = """
        Convertís el último mensaje de una conversación en una pregunta que se
        entienda sola, sin necesidad de leer los mensajes anteriores.

        REGLA PRINCIPAL

        Mirá estos campos del dominio UNO POR UNO y decidí para cada uno si el
        mensaje nuevo lo reemplaza, si se mantiene del historial, o si se suelta:

        1. Carrera
        2. Materia
        3. Período
        4. Cargo
        5. Persona

        Un campo se MANTIENE solo si el mensaje nuevo sigue hablando del mismo
        asunto y no lo menciona. Se REEMPLAZA si el mensaje nuevo trae otro valor
        para ese campo. Se SUELTA si el mensaje nuevo cambia de asunto.

        Soltar un campo es un resultado correcto y esperable. No arrastres un
        filtro solo porque estaba vigente: si lo arrastrás de más, el usuario
        recibe un resultado más angosto que el que pidió y no se entera.

        EJEMPLO A — se reemplaza la materia y se mantiene el resto

        Anterior: ¿Cuántos docentes están designados en Química Orgánica?
        Mensaje: ¿y en Física II?
        Reescrita: ¿Cuántos docentes están designados en Física II?

        EJEMPLO B — se descarta todo el historial

        Anterior: ¿Qué docentes tienen dedicación exclusiva en Química Orgánica?
        Mensaje: ¿Cuáles son los períodos de designación cargados?
        Reescrita: ¿Cuáles son los períodos de designación cargados?

        SALIDA

        Devolvé únicamente la pregunta reescrita, en español, sin comillas, sin
        encabezados y sin explicar qué hiciste. Si el mensaje ya se entiende solo,
        devolvelo igual.
        """;

    /// <summary>Arma el prompt de usuario de la reescritura.</summary>
    /// <remarks>
    /// Es <c>internal</c> y puro para poder afirmar en un test <b>qué se le mandó al
    /// modelo</b>. El test del pivote mira justamente eso: que no lleve ningún turno
    /// anterior. Verificarlo por la salida probaría al modelo, no a este código.
    /// </remarks>
    internal static string ArmarMensaje(string mensaje, IReadOnlyList<TurnoDelHilo> historial)
    {
        var prompt = new StringBuilder();

        foreach (var turno in historial)
        {
            prompt.Append(CultureInfo.InvariantCulture, $"Anterior: {turno.Pregunta}\n");
        }

        prompt.Append(CultureInfo.InvariantCulture, $"Mensaje: {mensaje}\n");
        prompt.Append("Reescrita:");

        return prompt.ToString();
    }

    /// <summary>
    /// Toma la reescritura, o el mensaje original si la respuesta no sirve.
    /// </summary>
    /// <remarks>
    /// Conservar el original ante una respuesta vacía o disparatada degrada el
    /// seguimiento y no rompe el turno. Aceptar cualquier cosa haría que un fallo
    /// del modelo se transformara en una pregunta distinta de la que el usuario
    /// hizo, que es peor: respondería bien algo que nadie preguntó.
    /// </remarks>
    internal static string Interpretar(string respuesta, string original)
    {
        var limpia = respuesta
            .Trim()
            .Trim('"', '«', '»')
            .Trim();

        if (limpia.Length == 0 || limpia.Length > original.Length * 8 + 200)
        {
            return original;
        }

        // Una reescritura que se quedó con la primera línea de una respuesta
        // charlatana sigue sirviendo; una que devolvió un párrafo, no.
        var primeraLinea = limpia.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].Trim();

        return primeraLinea.Length == 0 ? original : primeraLinea;
    }
}
