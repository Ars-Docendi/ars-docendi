using System.Text.RegularExpressions;
using ArsDocendi.Evaluacion.Nucleo.Fixture;
using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Ningún cassette versionado lleva la credencial ni datos personales reales.
/// </summary>
/// <remarks>
/// Los dos hechos quedan <b>verificados y no supuestos</b>, que es la diferencia
/// entre una garantía y una convención. La convención se cumple hasta que alguien
/// grabe contra su base de desarrollo con datos importados, o hasta que un cambio
/// del formato del sobre empiece a arrastrar cabeceras.
///
/// Cada guard viene en par, con el mismo criterio que
/// <c>ArquitecturaAsistenteTests</c>: uno corre sobre los cassettes reales y el
/// otro alimenta al mismo detector con una violación sintética. Sin el segundo,
/// un detector roto —una regex que no matchea nada— pasaría en verde para siempre.
/// </remarks>
public sealed partial class HigieneDeCassettesTests
{
    // ------------------------------------------------------------- la credencial

    [Fact]
    public void Ningun_cassette_versionado_trae_la_forma_de_la_credencial()
    {
        var archivos = Cassettes();

        // El sobre guarda el cuerpo de la RESPUESTA, y las cabeceras de la solicitud
        // no entran nunca: que la clave no pueda filtrarse a disco es estructural. Lo
        // que este guard cuida es que siga siéndolo — una credencial commiteada no se
        // arregla borrándola después, porque queda en el historial para siempre.
        Assert.NotEmpty(archivos);

        var culpables = archivos
            .Where(archivo => FormaDeLaCredencial().IsMatch(archivo.Contenido))
            .Select(archivo => archivo.Nombre)
            .ToList();

        Assert.True(
            culpables.Count == 0,
            "Hay cassettes versionados con la forma de una credencial adentro. "
            + "Detectado en: " + string.Join(", ", culpables));
    }

    [Fact]
    public void El_detector_reconoce_una_credencial_en_un_cassette_sintetico()
    {
        string[] sinteticos =
        [
            """{"cuerpo":"{}","x-api-key":"sk-ant-api03-abcdef123456"}""",
            """{"cabeceras":{"X-Api-Key":"loquesea"},"cuerpo":"{}"}""",
            """{"cabeceras":{"authorization":"Bearer abcdef1234567890"},"cuerpo":"{}"}""",
        ];

        Assert.All(sinteticos, texto => Assert.Matches(FormaDeLaCredencial(), texto));
    }

    [Fact]
    public void Un_cassette_legitimo_no_dispara_el_detector()
    {
        // Contraprueba del par: un detector que matcheara cualquier cosa sería tan
        // inútil como uno que no matchea nada, y encima bloquearía cada grabación.
        Assert.DoesNotMatch(
            FormaDeLaCredencial(),
            """{"modelo":"claude-sonnet-5","cuerpo":"{\"content\":[{\"text\":\"SELECT 1\"}]}"}""");
    }

    // ---------------------------------------------------------------- el fixture

    [Fact]
    public void Todos_los_cassettes_versionados_declaran_el_fixture_vigente()
    {
        var vigente = new GeneradorDeFixture().Huella();
        var cassettes = new AlmacenDeCassettes(RaizRepositorio.Cassettes()).Todos();

        Assert.NotEmpty(cassettes);

        var ajenos = cassettes
            .Where(cassette => !string.Equals(
                cassette.Sello.HashDelFixture, vigente, StringComparison.Ordinal))
            .Select(cassette => cassette.Archivo)
            .ToList();

        // Es lo que convierte «se graba contra el fixture sintético» de convención en
        // chequeo. Importa sobre todo por los cassettes de redacción, que llevan filas
        // adentro: enmascaradas, pero filas.
        Assert.True(
            ajenos.Count == 0,
            $"Hay cassettes grabados contra otro fixture (el vigente es {vigente}). "
            + "Volvé a grabarlos. Detectado en: " + string.Join(", ", ajenos));
    }

    [Fact]
    public void Todos_los_cassettes_versionados_declaran_su_sello_completo()
    {
        // `Todos()` ya falla al interpretar un sello incompleto, así que basta con
        // recorrerlos: lo que este test agrega es que el recorrido ocurra en CI.
        Assert.All(
            new AlmacenDeCassettes(RaizRepositorio.Cassettes()).Todos(),
            cassette =>
            {
                Assert.Empty(cassette.Sello.CamposVacios());
                Assert.NotEmpty(cassette.Cuerpo);
            });
    }

    // --------------------------------------------- sin excepción de arquitectura

    [Fact]
    public void Las_piezas_del_mecanismo_no_nombran_el_SDK_del_proveedor()
    {
        // El grabador ve cuerpos HTTP, no tipos de la librería. Es lo que permitió
        // sumar el mecanismo sin ampliar la excepción del guard general
        // —`El_SDK_del_proveedor_se_nombra_en_un_solo_archivo`—, que sigue teniendo
        // un solo archivo eximido.
        string[] piezas =
        [
            "GrabadorDeCassettes.cs",
            "AlmacenDeCassettes.cs",
            "ClaveDeCassette.cs",
            "SelloDelCassette.cs",
        ];

        foreach (var pieza in piezas)
        {
            var ruta = Path.Combine(
                RaizRepositorio.BackendSrc(), "Modules.Asistente", "Infrastructure", pieza);

            Assert.True(File.Exists(ruta), $"No se encontró {ruta}.");
            Assert.DoesNotMatch(SdkDelProveedor(), File.ReadAllText(ruta));
        }
    }

    // ------------------------------------------------------------------ apoyo

    private sealed record Archivo(string Nombre, string Contenido);

    private static Archivo[] Cassettes() =>
        !Directory.Exists(RaizRepositorio.Cassettes())
            ? []
            : [.. Directory.EnumerateFiles(RaizRepositorio.Cassettes(), "*.json")
                .OrderBy(ruta => ruta, StringComparer.Ordinal)
                .Select(ruta => new Archivo(Path.GetFileName(ruta), File.ReadAllText(ruta)))];

    /// <summary>
    /// La forma de una credencial del proveedor, o de la cabecera que la lleva.
    /// </summary>
    /// <remarks>
    /// Busca las tres formas en que podría aparecer: el prefijo con que el proveedor
    /// emite sus claves, el nombre de la cabecera en que viajan, y un portador
    /// genérico. Ninguna de las tres tiene por qué estar en un cassette, que guarda
    /// el cuerpo de una respuesta.
    /// </remarks>
    [GeneratedRegex(
        @"sk-ant-[A-Za-z0-9_\-]{6,}|x-api-key|\bBearer\s+[A-Za-z0-9._\-]{10,}",
        RegexOptions.IgnoreCase)]
    private static partial Regex FormaDeLaCredencial();

    /// <summary>El namespace del SDK y su cliente, igual que el guard general.</summary>
    [GeneratedRegex(@"\bAnthropic\.[A-Z]|\bAnthropicClient\b")]
    private static partial Regex SdkDelProveedor();
}
