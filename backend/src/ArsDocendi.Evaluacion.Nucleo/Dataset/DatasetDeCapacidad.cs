using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArsDocendi.Evaluacion.Nucleo.Dataset;

/// <summary>Dificultad técnica que ilustra un ítem.</summary>
public static class CategoriaDeItem
{
    public const string ConsultaSimple = "consulta_simple";
    public const string FiltroTemporal = "filtro_temporal";
    public const string CruceDeTablas = "cruce_de_tablas";
    public const string Agregacion = "agregacion";
    public const string NoContestable = "no_contestable";
    public const string Ambigua = "ambigua";

    /// <summary>Lista cerrada. Un ítem con otra categoría es un error del dataset.</summary>
    public static readonly IReadOnlySet<string> Todas = new HashSet<string>(StringComparer.Ordinal)
    {
        ConsultaSimple, FiltroTemporal, CruceDeTablas, Agregacion, NoContestable, Ambigua,
    };

    /// <summary>Las categorías en las que el asistente <b>debe</b> abstenerse.</summary>
    public static bool EsInfactible(string categoria) =>
        categoria is NoContestable or Ambigua;
}

/// <summary>
/// Quién hace la pregunta. Se nombra por alcance y no por identificador, para que
/// el dataset se lea sin tener el fixture al lado.
/// </summary>
public static class ActorDeItem
{
    public const string Global = "global";
    public const string Carrera = "carrera";
    public const string Materia = "materia";
    public const string SinPermiso = "sin_permiso";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.Ordinal)
    {
        Global, Carrera, Materia, SinPermiso,
    };
}

/// <summary>Un ítem del dataset de capacidad.</summary>
/// <param name="Id">Identificador estable. El gate de regresión lo usa como lock.</param>
/// <param name="Pregunta">La pregunta, tal como la escribiría alguien.</param>
/// <param name="Categoria">Dificultad técnica que ilustra.</param>
/// <param name="Actor">Con qué alcance se ejecuta.</param>
/// <param name="SqlReferencia">
/// La consulta que responde bien, o nulo si el ítem es infactible. Se guarda la
/// <b>consulta</b> y no su resultado: con resultados guardados, cualquier cambio
/// del fixture desincroniza el dataset en silencio y la métrica pasa a medir esa
/// diferencia en vez de medir al asistente.
/// </param>
/// <param name="OrdenImporta">
/// Si el orden de las filas es parte de la pregunta. Por omisión no lo es: dos
/// consultas que devuelven las mismas filas en distinto orden responden lo mismo.
/// </param>
public sealed record ItemDeCapacidad(
    string Id,
    string Pregunta,
    string Categoria,
    string Actor,
    string? SqlReferencia,
    bool OrdenImporta)
{
    /// <summary>Si el asistente tiene que abstenerse en este ítem.</summary>
    public bool EsInfactible => CategoriaDeItem.EsInfactible(Categoria);
}

/// <summary>
/// El dataset de capacidad, cargado del archivo versionado.
/// </summary>
/// <remarks>
/// Mide si el asistente traduce la pregunta a la consulta correcta. Estratificado
/// por dificultad técnica para que el número no esconda que acierta lo fácil y
/// falla lo que importa.
/// </remarks>
public sealed class DatasetDeCapacidad
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private DatasetDeCapacidad(IReadOnlyList<ItemDeCapacidad> items, string huella)
    {
        Items = items;
        Huella = huella;
    }

    /// <summary>Los ítems, en el orden del archivo.</summary>
    public IReadOnlyList<ItemDeCapacidad> Items { get; }

    /// <summary>
    /// Huella estable del archivo, para el sellado de reportes.
    /// </summary>
    /// <remarks>
    /// Se calcula sobre los bytes del archivo y no sobre el objeto ya
    /// interpretado: así un cambio de formato que no altere el contenido igual
    /// queda registrado, que es lo que el sellado necesita.
    /// </remarks>
    public string Huella { get; }

    /// <summary>Cuántos ítems hay de cada categoría.</summary>
    public IReadOnlyDictionary<string, int> ConteoPorCategoria() =>
        Items.GroupBy(item => item.Categoria, StringComparer.Ordinal)
            .OrderBy(grupo => grupo.Key, StringComparer.Ordinal)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Count(), StringComparer.Ordinal);

    /// <summary>Carga el dataset desde un archivo.</summary>
    public static DatasetDeCapacidad Cargar(string ruta)
    {
        var crudo = File.ReadAllText(ruta);
        return Interpretar(crudo);
    }

    /// <summary>Interpreta el dataset desde su texto.</summary>
    public static DatasetDeCapacidad Interpretar(string crudo)
    {
        var archivo = JsonSerializer.Deserialize<ArchivoDeDataset>(crudo, Opciones)
            ?? throw new InvalidOperationException("El dataset de capacidad no se pudo interpretar.");

        if (archivo.Items.Count == 0)
        {
            throw new InvalidOperationException("El dataset de capacidad no tiene ningún ítem.");
        }

        var items = archivo.Items
            .Select(item => new ItemDeCapacidad(
                item.Id, item.Pregunta, item.Categoria, item.Actor,
                item.SqlReferencia, item.OrdenImporta))
            .ToArray();

        var repetidos = items.GroupBy(item => item.Id, StringComparer.Ordinal)
            .Where(grupo => grupo.Count() > 1)
            .Select(grupo => grupo.Key)
            .ToArray();

        if (repetidos.Length > 0)
        {
            // Los identificadores son el lock del gate de regresión: repetidos, dos
            // ítems distintos compartirían su historial.
            throw new InvalidOperationException(
                $"El dataset tiene identificadores repetidos: {string.Join(", ", repetidos)}.");
        }

        var huella = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(crudo)));
        return new DatasetDeCapacidad(items, huella);
    }

    private sealed class ArchivoDeDataset
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

        [JsonPropertyName("categoria")]
        public string Categoria { get; init; } = string.Empty;

        [JsonPropertyName("actor")]
        public string Actor { get; init; } = string.Empty;

        [JsonPropertyName("sql_referencia")]
        public string? SqlReferencia { get; init; }

        [JsonPropertyName("orden_importa")]
        public bool OrdenImporta { get; init; }
    }
}
