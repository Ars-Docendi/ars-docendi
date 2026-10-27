using System.Globalization;

namespace Modules.Asistente.Application;

/// <summary>
/// Cuánta gente cargó un dato del portal, para no confundir el vacío con el hecho.
/// </summary>
public sealed record CoberturaDeUnDato(string Tabla, long ConDato, long Total)
{
    /// <summary>Cómo se le cuenta al usuario, en una oración.</summary>
    public string Frase() => string.Create(
        CultureInfo.InvariantCulture,
        $"De {Total} docentes del padrón, {ConDato} cargaron {CoberturaDelPortal.ComoSeLlama(Tabla)}.");
}

/// <summary>Qué tablas del portal tocó una consulta, y cómo se declara su cobertura.</summary>
internal static class CoberturaDelPortal
{
    /// <summary>Las tablas de portal cuya ausencia de datos hay que declarar.</summary>
    private static readonly Dictionary<string, string> Nombres = new(StringComparer.Ordinal)
    {
        ["educaciones"] = "su formación",
        ["certificaciones"] = "sus certificaciones",
        ["experiencias"] = "su experiencia laboral",
        ["docente_habilidades"] = "sus habilidades e intereses",
    };

    /// <summary>Si la tabla es una de las que se declaran.</summary>
    public static bool EsDeclarable(string tabla) => Nombres.ContainsKey(tabla);

    /// <summary>Cómo se nombra el dato de una tabla al hablarle a una persona.</summary>
    internal static string ComoSeLlama(string tabla) =>
        Nombres.TryGetValue(tabla, out var nombre) ? nombre : "ese dato";

    /// <summary>Las tablas de portal que la consulta toca.</summary>
    public static IReadOnlyList<string> TablasQueToca(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return [];
        }

        return [.. Nombres.Keys
            .Where(tabla => sql.Contains($"portal.{tabla}", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// La cobertura que se le declara al usuario cuando la respuesta salió vacía.
    /// </summary>
    public static CoberturaDeUnDato? LaQueExplicaElVacio(
        IReadOnlyList<CoberturaDeUnDato> coberturas)
    {
        ArgumentNullException.ThrowIfNull(coberturas);

        return coberturas
            .Where(cobertura => cobertura.Total > 0)
            .OrderBy(cobertura => cobertura.ConDato)
            .ThenBy(cobertura => cobertura.Tabla, StringComparer.Ordinal)
            .FirstOrDefault();
    }
}

/// <summary>
/// Cuenta cuánta gente cargó cada dato del portal. Corre con la conexión de lectura del actor, así
/// que la RLS se aplica: el numerador cuenta perfiles que ESE actor alcanza.
/// </summary>
public interface IConsultorDeCobertura
{
    /// <summary>La cobertura de cada tabla pedida, para el actor del turno.</summary>
    Task<IReadOnlyList<CoberturaDeUnDato>> ObtenerAsync(
        IReadOnlyList<string> tablas, Guid actor, CancellationToken ct);
}
