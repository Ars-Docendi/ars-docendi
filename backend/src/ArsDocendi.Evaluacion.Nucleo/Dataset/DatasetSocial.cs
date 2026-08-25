using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArsDocendi.Evaluacion.Nucleo.Dataset;

/// <summary>Qué mide cada ítem del eje social.</summary>
public static class ClaseSocial
{
    /// <summary>Saludo, agradecimiento o meta-pregunta. Aprueba <b>solo</b> a costo cero.</summary>
    public const string Social = "social";

    /// <summary>Pregunta de dominio imposible. Aprueba si se abstiene <b>y</b> sugiere.</summary>
    public const string NoContestable = "no_contestable";

    /// <summary>
    /// Pregunta legítima que el enrutador <b>no</b> debe capturar. Aprueba si llega al
    /// modelo.
    /// </summary>
    public const string Negativo = "negativo";

    public static readonly IReadOnlySet<string> Todas = new HashSet<string>(StringComparer.Ordinal)
    {
        Social, NoContestable, Negativo,
    };
}

/// <summary>Un ítem del eje social y meta.</summary>
public sealed record ItemSocial(string Id, string Pregunta, string Clase, string Actor);

/// <summary>
/// El eje social y meta.
/// </summary>
/// <remarks>
/// Mide dos cosas que ningún otro eje ve: qué proporción del tráfico trivial captura
/// el carril de cero tokens, y —lo más importante— qué proporción de preguntas
/// legítimas se come de más.
///
/// Los ítems negativos salen del eje de capacidad y de las clases coloquial y parcial
/// de robustez: son preguntas que el asistente <b>tiene</b> que mandar al modelo, y
/// capturarlas resta.
/// </remarks>
public sealed class DatasetSocial
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private DatasetSocial(IReadOnlyList<ItemSocial> items, string huella)
    {
        Items = items;
        Huella = huella;
    }

    /// <summary>Los ítems, en el orden del archivo.</summary>
    public IReadOnlyList<ItemSocial> Items { get; }

    /// <summary>Huella estable del archivo, para el sellado.</summary>
    public string Huella { get; }

    /// <summary>Cuántos ítems hay de cada clase.</summary>
    public IReadOnlyDictionary<string, int> ConteoPorClase() =>
        Items.GroupBy(item => item.Clase, StringComparer.Ordinal)
            .OrderBy(grupo => grupo.Key, StringComparer.Ordinal)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Count(), StringComparer.Ordinal);

    /// <summary>Carga el dataset desde un archivo.</summary>
    public static DatasetSocial Cargar(string ruta) => Interpretar(File.ReadAllText(ruta));

    /// <summary>Interpreta el dataset desde su texto.</summary>
    public static DatasetSocial Interpretar(string crudo)
    {
        var archivo = JsonSerializer.Deserialize<ArchivoSocial>(crudo, Opciones)
            ?? throw new InvalidOperationException("El dataset social no se pudo interpretar.");

        var items = archivo.Items
            .Select(item => new ItemSocial(item.Id, item.Pregunta, item.Clase, item.Actor))
            .ToArray();

        foreach (var item in items)
        {
            if (!ClaseSocial.Todas.Contains(item.Clase))
            {
                throw new InvalidOperationException(
                    $"El ítem social '{item.Id}' declara la clase '{item.Clase}', que no existe.");
            }
        }

        if (!items.Any(item => item.Clase == ClaseSocial.Negativo))
        {
            // Sin negativos, el eje solo puede subir: cualquier enrutador que capture
            // todo daría perfecto. Y además son los únicos ítems que consumen tokens,
            // que es lo que la guarda de proveedor caído necesita para funcionar.
            throw new InvalidOperationException(
                "El dataset social no tiene ítems negativos. Sin ellos el eje no puede detectar "
                + "un enrutador que captura de más, ni distinguir una corrida sana de una sin proveedor.");
        }

        var huella = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(crudo)));
        return new DatasetSocial(items, huella);
    }

    private sealed class ArchivoSocial
    {
        [JsonPropertyName("items")]
        public IReadOnlyList<ItemDeArchivo> Items { get; init; } = [];
    }

    private sealed class ItemDeArchivo
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("pregunta")]
        public string Pregunta { get; init; } = string.Empty;

        [JsonPropertyName("clase")]
        public string Clase { get; init; } = string.Empty;

        [JsonPropertyName("actor")]
        public string Actor { get; init; } = string.Empty;
    }
}
