using System.Globalization;
using System.Text;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Convierte lo leído del catálogo en el texto del prompt de sistema.
/// </summary>
/// <remarks>
/// El orden de todo lo que emite es determinista —esquema, tabla y número de
/// columna— porque el prefijo tiene que ser idéntico byte a byte entre procesos.
/// Un orden que dependiera del recorrido de un diccionario haría que la huella
/// cambiara sin que cambiara nada, y cada arranque pagaría escritura de caché en
/// lugar de lectura.
/// </remarks>
internal static class RenderizadorDeEsquema
{
    public static string Renderizar(
        IReadOnlyList<ColumnaLegible> columnas,
        IReadOnlyList<ReferenciaLegible> referencias,
        IReadOnlyList<VocabularioDeUnaColumna> vocabularios)
    {
        var texto = new StringBuilder(InstruccionesDeGeneracion.Instrucciones);
        texto.Append('\n');
        EscribirVocabulario(texto, vocabularios);
        texto.Append(InstruccionesDeGeneracion.EncabezadoDelEsquema);
        texto.Append('\n');

        var porTabla = columnas
            .GroupBy(c => (c.Esquema, c.Tabla))
            .OrderBy(g => g.Key.Esquema, StringComparer.Ordinal)
            .ThenBy(g => g.Key.Tabla, StringComparer.Ordinal);

        foreach (var tabla in porTabla)
        {
            EscribirTabla(texto, tabla.Key.Esquema, tabla.Key.Tabla, [.. tabla]);
        }

        EscribirReferencias(texto, referencias);

        return texto.ToString();
    }

    private static void EscribirVocabulario(
        StringBuilder texto, IReadOnlyList<VocabularioDeUnaColumna> vocabularios)
    {
        if (vocabularios.Count == 0)
        {
            return;
        }

        texto.Append(InstruccionesDeGeneracion.EncabezadoDelVocabulario);
        texto.Append('\n');

        foreach (var vocabulario in vocabularios)
        {
            texto.Append(CultureInfo.InvariantCulture,
                $"\n- {vocabulario.Cualificado}: "
                + $"{string.Join(", ", vocabulario.Valores.Select(v => $"'{v}'"))}");
        }

        texto.Append('\n');
    }

    private static void EscribirTabla(
        StringBuilder texto, string esquema, string tabla, IReadOnlyList<ColumnaLegible> columnas)
    {
        texto.Append(CultureInfo.InvariantCulture, $"\n## {esquema}.{tabla}\n");

        var descripcion = columnas[0].ComentarioDeTabla;
        if (!string.IsNullOrWhiteSpace(descripcion))
        {
            texto.Append(descripcion.Trim()).Append('\n');
        }

        foreach (var columna in columnas)
        {
            texto.Append(CultureInfo.InvariantCulture, $"- {columna.Columna} ({columna.Tipo}");
            if (!columna.Obligatoria)
            {
                texto.Append(", admite nulo");
            }

            texto.Append(')');

            if (!string.IsNullOrWhiteSpace(columna.ComentarioDeColumna))
            {
                texto.Append(": ").Append(columna.ComentarioDeColumna.Trim());
            }

            texto.Append('\n');
        }
    }

    private static void EscribirReferencias(
        StringBuilder texto, IReadOnlyList<ReferenciaLegible> referencias)
    {
        if (referencias.Count == 0)
        {
            return;
        }

        texto.Append("\n## Cómo se relacionan\n");

        var ordenadas = referencias
            .OrderBy(r => r.Esquema, StringComparer.Ordinal)
            .ThenBy(r => r.Tabla, StringComparer.Ordinal)
            .ThenBy(r => r.Columna, StringComparer.Ordinal);

        foreach (var referencia in ordenadas)
        {
            texto.Append(CultureInfo.InvariantCulture,
                $"- {referencia.Esquema}.{referencia.Tabla}.{referencia.Columna}"
                + $" referencia a {referencia.EsquemaReferido}.{referencia.TablaReferida}"
                + $".{referencia.ColumnaReferida}\n");
        }
    }
}
