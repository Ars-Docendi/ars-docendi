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
        var salen = columnasClave
            .Where(c => filas.All(f => f[c] is null || (f[c] is string s && Guid.TryParse(s, out _))))
            .Where(c => vinculos.Where(v => v.EsClave && v.Columna == c)
                .All(v => Rotulo(filas[v.Fila], columnasClave) is not null))
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
                ? Rotulo(filas[v.Fila], columnasClave)!.Value
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

    /// <summary>La primera celda de texto de la fila que no es una clave.</summary>
    private static int? Rotulo(IReadOnlyList<object?> fila, HashSet<int> claves)
    {
        for (var c = 0; c < fila.Count; c++)
        {
            if (!claves.Contains(c) && fila[c] is string { Length: > 0 })
            {
                return c;
            }
        }

        return null;
    }
}
