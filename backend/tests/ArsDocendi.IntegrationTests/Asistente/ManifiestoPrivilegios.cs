using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Manifiesto deny-by-default de los privilegios de lectura del asistente.
///
/// La frontera del asistente es el motor de base de datos y no una convención, pero eso
/// solo es cierto si es FALSABLE: un GRANT que nadie re-verifica se degrada en silencio y
/// el sistema sigue funcionando, solo deja de estar contenido. Este manifiesto es el lado
/// declarativo de esa verificación; <see cref="ComparadorManifiesto"/> es el otro.
/// </summary>
public sealed record Manifiesto
{
    [JsonPropertyName("version")]
    public int Version { get; init; }

    [JsonPropertyName("schemas")]
    public IReadOnlyList<SchemaManifiesto> Schemas { get; init; } = [];

    [JsonPropertyName("tablas")]
    public IReadOnlyList<TablaManifiesto> Tablas { get; init; } = [];

    /// <summary>Schemas que el asistente puede usar. El resto se deniega entero.</summary>
    public IReadOnlyList<string> SchemasExpuestos =>
        [.. Schemas.Where(s => s.Estado == "expuesto").Select(s => s.Nombre)];

    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static Manifiesto Cargar() => Cargar(RutaPorDefecto());

    public static Manifiesto Cargar(string ruta)
    {
        var json = File.ReadAllText(ruta);
        return JsonSerializer.Deserialize<Manifiesto>(json, Opciones)
            ?? throw new InvalidOperationException($"El manifiesto en {ruta} no deserializó.");
    }

    /// <summary>
    /// Resuelve la ruta subiendo desde el directorio de salida hasta encontrar la raíz del
    /// repositorio. Se busca el directorio en vez de contar niveles a propósito: contar
    /// niveles rompe en silencio si el proyecto se mueve.
    /// </summary>
    public static string RutaPorDefecto()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null)
        {
            var candidato = Path.Combine(
                directorio.FullName, "database", "asistente", "manifiesto-privilegios.json");
            if (File.Exists(candidato))
            {
                return candidato;
            }

            directorio = directorio.Parent;
        }

        throw new FileNotFoundException(
            "No se encontró database/asistente/manifiesto-privilegios.json subiendo desde " +
            AppContext.BaseDirectory);
    }
}

public sealed record SchemaManifiesto
{
    [JsonPropertyName("nombre")]
    public string Nombre { get; init; } = string.Empty;

    /// <summary>"expuesto" o "denegado".</summary>
    [JsonPropertyName("estado")]
    public string Estado { get; init; } = string.Empty;

    [JsonPropertyName("motivo")]
    public string? Motivo { get; init; }
}

public sealed record TablaManifiesto
{
    [JsonPropertyName("schema")]
    public string Schema { get; init; } = string.Empty;

    [JsonPropertyName("tabla")]
    public string Tabla { get; init; } = string.Empty;

    /// <summary>"concedida" o "denegada-explicita".</summary>
    [JsonPropertyName("estado")]
    public string Estado { get; init; } = string.Empty;

    [JsonPropertyName("motivo")]
    public string? Motivo { get; init; }

    [JsonPropertyName("columnas_concedidas")]
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ColumnasConcedidas { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>();

    [JsonPropertyName("columnas_denegadas")]
    public IReadOnlyList<ColumnaDenegada> ColumnasDenegadas { get; init; } = [];

    public bool EsConcedida => Estado == "concedida";

    public string Cualificado => $"{Schema}.{Tabla}";

    /// <summary>Toda columna nombrada por el manifiesto, concedida a cualquier rol o denegada.</summary>
    public IReadOnlySet<string> ColumnasClasificadas =>
        ColumnasConcedidas.Values
            .SelectMany(c => c)
            .Concat(ColumnasDenegadas.Select(c => c.Columna))
            .ToHashSet(StringComparer.Ordinal);

    public IEnumerable<PrivilegioDeclarado> Declarados() =>
        ColumnasConcedidas.SelectMany(par =>
            par.Value.Select(columna => new PrivilegioDeclarado(Schema, Tabla, columna, par.Key)));
}

public sealed record ColumnaDenegada
{
    [JsonPropertyName("columna")]
    public string Columna { get; init; } = string.Empty;

    [JsonPropertyName("motivo")]
    public string Motivo { get; init; } = string.Empty;
}

/// <summary>Un SELECT sobre una columna que el manifiesto concede a un rol.</summary>
public sealed record PrivilegioDeclarado(string Schema, string Tabla, string Columna, string Rol);

/// <summary>Un SELECT que la base efectivamente concede, leído de information_schema.</summary>
public sealed record PrivilegioEfectivo(string Schema, string Tabla, string Columna, string Rol);

/// <summary>Una columna que existe de verdad en la base.</summary>
public sealed record ColumnaReal(string Schema, string Tabla, string Columna);

public enum TipoDesviacion
{
    /// <summary>Dirección 1: la base concede algo que el manifiesto no declara.</summary>
    PrivilegioNoDeclarado,

    /// <summary>Dirección 2: el manifiesto declara algo que la base ya no concede.</summary>
    PrivilegioDeclaradoInexistente,

    /// <summary>Dirección 3: existe una tabla en un schema expuesto que el manifiesto no clasifica.</summary>
    TablaSinClasificar,

    /// <summary>Dirección 3: existe una columna en una tabla concedida que el manifiesto no clasifica.</summary>
    ColumnaSinClasificar,
}

public sealed record Desviacion(TipoDesviacion Tipo, string Objeto, string Detalle)
{
    public override string ToString() => $"[{Tipo}] {Objeto} — {Detalle}";
}

/// <summary>
/// Compara el manifiesto contra la realidad de la base en las TRES direcciones.
///
/// Verificar una sola deja las otras dos abiertas:
/// un privilegio efectivo no declarado es una concesión que nadie decidió;
/// un privilegio declarado que ya no existe es un manifiesto que miente y dejó de proteger;
/// y una tabla sin clasificar es exactamente la puerta por la que entró idempotencia_comandos.
/// </summary>
public static class ComparadorManifiesto
{
    public static IReadOnlyList<Desviacion> Comparar(
        Manifiesto manifiesto,
        IReadOnlyCollection<PrivilegioEfectivo> efectivos,
        IReadOnlyCollection<ColumnaReal> columnasReales)
    {
        var desviaciones = new List<Desviacion>();
        var expuestos = manifiesto.SchemasExpuestos.ToHashSet(StringComparer.Ordinal);

        var declarados = manifiesto.Tablas
            .SelectMany(t => t.Declarados())
            .Select(d => (d.Schema, d.Tabla, d.Columna, d.Rol))
            .ToHashSet();

        var concedidosPorLaBase = efectivos
            .Select(e => (e.Schema, e.Tabla, e.Columna, e.Rol))
            .ToHashSet();

        // Dirección 1 — la base concede algo que el manifiesto no declara.
        foreach (var efectivo in efectivos.OrderBy(e => e.Schema).ThenBy(e => e.Tabla).ThenBy(e => e.Columna))
        {
            if (!declarados.Contains((efectivo.Schema, efectivo.Tabla, efectivo.Columna, efectivo.Rol)))
            {
                desviaciones.Add(new Desviacion(
                    TipoDesviacion.PrivilegioNoDeclarado,
                    $"{efectivo.Schema}.{efectivo.Tabla}.{efectivo.Columna}",
                    $"la base concede SELECT a {efectivo.Rol} y el manifiesto no lo declara"));
            }
        }

        // Dirección 2 — el manifiesto declara algo que la base ya no concede.
        foreach (var declarado in manifiesto.Tablas
            .SelectMany(t => t.Declarados())
            .OrderBy(d => d.Schema).ThenBy(d => d.Tabla).ThenBy(d => d.Columna))
        {
            if (!concedidosPorLaBase.Contains(
                    (declarado.Schema, declarado.Tabla, declarado.Columna, declarado.Rol)))
            {
                desviaciones.Add(new Desviacion(
                    TipoDesviacion.PrivilegioDeclaradoInexistente,
                    $"{declarado.Schema}.{declarado.Tabla}.{declarado.Columna}",
                    $"el manifiesto lo declara concedido a {declarado.Rol} y la base no lo concede"));
            }
        }

        // Dirección 3 — tablas y columnas reales que el manifiesto no clasifica.
        var clasificadas = manifiesto.Tablas
            .Select(t => (t.Schema, t.Tabla))
            .ToHashSet();

        var tablasReales = columnasReales
            .Where(c => expuestos.Contains(c.Schema))
            .Select(c => (c.Schema, c.Tabla))
            .Distinct()
            .OrderBy(t => t.Schema).ThenBy(t => t.Tabla);

        foreach (var (schema, tabla) in tablasReales)
        {
            if (!clasificadas.Contains((schema, tabla)))
            {
                desviaciones.Add(new Desviacion(
                    TipoDesviacion.TablaSinClasificar,
                    $"{schema}.{tabla}",
                    "existe en un schema expuesto y el manifiesto no la clasifica como " +
                    "concedida ni como denegada-explicita"));
            }
        }

        foreach (var entrada in manifiesto.Tablas.Where(t => t.EsConcedida))
        {
            var clasificadasDeLaTabla = entrada.ColumnasClasificadas;
            var columnasDeLaTabla = columnasReales
                .Where(c => c.Schema == entrada.Schema && c.Tabla == entrada.Tabla)
                .OrderBy(c => c.Columna);

            foreach (var columna in columnasDeLaTabla)
            {
                if (!clasificadasDeLaTabla.Contains(columna.Columna))
                {
                    desviaciones.Add(new Desviacion(
                        TipoDesviacion.ColumnaSinClasificar,
                        $"{entrada.Cualificado}.{columna.Columna}",
                        "existe en una tabla concedida y el manifiesto no la concede ni la deniega"));
                }
            }
        }

        return desviaciones;
    }
}
