using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;

namespace Modules.Asistente.Application;

/// <summary>
/// Segunda llamada al modelo: narra las filas en español (RF-01, RF-10, RNF-18).
/// </summary>
/// <remarks>
/// Es la segunda superficie de falla de la métrica, y la que la evaluación no
/// suele mirar: el dataset de capacidad compara conjuntos de resultados y no ve
/// la redacción. Una consulta correcta narrada mal es tan falsa como una consulta
/// incorrecta.
///
/// Sin prefijo cacheado, a diferencia de la generación: el prompt de redacción es
/// distinto en cada turno por definición —lleva las filas—, así que no hay nada
/// estable que cachear.
/// </remarks>
public sealed class RedactorDeRespuesta(
    IProveedorDeModelo modelo, IOptions<OpcionesAsistente> opciones)
{
    /// <summary>
    /// Temperatura baja pero no cero: cero produce redacciones rígidas y
    /// repetitivas, y acá no se está generando código sino prosa.
    /// </summary>
    private const decimal Temperatura = 0.3m;


    /// <summary>Cuántas filas se le muestran al modelo como mucho.</summary>
    /// <remarks>
    /// El tope existe por costo del prompt, no por seguridad: las filas ya vienen
    /// acotadas por el ejecutor. Cuando entran menos de las que hay, se le dice al
    /// modelo que está viendo una muestra, para que no narre un total.
    /// </remarks>
    private const int TopeDeFilasEnElPrompt = 40;

    /// <summary>Redacta la respuesta sobre las filas devueltas.</summary>
    public async Task<string> RedactarAsync(
        string pregunta,
        ResultadoDeConsulta resultado,
        bool actorEsGlobal,
        CancellationToken ct)
    {
        var respuesta = await modelo.CompletarAsync(
            new SolicitudAlModelo
            {
                PrefijoEstable = Instrucciones,
                Mensaje = ArmarMensaje(pregunta, resultado, actorEsGlobal),
                Temperatura = Temperatura,
                MaximoDeTokens = opciones.Value.MaximoDeTokensDeRedaccion,
            },
            ct);

        return respuesta.Texto.Trim();
    }

    /// <summary>Instrucciones fijas de la redacción.</summary>
    internal const string Instrucciones = """
        Redactás en español la respuesta de un asistente de consulta de un sistema
        de gestión docente universitaria. Recibís la pregunta del usuario y las
        filas que devolvió la base, y escribís la respuesta que él va a leer.

        REGLAS

        1. Usá únicamente los valores de las filas. No agregues datos, nombres,
           cifras ni conclusiones que no estén ahí.
        2. Escribí en español rioplatense, en tono profesional y directo. Dos o
           tres oraciones alcanzan; si son muchas filas, resumí y dejá que la
           tabla hable.
        3. No menciones nombres de tablas ni de columnas, ni la consulta que se
           ejecutó, ni ningún detalle técnico del sistema.
        4. No pidas disculpas ni expliques cómo funcionás.
        5. Devolvé solo el texto de la respuesta, sin encabezados ni formato de
           código.
        """;

    /// <summary>
    /// Arma el prompt de usuario de la redacción.
    /// </summary>
    /// <remarks>
    /// Es <c>internal</c> y puro para poder afirmar en un test que las reglas de
    /// abstención llegaron al prompt. Verificarlo a través de la respuesta del
    /// modelo probaría al modelo, no a este código.
    /// </remarks>
    internal static string ArmarMensaje(
        string pregunta, ResultadoDeConsulta resultado, bool actorEsGlobal)
    {
        ArgumentNullException.ThrowIfNull(resultado);

        var mensaje = new StringBuilder();
        mensaje.Append(CultureInfo.InvariantCulture, $"Pregunta del usuario:\n{pregunta}\n");

        foreach (var regla in PoliticaDeAbstencion.ReglasDeRedaccion(actorEsGlobal, resultado.Truncado))
        {
            mensaje.Append(CultureInfo.InvariantCulture, $"\nIMPORTANTE: {regla}\n");
        }

        var mostradas = Math.Min(resultado.Filas.Count, TopeDeFilasEnElPrompt);
        if (mostradas < resultado.Filas.Count)
        {
            mensaje.Append(
                "\nIMPORTANTE: abajo va solo una muestra de las filas. No afirmes conteos.\n");
        }

        mensaje.Append(CultureInfo.InvariantCulture,
            $"\nColumnas: {string.Join(" | ", resultado.Columnas)}\n");
        mensaje.Append("Filas:\n");

        for (var indice = 0; indice < mostradas; indice++)
        {
            mensaje.Append(CultureInfo.InvariantCulture,
                $"{string.Join(" | ", resultado.Filas[indice].Select(Mostrar))}\n");
        }

        return mensaje.ToString();
    }

    private static string Mostrar(object? valor) => valor switch
    {
        null => "(sin dato)",
        DateTime fecha => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateOnly fecha => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IFormattable formateable => formateable.ToString(null, CultureInfo.InvariantCulture),
        _ => valor.ToString() ?? string.Empty,
    };
}
