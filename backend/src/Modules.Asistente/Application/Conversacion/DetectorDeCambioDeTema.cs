namespace Modules.Asistente.Application;

/// <summary>Marca cuándo el usuario soltó el tema anterior.</summary>
internal static class DetectorDeCambioDeTema
{
    /// <summary>Palabras que atan el mensaje a lo anterior.</summary>
    private static readonly HashSet<string> Anaforicos = new(StringComparer.Ordinal)
    {
        "eso", "esa", "ese", "esos", "esas", "esto", "estos", "estas",
        "mismo", "misma", "mismos", "mismas", "ahi", "alli", "alla",
        "ellos", "ellas", "ella", "el", "les", "le", "lo", "los", "las",
        "tambien", "ademas", "anterior", "anteriores", "ultimo", "ultima",
    };

    /// <summary>Decide si el mensaje suelta el tema del segmento vigente.</summary>
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

        // LA GUARDA QUE PROTEGE EL SEGUIMIENTO, y va primero por eso. «¿y en Sistemas?» menciona un
        // término del catálogo que no está activo, o sea que cumple la segunda condición.
        if (palabras[0] == "y" || palabras.Any(Anaforicos.Contains))
        {
            return false;
        }

        var activos = historialVigente
            .SelectMany(turno => TerminosDe(turno.Pregunta, catalogo))
            .ToHashSet(StringComparer.Ordinal);

        // Pivote solo si nombra algo del dominio que el segmento no venía tratando.
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
