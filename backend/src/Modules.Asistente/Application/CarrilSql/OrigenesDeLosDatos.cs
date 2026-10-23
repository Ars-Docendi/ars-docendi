namespace Modules.Asistente.Application;

/// <summary>
/// De qué áreas de datos salió una respuesta, a partir de la consulta que se ejecutó.
/// </summary>
/// <remarks>
/// <b>Devuelve códigos de schema</b> —<c>designaciones</c>, <c>identity</c>,
/// <c>portal</c>— y no frases: cómo se llama cada área para una persona lo decide el
/// cliente, que omite las que no conoce.
///
/// <b>Sólo cuenta lo calificado.</b> Una palabra es un schema si es uno de los
/// habilitados Y va seguida de un punto: <c>designaciones.pedidos</c> cita el schema,
/// y una columna o un alias que se llamara igual no. Se recorre el flujo del
/// tokenizador y no el texto, para que un comentario o un literal que nombre un schema
/// no declare un área que la consulta no leyó. Un identificador entre comillas
/// dobles no se cuenta: se declara de menos, que es la dirección inofensiva (la
/// respuesta calla un área, no inventa una).
///
/// <b>No informa tablas, ni la consulta, ni cuántas filas.</b> Los códigos de schema
/// son lo único que sale: son etiquetas internas, y el contrato no revela el resto
/// (design.md D4 de asistente-respuestas-con-vinculos).
///
/// Sin consulta ejecutada —aclaración, rechazo, error— no hay nada que declarar.
/// </remarks>
internal static class OrigenesDeLosDatos
{
    public static IReadOnlyList<string> De(string? sqlEjecutado)
    {
        if (string.IsNullOrWhiteSpace(sqlEjecutado))
        {
            return [];
        }

        IReadOnlyList<TokenSql> tokens;
        try
        {
            tokens = TokenizadorSql.Tokenizar(sqlEjecutado);
        }
        catch (SqlSinCerrar)
        {
            // Una consulta validada no llega acá sin cerrar; si pasara, no se declara
            // nada antes que declarar a ciegas.
            return [];
        }

        var origenes = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var token in tokens)
        {
            if (token.Clase == ClaseDeToken.Palabra
                && EtiquetasDeAreas.SchemasHabilitados.Contains(token.Texto)
                && token.Inicio >= 0
                && token.Inicio + token.Texto.Length < sqlEjecutado.Length
                && sqlEjecutado[token.Inicio + token.Texto.Length] == '.')
            {
                origenes.Add(token.Texto);
            }
        }

        return [.. origenes];
    }
}
