using System.Net;
using System.Text;
using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El parseo del pipeline, corrido sobre cuerpos que produjo un modelo.
/// </summary>
/// <remarks>
/// Es la mitad que ningún test cubría. <c>ProveedorGuionado</c> devuelve el JSON
/// que escribimos nosotros —y por eso es la capa correcta para afirmar sobre lo
/// que se le <b>manda</b> al modelo—, así que el parseo de
/// <c>GeneradorDeSql.Interpretar</c>, el del redactor y el del reescritor nunca
/// corrieron contra la salida real de uno.
///
/// <b>Los casos se descubren, no se escriben.</b> Un caso por cassette encontrado:
/// el día que la corrida financiada deje cuarenta, son cuarenta casos sin tocar un
/// archivo de test. El complemento obligatorio es que un directorio vacío falle —
/// una suite que itera una lista vacía pasa en verde con cero cobertura, que es el
/// peor resultado posible para un mecanismo cuyo propósito entero es cubrir algo.
///
/// <b>Se clasifican por el hash del prefijo del sello</b>, que las tres llamadas
/// del pipeline tienen distinto. No hace falta un campo nuevo en el sobre ni una
/// convención de nombres que alguien tenga que recordar al grabar.
/// </remarks>
public sealed class ParseoDesdeCassettesTests
{
    /// <summary>Un seguimiento elíptico, el que el reescritor tiene que resolver.</summary>
    private const string SeguimientoOriginal = "¿y en Sistemas?";

    private static readonly string HuellaDeRedaccion =
        ClaveDeCassette.HuellaDe(RedactorDeRespuesta.Instrucciones);

    private static readonly string HuellaDeReescritura =
        ClaveDeCassette.HuellaDe(ReescritorDePreguntas.Instrucciones);

    // ---------------------------------------------- un directorio vacío no pasa

    [Fact]
    public void Hay_cassettes_de_generacion_que_ejercitar()
    {
        Assert.True(
            DeGeneracion().Count > 0,
            $"No hay ningún cassette de generación en {RaizRepositorio.Cassettes()}: "
            + "estos tests estarían pasando en verde sin ejercitar nada.");
    }

    [Fact]
    public void Hay_cassettes_de_redaccion_que_ejercitar()
    {
        Assert.True(
            DeRedaccion().Count > 0,
            $"No hay ningún cassette de redacción en {RaizRepositorio.Cassettes()}: "
            + "estos tests estarían pasando en verde sin ejercitar nada.");
    }

    [Fact]
    public void Hay_cassettes_de_reescritura_que_ejercitar()
    {
        Assert.True(
            DeReescritura().Count > 0,
            $"No hay ningún cassette de reescritura en {RaizRepositorio.Cassettes()}: "
            + "estos tests estarían pasando en verde sin ejercitar nada.");
    }

    [Fact]
    public void Cada_cassette_del_directorio_cae_en_exactamente_un_carril()
    {
        var todos = Todos().Count;

        // ES LA PROPIEDAD QUE HACE QUE AGREGAR UN CASSETTE SUME UN CASO. Si la
        // clasificación dejara alguno afuera, el archivo entraría al repositorio y
        // no ejercitaría nada, que se ve exactamente igual que no haberlo agregado.
        Assert.Equal(
            todos,
            DeGeneracion().Count + DeRedaccion().Count + DeReescritura().Count);
        Assert.True(todos > 0);
    }

    // ------------------------------------------------------------- generación

    [Theory]
    [MemberData(nameof(NombresDeGeneracion))]
    public async Task El_generador_interpreta_cada_cassette_de_generacion(string archivo)
    {
        var texto = await TextoAsync(archivo, TestContext.Current.CancellationToken);
        var generacion = GeneradorDeSql.Interpretar(texto);

        // «No pude interpretar la pregunta» es el razonamiento con que el generador
        // resuelve una respuesta ININTELIGIBLE. Una abstención real trae el
        // razonamiento del modelo, así que ese texto exacto significa que el parseo
        // no pudo leer lo que el modelo escribió — que es justamente lo que estos
        // cassettes existen para detectar.
        Assert.NotEqual(
            "No pude interpretar la pregunta con la información disponible.",
            generacion.Razonamiento);

        if (generacion.EsContestable)
        {
            Assert.False(string.IsNullOrWhiteSpace(generacion.Sql));
            Assert.False(string.IsNullOrWhiteSpace(generacion.Categoria));
        }
        else
        {
            Assert.Null(generacion.Sql);
        }
    }

    // -------------------------------------------------------------- redacción

    [Theory]
    [MemberData(nameof(NombresDeRedaccion))]
    public async Task El_redactor_saca_prosa_de_cada_cassette_de_redaccion(string archivo)
    {
        var texto = await TextoAsync(archivo, TestContext.Current.CancellationToken);

        // Es lo que hace `RedactarAsync` con la respuesta: recortar y devolver. Lo
        // que se afirma es que de un cuerpo real sale una respuesta que se le puede
        // mostrar a alguien, y no un bloque de código ni una cadena vacía.
        var respuesta = texto.Trim();

        Assert.NotEmpty(respuesta);
        Assert.DoesNotContain("```", respuesta, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT ", respuesta, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------ reescritura

    [Theory]
    [MemberData(nameof(NombresDeReescritura))]
    public async Task El_reescritor_interpreta_cada_cassette_de_reescritura(string archivo)
    {
        var texto = await TextoAsync(archivo, TestContext.Current.CancellationToken);
        var reescrita = ReescritorDePreguntas.Interpretar(texto, SeguimientoOriginal);

        // `Interpretar` devuelve el ORIGINAL cuando la respuesta no sirve —vacía o
        // desmedida—, así que recibir el original de vuelta es la señal de que el
        // parseo descartó lo que el modelo escribió.
        Assert.NotEqual(SeguimientoOriginal, reescrita);
        Assert.DoesNotContain('\n', reescrita);
    }

    // ------------------------------------------------------------------ apoyo

    public static TheoryData<string> NombresDeGeneracion() => Nombres(DeGeneracion());

    public static TheoryData<string> NombresDeRedaccion() => Nombres(DeRedaccion());

    public static TheoryData<string> NombresDeReescritura() => Nombres(DeReescritura());

    private static TheoryData<string> Nombres(IReadOnlyList<CassetteEnDisco> cassettes)
    {
        var datos = new TheoryData<string>();

        foreach (var cassette in cassettes)
        {
            datos.Add(cassette.Archivo);
        }

        return datos;
    }

    private static IReadOnlyList<CassetteEnDisco> Todos() =>
        new AlmacenDeCassettes(RaizRepositorio.Cassettes()).Todos();

    /// <summary>
    /// Los que NO son de redacción ni de reescritura son de generación.
    /// </summary>
    /// <remarks>
    /// Las otras dos llamadas tienen un prefijo constante y por lo tanto una huella
    /// conocida. La de generación es el prefijo del esquema, que cambia con los
    /// privilegios efectivos: enumerarla sería fijar un valor que el sistema
    /// recalcula solo.
    /// </remarks>
    private static IReadOnlyList<CassetteEnDisco> DeGeneracion() =>
        [.. Todos().Where(cassette =>
            !EsDe(cassette, HuellaDeRedaccion) && !EsDe(cassette, HuellaDeReescritura))];

    private static IReadOnlyList<CassetteEnDisco> DeRedaccion() =>
        [.. Todos().Where(cassette => EsDe(cassette, HuellaDeRedaccion))];

    private static IReadOnlyList<CassetteEnDisco> DeReescritura() =>
        [.. Todos().Where(cassette => EsDe(cassette, HuellaDeReescritura))];

    private static bool EsDe(CassetteEnDisco cassette, string huella) =>
        string.Equals(cassette.Sello.HashDelPrefijo, huella, StringComparison.Ordinal);

    /// <summary>
    /// El texto que el adaptador REAL saca del cuerpo grabado.
    /// </summary>
    /// <remarks>
    /// No se lee el JSON del cassette a mano: se lo sirve por el cable al adaptador,
    /// que es quien tiene que saber leerlo. Interpretarlo acá probaría a este test.
    /// </remarks>
    private static async Task<string> TextoAsync(string archivo, CancellationToken ct)
    {
        var cassette = new AlmacenDeCassettes(RaizRepositorio.Cassettes())
            .Leer(Path.GetFileNameWithoutExtension(archivo));

        Assert.NotNull(cassette);

        using var terminal = new TransporteFalso(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(cassette.Cuerpo, Encoding.UTF8, "application/json"),
        });

        using var proveedor = new ProveedorAnthropic(
            new HttpClient(terminal, disposeHandler: false),
            "clave-de-prueba",
            cassette.Sello.Modelo,
            new RegistroDeCapturas().Logger<ProveedorAnthropic>());

        var respuesta = await proveedor.CompletarAsync(
            new SolicitudAlModelo
            {
                PrefijoEstable = "irrelevante: el cuerpo de la respuesta ya está grabado",
                Mensaje = "irrelevante",
                Temperatura = 0.0m,
                Esfuerzo = EsfuerzoDelModelo.Medio,
                MaximoDeTokens = 4000,
            },
            ct);

        return respuesta.Texto;
    }
}
