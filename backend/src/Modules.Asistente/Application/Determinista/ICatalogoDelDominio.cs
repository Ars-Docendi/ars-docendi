namespace Modules.Asistente.Application;

/// <summary>Qué clase de cosa puede exigir un slot de una intención.</summary>
/// <remarks>
/// Es un enum y no una cadena para que una intención con una clase inventada no
/// cargue. El catálogo de intenciones es un archivo, así que sin este cierre el
/// error se descubriría recién cuando un slot no resolviera nunca.
/// </remarks>
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

/// <summary>
/// Un valor del dominio que puede ocupar un slot.
/// </summary>
/// <param name="Clase">Qué clase de cosa es.</param>
/// <param name="Valor">El valor tal como está en la base.</param>
/// <param name="Termino">
/// El valor normalizado, que es contra lo que se busca en la pregunta.
/// </param>
/// <param name="Discriminador">
/// Lo que distingue a este valor de los que comparten su término: la carrera de una
/// materia, el nombre completo de una persona.
/// <b>Sin él dos personas apellidadas igual serían el mismo valor</b>, la colisión
/// no se vería y el carril enrutaría con el López equivocado.
/// </param>
public sealed record ValorDeSlot(
    ClaseDeSlot Clase, string Valor, string Termino, string Discriminador);

/// <summary>
/// Todos los valores que pueden ocupar un slot, indexados por término.
/// </summary>
/// <remarks>
/// Compone dos fuentes que se cargan por separado: el índice de entidades
/// —materias y personas, que ya existía— y el vocabulario cerrado del trámite
/// —estados, novedades, tipos de baja y cargos—.
///
/// <b>Compone en lugar de ampliar</b>, y no es un detalle de organización.
/// <see cref="CatalogoDeEntidades"/> lo consumen el detector de ambigüedad y el de
/// cambio de tema: el primero dispara cuando un término colisiona, el segundo mide
/// el solapamiento de entidades entre dos preguntas. Meterles «borrador», «Alta» y
/// «Titular» adentro les cambiaría el comportamiento en silencio, porque palabras
/// que hoy son texto común pasarían a ser entidades del dominio. Componiendo
/// afuera, los dos quedan intactos por construcción y no por un test que lo vigile.
/// </remarks>
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

        // De mayor a menor longitud, para que «bases de datos» le gane a «datos»
        // cuando los dos estén indexados. Es el mismo orden que usa el detector de
        // ambigüedad, y por el mismo motivo.
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

    /// <summary>
    /// El único valor de esa clase que corresponde al término, o nulo.
    /// </summary>
    /// <remarks>
    /// <b>Devuelve nulo también cuando hay más de uno</b>, y esa es la regla que
    /// hace honesto al carril: con dos Pérez, enrutar con uno de los dos devuelve
    /// las filas del otro, y esa respuesta es indistinguible de la correcta para
    /// quien preguntó. No resolver manda la pregunta al carril que sí puede
    /// responderla —o pedir la aclaración—, que es el default seguro de la épica.
    ///
    /// Distintos se cuentan por discriminador y no por valor: dos personas
    /// apellidadas igual comparten el valor, y contarlas por él las volvería una.
    /// </remarks>
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
