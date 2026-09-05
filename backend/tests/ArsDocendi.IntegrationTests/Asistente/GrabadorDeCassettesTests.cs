using System.Net;
using System.Text;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.Extensions.Logging;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El grabador en el pipeline: reproducir, grabar o fallar cerrado.
/// </summary>
/// <remarks>
/// Va como <see cref="DelegatingHandler"/> del cliente HTTP con nombre y no como
/// decorador del puerto. Un decorador grabaría la respuesta <b>ya traducida</b>, y
/// entonces el parseo del adaptador —que es la mitad no cubierta y el motivo
/// entero del mecanismo— quedaría del lado de afuera del cassette.
/// </remarks>
public sealed class GrabadorDeCassettesTests : IDisposable
{
    private const string Fixture = "hash-del-fixture-vigente";

    private readonly string _directorio = Path.Combine(
        Path.GetTempPath(), "cassettes-" + Guid.NewGuid().ToString("n"));

    // ------------------------------------------------------------ falla cerrada

    [Fact]
    public async Task Sin_cassette_y_sin_regrabacion_el_transporte_no_recibe_nada()
    {
        using var transporte = TransporteFalso.QueResponde();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => EnviarAsync(transporte, regrabar: false));

        // ES LA PROPIEDAD ENTERA DE «NUNCA UNA LLAMADA DE RED EN CI». El handler
        // lanza SIN invocar hacia adentro: contar los intentos del transporte y
        // exigir cero es la única forma de que sea una propiedad del código y no una
        // promesa.
        Assert.Equal(0, transporte.Intentos);
    }

    [Fact]
    public async Task El_error_de_falla_cerrada_nombra_la_clave_y_el_directorio()
    {
        using var transporte = TransporteFalso.QueResponde();

        var falla = await Assert.ThrowsAsync<InvalidOperationException>(
            () => EnviarAsync(transporte, regrabar: false));

        // El modo de fallar esperado es «alguien cambió una pregunta del dataset y
        // todavía no la grabó», y ese diagnóstico se resuelve leyendo el mensaje.
        Assert.Contains(ClaveEsperada(), falla.Message, StringComparison.Ordinal);
        Assert.Contains(_directorio, falla.Message, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- grabación

    [Fact]
    public async Task Con_la_regrabacion_puesta_y_sin_cassette_la_llamada_sale_y_queda_grabada()
    {
        using var transporte = new TransporteFalso(_ => TransporteFalso.Exito("SELECT 42"));

        using var respuesta = await EnviarAsync(transporte, regrabar: true);

        Assert.Equal(1, transporte.Intentos);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var cassette = new AlmacenDeCassettes(_directorio).Leer(ClaveEsperada());

        Assert.NotNull(cassette);
        Assert.Contains("SELECT 42", cassette.Cuerpo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_cuerpo_que_sigue_viaje_es_el_mismo_que_se_grabo()
    {
        using var transporte = new TransporteFalso(_ => TransporteFalso.Exito("SELECT 42"));

        using var respuesta = await EnviarAsync(transporte, regrabar: true);

        var recibido = await respuesta.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(new AlmacenDeCassettes(_directorio).Leer(ClaveEsperada())!.Cuerpo, recibido);
    }

    [Fact]
    public async Task El_sello_de_lo_grabado_sale_del_cuerpo_de_la_solicitud()
    {
        using var transporte = TransporteFalso.QueResponde();

        (await EnviarAsync(transporte, regrabar: true)).Dispose();

        var sello = new AlmacenDeCassettes(_directorio).Leer(ClaveEsperada())!.Sello;

        // El modelo y el hash del prefijo se leen del MISMO lugar que la clave, así
        // el archivo no puede declarar un modelo distinto del que lo produjo.
        Assert.Equal("claude-sonnet-5", sello.Modelo);
        Assert.Equal(Fixture, sello.HashDelFixture);
        Assert.Equal(ClaveDeCassette.Calcular(Cuerpo()).HashDelPrefijo, sello.HashDelPrefijo);
    }

    [Fact]
    public async Task Una_respuesta_que_el_pipeline_no_pudo_resolver_no_se_graba()
    {
        using var transporte = TransporteFalso.QueFalla(HttpStatusCode.InternalServerError);

        using var respuesta = await EnviarAsync(transporte, regrabar: true);

        // El sobre guarda un cuerpo y no un estado, así que grabar un 500 dejaría un
        // cassette que se reproduce como un 200 con un cuerpo de error adentro: un
        // cassette envenenado que falla al interpretarlo y no al servirlo.
        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        Assert.Null(new AlmacenDeCassettes(_directorio).Leer(ClaveEsperada()));
    }

    // -------------------------------------------------------------- reproducción

    [Fact]
    public async Task Con_el_cassette_presente_la_llamada_no_sale_aunque_la_regrabacion_este_puesta()
    {
        using var primero = new TransporteFalso(_ => TransporteFalso.Exito("SELECT 42"));
        (await EnviarAsync(primero, regrabar: true)).Dispose();

        using var segundo = TransporteFalso.QueResponde("SELECT 99");
        using var respuesta = await EnviarAsync(segundo, regrabar: true);

        // Re-grabar es una operación deliberada sobre las claves que faltan, no un
        // modo en que cada corrida gasta plata en respuestas que ya están.
        Assert.Equal(0, segundo.Intentos);
        Assert.Contains(
            "SELECT 42",
            await respuesta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            StringComparison.Ordinal);
    }

    // ------------------------------------------------------------- la credencial

    [Fact]
    public async Task El_cassette_no_guarda_ninguna_cabecera_de_la_solicitud()
    {
        using var transporte = TransporteFalso.QueResponde();

        using var solicitud = Solicitud();
        solicitud.Headers.Add("x-api-key", "sk-ant-api03-secreto-de-prueba");
        solicitud.Headers.Add("anthropic-version", "2023-06-01");

        (await EnviarAsync(transporte, regrabar: true, solicitud: solicitud)).Dispose();

        var archivo = File.ReadAllText(
            Path.Combine(_directorio, ClaveEsperada() + ".json"));

        // No hace falta filtrar nada: el sobre guarda el cuerpo de la RESPUESTA, y
        // las cabeceras de la solicitud nunca entran. Que la clave no pueda filtrarse
        // a disco es estructural y no una lista de exclusiones que hay que mantener.
        Assert.DoesNotContain("sk-ant-", archivo, StringComparison.Ordinal);
        Assert.DoesNotContain("x-api-key", archivo, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------ registro

    [Fact]
    public async Task El_registro_dice_que_clave_se_grabo_y_cual_se_sirvio()
    {
        var registro = new RegistroDeCapturas();

        using var primero = TransporteFalso.QueResponde();
        (await EnviarAsync(primero, regrabar: true, registro: registro)).Dispose();

        using var segundo = TransporteFalso.QueResponde();
        (await EnviarAsync(segundo, regrabar: true, registro: registro)).Dispose();

        var lineas = registro.Lineas;

        Assert.Contains(lineas, l => l.Contains("grabó", StringComparison.Ordinal));
        Assert.Contains(lineas, l => l.Contains("sirvió", StringComparison.Ordinal));
        Assert.All(lineas, l => Assert.Contains(ClaveEsperada(), l, StringComparison.Ordinal));
    }

    [Fact]
    public async Task El_registro_grita_la_clave_que_falto()
    {
        var registro = new RegistroDeCapturas();
        using var transporte = TransporteFalso.QueResponde();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => EnviarAsync(transporte, regrabar: false, registro: registro));

        Assert.Contains(
            registro.DeNivel(LogLevel.Error),
            l => l.Contains(ClaveEsperada(), StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------ apoyo

    private async Task<HttpResponseMessage> EnviarAsync(
        TransporteFalso transporte,
        bool regrabar,
        HttpRequestMessage? solicitud = null,
        RegistroDeCapturas? registro = null)
    {
        using var grabador = new GrabadorDeCassettes(
            new AlmacenDeCassettes(_directorio),
            regrabar,
            Fixture,
            TimeProvider.System,
            (registro ?? new RegistroDeCapturas()).Logger<GrabadorDeCassettes>())
        {
            InnerHandler = transporte,
        };

        using var invocador = new HttpMessageInvoker(grabador, disposeHandler: false);
        using var propia = solicitud is null ? Solicitud() : null;

        return await invocador.SendAsync(
            solicitud ?? propia!, TestContext.Current.CancellationToken);
    }

    private static HttpRequestMessage Solicitud() =>
        new(HttpMethod.Post, "https://proveedor.invalido/v1/messages")
        {
            Content = new StringContent(Cuerpo(), Encoding.UTF8, "application/json"),
        };

    private static string ClaveEsperada() => ClaveDeCassette.Calcular(Cuerpo()).Clave;

    /// <summary>Un cuerpo con la forma que el adaptador manda a la API de mensajes.</summary>
    private static string Cuerpo() =>
        """
        {"model":"claude-sonnet-5","max_tokens":4000,
         "system":[{"type":"text","text":"Esquema.","cache_control":{"type":"ephemeral"}}],
         "messages":[{"role":"user","content":"¿Cuántos pedidos hay?"}],
         "output_config":{"effort":"medium"}}
        """;

    public void Dispose()
    {
        if (Directory.Exists(_directorio))
        {
            Directory.Delete(_directorio, recursive: true);
        }
    }
}
