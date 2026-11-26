using System.Globalization;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Modules.Asistente.Application;

/// <summary>Segunda llamada al modelo: narra las filas en español (RF-01, RF-10, RNF-18).</summary>
public sealed class RedactorDeRespuesta(
    [FromKeyedServices(ModuleExtensions.ProveedorDeRedaccion)] IProveedorDeModelo modelo,
    IOptions<OpcionesAsistente> opciones,
    CanalDeRedaccion? canal = null)
{
    /// <summary>
    /// Temperatura baja pero no cero: cero produce redacciones rígidas y
    /// repetitivas, y acá no se está generando código sino prosa.
    /// </summary>
    private const decimal Temperatura = 0.3m;


    /// <summary>Cuántas filas se le muestran al modelo como mucho.</summary>
    private const int TopeDeFilasEnElPrompt = 40;

    /// <summary>Redacta la respuesta sobre las filas devueltas.</summary>
    public async Task<string> RedactarAsync(
        string pregunta,
        ResultadoDeConsulta resultado,
        bool alcanzaTodo,
        IReadOnlyList<CoberturaDeUnDato> cobertura,
        CancellationToken ct,
        bool hayTextoLibreOculto = false,
        string? comoSeBusco = null)
    {
        // RESULTADO TRIVIAL, SIN MODELO (asistente-optimizaciones-modelo-local, D5): una columna
        // con pocos valores, alcance completo, sin recorte ni cobertura autodeclarada.
        if (opciones.Value.RedaccionConPlantillas
            && PlantillaDeRedaccion.Intentar(resultado, alcanzaTodo, cobertura) is { } deterministica)
        {
            return deterministica;
        }

        var respuesta = await modelo.CompletarAsync(
            new SolicitudAlModelo
            {
                PrefijoEstable = Instrucciones,
                Mensaje = ArmarMensaje(pregunta, resultado, alcanzaTodo, cobertura, hayTextoLibreOculto, comoSeBusco),
                Temperatura = Temperatura,
                // Bajo a propósito: las filas ya están y esto es convertirlas en una oración.
                Esfuerzo = EsfuerzoConfigurado.Interpretar(
                    opciones.Value.EsfuerzoDeRedaccion,
                    nameof(OpcionesAsistente.EsfuerzoDeRedaccion)),
                MaximoDeTokens = opciones.Value.MaximoDeTokensDeRedaccion,
                // Por fragmentos sólo si la opción está prendida y alguien escucha
                // (D9). El texto que vale es el de la respuesta completa.
                AlRecibirTexto = opciones.Value.StreamingDeRedaccion ? canal?.AlRecibirTexto : null,
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

    /// <summary>Arma el prompt de usuario de la redacción.</summary>
    internal static string ArmarMensaje(
        string pregunta,
        ResultadoDeConsulta resultado,
        bool alcanzaTodo,
        IReadOnlyList<CoberturaDeUnDato>? cobertura = null,
        bool hayTextoLibreOculto = false,
        string? comoSeBusco = null)
    {
        ArgumentNullException.ThrowIfNull(resultado);

        var mensaje = new StringBuilder();
        mensaje.Append(CultureInfo.InvariantCulture, $"Pregunta del usuario:\n{pregunta}\n");

        // The rows do not always carry the column the query filtered on («solicitudes urgentes»
        // returns the urgent ones without a prioritario column). Without this, the model read the
        // rows and said «ninguna está marcada como urgente». It is the generation's own reading of
        // the question: model text, no data.
        if (!string.IsNullOrWhiteSpace(comoSeBusco))
        {
            mensaje.Append(CultureInfo.InvariantCulture,
                $"\nCómo se buscó (las filas ya cumplen esta condición): {comoSeBusco}\n");
        }

        foreach (var regla in PoliticaDeAbstencion.ReglasDeRedaccion(alcanzaTodo, resultado.Truncado, cobertura))
        {
            mensaje.Append(CultureInfo.InvariantCulture, $"\nIMPORTANTE: {regla}\n");
        }

        // A sensible-texto column was dropped before this prompt: without this line the model reads
        // its absence as missing data and says «no hay datos sobre el motivo» while the table shows it.
        // Column names stay out, as the suppression requires.
        if (hayTextoLibreOculto)
        {
            mensaje.Append(
                "\nIMPORTANTE: la tabla que ve el usuario tiene además columnas de texto libre " +
                "que no se te muestran. Si la pregunta pide ese texto, decí que está en la tabla; " +
                "nunca digas que no hay datos sobre eso.\n");
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
