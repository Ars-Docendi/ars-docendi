using System.Xml.Linq;
using ArsDocendi.IntegrationTests.Infraestructura;

namespace ArsDocendi.IntegrationTests.Backend;

/// <summary>Una referencia de proyecto tal como está escrita en un <c>.csproj</c>.</summary>
public sealed record AristaReal(string Origen, string Destino)
{
    public override string ToString() => $"{Origen} -> {Destino}";
}

/// <summary>El grafo de proyectos leído del código, sin nada declarado de por medio.</summary>
public sealed record GrafoDeProyectos(
    IReadOnlyList<string> Proyectos,
    IReadOnlyList<AristaReal> Aristas);

/// <summary>
/// Lee el grafo de proyectos del backend desde los <c>ProjectReference</c> reales.
/// </summary>
/// <remarks>
/// Barre <c>backend/src</c> ENTERO y no solo los <c>Modules.*</c>: el glob de
/// <c>ArquitecturaIdentityTests</c> se detiene en los módulos, y por eso no ve que
/// <c>ArsDocendi.Evaluacion.Nucleo</c> —que no es un módulo— referencia el proyecto
/// interno de uno. Una frontera que ningún barrido alcanza no es una frontera.
///
/// <c>backend/tests</c> queda afuera a propósito: un proyecto de tests referencia
/// todo por definición y su grafo no dice nada sobre las fronteras del sistema.
/// </remarks>
public static class LectorDeAristas
{
    /// <summary>El grafo real de <c>backend/src</c>.</summary>
    public static GrafoDeProyectos LeerBackendSrc() => Leer(RaizRepositorio.BackendSrc());

    /// <summary>El grafo de los <c>.csproj</c> que cuelgan de <paramref name="directorio"/>.</summary>
    public static GrafoDeProyectos Leer(string directorio)
    {
        var archivos = Directory.Exists(directorio)
            ? Directory.EnumerateFiles(directorio, "*.csproj", SearchOption.AllDirectories)
                .Where(ruta => !EnCarpetaDeSalida(ruta))
                .OrderBy(ruta => ruta, StringComparer.Ordinal)
                .ToArray()
            : [];

        // Un barrido vacío no es «no hay aristas»: es «no miré nada», y las dos
        // cosas se ven idénticas en verde. Es el mismo guard que ya escriben
        // `ArquitecturaIdentityTests` y el manifiesto de privilegios.
        if (archivos.Length == 0)
        {
            throw new InvalidOperationException(
                $"El barrido no encontró ningún .csproj bajo {directorio}. " +
                "Sin proyectos no hay nada que verificar, y pasar en verde sería mentir.");
        }

        var proyectos = archivos
            .Select(Path.GetFileNameWithoutExtension)
            .Select(nombre => nombre!)
            .ToArray();

        // La clave de un proyecto en el manifiesto es el nombre de su .csproj sin
        // extensión. Con dos homónimos esa clave deja de identificar y la fila diría
        // «Modules.Aulas» sin saber cuál: una clave que se degrada en silencio no es
        // una clave.
        var homonimos = proyectos
            .GroupBy(nombre => nombre, StringComparer.Ordinal)
            .Where(grupo => grupo.Count() > 1)
            .Select(grupo => grupo.Key)
            .OrderBy(nombre => nombre, StringComparer.Ordinal)
            .ToArray();

        if (homonimos.Length > 0)
        {
            throw new InvalidOperationException(
                "Hay .csproj homónimos bajo " + directorio + ", y el nombre del proyecto es la " +
                "clave del manifiesto de aristas: " + string.Join(", ", homonimos));
        }

        var aristas = archivos
            .SelectMany(archivo => ReferenciasDe(archivo)
                .Select(destino => new AristaReal(Path.GetFileNameWithoutExtension(archivo), destino)))
            .ToArray();

        return new GrafoDeProyectos(proyectos, aristas);
    }

    private static IEnumerable<string> ReferenciasDe(string archivo) =>
        XDocument.Load(archivo)
            .Descendants("ProjectReference")
            .Select(nodo => nodo.Attribute("Include")?.Value ?? string.Empty)
            .Where(ruta => !string.IsNullOrWhiteSpace(ruta))
            // Los .csproj escriben las rutas con separador de Windows. Path.* no lo
            // parte en Linux, así que hay que normalizar antes de quedarse con el nombre.
            .Select(ruta => Path.GetFileNameWithoutExtension(ruta.Replace('\\', '/')));

    private static bool EnCarpetaDeSalida(string ruta) =>
        ruta.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || ruta.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
}
