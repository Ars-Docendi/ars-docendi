namespace Modules.Asistente.Application;

/// <summary>
/// Las columnas que sólo existen para construir un vínculo (el uuid de una persona): el vínculo
/// pasa al rótulo de la fila y la columna sale del resultado.
/// </summary>
internal static class ClavesInternas
{
    /// <summary>El resultado sin las columnas de clave que se pudieron reemplazar, y sus vínculos.</summary>
    public static (ResultadoDelTurno Resultado, IReadOnlyList<VinculoDelResultado> Vinculos) Quitar(
        ResultadoDelTurno resultado, IReadOnlyList<VinculoDelResultado> vinculos)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        ArgumentNullException.ThrowIfNull(vinculos);

        var columnasClave = vinculos.Where(v => v.EsClave).Select(v => v.Columna).ToHashSet();

        if (columnasClave.Count == 0)
        {
            return (resultado, vinculos);
        }

        var filas = resultado.Filas;

        // Una columna sale sólo si no queda un uuid a la vista (todo lo que tiene es un uuid) y
        // cada vínculo que lleva tiene dónde ir. Si no, se queda, vinculada: mejor un uuid
        // enlazado que un vínculo huérfano.
        // A cell that already carries another link (a pedido number) is not a label: two links in
        // one cell collapse into one on screen and the other one is lost.
        var ocupadas = vinculos.Where(v => !v.EsClave).Select(v => (v.Fila, v.Columna)).ToHashSet();

        int? RotuloLibre(int fila) => Rotulo(filas[fila], columnasClave, c => ocupadas.Contains((fila, c)));

        var salen = columnasClave
            .Where(c => filas.All(f => f[c] is null or Guid || (f[c] is string s && Guid.TryParse(s, out _))))
            .Where(c => vinculos.Where(v => v.EsClave && v.Columna == c)
                .All(v => RotuloLibre(v.Fila) is not null))
            .ToHashSet();

        if (salen.Count == 0)
        {
            return (resultado, vinculos);
        }

        var quedan = Enumerable.Range(0, resultado.Columnas.Count).Where(c => !salen.Contains(c)).ToList();
        var nuevoIndice = quedan.Select((original, nuevo) => (original, nuevo))
            .ToDictionary(par => par.original, par => par.nuevo);

        var nuevosVinculos = vinculos.Select(v =>
        {
            var destino = salen.Contains(v.Columna)
                ? RotuloLibre(v.Fila)!.Value
                : v.Columna;
            return v with { Columna = nuevoIndice[destino] };
        }).ToList();

        var nuevo = resultado with
        {
            Columnas = [.. quedan.Select(c => resultado.Columnas[c])],
            Filas = [.. filas.Select(f => (IReadOnlyList<object?>)[.. quedan.Select(c => f[c])])],
            Sensibilidad = [.. quedan.Select(c => c < resultado.Sensibilidad.Count
                ? resultado.Sensibilidad[c]
                : SensibilidadDeColumna.Publica)],
        };

        return (nuevo, nuevosVinculos);
    }

    /// <summary>La primera celda de texto de la fila que no es una clave ni lleva ya otro vínculo.</summary>
    private static int? Rotulo(IReadOnlyList<object?> fila, HashSet<int> claves, Func<int, bool> ocupada)
    {
        for (var c = 0; c < fila.Count; c++)
        {
            if (!claves.Contains(c) && !ocupada(c) && fila[c] is string { Length: > 0 })
            {
                return c;
            }
        }

        return null;
    }
}
