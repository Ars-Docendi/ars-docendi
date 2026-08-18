using System.Text.RegularExpressions;

namespace ArsDocendi.IntegrationTests.Backend;

public sealed partial class ArquitecturaIdentityTests
{
    [Fact]
    public void Modulos_no_escriben_entidades_protegidas_de_identity()
    {
        var raiz = BuscarRaizRepositorio();
        var archivos = Directory.EnumerateFiles(
                Path.Combine(raiz, "backend", "src"), "*.cs", SearchOption.AllDirectories)
            .Where(ruta => Path.GetRelativePath(Path.Combine(raiz, "backend", "src"), ruta)
                .StartsWith("Modules.", StringComparison.Ordinal));
        var infracciones = new List<string>();

        foreach (var archivo in archivos)
        {
            var codigo = QuitarComentarios(File.ReadAllText(archivo));
            if (EscrituraIdentityProtegida().IsMatch(codigo))
            {
                infracciones.Add(Path.GetRelativePath(raiz, archivo));
            }
        }

        Assert.True(
            infracciones.Count == 0,
            "Los módulos sólo pueden leer identity mediante su contrato público. " +
            $"Escrituras o acceso directo detectados en: {string.Join(", ", infracciones)}");
    }

    private static string BuscarRaizRepositorio()
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

    private static string QuitarComentarios(string codigo) =>
        Comentarios().Replace(codigo, string.Empty);

    [GeneratedRegex(@"//.*?$|/\*.*?\*/", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex Comentarios();

    [GeneratedRegex(
        @"\bIdentityDbContext\b|\bnew\s+(?:Persona|Rol|Permiso|RolPermiso)\b|" +
        @"\bSet\s*<\s*(?:Persona|Rol|Permiso|RolPermiso)\s*>|" +
        @"(?:INSERT\s+INTO|UPDATE|DELETE\s+FROM)\s+identity\." +
        @"(?:personas|roles|permisos|rol_permisos)\b|" +
        @"\.(?:Personas|Roles|Permisos|RolPermisos)\s*\.\s*" +
        @"(?:Add|AddAsync|Update|UpdateRange|Remove|RemoveRange|ExecuteUpdate|ExecuteDelete)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex EscrituraIdentityProtegida();
}
