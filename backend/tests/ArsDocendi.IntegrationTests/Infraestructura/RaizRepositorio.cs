namespace ArsDocendi.IntegrationTests.Infraestructura;

/// <summary>
/// Ubica la raíz del repositorio desde el directorio de salida de los tests.
/// </summary>
/// <remarks>
/// Los tests de arquitectura leen archivos del repo —.csproj, .cs, .sql—, así que
/// necesitan la raíz real y no la carpeta bin.
///
/// Hay nueve copias privadas de esta misma búsqueda en otras clases de test, todas
/// anteriores a este archivo. Están registradas como deuda (TD-007); este tipo es
/// el destino al que hay que migrarlas.
/// </remarks>
public static class RaizRepositorio
{
    /// <summary>Ruta absoluta de la raíz del repositorio.</summary>
    public static string Ruta()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null)
        {
            if (Directory.Exists(Path.Combine(directorio.FullName, "backend", "src"))
                && File.Exists(Path.Combine(directorio.FullName, "CLAUDE.md")))
            {
                return directorio.FullName;
            }

            directorio = directorio.Parent;
        }

        throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
    }

    /// <summary>Ruta absoluta de <c>backend/src</c>.</summary>
    public static string BackendSrc() => Path.Combine(Ruta(), "backend", "src");
}
