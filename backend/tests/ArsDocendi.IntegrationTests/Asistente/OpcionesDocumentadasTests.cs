using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Asistente;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>La tabla de configuración del README dice la verdad sobre los defaults.</summary>
public sealed partial class OpcionesDocumentadasTests
{
    /// <summary>Filas de la tabla «Configuración» del README, por nombre de opción.</summary>
    [GeneratedRegex(@"^\|\s*`(?<opcion>\w+)`\s*\|\s*(?<default>\d+)\s*\|", RegexOptions.Multiline)]
    private static partial Regex FilaDeLaTabla();

    /// <summary>Filas que documentan una variable de ambiente con default vacío.</summary>
    [GeneratedRegex(
        @"^\|\s*`Asistente__(?<opcion>\w+)`\s*\|\s*vacío\s*\|", RegexOptions.Multiline)]
    private static partial Regex FilaVacia();

    /// <summary>
    /// Filas cuyo default es un booleano, escrito <c>`true`</c> o <c>`false`</c>.
    /// </summary>
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

            // Una fila que nombra una opción inexistente es tan mentira como una con el número
            // cambiado: las dos mandan a alguien a configurar algo que no existe.
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
        // La garantía del change asistente-glosario-institucional: con el default el prefijo no cambia.
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
