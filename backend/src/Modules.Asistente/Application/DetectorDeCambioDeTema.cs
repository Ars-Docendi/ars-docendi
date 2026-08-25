namespace Modules.Asistente.Application;

/// <summary>
/// Marca cuándo el usuario soltó el tema anterior.
/// </summary>
/// <remarks>
/// <b>Detectarlo no alcanza.</b> Hay evidencia de modelos que detectan el pivote y
/// arrastran contexto rancio igual la gran mayoría de las veces. Por eso este
/// detector no produce una instrucción para el prompt: produce una decisión que el
/// llamador ejecuta vaciando el historial. Se fuerza, no se pide.
///
/// Clase pura.
/// </remarks>
public static class DetectorDeCambioDeTema
{
    /// <summary>
    /// Palabras que atan el mensaje a lo anterior.
    /// </summary>
    /// <remarks>
    /// Demostrativos y pronombres. Un mensaje que los usa está hablando de algo ya
    /// dicho, aunque nombre una entidad nueva.
    /// </remarks>
    private static readonly HashSet<string> Anaforicos = new(StringComparer.Ordinal)
    {
        "eso", "esa", "ese", "esos", "esas", "esto", "estos", "estas",
        "mismo", "misma", "mismos", "mismas", "ahi", "alli", "alla",
        "ellos", "ellas", "ella", "el", "les", "le", "lo", "los", "las",
        "tambien", "ademas", "anterior", "anteriores", "ultimo", "ultima",
    };

    /// <summary>
    /// Decide si el mensaje suelta el tema del segmento vigente.
    /// </summary>
    /// <param name="mensaje">Lo que escribió el usuario, crudo.</param>
    /// <param name="historialVigente">Los turnos del segmento en curso.</param>
    /// <param name="catalogo">El índice de entidades del dominio.</param>
    public static bool EsPivote(
        string mensaje,
        IReadOnlyList<TurnoDelHilo> historialVigente,
        CatalogoDeEntidades catalogo)
    {
        ArgumentNullException.ThrowIfNull(historialVigente);
        ArgumentNullException.ThrowIfNull(catalogo);

        // Sin historial no hay tema del que salirse.
        if (historialVigente.Count == 0 || string.IsNullOrWhiteSpace(mensaje))
        {
            return false;
        }

        var palabras = NormalizadorLexico.Palabras(mensaje);
        if (palabras.Count == 0)
        {
            return false;
        }

        // LA GUARDA QUE PROTEGE EL SEGUIMIENTO, y va primero por eso.
        //
        // «¿y en Sistemas?» menciona un término del catálogo que no está activo, o
        // sea que cumple la segunda condición. Si esta guarda no estuviera, el caso
        // canónico de seguimiento se rompería en el turno más común que existe.
        if (palabras[0] == "y" || palabras.Any(Anaforicos.Contains))
        {
            return false;
        }

        var activos = historialVigente
            .SelectMany(turno => TerminosDe(turno.Pregunta, catalogo))
            .ToHashSet(StringComparer.Ordinal);

        // Pivote solo si nombra algo del dominio que el segmento no venía tratando.
        // Una pregunta que no nombra ninguna entidad conocida no es un pivote: es
        // una pregunta general, y soltarle el contexto la empeoraría.
        return TerminosDe(mensaje, catalogo).Any(termino => !activos.Contains(termino));
    }

    /// <summary>Los términos del índice que aparecen en un texto.</summary>
    private static IEnumerable<string> TerminosDe(string texto, CatalogoDeEntidades catalogo)
    {
        var enmarcado = $" {string.Join(' ', NormalizadorLexico.Palabras(texto))} ";

        return catalogo.Terminos
            .Where(termino => enmarcado.Contains($" {termino} ", StringComparison.Ordinal));
    }
}
