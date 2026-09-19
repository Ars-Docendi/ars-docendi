namespace Modules.Asistente.Application;

/// <summary>Qué clase de cosa emitió el tokenizador.</summary>
internal enum ClaseDeToken
{
    /// <summary>Palabra sin comillas: puede ser una palabra clave o un nombre.</summary>
    Palabra,

    /// <summary>Contenido de un identificador entre comillas dobles.</summary>
    IdentificadorEntrecomillado,

    /// <summary>Separador de sentencias en el nivel superior.</summary>
    FinDeSentencia,
}

/// <summary>Un token con su clase y su texto ya en minúsculas.</summary>
internal readonly record struct TokenSql(ClaseDeToken Clase, string Texto);

/// <summary>
/// El texto tiene una construcción que no cierra: comilla, comentario o
/// delimitador de signo pesos sin su par.
/// </summary>
internal sealed class SqlSinCerrar(string que)
    : Exception($"La consulta tiene {que} sin cerrar.");

/// <summary>
/// Parte una consulta en tokens, emitiendo lo que el validador necesita ver.
/// </summary>
/// <remarks>
/// <b>La decisión entera de esta clase</b> es que el contenido de las comillas
/// dobles se <b>emite</b> como token propio.
///
/// En PostgreSQL las comillas dobles delimitan un <b>identificador</b>, no una
/// cadena: <c>"set_config"</c> sigue siendo la función <c>set_config</c>. Un
/// tokenizador que las trate como comillas de cadena y descarte su contenido
/// deja pasar exactamente eso —la función que escribe el ajuste del actor—, con
/// la que una consulta puede fijarse un actor distinto del suyo y saltear Row
/// Level Security. Está verificado sobre una base real en el prototipo previo:
/// 26 filas contra 138.
///
/// El contenido de los comentarios y de los literales de texto, en cambio, se
/// descarta: ahí las palabras no son código.
/// </remarks>
internal static class TokenizadorSql
{
    public static IReadOnlyList<TokenSql> Tokenizar(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);

        var tokens = new List<TokenSql>();
        var posicion = 0;

        while (posicion < sql.Length)
        {
            var caracter = sql[posicion];

            switch (caracter)
            {
                case '-' when Siguiente(sql, posicion) == '-':
                    posicion = SaltarComentarioDeLinea(sql, posicion);
                    break;

                case '/' when Siguiente(sql, posicion) == '*':
                    posicion = SaltarComentarioDeBloque(sql, posicion);
                    break;

                case '\'':
                    posicion = SaltarLiteralDeTexto(sql, posicion);
                    break;

                case 'e' or 'E' when Siguiente(sql, posicion) == '\'':
                    // Literal con escapes: E'...'. La E es parte del literal, no
                    // una palabra suelta.
                    posicion = SaltarLiteralDeTexto(sql, posicion + 1);
                    break;

                case 'u' or 'U'
                    when Siguiente(sql, posicion) == '&' && Siguiente(sql, posicion + 1) == '"':
                    posicion = LeerIdentificadorConEscapes(sql, posicion, tokens);
                    break;

                case '"':
                    posicion = LeerIdentificador(sql, posicion, tokens);
                    break;

                case '$' when EsAperturaDeSignoPesos(sql, posicion, out var etiqueta):
                    posicion = SaltarLiteralDeSignoPesos(sql, posicion, etiqueta);
                    break;

                case ';':
                    tokens.Add(new TokenSql(ClaseDeToken.FinDeSentencia, ";"));
                    posicion++;
                    break;

                default:
                    posicion = char.IsLetter(caracter) || caracter == '_'
                        ? LeerPalabra(sql, posicion, tokens)
                        : posicion + 1;
                    break;
            }
        }

        return tokens;
    }

    private static char Siguiente(string sql, int posicion) =>
        posicion + 1 < sql.Length ? sql[posicion + 1] : '\0';

    private static int SaltarComentarioDeLinea(string sql, int posicion)
    {
        var fin = sql.IndexOf('\n', posicion);
        return fin < 0 ? sql.Length : fin + 1;
    }

    /// <summary>
    /// Salta un comentario de bloque. PostgreSQL los <b>anida</b>, así que se
    /// lleva la cuenta: buscar el primer cierre dejaría afuera el resto de un
    /// comentario anidado y volvería a leer su contenido como código.
    /// </summary>
    private static int SaltarComentarioDeBloque(string sql, int posicion)
    {
        var profundidad = 0;

        while (posicion < sql.Length)
        {
            if (sql[posicion] == '/' && Siguiente(sql, posicion) == '*')
            {
                profundidad++;
                posicion += 2;
                continue;
            }

            if (sql[posicion] == '*' && Siguiente(sql, posicion) == '/')
            {
                profundidad--;
                posicion += 2;

                if (profundidad == 0)
                {
                    return posicion;
                }

                continue;
            }

            posicion++;
        }

        throw new SqlSinCerrar("un comentario de bloque");
    }

    /// <summary>
    /// Salta un literal de texto. Dos comillas simples seguidas son una comilla
    /// escapada y no cierran el literal.
    /// </summary>
    private static int SaltarLiteralDeTexto(string sql, int posicion)
    {
        posicion++;

        while (posicion < sql.Length)
        {
            if (sql[posicion] != '\'')
            {
                posicion++;
                continue;
            }

            if (Siguiente(sql, posicion) == '\'')
            {
                posicion += 2;
                continue;
            }

            return posicion + 1;
        }

        throw new SqlSinCerrar("un literal de texto");
    }

    /// <summary>
    /// Lee un identificador entrecomillado y lo <b>emite</b>. Dos comillas dobles
    /// seguidas son una comilla escapada dentro del identificador.
    /// </summary>
    private static int LeerIdentificador(string sql, int posicion, List<TokenSql> tokens)
    {
        posicion++;
        var contenido = new System.Text.StringBuilder();

        while (posicion < sql.Length)
        {
            if (sql[posicion] != '"')
            {
                contenido.Append(sql[posicion]);
                posicion++;
                continue;
            }

            if (Siguiente(sql, posicion) == '"')
            {
                contenido.Append('"');
                posicion += 2;
                continue;
            }

            tokens.Add(new TokenSql(
                ClaseDeToken.IdentificadorEntrecomillado,
                contenido.ToString().ToLowerInvariant()));

            return posicion + 1;
        }

        throw new SqlSinCerrar("un identificador entrecomillado");
    }

    /// <summary>
    /// Lee un identificador con escapes Unicode —<c>U&amp;"…"</c>— y lo emite ya
    /// <b>decodificado</b>.
    /// </summary>
    /// <remarks>
    /// PostgreSQL resuelve los escapes antes de resolver el nombre:
    /// <c>U&amp;"\0073et_config"</c> es la función <c>set_config</c>. Emitir el
    /// texto crudo le entrega a la lista negra una cadena que no se parece a
    /// ningún nombre prohibido, y deja pasar exactamente la función que fija el
    /// actor de la sesión —el ataque contra el que existe esta clase—.
    ///
    /// La cláusula <c>UESCAPE 'c'</c> viene <b>después</b> de la comilla de
    /// cierre, así que el carácter de escape no se conoce hasta terminar de leer:
    /// se junta el contenido crudo, se mira si sigue un <c>UESCAPE</c>, y recién
    /// ahí se decodifica.
    /// </remarks>
    private static int LeerIdentificadorConEscapes(string sql, int posicion, List<TokenSql> tokens)
    {
        // Saltea «U&"» y queda sobre el primer carácter del contenido.
        posicion += 3;
        var crudo = new System.Text.StringBuilder();

        while (posicion < sql.Length)
        {
            if (sql[posicion] != '"')
            {
                crudo.Append(sql[posicion]);
                posicion++;
                continue;
            }

            if (Siguiente(sql, posicion) == '"')
            {
                crudo.Append('"');
                posicion += 2;
                continue;
            }

            posicion++;
            var escape = LeerCaracterDeEscape(sql, ref posicion);

            tokens.Add(new TokenSql(
                ClaseDeToken.IdentificadorEntrecomillado,
                Decodificar(crudo.ToString(), escape).ToLowerInvariant()));

            return posicion;
        }

        throw new SqlSinCerrar("un identificador entrecomillado");
    }

    /// <summary>
    /// Si detrás del identificador viene <c>UESCAPE 'c'</c>, devuelve ese carácter
    /// y consume la cláusula. Si no, devuelve la barra invertida, que es el
    /// carácter de escape por defecto.
    /// </summary>
    private static char LeerCaracterDeEscape(string sql, ref int posicion)
    {
        const string palabra = "uescape";
        var recorrido = SaltarBlancos(sql, posicion);

        if (recorrido + palabra.Length > sql.Length
            || !sql.AsSpan(recorrido, palabra.Length).Equals(palabra, StringComparison.OrdinalIgnoreCase))
        {
            return '\\';
        }

        // «uescapex» no es la cláusula: es un nombre que empieza igual.
        var despues = recorrido + palabra.Length;
        if (despues < sql.Length && (char.IsLetterOrDigit(sql[despues]) || sql[despues] == '_'))
        {
            return '\\';
        }

        recorrido = SaltarBlancos(sql, despues);

        if (recorrido + 2 >= sql.Length || sql[recorrido] != '\'' || sql[recorrido + 2] != '\'')
        {
            return '\\';
        }

        var escape = sql[recorrido + 1];
        posicion = recorrido + 3;
        return escape;
    }

    private static int SaltarBlancos(string sql, int posicion)
    {
        while (posicion < sql.Length && char.IsWhiteSpace(sql[posicion]))
        {
            posicion++;
        }

        return posicion;
    }

    /// <summary>
    /// Resuelve las secuencias de escape de un identificador Unicode: la corta
    /// —<c>\XXXX</c>—, la larga —<c>\+XXXXXX</c>— y el carácter de escape
    /// duplicado, que vale por el carácter literal.
    /// </summary>
    /// <remarks>
    /// Una secuencia mal formada se emite <b>verbatim</b>. PostgreSQL la rechaza
    /// con error de sintaxis, así que esa consulta no llega a ejecutarse nunca:
    /// inventarle acá un nombre decodificado sería afirmar algo que el motor no
    /// va a ver.
    /// </remarks>
    private static string Decodificar(string crudo, char escape)
    {
        if (crudo.IndexOf(escape) < 0)
        {
            return crudo;
        }

        var decodificado = new System.Text.StringBuilder(crudo.Length);
        var posicion = 0;

        while (posicion < crudo.Length)
        {
            if (crudo[posicion] != escape)
            {
                decodificado.Append(crudo[posicion]);
                posicion++;
                continue;
            }

            if (posicion + 1 < crudo.Length && crudo[posicion + 1] == escape)
            {
                decodificado.Append(escape);
                posicion += 2;
                continue;
            }

            var largo = posicion + 1 < crudo.Length && crudo[posicion + 1] == '+';
            var inicio = posicion + (largo ? 2 : 1);
            var digitos = largo ? 6 : 4;

            if (inicio + digitos > crudo.Length
                || !int.TryParse(
                       crudo.AsSpan(inicio, digitos),
                       System.Globalization.NumberStyles.AllowHexSpecifier,
                       System.Globalization.CultureInfo.InvariantCulture,
                       out var punto)
                || punto > 0x10FFFF)
            {
                decodificado.Append(crudo[posicion]);
                posicion++;
                continue;
            }

            // Los sustitutos sueltos se agregan tal cual: dos escapes seguidos
            // forman el par en UTF-16 sin que haya que juntarlos a mano.
            decodificado.Append(punto <= char.MaxValue
                ? ((char)punto).ToString()
                : char.ConvertFromUtf32(punto));

            posicion = inicio + digitos;
        }

        return decodificado.ToString();
    }

    /// <summary>
    /// Distingue un delimitador de literal —<c>$$</c>, <c>$etiqueta$</c>— de un
    /// parámetro posicional —<c>$1</c>—.
    /// </summary>
    private static bool EsAperturaDeSignoPesos(string sql, int posicion, out string etiqueta)
    {
        etiqueta = string.Empty;
        var recorrido = posicion + 1;

        while (recorrido < sql.Length && (char.IsLetterOrDigit(sql[recorrido]) || sql[recorrido] == '_'))
        {
            // Una etiqueta no puede empezar con dígito: eso es un parámetro.
            if (recorrido == posicion + 1 && char.IsDigit(sql[recorrido]))
            {
                return false;
            }

            recorrido++;
        }

        if (recorrido >= sql.Length || sql[recorrido] != '$')
        {
            return false;
        }

        etiqueta = sql[posicion..(recorrido + 1)];
        return true;
    }

    private static int SaltarLiteralDeSignoPesos(string sql, int posicion, string etiqueta)
    {
        var cierre = sql.IndexOf(etiqueta, posicion + etiqueta.Length, StringComparison.Ordinal);

        return cierre < 0
            ? throw new SqlSinCerrar($"un literal delimitado por «{etiqueta}»")
            : cierre + etiqueta.Length;
    }

    private static int LeerPalabra(string sql, int posicion, List<TokenSql> tokens)
    {
        var inicio = posicion;

        while (posicion < sql.Length && (char.IsLetterOrDigit(sql[posicion]) || sql[posicion] == '_'))
        {
            posicion++;
        }

        tokens.Add(new TokenSql(
            ClaseDeToken.Palabra,
            sql[inicio..posicion].ToLowerInvariant()));

        return posicion;
    }
}
