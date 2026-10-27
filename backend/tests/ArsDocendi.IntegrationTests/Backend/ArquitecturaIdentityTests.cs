using System.Xml.Linq;
using ArsDocendi.IntegrationTests.Infraestructura;

namespace ArsDocendi.IntegrationTests.Backend;

public sealed class ArquitecturaIdentityTests
{
    [Fact]
    public void Proyectos_de_modulo_no_referencian_internals_de_otro_modulo()
    {
        var raiz = RaizRepositorio.Ruta();
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
}
