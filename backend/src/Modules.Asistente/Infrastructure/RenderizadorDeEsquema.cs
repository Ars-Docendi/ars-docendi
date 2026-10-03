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
    /// <param name="compacto">
    /// La forma compacta (asistente-optimizaciones-modelo-local, D2): mismo
    /// contenido en menos texto. <c>false</c> —el default— produce BYTE A BYTE
    /// el prefijo de siempre, que es el que sellan los cassettes.
    /// </param>
    public static string Renderizar(
        IReadOnlyList<ColumnaLegible> columnas,
        IReadOnlyList<ReferenciaLegible> referencias,
        IReadOnlyList<VocabularioDeUnaColumna> vocabularios,
        bool compacto = false)
    {
        var texto = new StringBuilder(InstruccionesDeGeneracion.Instrucciones);
        texto.Append('\n');
        EscribirVocabulario(texto, vocabularios);
        texto.Append(compacto
            ? InstruccionesDeGeneracion.EncabezadoDelEsquemaCompacto
            : InstruccionesDeGeneracion.EncabezadoDelEsquema);
        texto.Append('\n');

        if (compacto)
        {
            EscribirCompacto(texto, columnas, referencias);
            return texto.ToString();
        }

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

    /// <summary>
    /// Las tablas en forma compacta, con cada clave foránea en la línea de su
    /// columna (D2).
    /// </summary>
    /// <remarks>
    /// Lo único que se omite es el comentario de una columna <c>id</c> que sólo
    /// dice «Identificador de …»: el nombre ya lo dice. Todo otro comentario y
    /// toda descripción de tabla viajan enteros, porque ahí están las reglas del
    /// dominio —«vigente_hasta NULO es vigencia abierta»— que un modelo chico
    /// necesita más que uno grande.
    /// </remarks>
    private static void EscribirCompacto(
        StringBuilder texto,
        IReadOnlyList<ColumnaLegible> columnas,
        IReadOnlyList<ReferenciaLegible> referencias)
    {
        var referidas = referencias
            .GroupBy(r => (r.Esquema, r.Tabla, r.Columna))
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(r => r.EsquemaReferido, StringComparer.Ordinal)
                    .ThenBy(r => r.TablaReferida, StringComparer.Ordinal)
                    .First());

        var porTabla = columnas
            .GroupBy(c => (c.Esquema, c.Tabla))
            .OrderBy(g => g.Key.Esquema, StringComparer.Ordinal)
            .ThenBy(g => g.Key.Tabla, StringComparer.Ordinal);

        foreach (var tabla in porTabla)
        {
            var lista = tabla.ToList();
            texto.Append(CultureInfo.InvariantCulture, $"\n## {tabla.Key.Esquema}.{tabla.Key.Tabla}\n");

            if (!string.IsNullOrWhiteSpace(lista[0].ComentarioDeTabla))
            {
                texto.Append(lista[0].ComentarioDeTabla!.Trim()).Append('\n');
            }

            foreach (var columna in lista)
            {
                texto.Append(CultureInfo.InvariantCulture,
                    $"- {columna.Columna} {TipoCompacto(columna.Tipo)}{(columna.Obligatoria ? string.Empty : "?")}");

                if (referidas.TryGetValue((columna.Esquema, columna.Tabla, columna.Columna), out var referida))
                {
                    texto.Append(CultureInfo.InvariantCulture,
                        $" → {referida.EsquemaReferido}.{referida.TablaReferida}.{referida.ColumnaReferida}");
                }

                var comentario = columna.ComentarioDeColumna?.Trim();
                var redundante = columna.Columna == "id"
                    && comentario?.StartsWith("Identificador", StringComparison.Ordinal) == true;

                if (!string.IsNullOrEmpty(comentario) && !redundante)
                {
                    texto.Append(": ").Append(comentario);
                }

                texto.Append('\n');
            }
        }
    }

    /// <summary>Los nombres largos de tipos de PostgreSQL, en su forma corta.</summary>
    internal static string TipoCompacto(string tipo) => tipo switch
    {
        "timestamp with time zone" => "timestamptz",
        "timestamp without time zone" => "timestamp",
        "time without time zone" => "time",
        "character varying" => "varchar",
        "integer" => "int",
        "bigint" => "int8",
        "smallint" => "int2",
        "boolean" => "bool",
        "double precision" => "float8",
        _ => tipo,
    };

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
