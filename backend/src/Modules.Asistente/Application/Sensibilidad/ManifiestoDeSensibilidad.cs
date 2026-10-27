using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArsDocendi.Shared.Persistencia;

namespace Modules.Asistente.Application;

/// <summary>
/// Clasificación versionada de cada columna legible, en las tres categorías que gobiernan qué sale
/// hacia el proveedor del modelo.
/// </summary>
internal sealed class ManifiestoDeSensibilidad
{
    /// <summary>Ruta lógica del recurso embebido, tal como la linkea el csproj.</summary>
    internal const string RutaDelRecurso = "asistente/manifiesto-sensibilidad.json";

    private readonly IReadOnlyDictionary<string, EntradaDeSensibilidad> porColumna;

    private ManifiestoDeSensibilidad(IReadOnlyList<TablaDeSensibilidad> tablas)
    {
        Tablas = tablas;
        porColumna = tablas
            .SelectMany(tabla => tabla.Columnas.Select(columna => (tabla, columna)))
            .ToDictionary(
                par => Clave(par.tabla.Schema, par.tabla.Tabla, par.columna.Columna),
                par => par.columna,
                StringComparer.Ordinal);
    }

    /// <summary>Las tablas clasificadas, en el orden del archivo.</summary>
    public IReadOnlyList<TablaDeSensibilidad> Tablas { get; }

    /// <summary>Toda entrada del manifiesto, aplanada.</summary>
    public IEnumerable<(string Schema, string Tabla, EntradaDeSensibilidad Entrada)> Entradas() =>
        Tablas.SelectMany(t => t.Columnas.Select(c => (t.Schema, t.Tabla, c)));

    /// <summary>
    /// Clasificación de una columna cualificada. Una columna que el manifiesto no
    /// nombra es <see cref="ClasificacionDeSensibilidad.Desconocida"/>.
    /// </summary>
    public ClasificacionDeSensibilidad Clasificacion(string schema, string tabla, string columna) =>
        porColumna.TryGetValue(Clave(schema, tabla, columna), out var entrada)
            ? entrada.Clasificacion
            : ClasificacionDeSensibilidad.Desconocida;

    /// <summary>Carga el manifiesto embebido en este assembly.</summary>
    public static ManifiestoDeSensibilidad Cargar() =>
        Interpretar(RecursosSql.Leer(
            Assembly.GetExecutingAssembly(), RutaDelRecurso));

    /// <summary>Interpreta el manifiesto desde su texto JSON.</summary>
    public static ManifiestoDeSensibilidad Interpretar(string json)
    {
        var crudo = JsonSerializer.Deserialize<ManifiestoCrudo>(json, Opciones)
            ?? throw new InvalidOperationException("El manifiesto de sensibilidad no deserializó.");

        if (crudo.Tablas.Count == 0)
        {
            throw new InvalidOperationException(
                "El manifiesto de sensibilidad no clasifica ninguna tabla.");
        }

        var tablas = new List<TablaDeSensibilidad>(crudo.Tablas.Count);
        foreach (var tabla in crudo.Tablas)
        {
            var columnas = tabla.Columnas
                .Select(columna => new EntradaDeSensibilidad(
                    columna.Columna,
                    Interpretar(tabla.Schema, tabla.Tabla, columna),
                    columna.Etiqueta,
                    columna.Motivo))
                .ToArray();

            tablas.Add(new TablaDeSensibilidad(tabla.Schema, tabla.Tabla, columnas));
        }

        return new ManifiestoDeSensibilidad(tablas);
    }

    private static ClasificacionDeSensibilidad Interpretar(
        string schema, string tabla, ColumnaCruda columna) => columna.Clasificacion switch
        {
            "publica" => ClasificacionDeSensibilidad.Publica,
            "sensible-valor" => ClasificacionDeSensibilidad.SensibleValor,
            "sensible-texto" => ClasificacionDeSensibilidad.SensibleTexto,
            _ => throw new InvalidOperationException(
                $"La columna '{schema}.{tabla}.{columna.Columna}' declara la categoría " +
                $"'{columna.Clasificacion}', que no es 'publica', 'sensible-valor' ni " +
                "'sensible-texto'."),
        };

    private static string Clave(string schema, string tabla, string columna) =>
        $"{schema}.{tabla}.{columna}";

    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed record ManifiestoCrudo
    {
        [JsonPropertyName("tablas")]
        public IReadOnlyList<TablaCruda> Tablas { get; init; } = [];
    }

    private sealed record TablaCruda
    {
        [JsonPropertyName("schema")]
        public string Schema { get; init; } = string.Empty;

        [JsonPropertyName("tabla")]
        public string Tabla { get; init; } = string.Empty;

        [JsonPropertyName("columnas")]
        public IReadOnlyList<ColumnaCruda> Columnas { get; init; } = [];
    }

    private sealed record ColumnaCruda
    {
        [JsonPropertyName("columna")]
        public string Columna { get; init; } = string.Empty;

        [JsonPropertyName("clasificacion")]
        public string Clasificacion { get; init; } = string.Empty;

        [JsonPropertyName("etiqueta")]
        public string? Etiqueta { get; init; }

        [JsonPropertyName("motivo")]
        public string? Motivo { get; init; }
    }
}

/// <summary>Una tabla con todas sus columnas clasificadas.</summary>
internal sealed record TablaDeSensibilidad(
    string Schema, string Tabla, IReadOnlyList<EntradaDeSensibilidad> Columnas);

/// <summary>La clasificación de una columna.</summary>
internal sealed record EntradaDeSensibilidad(
    string Columna,
    ClasificacionDeSensibilidad Clasificacion,
    string? Etiqueta,
    string? Motivo);
