using System.Text.Json;

namespace ArsDocendi.Evaluacion.Nucleo.Runner;

/// <summary>
/// Los ítems que oscilan en vivo sin que el código cambie, por eje.
/// </summary>
/// <remarks>
/// <b>Vive en un archivo versionado aparte</b> (<c>lineas-de-base/inestables.json</c>) y
/// no en el dataset: el dataset entra en el hash del sello, así que marcar un ítem ahí
/// invalidaría las líneas de base recién congeladas y el gate se negaría a comparar.
/// Esta lista no es parte del sello: tolerar un ítem no cambia contra qué se midió.
///
/// Formato: <c>{ "eje": { "id-del-item": "motivo" } }</c>. Para diálogo el id es el de
/// la clave de la línea de base, o sea por turno (<c>dia-001-seguimiento#2</c>).
/// </remarks>
public static class ItemsInestables
{
    /// <summary>Carga el archivo, o un mapa vacío si no existe.</summary>
    /// <exception cref="InvalidOperationException">El archivo es inválido o algún motivo está vacío.</exception>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Cargar(string ruta)
    {
        if (!File.Exists(ruta))
        {
            return new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        }

        Dictionary<string, Dictionary<string, string>>? crudo;
        try
        {
            crudo = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(
                File.ReadAllText(ruta));
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException($"{ruta}: no se pudo interpretar ({error.Message}).", error);
        }

        var resultado = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var (eje, items) in crudo ?? [])
        {
            foreach (var (id, motivo) in items)
            {
                if (string.IsNullOrWhiteSpace(motivo))
                {
                    throw new InvalidOperationException(
                        $"{ruta}: el ítem inestable «{id}» ({eje}) necesita un motivo.");
                }
            }

            resultado[eje] = new Dictionary<string, string>(items, StringComparer.Ordinal);
        }

        return resultado;
    }

    /// <summary>Los inestables de un eje; vacío si no tiene.</summary>
    public static IReadOnlyDictionary<string, string> DelEje(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> todas, string eje) =>
        todas.TryGetValue(eje, out var items)
            ? items
            : new Dictionary<string, string>(StringComparer.Ordinal);
}
