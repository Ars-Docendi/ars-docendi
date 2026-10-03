using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArsDocendi.Evaluacion.Nucleo.Dataset;

/// <summary>Cómo está perturbada la pregunta respecto de su origen.</summary>
public static class ClaseDePerturbacion
{
    /// <summary>«cuantos docentes hay» — sin tildes ni signos de apertura.</summary>
    public const string SinTildes = "sin_tildes";

    /// <summary>«cuántos docenets hay» — errores de tipeo verosímiles.</summary>
    public const string Tipeo = "tipeo";

    /// <summary>«cuántos profesores hay» — la misma cosa dicha con otra palabra.</summary>
    public const string Sinonimo = "sinonimo";

    /// <summary>«docentes de Álgebra» — sin verbo ni pregunta completa.</summary>
    public const string Parcial = "parcial";

    /// <summary>«che, ¿cuántos docentes hay?» — cómo se escribe de verdad.</summary>
    public const string Coloquial = "coloquial";

    /// <summary>Lista cerrada. Una clase desconocida es un error del dataset.</summary>
    public static readonly IReadOnlySet<string> Todas = new HashSet<string>(StringComparer.Ordinal)
    {
        SinTildes, Tipeo, Sinonimo, Parcial, Coloquial,
    };
}

/// <summary>
/// Una pregunta del eje de capacidad, dicha de otra manera.
/// </summary>
/// <param name="Id">Identificador estable; es el lock del gate.</param>
/// <param name="Origen">Identificador del ítem de capacidad del que sale.</param>
/// <param name="Pregunta">La pregunta perturbada.</param>
/// <param name="Clase">Qué clase de perturbación es.</param>
/// <param name="Item">
/// El ítem que se evalúa: la pregunta perturbada con <b>todo lo demás heredado</b>
/// del origen.
/// </param>
public sealed record ItemDeRobustez(
    string Id,
    string Origen,
    string Pregunta,
    string Clase,
    ItemDeCapacidad Item);

/// <summary>
/// El eje de robustez de fraseo.
/// </summary>
/// <remarks>
/// <b>Ningún ítem declara su propia consulta de referencia: la hereda del origen.</b>
/// La forma obvia sería copiarla y poner un test que compare las dos, y es peor: un
/// test que compara copias falla <i>después</i> de que alguien las desincronizó, y
/// copiar mal es exactamente el error que un humano comete al agregar el ítem quince.
///
/// Al derivarla, no hay dos copias que puedan diferir. La reutilización byte-idéntica
/// deja de ser una convención vigilada y pasa a ser estructural.
///
/// El motivo por el que ese invariante importa: sin él, un fallo sería ambiguo. ¿No
/// entendió el fraseo, o no supo escribir la consulta? Con la referencia compartida,
/// lo único que cambia entre el ítem de capacidad y el de robustez es cómo está
/// escrita la pregunta.
/// </remarks>
public sealed class DatasetDeRobustez
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private DatasetDeRobustez(IReadOnlyList<ItemDeRobustez> items, string huella)
    {
        Items = items;
        Huella = huella;
    }

    /// <summary>Los ítems, en el orden del archivo.</summary>
    public IReadOnlyList<ItemDeRobustez> Items { get; }

    /// <summary>Huella estable del archivo, para el sellado.</summary>
    public string Huella { get; }

    /// <summary>Cuántos ítems hay de cada clase de perturbación.</summary>
    /// <remarks>
    /// Desagregar por clase es lo que hace útil el eje: un número global que baja no
    /// dice si el problema son los tildes o los sinónimos, y son dos arreglos
    /// distintos.
    /// </remarks>
    public IReadOnlyDictionary<string, int> ConteoPorClase() =>
        Items.GroupBy(item => item.Clase, StringComparer.Ordinal)
            .OrderBy(grupo => grupo.Key, StringComparer.Ordinal)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Count(), StringComparer.Ordinal);

    /// <summary>Carga el dataset resolviendo los orígenes contra el de capacidad.</summary>
    public static DatasetDeRobustez Cargar(string ruta, DatasetDeCapacidad capacidad) =>
        Interpretar(File.ReadAllText(ruta), capacidad);

    /// <summary>Interpreta el dataset desde su texto.</summary>
    public static DatasetDeRobustez Interpretar(string crudo, DatasetDeCapacidad capacidad)
    {
        ArgumentNullException.ThrowIfNull(capacidad);

        var archivo = JsonSerializer.Deserialize<ArchivoDeRobustez>(crudo, Opciones)
            ?? throw new InvalidOperationException("El dataset de robustez no se pudo interpretar.");

        if (archivo.Items.Count == 0)
        {
            throw new InvalidOperationException("El dataset de robustez no tiene ningún ítem.");
        }

        var porId = capacidad.Items.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var items = new List<ItemDeRobustez>(archivo.Items.Count);

        foreach (var crudoItem in archivo.Items)
        {
            items.Add(Resolver(crudoItem, porId));
        }

        var repetidos = items.GroupBy(item => item.Id, StringComparer.Ordinal)
            .Where(grupo => grupo.Count() > 1)
            .Select(grupo => grupo.Key)
            .ToArray();

        if (repetidos.Length > 0)
        {
            throw new InvalidOperationException(
                $"El dataset de robustez tiene identificadores repetidos: {string.Join(", ", repetidos)}.");
        }

        var huella = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(crudo)));
        return new DatasetDeRobustez(items, huella);
    }

    private static ItemDeRobustez Resolver(
        ItemDeArchivo crudo, IReadOnlyDictionary<string, ItemDeCapacidad> porId)
    {
        if (crudo.SqlReferencia is not null)
        {
            // El campo NO existe en el contrato de este dataset. Que alguien lo
            // escriba significa que está a punto de mantener dos copias de la misma
            // consulta, así que se lo frena acá y no cuando ya divergieron.
            throw new InvalidOperationException(
                $"El ítem de robustez '{crudo.Id}' declara una consulta propia. "
                + "La consulta se hereda del ítem de origen: dos copias se desincronizan.");
        }

        if (!ClaseDePerturbacion.Todas.Contains(crudo.Clase))
        {
            throw new InvalidOperationException(
                $"El ítem de robustez '{crudo.Id}' declara la clase '{crudo.Clase}', que no existe. "
                + $"Las válidas son: {string.Join(", ", ClaseDePerturbacion.Todas.Order(StringComparer.Ordinal))}.");
        }

        if (!porId.TryGetValue(crudo.Origen, out var origen))
        {
            throw new InvalidOperationException(
                $"El ítem de robustez '{crudo.Id}' declara el origen '{crudo.Origen}', "
                + "que no está en el dataset de capacidad.");
        }

        if (string.Equals(crudo.Pregunta, origen.Pregunta, StringComparison.Ordinal))
        {
            // Un ítem idéntico al origen no mide robustez: mide dos veces lo mismo y
            // le sube el peso a ese caso en el promedio.
            throw new InvalidOperationException(
                $"El ítem de robustez '{crudo.Id}' tiene la misma pregunta que su origen.");
        }

        // Se hereda TODO menos la pregunta. El identificador es el del ítem de
        // robustez porque el gate necesita distinguirlo del original.
        var item = origen with { Id = crudo.Id, Pregunta = crudo.Pregunta };

        return new ItemDeRobustez(crudo.Id, crudo.Origen, crudo.Pregunta, crudo.Clase, item);
    }

    private sealed class ArchivoDeRobustez
    {
        [JsonPropertyName("items")]
        public IReadOnlyList<ItemDeArchivo> Items { get; init; } = [];
    }

    private sealed class ItemDeArchivo
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("origen")]
        public string Origen { get; init; } = string.Empty;

        [JsonPropertyName("pregunta")]
        public string Pregunta { get; init; } = string.Empty;

        [JsonPropertyName("clase")]
        public string Clase { get; init; } = string.Empty;

        /// <summary>
        /// No forma parte del contrato: existe solo para poder rechazarlo con un
        /// mensaje que explique por qué.
        /// </summary>
        [JsonPropertyName("sql_referencia")]
        public string? SqlReferencia { get; init; }
    }
}
