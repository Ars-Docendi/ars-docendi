using System.Globalization;
using System.Text;

namespace Modules.Asistente.Application;

/// <summary>
/// Detecta una generación que es copia literal de un ejemplo del catálogo sin que
/// el turno haya pedido ese ejemplo.
/// </summary>
/// <remarks>
/// Un modelo chico, ante un mensaje que no puede responder («status» repetido en
/// el mismo hilo), a veces devuelve el primer ejemplo del catálogo tal cual y
/// declara que es contestable: la consulta valida, ejecuta y el usuario recibe una
/// respuesta segura sobre una materia que nunca nombró. Copiar un ejemplo es
/// legítimo sólo si la pregunta lo pide; si no, la generación se trata como una
/// abstención.
///
/// La evidencia de que lo pidió es léxica y determinista: todos los literales del
/// ejemplo aparecen en el texto del turno o, si no tiene literales, la pregunta se
/// parece a la del ejemplo según el mismo selector que lo ofrece al modelo.
/// </remarks>
internal static class CopiaDeEjemplo
{
    /// <summary>El ejemplo del catálogo que la generación copió sin que se lo pidieran, o nulo.</summary>
    /// <param name="sql">La consulta generada.</param>
    /// <param name="textoDelTurno">
    /// Todo lo que el turno dijo: la pregunta, la pregunta interpretada por el
    /// modelo y los nombres de las entidades mencionadas.
    /// </param>
    public static EjemploSql? CopiadoSinPedirlo(
        string sql, IReadOnlyList<string> textoDelTurno, ISelectorDeEjemplos ejemplos)
    {
        var normalizada = Normalizar(sql);
        var plegado = Plegar(string.Join('\n', textoDelTurno));

        foreach (var ejemplo in ejemplos.Catalogo)
        {
            if (!string.Equals(normalizada, Normalizar(ejemplo.Sql), StringComparison.Ordinal))
            {
                continue;
            }

            var literales = Literales(ejemplo.Sql);
            var loPidio = literales.Count > 0
                ? literales.All(literal => plegado.Contains(Plegar(literal), StringComparison.Ordinal))
                : textoDelTurno.Any(texto => ejemplos.EsParecida(texto, ejemplo));

            return loPidio ? null : ejemplo;
        }

        return null;
    }

    /// <summary>
    /// Espacios colapsados, sin <c>;</c> final y en minúsculas fuera de los
    /// literales: lo que no cambia lo que la consulta hace.
    /// </summary>
    private static string Normalizar(string sql)
    {
        var resultado = new StringBuilder(sql.Length);
        var enLiteral = false;
        var espacio = false;

        foreach (var caracter in sql.Trim().TrimEnd(';').TrimEnd())
        {
            if (caracter == '\'')
            {
                enLiteral = !enLiteral;
            }

            if (!enLiteral && char.IsWhiteSpace(caracter))
            {
                espacio = true;
                continue;
            }

            if (espacio && resultado.Length > 0)
            {
                resultado.Append(' ');
            }

            espacio = false;
            resultado.Append(enLiteral ? caracter : char.ToLowerInvariant(caracter));
        }

        return resultado.ToString();
    }

    private static List<string> Literales(string sql)
    {
        var literales = new List<string>();
        var actual = new StringBuilder();
        var enLiteral = false;

        for (var i = 0; i < sql.Length; i++)
        {
            if (sql[i] != '\'')
            {
                if (enLiteral)
                {
                    actual.Append(sql[i]);
                }

                continue;
            }

            if (enLiteral && i + 1 < sql.Length && sql[i + 1] == '\'')
            {
                actual.Append('\'');
                i++;
            }
            else if (enLiteral)
            {
                literales.Add(actual.ToString());
                actual.Clear();
                enLiteral = false;
            }
            else
            {
                enLiteral = true;
            }
        }

        return literales;
    }

    /// <summary>Minúsculas y sin acentos, para comparar sin que una tilde decida.</summary>
    private static string Plegar(string texto)
    {
        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        var plegado = new StringBuilder(descompuesto.Length);

        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
            {
                plegado.Append(char.ToLowerInvariant(caracter));
            }
        }

        return plegado.ToString();
    }
}
