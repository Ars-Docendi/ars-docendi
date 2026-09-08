namespace Modules.Asistente.Application;

/// <summary>
/// Detecta choques de valores contra el índice del dominio y arma el menú de
/// aclaración.
/// </summary>
/// <remarks>
/// <b>Con una consulta, no con el modelo.</b> Es el precedente arquitectónico del
/// proyecto: cuando el dominio permite resolver con un <c>SELECT</c>, se prefiere
/// determinismo a juicio del modelo. El mismo criterio es el que después justifica
/// el enrutador de intención y el de dominio.
///
/// <b>No se extiende a la vaguedad</b>, y es una restricción, no una omisión.
/// Preguntar tiene un costo medido: las aclaraciones de calidad baja y media son
/// peores que no preguntar. Este detector dispara solo ante una colisión verificada
/// por consulta; llevarlo a «esta pregunta me parece vaga» lo devuelve al terreno
/// del acierto parcial y le pega justo a la precisión, que es la métrica primaria.
///
/// Puro sobre el catálogo: quien lo carga es el índice.
/// </remarks>
public static class DetectorDeAmbiguedad
{
    /// <summary>
    /// Devuelve la aclaración que hace falta, o <c>null</c> si la pregunta no es
    /// ambigua.
    /// </summary>
    public static Aclaracion? Detectar(string pregunta, CatalogoDeEntidades catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);

        if (string.IsNullOrWhiteSpace(pregunta))
        {
            return null;
        }

        var normalizada = Enmarcar(pregunta);

        // De mayor a menor longitud: «analisis matematico» tiene que ganarle a
        // «analisis» si los dos estuvieran indexados, o se ofrecería el menú
        // equivocado.
        var candidatos = catalogo.Colisiones
            .Where(termino => normalizada.Contains($" {termino} ", StringComparison.Ordinal))
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

    /// <summary>
    /// Si la pregunta ya trae el discriminador de alguna de las opciones.
    /// </summary>
    /// <remarks>
    /// «¿Quiénes dan Análisis Matemático en Ingeniería en Informática?» nombra la
    /// materia ambigua y también su carrera: preguntar sería pedirle al usuario que
    /// repita lo que acaba de decir.
    /// </remarks>
    private static bool YaDesambiguada(
        string preguntaEnmarcada, IReadOnlyList<ValorDelDominio> valores) =>
        valores.Any(valor => preguntaEnmarcada.Contains(
            $" {IndiceNormalizado(valor.Discriminador)} ", StringComparison.Ordinal));

    /// <summary>
    /// La pregunta autocontenida que queda si el usuario elige esta opción.
    /// </summary>
    /// <remarks>
    /// Se agrega una oración en vez de sustituir el término adentro de la pregunta.
    /// Sustituir exigiría ubicar el tramo original —y la normalización que permite
    /// encontrarlo pierde las posiciones—, así que sería una cirugía frágil sobre
    /// el texto del usuario. Agregar una aclaración es lo que haría una persona, se
    /// lee bien si se la muestra, y le da al generador el dato que necesita.
    /// </remarks>
    private static string Resolver(string pregunta, ValorDelDominio valor) => valor.Clase switch
    {
        ClaseDeEntidad.Materia =>
            $"{pregunta.Trim()} Me refiero a la materia de la carrera {valor.Discriminador}.",

        ClaseDeEntidad.Persona =>
            $"{pregunta.Trim()} Me refiero a {valor.Discriminador}.",

        _ => pregunta.Trim(),
    };

    /// <summary>La pregunta normalizada y con espacios en los bordes.</summary>
    /// <remarks>
    /// Los espacios de los bordes son lo que permite buscar <c>" termino "</c> y
    /// que un término al principio o al final coincida igual, sin que «ana» matchee
    /// adentro de «anales».
    /// </remarks>
    private static string Enmarcar(string texto) => $" {IndiceNormalizado(texto)} ";

    private static string IndiceNormalizado(string texto) =>
        string.Join(' ', NormalizadorLexico.Palabras(texto));
}
