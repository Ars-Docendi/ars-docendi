namespace Modules.Asistente.Application;

/// <summary>Cómo terminó el intento de reconocer la respuesta del usuario.</summary>
public enum Reconocimiento
{
    /// <summary>Se identificó exactamente una opción.</summary>
    Elegida,

    /// <summary>La respuesta empata con más de una opción.</summary>
    Ambigua,

    /// <summary>La respuesta no se parece a ninguna opción.</summary>
    NoReconocida,
}

/// <summary>El resultado de reconocer una respuesta a una aclaración.</summary>
public sealed record RespuestaAAclaracion(Reconocimiento Estado, OpcionDeAclaracion? Opcion);

/// <summary>
/// Reconoce cuál de las opciones eligió el usuario, sin llamar al modelo.
/// </summary>
/// <remarks>
/// Tres pasos en orden de especificidad: etiqueta completa, token distintivo,
/// ordinal.
///
/// <b>Corre antes del reescritor</b>, y el orden importa: le entrega la etiqueta
/// canónica del catálogo, no el «2» que el usuario tipeó. Si el reescritor viera el
/// ordinal tendría que adivinar a qué se refiere, y adivinar es justo lo que esta
/// capa evita.
///
/// Clase pura.
/// </remarks>
public static class ReconocedorDeAclaracion
{
    /// <summary>Reconoce la respuesta contra las opciones ofrecidas.</summary>
    public static RespuestaAAclaracion Reconocer(string respuesta, Aclaracion aclaracion)
    {
        ArgumentNullException.ThrowIfNull(aclaracion);

        if (string.IsNullOrWhiteSpace(respuesta) || aclaracion.Opciones.Count == 0)
        {
            return new RespuestaAAclaracion(Reconocimiento.NoReconocida, null);
        }

        var palabras = NormalizadorLexico.Palabras(respuesta);
        var enmarcada = $" {string.Join(' ', palabras)} ";

        return PorEtiqueta(enmarcada, aclaracion)
            ?? PorTokenDistintivo(palabras, aclaracion)
            ?? PorOrdinal(palabras, aclaracion)
            ?? new RespuestaAAclaracion(Reconocimiento.NoReconocida, null);
    }

    /// <summary>Paso 1 — la etiqueta completa aparece en la respuesta.</summary>
    private static RespuestaAAclaracion? PorEtiqueta(string enmarcada, Aclaracion aclaracion)
    {
        var coinciden = aclaracion.Opciones
            .Where(opcion => enmarcada.Contains($" {Normalizar(opcion.Etiqueta)} ", StringComparison.Ordinal))
            .ToArray();

        return coinciden.Length switch
        {
            1 => new RespuestaAAclaracion(Reconocimiento.Elegida, coinciden[0]),

            // Dos etiquetas completas en una respuesta: el usuario nombró las dos.
            // No se elige la primera.
            > 1 => new RespuestaAAclaracion(Reconocimiento.Ambigua, null),

            _ => null,
        };
    }

    /// <summary>
    /// Paso 2 — una palabra que aparece en una sola de las etiquetas.
    /// </summary>
    /// <remarks>
    /// «Informática» distingue a «Ingeniería en Informática» de «Ingeniería en
    /// Electrónica»; «ingeniería» no distingue nada. Solo las distintivas cuentan,
    /// y si el usuario dijo una palabra compartida el resultado es ambiguo — que
    /// vuelve a preguntar, no que elija.
    /// </remarks>
    private static RespuestaAAclaracion? PorTokenDistintivo(
        IReadOnlyList<string> palabras, Aclaracion aclaracion)
    {
        var porOpcion = aclaracion.Opciones
            .Select(opcion => NormalizadorLexico.Palabras(opcion.Etiqueta).ToHashSet(StringComparer.Ordinal))
            .ToArray();

        var compartidas = porOpcion
            .SelectMany(palabrasDeLaOpcion => palabrasDeLaOpcion)
            .GroupBy(palabra => palabra, StringComparer.Ordinal)
            .Where(grupo => grupo.Count() > 1)
            .Select(grupo => grupo.Key)
            .ToHashSet(StringComparer.Ordinal);

        var dichas = palabras.ToHashSet(StringComparer.Ordinal);

        var elegidas = aclaracion.Opciones
            .Where((_, indice) => porOpcion[indice]
                .Any(palabra => !compartidas.Contains(palabra) && dichas.Contains(palabra)))
            .ToArray();

        if (elegidas.Length == 1)
        {
            return new RespuestaAAclaracion(Reconocimiento.Elegida, elegidas[0]);
        }

        if (elegidas.Length > 1)
        {
            return new RespuestaAAclaracion(Reconocimiento.Ambigua, null);
        }

        // Ninguna distintiva, pero el usuario dijo algo que las opciones comparten:
        // señaló el grupo, no un miembro. Se vuelve a preguntar.
        return compartidas.Overlaps(dichas)
            ? new RespuestaAAclaracion(Reconocimiento.Ambigua, null)
            : null;
    }

    /// <summary>Paso 3 — el número de la opción en el menú.</summary>
    private static RespuestaAAclaracion? PorOrdinal(
        IReadOnlyList<string> palabras, Aclaracion aclaracion)
    {
        var numeros = palabras
            .Where(palabra => palabra.All(char.IsDigit))
            .Select(palabra => int.TryParse(palabra, out var numero) ? numero : 0)
            .Where(numero => numero >= 1 && numero <= aclaracion.Opciones.Count)
            .Distinct()
            .ToArray();

        return numeros.Length switch
        {
            1 => new RespuestaAAclaracion(Reconocimiento.Elegida, aclaracion.Opciones[numeros[0] - 1]),
            > 1 => new RespuestaAAclaracion(Reconocimiento.Ambigua, null),
            _ => null,
        };
    }

    private static string Normalizar(string texto) =>
        string.Join(' ', NormalizadorLexico.Palabras(texto));
}
