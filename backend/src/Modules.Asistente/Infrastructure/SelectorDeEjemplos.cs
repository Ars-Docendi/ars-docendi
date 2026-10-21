using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Modules.Asistente.Application;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Selector por solapamiento de términos normalizados.
/// </summary>
/// <remarks>
/// El parecido es el índice de Jaccard sobre los conjuntos de términos: los
/// términos compartidos sobre los términos distintos que hay entre las dos.
/// Dividir por la unión y no solo por la intersección es lo que impide que un
/// ejemplo muy largo gane por acumulación.
/// </remarks>
internal sealed class SelectorDeEjemplos : ISelectorDeEjemplos
{
    /// <summary>Ruta lógica del recurso embebido con el catálogo.</summary>
    public const string RecursoCatalogo = "Modules.Asistente.Recursos.ejemplos-sql.json";

    /// <summary>Cuántos ejemplos entran como mucho en el prompt de usuario.</summary>
    /// <remarks>
    /// El tope existe por costo: cada ejemplo es texto que se paga en el prompt
    /// variable, que no se cachea. Cuatro alcanzan para mostrar la forma de la
    /// respuesta y las convenciones del esquema.
    /// </remarks>
    private const int TopeDeEjemplos = 4;

    /// <summary>
    /// Parecido mínimo para que un ejemplo entre.
    /// </summary>
    /// <remarks>
    /// Con términos normalizados y sin palabras vacías, compartir un solo término
    /// del dominio sobre una decena ya da alrededor de este valor. Por debajo, lo
    /// que comparten es casualidad.
    /// </remarks>
    private const double ParecidoMinimo = 0.08;

    private readonly IReadOnlyList<EjemploIndexado> _indexados;

    public SelectorDeEjemplos()
    {
        var crudo = LeerRecurso();

        var archivo = JsonSerializer.Deserialize<ArchivoDeEjemplos>(crudo)
            ?? throw new InvalidOperationException(
                "El catálogo de ejemplos del asistente está vacío o no se pudo interpretar.");

        if (archivo.Ejemplos.Count == 0)
        {
            throw new InvalidOperationException(
                "El catálogo de ejemplos del asistente no tiene ningún ejemplo.");
        }

        Catalogo = [.. archivo.Ejemplos.Select(e => new EjemploSql(e.Pregunta, e.Sql, e.Categoria))];
        _indexados = [.. Catalogo.Select(e => new EjemploIndexado(e, NormalizadorLexico.Terminos(e.Pregunta)))];

        // La huella se calcula sobre los bytes del recurso, no sobre el objeto ya
        // interpretado: así un cambio de formato que no altere el contenido
        // igual queda registrado, que es lo que el sellado de reportes necesita.
        Huella = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(crudo)));
    }

    public string Huella { get; }

    public IReadOnlyList<EjemploSql> Catalogo { get; }

    public IReadOnlyList<EjemploSql> Elegir(string pregunta)
    {
        ArgumentNullException.ThrowIfNull(pregunta);

        var terminos = NormalizadorLexico.Terminos(pregunta);
        if (terminos.Count == 0)
        {
            return [];
        }

        return
        [
            .. _indexados
                .Select(indexado => (indexado.Ejemplo, Parecido: Jaccard(terminos, indexado.Terminos)))
                .Where(candidato => candidato.Parecido >= ParecidoMinimo)
                // El desempate por pregunta mantiene el orden estable: dos
                // ejemplos igual de parecidos no pueden alternar entre corridas.
                .OrderByDescending(candidato => candidato.Parecido)
                .ThenBy(candidato => candidato.Ejemplo.Pregunta, StringComparer.Ordinal)
                .Take(TopeDeEjemplos)
                .Select(candidato => candidato.Ejemplo),
        ];
    }

    private static double Jaccard(IReadOnlySet<string> unos, IReadOnlySet<string> otros)
    {
        if (unos.Count == 0 || otros.Count == 0)
        {
            return 0;
        }

        var compartidos = unos.Count(otros.Contains);
        var union = unos.Count + otros.Count - compartidos;

        return union == 0 ? 0 : (double)compartidos / union;
    }

    private static string LeerRecurso()
    {
        var assembly = typeof(SelectorDeEjemplos).Assembly;

        using var flujo = assembly.GetManifestResourceStream(RecursoCatalogo)
            ?? throw new InvalidOperationException(
                $"No se encontró el recurso embebido '{RecursoCatalogo}'. "
                + $"Recursos disponibles: {string.Join(", ", assembly.GetManifestResourceNames())}");

        using var lector = new StreamReader(flujo, Encoding.UTF8);
        return lector.ReadToEnd();
    }

    private sealed record EjemploIndexado(EjemploSql Ejemplo, IReadOnlySet<string> Terminos);

    private sealed class ArchivoDeEjemplos
    {
        [JsonPropertyName("ejemplos")]
        public IReadOnlyList<EjemploDeArchivo> Ejemplos { get; init; } = [];
    }

    private sealed class EjemploDeArchivo
    {
        [JsonPropertyName("pregunta")]
        public string Pregunta { get; init; } = string.Empty;

        [JsonPropertyName("sql")]
        public string Sql { get; init; } = string.Empty;

        [JsonPropertyName("categoria")]
        public string Categoria { get; init; } = string.Empty;
    }
}
