namespace Modules.Asistente.Application;

/// <summary>
/// De qué áreas de datos salió una respuesta, a partir de la consulta que se ejecutó.
/// </summary>
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
