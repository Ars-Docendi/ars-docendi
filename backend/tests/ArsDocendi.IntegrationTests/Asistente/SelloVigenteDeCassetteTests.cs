using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El sello se compara contra lo vigente antes de servir cualquier cosa.
/// </summary>
/// <remarks>
/// Un cassette es una respuesta grabada contra <b>un</b> esquema y <b>un</b>
/// fixture. Servirlo contra otros dos no es reproducir: es contestar una pregunta
/// que ya no es la misma, y con la ventaja de que nada falla mientras tanto.
/// </remarks>
public sealed class SelloVigenteDeCassetteTests : IDisposable
{
    private const string PrefijoVigente = "hash-del-prefijo-vigente";
    private const string FixtureVigente = "hash-del-fixture-vigente";
    private const string Cuerpo = """{"type":"message","content":[]}""";

    private static readonly SelloDelCassette Sello = new(
        Modelo: "claude-sonnet-5",
        Fecha: "2026-09-04",
        HashDelPrefijo: PrefijoVigente,
        HashDelFixture: FixtureVigente);

    private readonly string _directorio = Path.Combine(
        Path.GetTempPath(), "cassettes-" + Guid.NewGuid().ToString("n"));

    // ------------------------------------------------------------ el caso feliz

    [Fact]
    public void Un_cassette_con_el_sello_vigente_se_sirve()
    {
        var almacen = new AlmacenDeCassettes(_directorio);
        almacen.Escribir("clave-uno", Sello, Cuerpo);

        Assert.Equal(
            Cuerpo, almacen.Reproducir("clave-uno", PrefijoVigente, FixtureVigente));
    }

    // ---------------------------------------------------------------- el prefijo

    [Fact]
    public void Un_cassette_sellado_con_otro_prefijo_se_rechaza_en_vez_de_servirse()
    {
        var almacen = new AlmacenDeCassettes(_directorio);
        almacen.Escribir("clave-uno", Sello with { HashDelPrefijo = "prefijo-viejo" }, Cuerpo);

        var falla = Assert.Throws<InvalidOperationException>(
            () => almacen.Reproducir("clave-uno", PrefijoVigente, FixtureVigente));

        // Servirlo igual sería contestar con una respuesta que el modelo dio sobre
        // otro esquema. El prefijo se deriva de los privilegios efectivos: uno
        // distinto significa que las columnas visibles cambiaron.
        Assert.Contains("prefijo", falla.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("clave-uno.json", falla.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Con_el_directorio_lleno_de_cassettes_de_otro_prefijo_el_error_lo_dice()
    {
        var almacen = new AlmacenDeCassettes(_directorio);
        almacen.Escribir("otra-uno", Sello with { HashDelPrefijo = "prefijo-viejo" }, Cuerpo);
        almacen.Escribir("otra-dos", Sello with { HashDelPrefijo = "prefijo-viejo" }, Cuerpo);

        var mensaje = almacen.ExplicarAusencia("clave-que-falta", PrefijoVigente);

        // «Falta el cassette» y «los cassettes son de otro prefijo» mandan a hacer
        // cosas distintas: la primera, a grabar esa pregunta; la segunda, a
        // regrabarlas todas. Sin la distinción, el segundo caso se ve como el
        // primero y alguien graba de a una para siempre.
        Assert.Contains("clave-que-falta", mensaje, StringComparison.Ordinal);
        Assert.Contains(_directorio, mensaje, StringComparison.Ordinal);
        Assert.Contains("otro prefijo", mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Con_cassettes_del_prefijo_vigente_el_error_dice_solo_que_falta_ese()
    {
        var almacen = new AlmacenDeCassettes(_directorio);
        almacen.Escribir("otra-uno", Sello, Cuerpo);

        var mensaje = almacen.ExplicarAusencia("clave-que-falta", PrefijoVigente);

        Assert.Contains("clave-que-falta", mensaje, StringComparison.Ordinal);
        Assert.DoesNotContain("otro prefijo", mensaje, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- el fixture

    [Fact]
    public void Un_cassette_sin_el_hash_del_fixture_vigente_no_se_sirve()
    {
        var almacen = new AlmacenDeCassettes(_directorio);
        almacen.Escribir("clave-uno", Sello with { HashDelFixture = "fixture-viejo" }, Cuerpo);

        var falla = Assert.Throws<InvalidOperationException>(
            () => almacen.Reproducir("clave-uno", PrefijoVigente, FixtureVigente));

        // Es lo que hace mecánica la garantía de que ningún cassette lleva filas
        // reales: el que se grabó contra otros datos describe otro sistema.
        Assert.Contains("fixture", falla.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("clave-uno.json", falla.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Sin_saber_cual_es_el_fixture_vigente_no_se_sirve_nada()
    {
        var almacen = new AlmacenDeCassettes(_directorio);
        almacen.Escribir("clave-uno", Sello, Cuerpo);

        // Un cassette que no se puede verificar es indistinguible de uno grabado
        // contra una base de desarrollo con datos importados. Sin con qué comparar,
        // la respuesta correcta es no servirlo.
        var falla = Assert.Throws<InvalidOperationException>(
            () => almacen.Reproducir("clave-uno", PrefijoVigente, string.Empty));

        Assert.Contains("fixture", falla.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------ lo que no está

    [Fact]
    public void Una_clave_sin_cassette_se_reproduce_como_ausente()
    {
        Assert.Null(new AlmacenDeCassettes(_directorio)
            .Reproducir("clave-uno", PrefijoVigente, FixtureVigente));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directorio))
        {
            Directory.Delete(_directorio, recursive: true);
        }
    }
}
