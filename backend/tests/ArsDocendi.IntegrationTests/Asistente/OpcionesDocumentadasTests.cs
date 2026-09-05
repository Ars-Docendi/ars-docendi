using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Asistente;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// La tabla de configuración del README dice la verdad sobre los defaults.
/// </summary>
/// <remarks>
/// Es el mismo criterio con que <c>ManifiestoPrivilegiosTests</c> trata a
/// <c>manifiesto-privilegios.json</c>: la documentación de un valor operativo es un
/// dato verificado, no prosa. Sin este test, subir un default y no tocar el README
/// deja una cifra equivocada en el único lugar donde alguien la va a buscar —y el
/// síntoma recién aparece cuando alguien planifica con ella.
///
/// Se compara contra <c>new OpcionesAsistente()</c> y no contra constantes propias:
/// duplicar los números acá sería crear una tercera versión de la verdad.
/// </remarks>
public sealed partial class OpcionesDocumentadasTests
{
    /// <summary>
    /// Filas de la tabla «Configuración» del README, por nombre de opción.
    /// </summary>
    /// <remarks>
    /// Sólo se leen las filas cuyo default es un entero. Las opciones de texto
    /// —proveedor, modelo, esfuerzos— se documentan en otra tabla, con su propio
    /// formato, y no las cubre este test.
    /// </remarks>
    [GeneratedRegex(@"^\|\s*`(?<opcion>\w+)`\s*\|\s*(?<default>\d+)\s*\|", RegexOptions.Multiline)]
    private static partial Regex FilaDeLaTabla();

    [Fact]
    public void Cada_default_documentado_coincide_con_el_del_codigo()
    {
        var readme = File.ReadAllText(
            Path.Combine(
                RaizRepositorio.BackendSrc(), "Modules.Asistente", "README.md"));

        var documentadas = FilaDeLaTabla()
            .Matches(readme)
            .Select(fila => (
                Opcion: fila.Groups["opcion"].Value,
                Documentado: int.Parse(fila.Groups["default"].Value, CultureInfo.InvariantCulture)))
            .ToList();

        Assert.NotEmpty(documentadas);

        var reales = new OpcionesAsistente();
        var derivas = new List<string>();

        foreach (var (opcion, documentado) in documentadas)
        {
            var propiedad = typeof(OpcionesAsistente).GetProperty(
                opcion, BindingFlags.Public | BindingFlags.Instance);

            // Una fila que nombra una opción inexistente es tan mentira como una con
            // el número cambiado: las dos mandan a alguien a configurar algo que no
            // existe.
            if (propiedad is null || propiedad.PropertyType != typeof(int))
            {
                derivas.Add($"{opcion}: el README la documenta y no es una opción entera del código.");
                continue;
            }

            var real = (int)propiedad.GetValue(reales)!;

            if (real != documentado)
            {
                derivas.Add($"{opcion}: el código dice {real} y el README dice {documentado}.");
            }
        }

        Assert.True(
            derivas.Count == 0,
            "La tabla de configuración del README no coincide con los defaults del código:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, derivas));
    }
}
