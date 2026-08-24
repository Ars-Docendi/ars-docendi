using System.Net;
using System.Net.Http.Headers;
using ArsDocendi.Shared;
using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica el reintento de transporte (RNF-11) y el techo de llamadas por turno
/// (RNF-10).
/// </summary>
/// <remarks>
/// Los dos van juntos porque se multiplican: el reintento semántico del pipeline y
/// el de transporte, uno por uno, dan hasta seis llamadas facturadas por pregunta.
/// El techo se pone sobre el total de llamadas al modelo del turno, no por capa.
/// </remarks>
public sealed class ReintentoYTechoTests
{
    private const string CadenaDelDueno =
        "Host=arsdocendi-postgres;Port=5432;Database=arsdocendi_pr_123;Username=app_pr_123;Password=x";

    private static readonly TimeSpan EsperaBase = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan EsperaMaxima = TimeSpan.FromMilliseconds(4);

    private static readonly SolicitudAlModelo Solicitud = new()
    {
        PrefijoEstable = "Esquema.",
        Mensaje = "¿Cuántos pedidos hay?",
        Temperatura = 0.0m,
        MaximoDeTokens = 256,
    };

    // ------------------------------------------------------ reintento de transporte

    [Fact]
    public async Task Un_429_se_reintenta_y_la_segunda_respuesta_es_la_que_vale()
    {
        var servidor = new RespuestasEnCola(HttpStatusCode.TooManyRequests, HttpStatusCode.OK);

        var respuesta = await EnviarAsync(servidor, maximoDeIntentos: 3);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(2, servidor.Intentos);
    }

    [Fact]
    public async Task Un_400_no_se_reintenta_nunca()
    {
        var servidor = new RespuestasEnCola(HttpStatusCode.BadRequest, HttpStatusCode.OK);

        var respuesta = await EnviarAsync(servidor, maximoDeIntentos: 3);

        // Incluye el 400 del límite de gasto: reintentar un rechazo por presupuesto
        // agotado gasta presupuesto que ya no hay.
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(1, servidor.Intentos);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, 2)]
    [InlineData(HttpStatusCode.BadGateway, 2)]
    [InlineData(HttpStatusCode.ServiceUnavailable, 2)]
    [InlineData(HttpStatusCode.GatewayTimeout, 2)]
    [InlineData(HttpStatusCode.Unauthorized, 1)]
    [InlineData(HttpStatusCode.Forbidden, 1)]
    [InlineData(HttpStatusCode.NotFound, 1)]
    public async Task Solo_se_reintenta_lo_que_puede_mejorar_solo(
        HttpStatusCode primera, int intentosEsperados)
    {
        var servidor = new RespuestasEnCola(primera, HttpStatusCode.OK);

        await EnviarAsync(servidor, maximoDeIntentos: 3);

        // Una credencial no se arregla esperando; un 429 sí.
        Assert.Equal(intentosEsperados, servidor.Intentos);
    }

    [Fact]
    public async Task El_reintento_se_detiene_en_el_maximo_de_intentos()
    {
        var servidor = new RespuestasEnCola(
            HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests,
            HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests);

        var respuesta = await EnviarAsync(servidor, maximoDeIntentos: 3);

        Assert.Equal(HttpStatusCode.TooManyRequests, respuesta.StatusCode);
        Assert.Equal(3, servidor.Intentos);
    }

    [Fact]
    public void Se_honra_retry_after_cuando_el_proveedor_lo_manda()
    {
        var indicada = TimeSpan.FromMilliseconds(3);

        var espera = ReintentoDeTransporte.CalcularEspera(
            intento: 1, EsperaBase, EsperaMaxima, indicada, new Random(1));

        // El proveedor sabe cuándo se le libera la cuota mejor que cualquier
        // fórmula; desoírlo es la forma más rápida de extender la ventana.
        Assert.Equal(indicada, espera);
    }

    [Fact]
    public void Un_retry_after_mayor_al_tope_se_recorta()
    {
        var espera = ReintentoDeTransporte.CalcularEspera(
            intento: 1, EsperaBase, EsperaMaxima, TimeSpan.FromHours(1), new Random(1));

        Assert.Equal(EsperaMaxima, espera);
    }

    [Fact]
    public void El_backoff_crece_de_forma_exponencial_y_nunca_pasa_el_tope()
    {
        var azar = new Random(20260824);

        var esperas = Enumerable.Range(1, 8)
            .Select(intento => ReintentoDeTransporte.CalcularEspera(
                intento, EsperaBase, EsperaMaxima, null, azar))
            .ToArray();

        Assert.All(esperas, espera =>
        {
            Assert.True(espera >= TimeSpan.Zero);
            Assert.True(espera <= EsperaMaxima);
        });
    }

    [Fact]
    public void El_backoff_lleva_jitter_y_no_devuelve_siempre_lo_mismo()
    {
        var esperas = Enumerable.Range(1, 30)
            .Select(_ => ReintentoDeTransporte.CalcularEspera(
                intento: 4, EsperaBase, EsperaMaxima, null, new Random(Guid.NewGuid().GetHashCode())))
            .Distinct()
            .Count();

        // Sin jitter, todos los turnos que chocaron con el mismo 429 vuelven juntos
        // al mismo milisegundo y reconstruyen el pico que los rechazó.
        Assert.True(esperas > 1, "El backoff debería tener jitter, no una espera fija.");
    }

    // -------------------------------------------------------- techo por turno

    [Fact]
    public async Task El_turno_no_puede_pasar_su_techo_de_llamadas()
    {
        var ct = TestContext.Current.CancellationToken;
        using var raiz = Componer(techo: 4).BuildServiceProvider();
        using var turno = raiz.CreateScope();
        var proveedor = turno.ServiceProvider.GetRequiredService<IProveedorDeModelo>();

        for (var i = 0; i < 4; i++)
        {
            await proveedor.CompletarAsync(Solicitud, ct);
        }

        await Assert.ThrowsAsync<TechoDeLlamadasSuperado>(
            () => proveedor.CompletarAsync(Solicitud, ct));
    }

    [Fact]
    public async Task El_techo_es_global_del_turno_y_no_por_capa()
    {
        var ct = TestContext.Current.CancellationToken;
        using var raiz = Componer(techo: 3).BuildServiceProvider();
        using var turno = raiz.CreateScope();

        // Tres "capas" distintas pidiendo el proveedor por inyección, como haría el
        // reescritor, la generación y la redacción. Reparten un solo cupo.
        var reescritor = turno.ServiceProvider.GetRequiredService<IProveedorDeModelo>();
        var generacion = turno.ServiceProvider.GetRequiredService<IProveedorDeModelo>();
        var redaccion = turno.ServiceProvider.GetRequiredService<IProveedorDeModelo>();

        await reescritor.CompletarAsync(Solicitud, ct);
        await generacion.CompletarAsync(Solicitud, ct);
        await redaccion.CompletarAsync(Solicitud, ct);

        // Con un techo por capa, cada una habría respetado su límite y el total se
        // habría multiplicado igual.
        await Assert.ThrowsAsync<TechoDeLlamadasSuperado>(
            () => reescritor.CompletarAsync(Solicitud, ct));
    }

    [Fact]
    public async Task Cada_turno_arranca_con_su_propio_cupo()
    {
        var ct = TestContext.Current.CancellationToken;
        using var raiz = Componer(techo: 1).BuildServiceProvider();

        for (var turnos = 0; turnos < 3; turnos++)
        {
            using var turno = raiz.CreateScope();
            var proveedor = turno.ServiceProvider.GetRequiredService<IProveedorDeModelo>();

            await proveedor.CompletarAsync(Solicitud, ct);

            await Assert.ThrowsAsync<TechoDeLlamadasSuperado>(
                () => proveedor.CompletarAsync(Solicitud, ct));
        }
    }

    [Fact]
    public async Task El_conteo_del_turno_queda_a_la_vista_para_las_metricas()
    {
        var ct = TestContext.Current.CancellationToken;
        using var raiz = Componer(techo: 4).BuildServiceProvider();
        using var turno = raiz.CreateScope();
        var proveedor = turno.ServiceProvider.GetRequiredService<IProveedorDeModelo>();
        var contador = turno.ServiceProvider.GetRequiredService<ContadorDeLlamadasDelTurno>();

        await proveedor.CompletarAsync(Solicitud, ct);
        await proveedor.CompletarAsync(Solicitud, ct);

        // Una llamada al proveedor consume exactamente una unidad. Un reintento de
        // transporte ocurre DENTRO de una llamada y no suma acá: tiene su propio
        // máximo de intentos, y las dos cotas juntas acotan el peor caso.
        Assert.Equal(2, contador.Llamadas);
        Assert.Equal(4, contador.Techo);
    }

    [Fact]
    public void Un_techo_no_positivo_se_rechaza_al_construirlo()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ContadorDeLlamadasDelTurno(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ContadorDeLlamadasDelTurno(-1));
    }

    // ------------------------------------------------------------------------ apoyo

    private static async Task<HttpResponseMessage> EnviarAsync(
        RespuestasEnCola servidor, int maximoDeIntentos)
    {
        using var reintento = new ReintentoDeTransporte(
            maximoDeIntentos, EsperaBase, EsperaMaxima, new Random(20260824))
        {
            InnerHandler = servidor,
        };
        using var invocador = new HttpMessageInvoker(reintento);
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, "https://proveedor.invalido/v1");

        return await invocador.SendAsync(solicitud, TestContext.Current.CancellationToken);
    }

    private static ServiceCollection Componer(int techo)
    {
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{CadenaDuena.Clave}"] = CadenaDelDueno,
                [$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.MaximoDeLlamadasPorTurno)}"] =
                    techo.ToString(System.Globalization.CultureInfo.InvariantCulture),
            }).Build();

        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddArsDocendiShared(configuracion);
        servicios.AddAsistenteModule(configuracion);
        return servicios;
    }

    /// <summary>Servidor de mentira: devuelve los códigos en orden y cuenta intentos.</summary>
    private sealed class RespuestasEnCola(params HttpStatusCode[] codigos) : HttpMessageHandler
    {
        public int Intentos { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage solicitud, CancellationToken ct)
        {
            var codigo = Intentos < codigos.Length ? codigos[Intentos] : codigos[^1];
            Intentos++;

            var respuesta = new HttpResponseMessage(codigo);
            if (codigo == HttpStatusCode.TooManyRequests)
            {
                respuesta.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
            }

            return Task.FromResult(respuesta);
        }
    }
}
