namespace Modules.Asistente.Application;

/// <summary>
/// Valida el término candidato que la generación propone para un rechazo
/// (design.md D3 de asistente-rechazos-dinamicos).
/// </summary>
/// <remarks>
/// <b>El modelo propone, el código re-copia.</b> Lo único que puede llegar a la
/// respuesta es un span de <c>mensaje</c> —lo que el usuario tipeó ESTE turno—,
/// nunca el texto que el modelo devolvió: éste sólo sirve para ENCONTRAR ese
/// span. Confiar en la cadena del modelo pondría texto del modelo en el cuerpo
/// de un rechazo, que es exactamente lo que el ticket prohíbe.
///
/// Por eso se valida contra <c>mensaje</c> y no contra la pregunta reescrita: la
/// reescritura ya es una salida del modelo (<see cref="ReescritorDePreguntas"/>),
/// y un término que sólo existe después de resolver una anáfora nunca lo tipeó
/// el usuario.
/// </remarks>
internal static class TerminoDelRechazo
{
    private const int MinimoDeCaracteres = 2;
    private const int MaximoDeCaracteres = 40;
    private const int MaximoDePalabras = 4;

    /// <summary>
    /// Devuelve el span de <paramref name="mensaje"/> que <paramref name="candidato"/>
    /// nombra, o <c>null</c> si no cumple alguna de las condiciones de design.md D3.
    /// </summary>
    public static string? Validar(string? candidato, string mensaje)
    {
        if (string.IsNullOrWhiteSpace(candidato) || string.IsNullOrWhiteSpace(mensaje))
        {
            return null;
        }

        var recortado = candidato.Trim();

        if (recortado.Length < MinimoDeCaracteres || recortado.Length > MaximoDeCaracteres)
        {
            return null;
        }

        if (ContarPalabras(recortado) > MaximoDePalabras)
        {
            return null;
        }

        if (recortado.Any(EsCaracterProhibido))
        {
            return null;
        }

        if (EsSoloDemostrativosOVacias(recortado))
        {
            return null;
        }

        // PLEGADO CARÁCTER A CARÁCTER: los índices del texto plegado tienen que
        // corresponderse 1:1 con los de `mensaje`, porque lo que se devuelve es
        // el span del mensaje ORIGINAL —los caracteres del usuario, nunca los
        // del modelo—.
        var mensajePlegado = Plegar(mensaje);
        var candidatoPlegado = Plegar(recortado);

        var inicio = mensajePlegado.IndexOf(candidatoPlegado, StringComparison.Ordinal);
        if (inicio < 0)
        {
            return null;
        }

        var fin = inicio + candidatoPlegado.Length;

        // LÍMITES DE PALABRA: sin esto, «pyth» matchearía adentro de «python» y
        // el rechazo terminaría citando un fragmento que el usuario no escribió
        // como palabra propia.
        if (EsLetraODigito(mensaje, inicio - 1) || EsLetraODigito(mensaje, fin))
        {
            return null;
        }

        return mensaje.Substring(inicio, candidatoPlegado.Length);
    }

    private static bool EsCaracterProhibido(char caracter) =>
        caracter is '\n' or '\r' or '«' or '»' or '"' || char.IsControl(caracter);

    private static int ContarPalabras(string texto) =>
        texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>
    /// Si <paramref name="recortado"/> está hecho SÓLO de palabras vacías o de
    /// demostrativos —«eso», «esa materia»—, que nunca deben citarse como si
    /// nombraran algo puntual.
    /// </summary>
    private static bool EsSoloDemostrativosOVacias(string recortado)
    {
        var palabras = NormalizadorLexico.Palabras(recortado);

        return palabras.Count == 0
            || palabras.All(palabra =>
                NormalizadorLexico.PalabrasVacias.Contains(palabra)
                || PoliticaDeAbstencion.Demostrativos.Contains(palabra));
    }

    private static bool EsLetraODigito(string texto, int indice) =>
        indice >= 0 && indice < texto.Length && char.IsLetterOrDigit(texto[indice]);

    /// <summary>
    /// Pliega a minúscula sin acentos, preservando la longitud carácter por
    /// carácter para que los índices del resultado sigan valiendo sobre el
    /// texto original.
    /// </summary>
    private static string Plegar(string texto) => NormalizadorLexico.SinAcentos(texto).ToLowerInvariant();
}
