using System.Text.Json;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El sobre en disco: sello arriba, cuerpo del proveedor verbatim abajo.
/// </summary>
/// <remarks>
/// Lo que se guarda es el cuerpo <b>crudo</b> de la respuesta y no la respuesta ya
/// traducida al contrato del puerto. Guardar la traducida dejaría el parseo del
/// adaptador —que es la mitad no cubierta y el motivo entero del mecanismo— del
/// lado de afuera del cassette.
/// </remarks>
public sealed class CassettesEnDiscoTests : IDisposable
{
    /// <summary>
    /// Un cuerpo con espaciado propio, claves fuera de orden alfabético y acentos.
    /// </summary>
    /// <remarks>
    /// Todo eso está a propósito: un cuerpo reserializado por nuestro serializador
    /// es un registro de <b>nuestro</b> formato, no del suyo, y taparía justo lo que
    /// la fixture existe para no tapar —el día que el proveedor cambie el orden de
    /// las claves, agregue un campo o mande otro escape—.
    /// </remarks>
    private const string CuerpoCrudo =
        "{  \"type\":\"message\" ,   \"content\" : "
        + "[ {\"type\":\"text\",\"text\":\"acentuación — «así»\"} ],\n"
        + "\"usage\":{\"output_tokens\":8,\"input_tokens\":120}   }";

    private static readonly SelloDelCassette Sello = new(
        Modelo: "claude-sonnet-5",
        Fecha: "2026-09-04",
        HashDelPrefijo: "7478338b5579b75192adfa2cfd1349e2930546496e4afcbeaf1fd18c05b09dc4",
        HashDelFixture: "aa11bb22cc33dd44");

    private readonly string _directorio = Path.Combine(
        Path.GetTempPath(), "cassettes-" + Guid.NewGuid().ToString("n"));

    // ---------------------------------------------------------------- el sello

    [Fact]
    public void El_cassette_escrito_declara_los_cuatro_campos_del_sello()
    {
        var almacen = new AlmacenDeCassettes(_directorio);

        almacen.Escribir("clave-uno", Sello, CuerpoCrudo);

        var archivo = JsonDocument.Parse(File.ReadAllText(Ruta("clave-uno")));
        var raiz = archivo.RootElement;

        // Los tres primeros son los que RNF-03 le exige a los reportes de
        // evaluación. El cuarto —el fixture— es lo que vuelve mecánica la garantía
        // de que ningún cassette lleva filas reales.
        Assert.Equal(Sello.Modelo, raiz.GetProperty("modelo").GetString());
        Assert.Equal(Sello.Fecha, raiz.GetProperty("fecha").GetString());
        Assert.Equal(Sello.HashDelPrefijo, raiz.GetProperty("hash_del_prefijo").GetString());
        Assert.Equal(Sello.HashDelFixture, raiz.GetProperty("hash_del_fixture").GetString());
    }

    [Fact]
    public void El_sello_va_arriba_del_cuerpo_en_el_archivo()
    {
        // El cuerpo verbatim es un muro de texto que se revisa mal en un diff. Se
        // acota poniendo el sello primero: lo que un revisor necesita mirar está
        // antes del muro.
        var almacen = new AlmacenDeCassettes(_directorio);
        almacen.Escribir("clave-uno", Sello, CuerpoCrudo);

        var texto = File.ReadAllText(Ruta("clave-uno"));

        Assert.True(
            texto.IndexOf("hash_del_fixture", StringComparison.Ordinal)
            < texto.IndexOf("\"cuerpo\"", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------- el cuerpo

    [Fact]
    public void El_cuerpo_almacenado_es_byte_por_byte_el_recibido()
    {
        var almacen = new AlmacenDeCassettes(_directorio);

        almacen.Escribir("clave-uno", Sello, CuerpoCrudo);

        var leido = almacen.Leer("clave-uno");

        Assert.NotNull(leido);
        Assert.Equal(CuerpoCrudo, leido.Cuerpo);
    }

    [Fact]
    public void Una_clave_que_no_esta_se_lee_como_ausente_y_no_como_error()
    {
        var almacen = new AlmacenDeCassettes(_directorio);

        // Distinguir «no está» de «está roto» es lo que permite que el handler
        // decida entre grabar y fallar cerrado.
        Assert.Null(almacen.Leer("clave-que-no-existe"));
    }

    [Fact]
    public void Leer_un_directorio_que_no_existe_no_revienta()
    {
        Assert.Null(new AlmacenDeCassettes(_directorio).Leer("clave-uno"));
    }

    // ------------------------------------------------- un sello incompleto

    [Fact]
    public void Un_cassette_al_que_le_falta_un_campo_del_sello_no_se_sirve()
    {
        Directory.CreateDirectory(_directorio);
        File.WriteAllText(
            Ruta("clave-uno"),
            """{"modelo":"claude-sonnet-5","fecha":"2026-09-04","hash_del_prefijo":"ab","cuerpo":"{}"}""");

        var falla = Assert.Throws<InvalidOperationException>(
            () => new AlmacenDeCassettes(_directorio).Leer("clave-uno"));

        // Archivo Y campo: sin el archivo no se sabe cuál rehacer, y sin el campo no
        // se sabe qué le falta.
        Assert.Contains("clave-uno.json", falla.Message, StringComparison.Ordinal);
        Assert.Contains("hash_del_fixture", falla.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_cassette_sin_cuerpo_tampoco_se_sirve()
    {
        Directory.CreateDirectory(_directorio);
        File.WriteAllText(
            Ruta("clave-uno"),
            """
            {"modelo":"m","fecha":"2026-09-04","hash_del_prefijo":"ab","hash_del_fixture":"cd"}
            """);

        var falla = Assert.Throws<InvalidOperationException>(
            () => new AlmacenDeCassettes(_directorio).Leer("clave-uno"));

        Assert.Contains("cuerpo", falla.Message, StringComparison.Ordinal);
    }

    // ------------------------------------------------- la escritura es atómica

    [Fact]
    public void Una_escritura_con_el_sello_incompleto_no_deja_archivo_en_el_directorio()
    {
        var almacen = new AlmacenDeCassettes(_directorio);

        var falla = Assert.Throws<InvalidOperationException>(
            () => almacen.Escribir("clave-uno", Sello with { HashDelFixture = "" }, CuerpoCrudo));

        Assert.Contains("hash_del_fixture", falla.Message, StringComparison.Ordinal);

        // Ni el definitivo ni un temporal a medio escribir. Un cassette parcial es
        // peor que ninguno: se encuentra por clave y falla recién al interpretarlo.
        Assert.Empty(
            Directory.Exists(_directorio)
                ? Directory.GetFileSystemEntries(_directorio)
                : []);
    }

    [Fact]
    public void Una_escritura_completa_no_deja_ningun_temporal()
    {
        var almacen = new AlmacenDeCassettes(_directorio);

        almacen.Escribir("clave-uno", Sello, CuerpoCrudo);

        Assert.Equal(
            [Ruta("clave-uno")],
            Directory.GetFileSystemEntries(_directorio).OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void Regrabar_la_misma_clave_pisa_el_archivo_y_no_lo_duplica()
    {
        var almacen = new AlmacenDeCassettes(_directorio);

        almacen.Escribir("clave-uno", Sello, CuerpoCrudo);
        almacen.Escribir("clave-uno", Sello, """{"otro":"cuerpo"}""");

        Assert.Single(Directory.GetFileSystemEntries(_directorio));
        Assert.Equal("""{"otro":"cuerpo"}""", almacen.Leer("clave-uno")!.Cuerpo);
    }

    // ------------------------------------------------ los sellos del directorio

    [Fact]
    public void Los_sellos_del_directorio_se_pueden_recorrer_para_diagnosticar()
    {
        var almacen = new AlmacenDeCassettes(_directorio);

        almacen.Escribir("clave-uno", Sello, CuerpoCrudo);
        almacen.Escribir("clave-dos", Sello with { HashDelPrefijo = "otro" }, CuerpoCrudo);

        Assert.Equal(
            [Sello.HashDelPrefijo, "otro"],
            almacen.HashesDePrefijoPresentes().OrderBy(x => x, StringComparer.Ordinal));
    }

    // ------------------------------------------------------------------ apoyo

    private string Ruta(string clave) => Path.Combine(_directorio, clave + ".json");

    public void Dispose()
    {
        if (Directory.Exists(_directorio))
        {
            Directory.Delete(_directorio, recursive: true);
        }
    }
}
