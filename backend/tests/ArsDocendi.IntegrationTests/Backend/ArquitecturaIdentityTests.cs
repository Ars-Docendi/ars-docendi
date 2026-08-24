using System.Text.RegularExpressions;
using System.Xml.Linq;
using ArsDocendi.IntegrationTests.Infraestructura;

namespace ArsDocendi.IntegrationTests.Backend;

public sealed partial class ArquitecturaIdentityTests
{
    [Fact]
    public void Controllers_no_acceden_a_dbcontext_ni_repositorios()
    {
        var raiz = BuscarRaizRepositorio();
        var controllers = Directory.EnumerateFiles(
            Path.Combine(raiz, "backend", "src"), "*Controller.cs", SearchOption.AllDirectories)
            .ToArray();

        // Un guard que no distingue «no hay infracciones» de «no escaneé nada» no es
        // un guard: si mañana se renombra una carpeta, seguiría pasando en verde.
        Assert.NotEmpty(controllers);
        Assert.Contains(controllers, ruta => ruta.EndsWith("AsistenteController.cs", StringComparison.Ordinal));

        var infracciones = controllers
            .Where(archivo => DependenciaInfraestructuraEnController().IsMatch(
                QuitarComentarios(File.ReadAllText(archivo))))
            .Select(archivo => Path.GetRelativePath(raiz, archivo))
            .ToArray();

        Assert.True(
            infracciones.Length == 0,
            "Los controllers sólo pueden delegar en servicios. " +
            $"Dependencias de persistencia detectadas en: {string.Join(", ", infracciones)}");
    }

    [Fact]
    public void Proyectos_de_modulo_no_referencian_internals_de_otro_modulo()
    {
        var raiz = BuscarRaizRepositorio();
        var proyectos = Directory.EnumerateFiles(
                Path.Combine(raiz, "backend", "src"), "Modules.*.csproj", SearchOption.AllDirectories)
            .Where(ruta => !ruta.Contains(".Contracts", StringComparison.Ordinal))
            .ToArray();
        var infracciones = new List<string>();

        // Los cinco módulos internos, Asistente incluido. Si el glob dejara de
        // alcanzar alguno, el test pasaría sin haberlo mirado.
        Assert.Equal(5, proyectos.Length);
        Assert.Contains(proyectos, ruta => ruta.EndsWith("Modules.Asistente.csproj", StringComparison.Ordinal));

        foreach (var proyecto in proyectos)
        {
            var nombrePropio = Path.GetFileNameWithoutExtension(proyecto);
            var documento = XDocument.Load(proyecto);
            var referencias = documento.Descendants("ProjectReference")
                .Select(nodo => nodo.Attribute("Include")?.Value)
                .Where(ruta => ruta is not null && ruta.Contains("Modules.", StringComparison.Ordinal));
            foreach (var referencia in referencias)
            {
                var destino = Path.GetFileNameWithoutExtension(referencia!);
                if (!destino.EndsWith(".Contracts", StringComparison.Ordinal)
                    && !destino.Equals(nombrePropio, StringComparison.Ordinal))
                {
                    infracciones.Add($"{Path.GetFileName(proyecto)} -> {destino}");
                }
            }
        }

        Assert.True(
            infracciones.Count == 0,
            "La comunicación cross-module sólo puede usar Contracts. " +
            $"Referencias detectadas: {string.Join(", ", infracciones)}");
    }

    [Fact]
    public void Modulos_no_escriben_entidades_protegidas_de_identity()
    {
        var raiz = BuscarRaizRepositorio();
        var archivos = Directory.EnumerateFiles(
                Path.Combine(raiz, "backend", "src"), "*.cs", SearchOption.AllDirectories)
            .Where(ruta => Path.GetRelativePath(Path.Combine(raiz, "backend", "src"), ruta)
                .StartsWith("Modules.", StringComparison.Ordinal))
            .ToArray();
        var infracciones = new List<string>();

        // El asistente tiene una excepción declarada —consulta schemas ajenos sin
        // pasar por Contracts—, y por eso mismo su código tiene que estar dentro de
        // este barrido, no supuesto.
        Assert.NotEmpty(archivos);
        Assert.Contains(archivos, ruta => ruta.Contains(
            Path.Combine("Modules.Asistente", string.Empty), StringComparison.Ordinal));

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

    private static string BuscarRaizRepositorio() => RaizRepositorio.Ruta();

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

    [GeneratedRegex(@"\b(?:[A-Za-z]+DbContext|I?Repositorio[A-Za-z]+)\b")]
    private static partial Regex DependenciaInfraestructuraEnController();

}
