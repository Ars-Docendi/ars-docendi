using System.Globalization;

namespace Modules.Asistente.Application;

/// <summary>
/// La respuesta de un resultado trivial, escrita sin modelo
/// (asistente-optimizaciones-modelo-local, design.md D5).
/// </summary>
/// <remarks>
/// <b>Sólo cuando no hay nada que matizar.</b> Las reglas de la redacción —no
/// afirmar totales si el resultado se recortó, encuadrar en el alcance si el
/// actor no ve todo, decir sobre cuántos se sabe si el dato es autodeclarado—
/// necesitan prosa, y ahí sigue el modelo. Fuera de esos casos, una columna con
/// pocos valores se dice igual de bien con una plantilla, y la llamada se ahorra.
///
/// Usa los valores que recibe el redactor, que ya vienen enmascarados: la
/// plantilla no ve nada que el modelo no hubiera visto.
/// </remarks>
internal static class PlantillaDeRedaccion
{
    /// <summary>Cuántas filas entran como mucho en una lista dicha en una oración.</summary>
    public const int TopeDeFilas = 5;

    /// <summary>La respuesta, o nulo si el resultado necesita al modelo.</summary>
    public static string? Intentar(
        ResultadoDeConsulta resultado, bool alcanzaTodo, IReadOnlyList<CoberturaDeUnDato>? cobertura)
    {
        ArgumentNullException.ThrowIfNull(resultado);

        if (!alcanzaTodo
            || resultado.Truncado
            || cobertura is { Count: > 0 }
            || resultado.Columnas.Count != 1
            || resultado.Filas.Count is 0 or > TopeDeFilas)
        {
            return null;
        }

        var valores = resultado.Filas.Select(fila => Mostrar(fila[0])).ToList();

        return valores.Count == 1
            ? $"El resultado es {valores[0]}."
            : string.Create(CultureInfo.InvariantCulture,
                $"Encontré {valores.Count} resultados: {string.Join(", ", valores[..^1])} y {valores[^1]}.");
    }

    private static string Mostrar(object? valor) => valor switch
    {
        null => "(sin dato)",
        DateTime fecha => fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
        DateOnly fecha => fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
        bool verdadero => verdadero ? "sí" : "no",
        IFormattable formateable => formateable.ToString(null, CultureInfo.GetCultureInfo("es-AR")),
        _ => valor.ToString() ?? string.Empty,
    };
}
