using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Modules.Asistente.Application;

namespace Modules.Asistente.Infrastructure;

/// <summary>
/// Una columna que un término del glosario nombra, con los valores guardados que le corresponden.
/// </summary>
internal sealed record ReferenciaDeGlosario(
    string Columna,
    IReadOnlyList<string> Valores,
    string? VerificaContra)
{
    public string Esquema => Columna.Split('.')[0];

    public string Tabla => Columna.Split('.')[1];

    public string NombreDeColumna => Columna.Split('.')[2];
}

/// <summary>Un término del vocabulario del Departamento.</summary>
internal sealed record TerminoDeGlosario(
    string Termino,
    IReadOnlyList<string> Sinonimos,
    string Explicacion,
    IReadOnlyList<ReferenciaDeGlosario> Referencias,
    string? Sql);

/// <summary>
/// El glosario institucional: el vocabulario del Departamento y el valor exacto que le corresponde
/// en el esquema (asistente-glosario-institucional, D3).
/// </summary>
internal sealed partial class CatalogoDeGlosario
{
    /// <summary>Ruta lógica del recurso embebido con el glosario.</summary>
    public const string RecursoCatalogo = "Modules.Asistente.Recursos.glosario.json";

    /// <summary>Las palabras de SQL que una pista puede usar además de las columnas.</summary>
    private static readonly HashSet<string> PalabrasDeSql =
        new(["COALESCE", "NULL", "IN", "AND", "OR", "NOT", "IS"], StringComparer.OrdinalIgnoreCase);

    private static readonly Lazy<CatalogoDeGlosario> Perezoso = new(Cargar);

    private CatalogoDeGlosario(IReadOnlyList<TerminoDeGlosario> terminos) => Terminos = terminos;

    /// <summary>El glosario del recurso embebido, leído la primera vez que se pide.</summary>
    public static CatalogoDeGlosario Vigente => Perezoso.Value;

    /// <summary>Los términos, en el orden del archivo.</summary>
    public IReadOnlyList<TerminoDeGlosario> Terminos { get; }

    /// <summary>Lee y valida el recurso embebido.</summary>
    public static CatalogoDeGlosario Cargar() => Interpretar(LeerRecurso());

    /// <summary>Interpreta el glosario desde su texto JSON.</summary>
    public static CatalogoDeGlosario Interpretar(string json)
    {
        ArchivoDeGlosario? archivo;

        try
        {
            archivo = JsonSerializer.Deserialize<ArchivoDeGlosario>(json);
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException(
                $"El glosario del asistente no se pudo interpretar: {error.Message}", error);
        }

        if (archivo is null || archivo.Terminos.Count == 0)
        {
            throw new InvalidOperationException(
                "El glosario del asistente está vacío o no tiene ningún término.");
        }

        var terminos = new List<TerminoDeGlosario>(archivo.Terminos.Count);
        var vistos = new HashSet<string>(StringComparer.Ordinal);

        for (var posicion = 0; posicion < archivo.Terminos.Count; posicion++)
        {
            var termino = Validar(archivo.Terminos[posicion], posicion + 1);

            if (!vistos.Add(Clave(termino.Termino)))
            {
                throw new InvalidOperationException(
                    $"El término «{termino.Termino}» del glosario está repetido "
                    + "(se comparan sin mayúsculas ni acentos).");
            }

            terminos.Add(termino);
        }

        return new CatalogoDeGlosario(terminos);
    }

    private static TerminoDeGlosario Validar(TerminoDeArchivo crudo, int posicion)
    {
        if (string.IsNullOrWhiteSpace(crudo.Termino))
        {
            throw new InvalidOperationException(
                $"La entrada {posicion} del glosario no tiene término.");
        }

        var nombre = crudo.Termino;

        if (string.IsNullOrWhiteSpace(crudo.Explicacion) || crudo.Explicacion.Contains('\n', StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"El término «{nombre}» necesita una explicación de una sola línea.");
        }

        if (crudo.Referencias.Count == 0)
        {
            throw new InvalidOperationException(
                $"El término «{nombre}» no tiene ninguna referencia.");
        }

        var referencias = crudo.Referencias.Select(r => Validar(nombre, r)).ToArray();

        if (!string.IsNullOrWhiteSpace(crudo.Sql))
        {
            ValidarPista(nombre, crudo.Sql, referencias);
        }

        return new TerminoDeGlosario(
            nombre,
            [.. crudo.Sinonimos],
            crudo.Explicacion.Trim(),
            referencias,
            string.IsNullOrWhiteSpace(crudo.Sql) ? null : crudo.Sql.Trim());
    }

    private static ReferenciaDeGlosario Validar(string termino, ReferenciaDeArchivo crudo)
    {
        if (!ColumnaCualificada().IsMatch(crudo.Columna))
        {
            throw new InvalidOperationException(
                $"El término «{termino}» nombra la columna «{crudo.Columna}», que no tiene "
                + "la forma esquema.tabla.columna en minúsculas.");
        }

        if (crudo.Valores.Any(string.IsNullOrEmpty))
        {
            throw new InvalidOperationException(
                $"El término «{termino}» tiene un valor vacío en «{crudo.Columna}».");
        }

        if (crudo.VerificaContra is not null)
        {
            if (!ColumnaCualificada().IsMatch(crudo.VerificaContra))
            {
                throw new InvalidOperationException(
                    $"El término «{termino}» verifica «{crudo.Columna}» contra "
                    + $"«{crudo.VerificaContra}», que no tiene la forma esquema.tabla.columna.");
            }

            if (crudo.Valores.Count == 0)
            {
                throw new InvalidOperationException(
                    $"El término «{termino}» declara «verificaContra» en «{crudo.Columna}» "
                    + "sin ningún valor que verificar.");
            }
        }

        return new ReferenciaDeGlosario(crudo.Columna, [.. crudo.Valores], crudo.VerificaContra);
    }

    /// <summary>La pista sólo puede nombrar literales y columnas del propio término.</summary>
    private static void ValidarPista(
        string termino, string sql, IReadOnlyList<ReferenciaDeGlosario> referencias)
    {
        var valores = referencias.SelectMany(r => r.Valores).ToHashSet(StringComparer.Ordinal);
        var columnas = referencias.Select(r => r.NombreDeColumna).ToHashSet(StringComparer.Ordinal);

        foreach (Match literal in LiteralDeSql().Matches(sql))
        {
            var valor = literal.Groups[1].Value.Replace("''", "'", StringComparison.Ordinal);

            if (!valores.Contains(valor))
            {
                throw new InvalidOperationException(
                    $"La pista SQL del término «{termino}» usa el literal '{valor}', "
                    + "que no es un valor de sus referencias.");
            }
        }

        var sinLiterales = LiteralDeSql().Replace(sql, " ");

        foreach (Match identificador in IdentificadorDeSql().Matches(sinLiterales))
        {
            if (!PalabrasDeSql.Contains(identificador.Value) && !columnas.Contains(identificador.Value))
            {
                throw new InvalidOperationException(
                    $"La pista SQL del término «{termino}» nombra «{identificador.Value}», que no es "
                    + "una columna de sus referencias ni una palabra de SQL admitida.");
            }
        }
    }

    /// <summary>La clave de unicidad: sin mayúsculas ni acentos.</summary>
    internal static string Clave(string termino) =>
        NormalizadorLexico.SinAcentos(termino.Trim()).ToLowerInvariant();

    private static string LeerRecurso()
    {
        var assembly = typeof(CatalogoDeGlosario).Assembly;

        using var flujo = assembly.GetManifestResourceStream(RecursoCatalogo)
            ?? throw new InvalidOperationException(
                $"No se encontró el recurso embebido '{RecursoCatalogo}'. "
                + $"Recursos disponibles: {string.Join(", ", assembly.GetManifestResourceNames())}");

        using var lector = new StreamReader(flujo, Encoding.UTF8);
        return lector.ReadToEnd();
    }

    [GeneratedRegex(@"^[a-z_][a-z0-9_]*\.[a-z_][a-z0-9_]*\.[a-z_][a-z0-9_]*$")]
    private static partial Regex ColumnaCualificada();

    [GeneratedRegex(@"'((?:[^']|'')*)'")]
    private static partial Regex LiteralDeSql();

    [GeneratedRegex(@"[A-Za-z_][A-Za-z0-9_]*")]
    private static partial Regex IdentificadorDeSql();

    private sealed class ArchivoDeGlosario
    {
        [JsonPropertyName("terminos")]
        public IReadOnlyList<TerminoDeArchivo> Terminos { get; init; } = [];
    }

    private sealed class TerminoDeArchivo
    {
        [JsonPropertyName("termino")]
        public string Termino { get; init; } = string.Empty;

        [JsonPropertyName("sinonimos")]
        public IReadOnlyList<string> Sinonimos { get; init; } = [];

        [JsonPropertyName("explicacion")]
        public string Explicacion { get; init; } = string.Empty;

        [JsonPropertyName("referencias")]
        public IReadOnlyList<ReferenciaDeArchivo> Referencias { get; init; } = [];

        [JsonPropertyName("sql")]
        public string? Sql { get; init; }
    }

    private sealed class ReferenciaDeArchivo
    {
        [JsonPropertyName("columna")]
        public string Columna { get; init; } = string.Empty;

        [JsonPropertyName("valores")]
        public IReadOnlyList<string> Valores { get; init; } = [];

        [JsonPropertyName("verificaContra")]
        public string? VerificaContra { get; init; }
    }
}
