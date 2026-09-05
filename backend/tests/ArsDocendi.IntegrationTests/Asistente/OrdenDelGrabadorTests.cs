using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared;
using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Asistente;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El grabador va por FUERA del reintento, y eso se puede afirmar.
/// </summary>
/// <remarks>
/// En <c>AddHttpMessageHandler</c> el orden de registración es de afuera hacia
/// adentro, así que el pipeline queda
/// <c>adaptador → grabador → reintento → transporte</c>. Con ese orden el grabador
/// ve <b>una solicitud por llamada lógica</b> y la respuesta que el reintento
/// resolvió: la que el pipeline efectivamente le devolvió al adaptador.
///
/// Del lado de adentro grabaría también los fallos —con ellos el 429 y el 503
/// verdaderos del proveedor—, y se descarta por tres cosas: rompe la identidad del
/// cassette (los cuatro campos de la clave son iguales en los tres intentos),
/// reproducir un fallo reproduciría la espera, y no cubre nada que
/// <c>ReintentoYTechoTests</c> no cubra ya.
/// </remarks>
public sealed class OrdenDelGrabadorTests : IDisposable
{
    private const string Fixture = "hash-del-fixture-vigente";

    private const string CadenaDelDueno =
        "Host=arsdocendi-postgres;Port=5432;Database=arsdocendi_pr_123;Username=app_pr_123;Password=x";

    private static readonly TimeSpan EsperaLarga = TimeSpan.FromSeconds(30);

    private readonly string _directorio = Path.Combine(
        Path.GetTempPath(), "cassettes-" + Guid.NewGuid().ToString("n"));

    // ------------------------------------------------- un reintento, un cassette

    [Fact]
    public async Task Un_transporte_que_falla_y_despues_responde_deja_un_solo_cassette()
    {
        var registro = new RegistroDeCapturas();
        using var transporte = new TransporteFalso(
            cual => cual == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : TransporteFalso.Exito("SELECT 42"));

        (await EnviarAsync(transporte, regrabar: true, registro: registro)).Dispose();

        Assert.Equal(2, transporte.Intentos);

        var cassettes = new AlmacenDeCassettes(_directorio).Todos();

        Assert.Single(cassettes);
        Assert.Contains("SELECT 42", cassettes[0].Cuerpo, StringComparison.Ordinal);

        // Y una sola vuelta del grabador. Registrado del lado de adentro vería los
        // dos intentos —el fallo y el éxito— y dejaría dos líneas: la identidad del
        // cassette dejaría de ser «una llamada lógica» para pasar a ser «un intento
        // de transporte», que es estado del transporte y no de la pregunta.
        Assert.Single(registro.Lineas);
    }

    [Fact]
    public async Task Reproducir_un_cassette_de_una_llamada_con_reintentos_no_espera_backoff()
    {
        using var primero = new TransporteFalso(
            cual => cual == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : TransporteFalso.Exito("SELECT 42"));

        (await EnviarAsync(primero, regrabar: true)).Dispose();

        // El segundo pipeline tiene un backoff de treinta segundos y un transporte
        // que pide otros treinta de espera: si la reproducción llegara hasta ahí, el
        // test tardaría media hora en vez de un instante. Una suite que duerme por
        // un cassette es una suite que alguien va a apagar.
        using var segundo = new TransporteFalso(_ => Rechazo());
        var reloj = Stopwatch.StartNew();

        using var respuesta = await EnviarAsync(
            segundo, regrabar: false, espera: EsperaLarga);

        reloj.Stop();

        Assert.Equal(0, segundo.Intentos);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(
            reloj.Elapsed < TimeSpan.FromSeconds(5),
            $"La reproducción esperó {reloj.Elapsed}, así que pasó por el backoff.");
    }

    // ----------------------------------------------------- el cableado del módulo

    [Fact]
    public void El_grabador_se_registra_antes_del_reintento()
    {
        using var raiz = Componer(_directorio).BuildServiceProvider();

        Assert.Equal(
            [typeof(GrabadorDeCassettes), typeof(ReintentoDeTransporte)],
            HandlersDelCliente(raiz));
    }

    [Fact]
    public void El_reintento_sigue_en_el_pipeline_con_el_mecanismo_encendido()
    {
        using var raiz = Componer(_directorio).BuildServiceProvider();

        // El grabador se suma al pipeline; no reemplaza nada. Su comportamiento
        // completo lo sigue verificando ReintentoYTechoTests.
        Assert.Contains(typeof(ReintentoDeTransporte), HandlersDelCliente(raiz));
    }

    // ------------------------------------------------------------------ apoyo

    /// <summary>
    /// Los handlers PROPIOS del cliente con nombre, de afuera hacia adentro.
    /// </summary>
    /// <remarks>
    /// La fábrica intercala los suyos —seguimiento de vida útil y registro— y no
    /// son asunto de este módulo. Lo que se afirma es el orden de los nuestros, que
    /// es la decisión que este test cuida.
    /// </remarks>
    internal static IReadOnlyList<Type> HandlersDelCliente(IServiceProvider raiz)
    {
        var handler = raiz.GetRequiredService<IHttpMessageHandlerFactory>()
            .CreateHandler(ModuleExtensions.ClienteDelProveedor);

        var tipos = new List<Type>();

        while (handler is DelegatingHandler delegante)
        {
            if (delegante.GetType().Assembly == typeof(ModuleExtensions).Assembly)
            {
                tipos.Add(delegante.GetType());
            }

            handler = delegante.InnerHandler!;
        }

        return tipos;
    }

    private static ServiceCollection Componer(string directorio)
    {
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{CadenaDuena.Clave}"] = CadenaDelDueno,
                [$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.DirectorioDeCassettes)}"] =
                    directorio,
            }).Build();

        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddArsDocendiShared(configuracion);
        servicios.AddAsistenteModule(configuracion);
        return servicios;
    }

    private async Task<HttpResponseMessage> EnviarAsync(
        TransporteFalso transporte,
        bool regrabar,
        RegistroDeCapturas? registro = null,
        TimeSpan? espera = null)
    {
        var backoff = espera ?? TimeSpan.FromMilliseconds(1);

        using var reintento = new ReintentoDeTransporte(
            maximoDeIntentos: 3, backoff, backoff, new Random(20260905))
        {
            InnerHandler = transporte,
        };

        using var grabador = new GrabadorDeCassettes(
            new AlmacenDeCassettes(_directorio),
            regrabar,
            Fixture,
            TimeProvider.System,
            (registro ?? new RegistroDeCapturas()).Logger<GrabadorDeCassettes>())
        {
            InnerHandler = reintento,
        };

        using var invocador = new HttpMessageInvoker(grabador, disposeHandler: false);
        using var solicitud = new HttpRequestMessage(
            HttpMethod.Post, "https://proveedor.invalido/v1/messages")
        {
            Content = new StringContent(Cuerpo(), Encoding.UTF8, "application/json"),
        };

        return await invocador.SendAsync(solicitud, TestContext.Current.CancellationToken);
    }

    /// <summary>Un 429 que además pide media hora de espera.</summary>
    private static HttpResponseMessage Rechazo()
    {
        var respuesta = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        respuesta.Headers.RetryAfter = new RetryConditionHeaderValue(EsperaLarga);
        return respuesta;
    }

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
