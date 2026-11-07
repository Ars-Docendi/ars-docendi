using System.Text.RegularExpressions;
using ArsDocendi.Evaluacion.Nucleo.Fixture;

namespace ArsDocendi.IntegrationTests.Evaluacion;

/// <summary>Verifica el fixture sintético de evaluación.</summary>
public sealed partial class FixtureDeterministaTests
{
    // ------------------------------------------------------- determinismo

    [Fact]
    public void Dos_ejecuciones_producen_el_mismo_contenido()
    {
        Assert.Equal(new GeneradorDeFixture().Generar(), new GeneradorDeFixture().Generar());
    }

    [Fact]
    public void Un_fixture_distinto_tiene_otra_huella()
    {
        Assert.NotEqual(new GeneradorDeFixture(24).Huella(), new GeneradorDeFixture(25).Huella());
    }

    [Fact]
    public void Un_cambio_de_seccion_no_corre_los_valores_de_las_siguientes()
    {
        // LA TRAMPA DEL GENERADOR. Con una sola fuente de aleatoriedad, agregar una persona
        // correría todos los valores de designaciones y pedidos, y los ítems de diálogo anclados en
        // apellidos compartidos se romperían sin que nadie los tocara.
        var conVeinticuatro = SeccionDe(new GeneradorDeFixture(24).Generar(), "designaciones.pedidos");
        var conVeinticinco = SeccionDe(new GeneradorDeFixture(25).Generar(), "designaciones.pedidos");

        // Los pedidos son la mitad de las personas, así que con 25 hay uno más:
        // se comparan las filas que existen en las dos.
        var comunes = Math.Min(conVeinticuatro.Count, conVeinticinco.Count);

        Assert.True(comunes > 0);
        Assert.Equal(
            conVeinticuatro.Take(comunes),
            conVeinticinco.Take(comunes));
    }

    // ------------------------------------------------------------- reloj

    // --------------------------------------------------------- colisiones

    [Fact]
    public void Subir_las_personas_por_encima_de_los_apellidos_se_rechaza()
    {
        // Ciclar los apellidos haría que los compartidos aparecieran MÁS veces de
        // las declaradas, y el test de cardinalidades fallaría sin explicar por qué.
        var excepcion = Assert.Throws<ArgumentOutOfRangeException>(
            () => new GeneradorDeFixture(500));

        Assert.Contains("apellidos", excepcion.Message, StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------- banderas del dominio

    // ------------------------------------------------------------------ apoyo

    /// <summary>Las filas de valores de un INSERT sobre una tabla dada.</summary>
    private static IReadOnlyList<string> SeccionDe(string contenido, string tabla)
    {
        var lineas = contenido.Split('\n');
        var adentro = false;
        var filas = new List<string>();

        foreach (var linea in lineas)
        {
            if (linea.StartsWith("INSERT INTO ", StringComparison.Ordinal))
            {
                adentro = linea.Contains(tabla, StringComparison.Ordinal);
                continue;
            }

            if (linea.StartsWith("ON CONFLICT", StringComparison.Ordinal))
            {
                adentro = false;
                continue;
            }

            if (adentro && linea.StartsWith("    (", StringComparison.Ordinal))
            {
                filas.Add(linea);
            }
        }

        Assert.True(filas.Count > 0, $"No se encontró ninguna fila de {tabla}.");
        return filas;
    }

    private static int Contar(IReadOnlyList<string> filas, string texto) =>
        filas.Count(fila => fila.Contains(texto, StringComparison.Ordinal));

    private static int Apariciones(string texto, string buscado)
    {
        var cuantas = 0;
        var desde = 0;

        while ((desde = texto.IndexOf(buscado, desde, StringComparison.Ordinal)) >= 0)
        {
            cuantas++;
            desde += buscado.Length;
        }

        return cuantas;
    }

    [GeneratedRegex(@"DATE '(\d{4}-\d{2}-\d{2})'")]
    private static partial Regex Fechas();
}
