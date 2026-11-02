using System.Xml.Linq;

namespace ArsDocendi.IntegrationTests.Backend;

public sealed class ArquitecturaIdentityTests
{
    [Fact]
    public void Proyectos_de_modulo_no_referencian_internals_de_otro_modulo()
    {
        var raiz = BuscarRaizRepositorio();
        var proyectos = Directory.EnumerateFiles(
                Path.Combine(raiz, "backend", "src"), "Modules.*.csproj", SearchOption.AllDirectories)
            .Where(ruta => !ruta.Contains(".Contracts", StringComparison.Ordinal));
        var infracciones = new List<string>();

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
    public void Notificaciones_es_una_hoja_del_grafo_y_sus_emisores_solo_ven_sus_Contracts()
    {
        var raiz = BuscarRaizRepositorio();
        string[] Referencias(string proyecto) =>
            XDocument.Load(Path.Combine(raiz, "backend", "src", proyecto, proyecto + ".csproj"))
                .Descendants("ProjectReference")
                .Select(nodo => Path.GetFileNameWithoutExtension(nodo.Attribute("Include")!.Value.Replace('\\', '/')))
                .ToArray();

        // Contracts hold only records and one interface: no project reference at all.
        Assert.Empty(Referencias("Modules.Notificaciones.Contracts"));
        // The module depends on infrastructure and on its own Contracts; no business module.
        Assert.Equal(
            ["ArsDocendi.Migraciones", "ArsDocendi.Shared", "Modules.Notificaciones.Contracts"],
            Referencias("Modules.Notificaciones").Order().ToArray());
        // Emitters reference the Contracts, never the implementation.
        foreach (var emisor in new[] { "Modules.Aulas", "Modules.Designaciones", "Modules.Tareas" })
        {
            var referencias = Referencias(emisor);
            Assert.Contains("Modules.Notificaciones.Contracts", referencias);
            Assert.DoesNotContain("Modules.Notificaciones", referencias);
        }
    }

    private static string BuscarRaizRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null)
        {
            if (Directory.Exists(Path.Combine(directorio.FullName, "backend", "src"))
                && File.Exists(Path.Combine(directorio.FullName, "AGENTS.md")))
            {
                return directorio.FullName;
            }

            directorio = directorio.Parent;
        }

        throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
    }
}
