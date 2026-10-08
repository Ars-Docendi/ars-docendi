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

    /// <summary>
    /// Filas que documentan una variable de ambiente con default vacío.
    /// </summary>
    /// <remarks>
    /// Las opciones de texto van en las tablas de variables —con el prefijo de la
    /// sección, que es como se escriben en un ambiente— y su default se escribe
    /// «vacío» en vez de un valor. Una fila que dice «vacío» sobre una opción cuyo
    /// default NO lo es manda a alguien a confiar en que el mecanismo está apagado
    /// cuando no lo está, que es el peor de los dos errores posibles.
    /// </remarks>
    [GeneratedRegex(
        @"^\|\s*`Asistente__(?<opcion>\w+)`\s*\|\s*vacío\s*\|", RegexOptions.Multiline)]
    private static partial Regex FilaVacia();

    /// <summary>
    /// Filas cuyo default es un booleano, escrito <c>`true`</c> o <c>`false`</c>.
    /// </summary>
    /// <remarks>
    /// Una opción booleana documentada «apagada» que en el código está prendida es
    /// la forma más cara de mentir: quien la lee confía en que el prompt no cambia.
    /// </remarks>
    [GeneratedRegex(@"^\|\s*`(?<opcion>\w+)`\s*\|\s*`(?<default>true|false)`\s*\|", RegexOptions.Multiline)]
    private static partial Regex FilaBooleana();

    [Fact]
    public void Cada_default_documentado_coincide_con_el_del_codigo()
    {
        var documentadas = FilaDeLaTabla()
            .Matches(Readme())
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

    [Fact]
    public void Cada_default_booleano_documentado_coincide_con_el_del_codigo()
    {
        var documentadas = FilaBooleana()
            .Matches(Readme())
            .Select(fila => (
                Opcion: fila.Groups["opcion"].Value,
                Documentado: bool.Parse(fila.Groups["default"].Value)))
            .ToList();

        Assert.NotEmpty(documentadas);

        var reales = new OpcionesAsistente();
        var derivas = new List<string>();

        foreach (var (opcion, documentado) in documentadas)
        {
            var propiedad = typeof(OpcionesAsistente).GetProperty(
                opcion, BindingFlags.Public | BindingFlags.Instance);

            if (propiedad is null || propiedad.PropertyType != typeof(bool))
            {
                derivas.Add($"{opcion}: el README la documenta y no es una opción booleana del código.");
                continue;
            }

            var real = (bool)propiedad.GetValue(reales)!;

            if (real != documentado)
            {
                derivas.Add($"{opcion}: el código dice {real} y el README dice {documentado}.");
            }
        }

        Assert.True(
            derivas.Count == 0,
            "Hay defaults booleanos del README que no coinciden con los del código:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, derivas));
    }

    [Fact]
    public void El_glosario_esta_documentado_apagado()
    {
        // La garantía del change asistente-glosario-institucional: con el default el
        // prefijo no cambia. Se busca por nombre porque el test de arriba sólo valida
        // las filas que existen; no exige que ésta exista.
        var fila = FilaBooleana()
            .Matches(Readme())
            .SingleOrDefault(f => f.Groups["opcion"].Value == "GlosarioEnElPrefijo");

        Assert.NotNull(fila);
        Assert.Equal("false", fila.Groups["default"].Value);
    }

    [Fact]
    public void Cada_variable_documentada_como_vacia_lo_esta_de_verdad()
    {
        var documentadas = FilaVacia()
            .Matches(Readme())
            .Select(fila => fila.Groups["opcion"].Value)
            .ToList();

        Assert.NotEmpty(documentadas);

        var reales = new OpcionesAsistente();
        var derivas = new List<string>();

        foreach (var opcion in documentadas)
        {
            var propiedad = typeof(OpcionesAsistente).GetProperty(
                opcion, BindingFlags.Public | BindingFlags.Instance);

            if (propiedad is null || propiedad.PropertyType != typeof(string))
            {
                derivas.Add($"{opcion}: el README la documenta y no es una opción de texto del código.");
                continue;
            }

            if (!string.IsNullOrEmpty((string?)propiedad.GetValue(reales)))
            {
                derivas.Add($"{opcion}: el README la declara vacía y el código le da un valor.");
            }
        }

        Assert.True(
            derivas.Count == 0,
            "Hay variables documentadas como vacías que no lo son:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, derivas));
    }

    [Fact]
    public void Las_dos_opciones_de_los_cassettes_estan_documentadas()
    {
        var documentadas = FilaVacia()
            .Matches(Readme())
            .Select(fila => fila.Groups["opcion"].Value)
            .ToList();

        // APAGADAS POR DEFAULT ES LA GARANTÍA, y una garantía que no está escrita
        // donde alguien la va a buscar no sirve: la variable de re-grabación es la
        // única perilla del módulo que puede gastar plata.
        Assert.Contains(nameof(OpcionesAsistente.DirectorioDeCassettes), documentadas);
        Assert.Contains(nameof(OpcionesAsistente.RegrabarCassettes), documentadas);
    }

    private static string Readme() => File.ReadAllText(
        Path.Combine(RaizRepositorio.BackendSrc(), "Modules.Asistente", "README.md"));
}
