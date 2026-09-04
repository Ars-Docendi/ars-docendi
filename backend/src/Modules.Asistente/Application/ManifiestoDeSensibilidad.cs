using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArsDocendi.Shared.Persistencia;

namespace Modules.Asistente.Application;

/// <summary>
/// Clasificación versionada de cada columna legible, en las tres categorías que
/// gobiernan qué sale hacia el proveedor del modelo.
/// </summary>
/// <remarks>
/// A diferencia del manifiesto de privilegios —que se deserializa en el proyecto
/// de tests porque nadie en producción lo consume, existe solo para ser comparado
/// contra la base—, éste lo lee el enmascarador en cada turno. Por eso el tipo
/// vive acá y no allá.
///
/// El criterio «la clasificación es de una sola fuente» se sostiene porque el test
/// que verifica la cobertura carga <b>este mismo tipo</b>, no una copia suya.
/// </remarks>
public sealed class ManifiestoDeSensibilidad
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
    /// <exception cref="InvalidOperationException">
    /// Si el archivo no deserializa, o si alguna columna declara una categoría que
    /// no es ninguna de las tres. Fallar en la carga es deliberado: una categoría
    /// mal escrita que se degradara a «pública» filtraría en silencio.
    /// </exception>
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
public sealed record TablaDeSensibilidad(
    string Schema, string Tabla, IReadOnlyList<EntradaDeSensibilidad> Columnas);

/// <summary>La clasificación de una columna.</summary>
/// <param name="Columna">Nombre de la columna en la tabla, no su alias en el resultado.</param>
/// <param name="Clasificacion">En cuál de las tres categorías cae.</param>
/// <param name="Etiqueta">
/// Cómo nombrar el dato en el marcador, para las <c>sensible-valor</c>. Es lo que
/// le permite al modelo redactar «el documento de la primera persona» sin
/// conocerlo.
/// </param>
/// <param name="Motivo">Por qué está clasificada así, cuando no es pública.</param>
public sealed record EntradaDeSensibilidad(
    string Columna,
    ClasificacionDeSensibilidad Clasificacion,
    string? Etiqueta,
    string? Motivo);
