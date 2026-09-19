namespace Modules.Asistente.Application;

/// <summary>Qué clase de cosa del dominio es un valor del índice.</summary>
public enum ClaseDeEntidad
{
    /// <summary>Una materia.</summary>
    Materia,

    /// <summary>Una persona, indexada por su apellido.</summary>
    Persona,
}

/// <summary>
/// Un valor del dominio que existe en la base, listo para buscar en una pregunta.
/// </summary>
/// <param name="Clase">Materia o persona.</param>
/// <param name="Valor">El valor tal como está en la base.</param>
/// <param name="Termino">
/// El valor normalizado —minúscula, sin acentos, sin puntuación— que es contra lo
/// que se busca en la pregunta del usuario.
/// </param>
/// <param name="Discriminador">
/// Lo que distingue a este valor de los que comparten su término: la carrera de una
/// materia, el nombre completo de una persona.
/// </param>
public sealed record ValorDelDominio(
    ClaseDeEntidad Clase,
    string Valor,
    string Termino,
    string Discriminador);

/// <summary>
/// Los valores del dominio y sus colisiones, tal como están en la base.
/// </summary>
/// <remarks>
/// Lo comparten el detector de ambigüedad y el de cambio de tema, y lo va a
/// necesitar el enrutador de dominio de la épica siguiente para resolver sus slots.
/// Tres piezas con tres copias del mismo índice serían tres oportunidades de que
/// una quede vieja.
/// </remarks>
public sealed class CatalogoDeEntidades
{
    private readonly Dictionary<string, IReadOnlyList<ValorDelDominio>> _porTermino;

    public CatalogoDeEntidades(IEnumerable<ValorDelDominio> valores)
    {
        _porTermino = valores
            .GroupBy(valor => valor.Termino, StringComparer.Ordinal)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => (IReadOnlyList<ValorDelDominio>)[.. grupo],
                StringComparer.Ordinal);
    }

    /// <summary>Todos los términos indexados.</summary>
    public IReadOnlyCollection<string> Terminos => _porTermino.Keys;

    /// <summary>Los términos que corresponden a más de un valor distinto.</summary>
    public IEnumerable<string> Colisiones =>
        _porTermino
            .Where(par => par.Value.Count > 1)
            .Select(par => par.Key);

    /// <summary>Los valores de un término. Vacío si no está indexado.</summary>
    public IReadOnlyList<ValorDelDominio> Valores(string termino) =>
        _porTermino.TryGetValue(termino, out var valores) ? valores : [];

    /// <summary>Si el término corresponde a más de un valor.</summary>
    public bool Colisiona(string termino) => Valores(termino).Count > 1;
}

/// <summary>Carga el catálogo de entidades desde la base y lo cachea.</summary>
public interface IIndiceDeEntidades
{
    /// <summary>Devuelve el catálogo, construyéndolo la primera vez.</summary>
    Task<CatalogoDeEntidades> ObtenerAsync(CancellationToken ct);
}
