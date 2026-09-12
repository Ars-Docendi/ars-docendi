namespace ArsDocendi.IntegrationTests.Infraestructura;

/// <summary>
/// Ubica la raíz del repositorio desde el directorio de salida de los tests.
/// </summary>
/// <remarks>
/// Los tests de arquitectura leen archivos del repo —.csproj, .cs, .sql—, así que
/// necesitan la raíz real y no la carpeta bin.
/// </remarks>
public static class RaizRepositorio
{
    /// <summary>Ruta absoluta de la raíz del repositorio.</summary>
    public static string Ruta()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null)
        {
            // Se busca AGENTS.md y no CLAUDE.md: las instrucciones del repositorio
            // se mudaron ahí, y CLAUDE.md quedó como un adaptador de tres líneas
            // que lo enlaza. Con las once copias de esta búsqueda ya centralizadas
            // acá (TD-007), este cambio se hizo en un solo lugar en vez de once.
            if (Directory.Exists(Path.Combine(directorio.FullName, "backend", "src"))
                && File.Exists(Path.Combine(directorio.FullName, "AGENTS.md")))
            {
                return directorio.FullName;
            }

            directorio = directorio.Parent;
        }

        throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
    }

    /// <summary>Ruta absoluta de <c>backend/src</c>.</summary>
    public static string BackendSrc() => Path.Combine(Ruta(), "backend", "src");

    /// <summary>
    /// Ruta absoluta del directorio de cassettes versionados del proveedor.
    /// </summary>
    /// <remarks>
    /// Viven bajo el proyecto de tests y entran al repositorio como cualquier otro
    /// fixture: son el activo que el mecanismo de grabación existe para producir, y
    /// un cassette que no se commitea es una corrida financiada tirada.
    /// </remarks>
    public static string Cassettes() => Path.Combine(
        Ruta(), "backend", "tests", "ArsDocendi.IntegrationTests", "Cassettes");
}
