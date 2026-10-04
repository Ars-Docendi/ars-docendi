using System.Globalization;
using System.Text;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Convierte el glosario en el bloque «GLOSARIO INSTITUCIONAL» del prefijo
/// (asistente-glosario-institucional, D6).
/// </summary>
/// <remarks>
/// El bloque sale de los campos ya interpretados y no de los bytes del archivo, y
/// no ordena nada: términos, sinónimos, referencias y valores van en el orden del
/// archivo. Nunca lee la base, el reloj ni el actor, así que es el mismo texto en
/// cada turno y para los dos roles de lectura.
///
/// <b>El literal sale de las referencias</b> —<c>col = 'v'</c> o <c>col IN (…)</c>—
/// y no se escribe aparte: el valor que viaja al modelo está en un solo lugar, el
/// mismo que verifica <c>GlosarioInstitucionalTests</c>.
/// </remarks>
internal static class RenderizadorDeGlosario
{
    /// <summary>
    /// Dice «valor exacto» y no «siempre»: es la misma idea que el bloque de valores
    /// posibles, y deja a las columnas de texto libre con la regla 8 del prompt.
    /// </summary>
    internal const string Introduccion =
        "Vocabulario del Departamento y el valor exacto que le corresponde en el esquema. "
        + "Cuando la pregunta use uno de estos términos, filtrá con la columna y el valor "
        + "indicados; no uses las palabras de la pregunta como literal.";

    public static string Renderizar(IReadOnlyList<TerminoDeGlosario> terminos)
    {
        var bloque = new StringBuilder();
        bloque.Append("\nGLOSARIO INSTITUCIONAL\n\n").Append(Introduccion).Append("\n\n");

        foreach (var termino in terminos)
        {
            EscribirTermino(bloque, termino);
        }

        return bloque.ToString();
    }

    private static void EscribirTermino(StringBuilder bloque, TerminoDeGlosario termino)
    {
        bloque.Append(CultureInfo.InvariantCulture, $"- «{termino.Termino}»");

        if (termino.Sinonimos.Count > 0)
        {
            bloque.Append(CultureInfo.InvariantCulture,
                $" (también: {string.Join(", ", termino.Sinonimos.Select(s => $"«{s}»"))})");
        }

        // Una conjunción de igualdades se escribe con AND; si alguna referencia no
        // fija un valor, es una lista de columnas y se separa con comas.
        var separador = termino.Referencias.All(r => r.Valores.Count > 0) ? " AND " : ", ";

        bloque.Append(CultureInfo.InvariantCulture,
            $": {termino.Explicacion} → {string.Join(separador, termino.Referencias.Select(Referencia))}");

        if (termino.Sql is not null)
        {
            bloque.Append(CultureInfo.InvariantCulture, $"; SQL: {termino.Sql}");
        }

        bloque.Append('\n');
    }

    private static string Referencia(ReferenciaDeGlosario referencia) => referencia.Valores.Count switch
    {
        0 => referencia.Columna,
        1 => $"{referencia.Columna} = {Literal(referencia.Valores[0])}",
        _ => $"{referencia.Columna} IN ({string.Join(", ", referencia.Valores.Select(Literal))})",
    };

    private static string Literal(string valor) =>
        $"'{valor.Replace("'", "''", StringComparison.Ordinal)}'";
}
