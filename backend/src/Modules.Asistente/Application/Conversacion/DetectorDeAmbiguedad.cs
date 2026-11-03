namespace Modules.Asistente.Application;

/// <summary>
/// Detecta choques de valores contra el índice del dominio y arma el menú de aclaración.
/// </summary>
internal static class DetectorDeAmbiguedad
{
    /// <summary>
    /// Devuelve la aclaración que hace falta, o <c>null</c> si la pregunta no es ambigua.
    /// </summary>
    public static Aclaracion? Detectar(
        string pregunta,
        CatalogoDeEntidades catalogo,
        IReadOnlyCollection<string>? etiquetasDeReferencias = null)
    {
        ArgumentNullException.ThrowIfNull(catalogo);

        if (string.IsNullOrWhiteSpace(pregunta))
        {
            return null;
        }

        var normalizada = Enmarcar(pregunta);

        // Las etiquetas también se enmarcan con el MISMO criterio de normalización que el catálogo:
        // si no se hiciera, "Inglés Técnico I" (la etiqueta) nunca coincidiría con "ingles tecnico
        // i" (el término indexado), y el descarte de abajo no descartaría nada.
        var referenciasEnmarcadas = etiquetasDeReferencias is { Count: > 0 }
            ? etiquetasDeReferencias.Select(Enmarcar).ToArray()
            : [];

        // De mayor a menor longitud: «analisis matematico» tiene que ganarle a «analisis» si los
        // dos estuvieran indexados, o se ofrecería el menú equivocado.
        var candidatos = catalogo.Colisiones
            .Where(termino => normalizada.Contains($" {termino} ", StringComparison.Ordinal))
            // Un término que una mención de este turno ya cubre no es ambiguo PARA ESTE TURNO: la
            // etiqueta lo nombra entero, palabra por palabra, aunque el catálogo lo haya indexado
            // por una parte más corta (el apellido de un docente, por ejemplo).
            .Where(termino => !referenciasEnmarcadas.Any(
                referencia => referencia.Contains($" {termino} ", StringComparison.Ordinal)))
            .OrderByDescending(termino => termino.Length)
            .ThenBy(termino => termino, StringComparer.Ordinal);

        foreach (var termino in candidatos)
        {
            var valores = catalogo.Valores(termino);

            if (YaDesambiguada(normalizada, valores))
            {
                continue;
            }

            var opciones = valores
                .OrderBy(valor => valor.Discriminador, StringComparer.Ordinal)
                .Select(valor => new OpcionDeAclaracion(
                    valor.Discriminador,
                    Resolver(pregunta, valor)))
                .ToArray();

            return new Aclaracion(valores[0].Valor, pregunta, opciones);
        }

        return null;
    }

    /// <summary>Si la pregunta ya trae el discriminador de alguna de las opciones.</summary>
    private static bool YaDesambiguada(
        string preguntaEnmarcada, IReadOnlyList<ValorDelDominio> valores) =>
        valores.Any(valor => preguntaEnmarcada.Contains(
            $" {IndiceNormalizado(valor.Discriminador)} ", StringComparison.Ordinal));

    /// <summary>La pregunta autocontenida que queda si el usuario elige esta opción.</summary>
    private static string Resolver(string pregunta, ValorDelDominio valor) => valor.Clase switch
    {
        ClaseDeEntidad.Materia =>
            $"{pregunta.Trim()} Me refiero a la materia de la carrera {valor.Discriminador}.",

        ClaseDeEntidad.Persona =>
            $"{pregunta.Trim()} Me refiero a {valor.Discriminador}.",

        _ => pregunta.Trim(),
    };

    /// <summary>La pregunta normalizada y con espacios en los bordes.</summary>
    private static string Enmarcar(string texto) => $" {IndiceNormalizado(texto)} ";

    private static string IndiceNormalizado(string texto) =>
        string.Join(' ', NormalizadorLexico.Palabras(texto));
}
