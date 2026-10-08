using System.Text.RegularExpressions;
using ArsDocendi.Evaluacion.Nucleo.Fixture;

namespace ArsDocendi.IntegrationTests.Evaluacion;

/// <summary>
/// Verifica el fixture sintético de evaluación.
/// </summary>
/// <remarks>
/// Un fixture que no es determinista hace que dos corridas no se puedan comparar,
/// y uno cuyo resultado esperado cambia con el calendario mide qué día lo
/// corriste. Las dos cosas rompen la métrica sin romper ningún test, así que hacen
/// falta tests que las miren de frente.
/// </remarks>
public sealed partial class FixtureDeterministaTests
{
    // ------------------------------------------------------- determinismo

    [Fact]
    public void Dos_ejecuciones_producen_el_mismo_contenido()
    {
        Assert.Equal(new GeneradorDeFixture().Generar(), new GeneradorDeFixture().Generar());
    }

    [Fact]
    public void Dos_generadores_distintos_dan_la_misma_huella()
    {
        Assert.Equal(new GeneradorDeFixture().Huella(), new GeneradorDeFixture().Huella());
    }

    [Fact]
    public void La_huella_tiene_forma_de_resumen_criptografico()
    {
        var huella = new GeneradorDeFixture().Huella();

        Assert.Equal(64, huella.Length);
        Assert.All(huella, caracter => Assert.Contains(caracter, "0123456789abcdef"));
    }

    [Fact]
    public void Un_fixture_distinto_tiene_otra_huella()
    {
        Assert.NotEqual(new GeneradorDeFixture(24).Huella(), new GeneradorDeFixture(25).Huella());
    }

    [Fact]
    public void Un_cambio_de_seccion_no_corre_los_valores_de_las_siguientes()
    {
        // LA TRAMPA DEL GENERADOR. Con una sola fuente de aleatoriedad, agregar una
        // persona correría todos los valores de designaciones y pedidos, y los
        // ítems de diálogo anclados en apellidos compartidos se romperían sin que
        // nadie los tocara.
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

    [Theory]
    [InlineData("now(")]
    [InlineData("current_date")]
    [InlineData("current_timestamp")]
    [InlineData("localtime")]
    [InlineData("clock_timestamp")]
    [InlineData("transaction_timestamp")]
    [InlineData("gen_random_uuid")]
    public void El_fixture_no_usa_el_reloj_ni_identificadores_al_azar(string prohibido)
    {
        // Un dataset cuyo resultado esperado cambia con el calendario mide qué día
        // lo corriste. Y un identificador al azar hace que el archivo cambie en
        // cada ejecución.
        Assert.DoesNotContain(
            prohibido, new GeneradorDeFixture().Generar(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Todas_las_fechas_del_fixture_derivan_del_ancla()
    {
        var contenido = new GeneradorDeFixture().Generar();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        // Si alguna fecha viniera del reloj, la de hoy aparecería en el archivo.
        Assert.DoesNotContain(hoy.ToString("yyyy-MM-dd"), contenido, StringComparison.Ordinal);

        var fechas = Fechas().Matches(contenido).Select(m => DateOnly.Parse(m.Groups[1].Value)).ToArray();

        Assert.NotEmpty(fechas);
        Assert.All(fechas, fecha =>
            Assert.InRange(fecha, GeneradorDeFixture.Ancla.AddYears(-60), GeneradorDeFixture.Ancla.AddYears(2)));
    }

    // --------------------------------------------------------- colisiones

    [Fact]
    public void Hay_nombres_de_materia_repetidos_entre_carreras()
    {
        // Sin esto, el detector de ambigüedad —épica posterior— no dispara nunca y
        // los ítems de diálogo que lo prueban dan verde sin medir nada.
        var contenido = new GeneradorDeFixture().Generar();

        foreach (var (nombre, carreras) in GeneradorDeFixture.MateriasCompartidas)
        {
            var apariciones = Contar(SeccionDe(contenido, "identity.materias"), nombre);

            Assert.Equal(carreras, apariciones);
        }
    }

    [Fact]
    public void Al_menos_una_materia_aparece_en_tres_carreras()
    {
        Assert.Contains(GeneradorDeFixture.MateriasCompartidas, materia => materia.Carreras == 3);
    }

    [Fact]
    public void Hay_apellidos_compartidos_con_las_cardinalidades_declaradas()
    {
        var personas = SeccionDe(new GeneradorDeFixture().Generar(), "identity.personas");

        foreach (var (apellido, cuantas) in GeneradorDeFixture.ApellidosCompartidos)
        {
            var apariciones = Contar(personas, $"'{apellido}'");

            Assert.Equal(cuantas, apariciones);
        }
    }

    [Fact]
    public void Al_menos_un_apellido_lo_comparten_tres_personas()
    {
        Assert.Contains(GeneradorDeFixture.ApellidosCompartidos, apellido => apellido.Personas == 3);
    }

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

    [Fact]
    public void Hay_exactamente_un_periodo_activo()
    {
        // «Lo actual» se resuelve con esta bandera y no comparando contra el reloj.
        var periodos = SeccionDe(new GeneradorDeFixture().Generar(), "designaciones.periodos");

        Assert.Equal(1, Contar(periodos, "TRUE)"));
    }

    [Fact]
    public void Hay_designaciones_con_vigencia_abierta_y_cerrada()
    {
        var designaciones = SeccionDe(
            new GeneradorDeFixture().Generar(), "designaciones.designaciones");

        // Las dos hacen falta: sin abiertas no hay «actual», y sin cerradas no se
        // puede distinguir «vigente» de «hubo».
        //
        // Una designación abierta lleva UNA fecha —vigente_desde— y una cerrada
        // lleva DOS. Contar las apariciones de la fecha es más robusto que mirar
        // cómo termina la línea, que depende de si es la última del INSERT.
        var abiertas = designaciones.Count(fila => Apariciones(fila, "DATE '") == 1);
        var cerradas = designaciones.Count(fila => Apariciones(fila, "DATE '") == 2);

        Assert.True(abiertas > 0, "No hay designaciones vigentes.");
        Assert.True(cerradas > 0, "No hay designaciones cerradas.");
        Assert.Equal(designaciones.Count, abiertas + cerradas);
    }

    [Fact]
    public void Hay_un_actor_de_cada_alcance()
    {
        // La métrica de abstención vive en la diferencia entre alcances: un actor
        // acotado que recibe cero filas no está en el mismo caso que uno global.
        var asignaciones = SeccionDe(new GeneradorDeFixture().Generar(), "identity.user_roles");

        Assert.Equal(4, asignaciones.Count);
    }

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
