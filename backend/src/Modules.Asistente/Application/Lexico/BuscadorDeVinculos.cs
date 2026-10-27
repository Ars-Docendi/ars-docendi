namespace Modules.Asistente.Application;

/// <summary>
/// De las filas de un resultado a los candidatos a vínculo, y de los candidatos resueltos a los
/// vínculos ubicados.
/// </summary>
internal static class BuscadorDeVinculos
{
    /// <summary>Largo máximo de un candidato.</summary>
    internal const int LargoMaximoDelCandidato = 40;

    /// <summary>Cuántos candidatos distintos se proponen como mucho.</summary>
    internal const int TopeDeCandidatos = 200;

    /// <summary>Los valores de celda que podrían ser el identificador de algo.</summary>
    public static IReadOnlyList<string> Candidatos(IReadOnlyList<IReadOnlyList<object?>> filas)
    {
        ArgumentNullException.ThrowIfNull(filas);

        var vistos = new HashSet<string>(StringComparer.Ordinal);

        foreach (var fila in filas)
        {
            foreach (var valor in fila)
            {
                if (EsCandidato(valor, out var texto) && vistos.Count < TopeDeCandidatos)
                {
                    vistos.Add(texto);
                }
            }
        }

        return [.. vistos];
    }

    /// <summary>Ubica en el resultado cada candidato que resolvió.</summary>
    public static IReadOnlyList<VinculoDelResultado> Ubicar(
        IReadOnlyList<IReadOnlyList<object?>> filas,
        IReadOnlyDictionary<string, DestinoDelVinculo> destinos)
    {
        ArgumentNullException.ThrowIfNull(filas);
        ArgumentNullException.ThrowIfNull(destinos);

        if (destinos.Count == 0)
        {
            return [];
        }

        var vinculos = new List<VinculoDelResultado>();

        for (var fila = 0; fila < filas.Count; fila++)
        {
            var celdas = filas[fila];

            for (var columna = 0; columna < celdas.Count; columna++)
            {
                if (EsCandidato(celdas[columna], out var texto)
                    && destinos.TryGetValue(texto, out var destino))
                {
                    vinculos.Add(
                        new VinculoDelResultado(fila, columna, destino.Tipo, destino.Id));
                }
            }
        }

        return vinculos;
    }

    /// <summary>Si el valor de una celda tiene forma de identificador.</summary>
    private static bool EsCandidato(object? valor, out string texto)
    {
        texto = string.Empty;

        if (valor is not string crudo)
        {
            return false;
        }

        if (crudo.Length is 0 or > LargoMaximoDelCandidato)
        {
            return false;
        }

        if (crudo.Any(char.IsWhiteSpace))
        {
            return false;
        }

        texto = crudo;
        return true;
    }
}
