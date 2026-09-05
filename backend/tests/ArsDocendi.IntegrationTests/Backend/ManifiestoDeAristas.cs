using System.Text.Json;
using System.Text.Json.Serialization;
using ArsDocendi.IntegrationTests.Infraestructura;

namespace ArsDocendi.IntegrationTests.Backend;

/// <summary>
/// Declaración única de las aristas del grafo de proyectos del backend.
/// </summary>
/// <remarks>
/// Espeja la forma de <c>database/asistente/manifiesto-privilegios.json</c>, que es
/// el único registro de fronteras de este repo que funciona: un archivo declarativo
/// y un test que lo confronta con la realidad en tres direcciones. El «Edge registry»
/// de <c>docs/architecture/dependency-graph.md</c> era una tabla markdown que nadie
/// leía, y acumuló las tres desviaciones posibles sin que nada se pusiera en rojo.
///
/// La clave de un proyecto es el nombre de su <c>.csproj</c> sin extensión: es lo que
/// un humano escribe y lo que ya usa el diagrama. Como el nombre deja de ser clave si
/// dos proyectos lo comparten, el barrido falla ante homónimos.
/// </remarks>
public sealed record ManifiestoDeAristas
{
    /// <summary>Las vías que el verificador sabe comprobar. Lo que no está acá, no carga.</summary>
    /// <remarks>
    /// Vocabulario cerrado a propósito: una vía que el verificador no sabe comprobar
    /// es una fila que se lee como verificada sin serlo. Si algún día hace falta una
    /// segunda, agregarla exige enseñarle al verificador a comprobarla en el mismo cambio.
    /// </remarks>
    public static readonly IReadOnlySet<string> ViasConocidas =
        new HashSet<string>(StringComparer.Ordinal) { "project-reference" };

    /// <summary>Estados admitidos de un proyecto.</summary>
    public static readonly IReadOnlySet<string> EstadosConocidos =
        new HashSet<string>(StringComparer.Ordinal) { "activo", "huerfano" };

    [JsonPropertyName("version")]
    public int Version { get; init; }

    [JsonPropertyName("proyectos")]
    public IReadOnlyList<ProyectoDeclarado> Proyectos { get; init; } = [];

    [JsonPropertyName("aristas")]
    public IReadOnlyList<AristaDeclarada> Aristas { get; init; } = [];

    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Carga el manifiesto versionado del repositorio.</summary>
    public static ManifiestoDeAristas Cargar() => Cargar(RutaPorDefecto());

    /// <summary>Carga el manifiesto que vive en <paramref name="ruta"/>.</summary>
    public static ManifiestoDeAristas Cargar(string ruta) => Interpretar(File.ReadAllText(ruta));

    /// <summary>Interpreta el texto de un manifiesto, valide o no.</summary>
    public static ManifiestoDeAristas Interpretar(string json)
    {
        var manifiesto = JsonSerializer.Deserialize<ManifiestoDeAristas>(json, Opciones)
            ?? throw new InvalidOperationException("El manifiesto de aristas no deserializó.");

        manifiesto.Validar();

        return manifiesto;
    }

    /// <summary>
    /// Rechaza las filas que el verificador no podría comprobar.
    /// </summary>
    /// <remarks>
    /// Una fila incompleta no llega al comparador a propósito: si llegara, saldría
    /// en verde por no tener con qué compararse, y se leería como verificada. Es la
    /// misma disciplina que exige motivo escrito a toda denegación del manifiesto de
    /// privilegios.
    /// </remarks>
    private void Validar()
    {
        var problemas = new List<string>();

        foreach (var proyecto in Proyectos)
        {
            if (string.IsNullOrWhiteSpace(proyecto.Nombre))
            {
                problemas.Add("Hay un proyecto declarado sin nombre.");
                continue;
            }

            if (!EstadosConocidos.Contains(proyecto.Estado))
            {
                problemas.Add(
                    $"El proyecto {proyecto.Nombre} declara el estado «{proyecto.Estado}», que no " +
                    $"pertenece al vocabulario conocido ({string.Join(", ", EstadosConocidos)}).");
            }
        }

        foreach (var arista in Aristas)
        {
            foreach (var (campo, valor) in new[]
            {
                ("origen", arista.Origen),
                ("destino", arista.Destino),
                ("via", arista.Via),
                ("motivo", arista.Motivo),
            })
            {
                if (string.IsNullOrWhiteSpace(valor))
                {
                    problemas.Add($"La arista {arista} no declara «{campo}».");
                }
            }

            if (!string.IsNullOrWhiteSpace(arista.Via) && !ViasConocidas.Contains(arista.Via))
            {
                problemas.Add(
                    $"La arista {arista} declara la vía «{arista.Via}», que el verificador no sabe " +
                    $"comprobar. Vías conocidas: {string.Join(", ", ViasConocidas)}.");
            }
        }

        if (problemas.Count > 0)
        {
            throw new InvalidOperationException(
                $"El manifiesto de aristas no carga por {problemas.Count} problema(s):\n"
                + string.Join("\n", problemas));
        }
    }

    /// <summary>Ruta del manifiesto: vive con los <c>.csproj</c> que declara.</summary>
    public static string RutaPorDefecto() =>
        Path.Combine(RaizRepositorio.Ruta(), "backend", "manifiesto-de-aristas.json");
}

/// <summary>Un proyecto de <c>backend/src</c> con su estado declarado.</summary>
public sealed record ProyectoDeclarado
{
    [JsonPropertyName("nombre")]
    public string Nombre { get; init; } = string.Empty;

    /// <summary><c>activo</c> u <c>huerfano</c>.</summary>
    [JsonPropertyName("estado")]
    public string Estado { get; init; } = string.Empty;

    [JsonPropertyName("motivo")]
    public string? Motivo { get; init; }

    public bool EsHuerfano => Estado.Equals("huerfano", StringComparison.Ordinal);
}

/// <summary>Una arista declarada, con el motivo por el que existe.</summary>
public sealed record AristaDeclarada
{
    [JsonPropertyName("origen")]
    public string Origen { get; init; } = string.Empty;

    [JsonPropertyName("destino")]
    public string Destino { get; init; } = string.Empty;

    /// <summary>Cómo se materializa la arista. Hoy sólo <c>project-reference</c>.</summary>
    [JsonPropertyName("via")]
    public string Via { get; init; } = string.Empty;

    [JsonPropertyName("motivo")]
    public string Motivo { get; init; } = string.Empty;

    public override string ToString() => $"{Origen} -> {Destino}";
}
