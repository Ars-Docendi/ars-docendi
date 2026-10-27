namespace Modules.Asistente.Application;

/// <summary>Qué clase de cosa puede exigir un slot de una intención.</summary>
public enum ClaseDeSlot
{
    /// <summary>Una materia, del índice de entidades.</summary>
    Materia,

    /// <summary>Una persona por su apellido, del índice de entidades.</summary>
    Persona,

    /// <summary>Un estado del trámite.</summary>
    Estado,

    /// <summary>Un tipo de novedad del pedido.</summary>
    Novedad,

    /// <summary>Un motivo de baja.</summary>
    TipoDeBaja,

    /// <summary>Un cargo docente, por su nombre o su abreviatura.</summary>
    Cargo,
}

/// <summary>Un valor del dominio que puede ocupar un slot.</summary>
public sealed record ValorDeSlot(
    ClaseDeSlot Clase, string Valor, string Termino, string Discriminador);

/// <summary>Todos los valores que pueden ocupar un slot, indexados por término.</summary>
public sealed class CatalogoDelDominio
{
    private readonly Dictionary<string, IReadOnlyList<ValorDeSlot>> _porTermino;
    private readonly Dictionary<ClaseDeSlot, IReadOnlyList<string>> _terminosPorClase;

    public CatalogoDelDominio(IEnumerable<ValorDeSlot> valores)
    {
        ArgumentNullException.ThrowIfNull(valores);

        var distintos = valores
            .GroupBy(valor => (valor.Clase, valor.Termino, valor.Discriminador))
            .Select(grupo => grupo.First())
            .ToList();

        _porTermino = distintos
            .GroupBy(valor => valor.Termino, StringComparer.Ordinal)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => (IReadOnlyList<ValorDeSlot>)[.. grupo],
                StringComparer.Ordinal);

        // De mayor a menor longitud, para que «bases de datos» le gane a «datos» cuando los dos
        // estén indexados.
        _terminosPorClase = distintos
            .GroupBy(valor => valor.Clase)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => (IReadOnlyList<string>)[.. grupo
                    .Select(valor => valor.Termino)
                    .Distinct(StringComparer.Ordinal)
                    .OrderByDescending(termino => termino.Length)
                    .ThenBy(termino => termino, StringComparer.Ordinal)]);
    }

    /// <summary>Todos los términos indexados.</summary>
    public IReadOnlyCollection<string> Terminos => _porTermino.Keys;

    /// <summary>Los términos de una clase, de más largo a más corto.</summary>
    public IReadOnlyList<string> TerminosDe(ClaseDeSlot clase) =>
        _terminosPorClase.TryGetValue(clase, out var terminos) ? terminos : [];

    /// <summary>El único valor de esa clase que corresponde al término, o nulo.</summary>
    public ValorDeSlot? Unico(ClaseDeSlot clase, string termino)
    {
        if (!_porTermino.TryGetValue(termino, out var valores))
        {
            return null;
        }

        var deLaClase = valores.Where(valor => valor.Clase == clase).Take(2).ToList();

        return deLaClase.Count == 1 ? deLaClase[0] : null;
    }
}

/// <summary>Carga el catálogo del dominio y lo cachea.</summary>
public interface ICatalogoDelDominio
{
    /// <summary>Devuelve el catálogo, construyéndolo la primera vez.</summary>
    Task<CatalogoDelDominio> ObtenerAsync(CancellationToken ct);
}
