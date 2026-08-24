using System.Net;
using System.Text.RegularExpressions;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared;
using ArsDocendi.Shared.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Asistente;
using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica la abstracción del proveedor de modelo y su cliente simulado.
/// </summary>
/// <remarks>
/// No necesitan base ni red: lo que se prueba es que el default sea el simulado,
/// que sea determinista y que se identifique como tal.
/// </remarks>
public sealed partial class ProveedorDeModeloTests
{
    private const string CadenaDelDueno =
        "Host=arsdocendi-postgres;Port=5432;Database=arsdocendi_pr_123;Username=app_pr_123;Password=x";

    private static readonly SolicitudAlModelo Solicitud = new()
    {
        PrefijoEstable = "Esquema de identity y designaciones.",
        Mensaje = "¿Qué docentes dictan Bases de Datos?",
        Temperatura = 0.0m,
        MaximoDeTokens = 512,
    };

    [Fact]
    public void El_default_sin_ninguna_configuracion_es_el_cliente_simulado()
    {
        using var servicios = Componer().BuildServiceProvider();

        using var turno = servicios.CreateScope();
        var proveedor = turno.ServiceProvider.GetRequiredService<IProveedorDeModelo>();

        // Usar un proveedor real tiene que exigir configuración explícita. Si el
        // default fuese el real, un ambiente mal configurado gastaría plata —o
        // fallaría— sin que nadie lo hubiera pedido.
        Assert.Equal("simulado", proveedor.Nombre);
        Assert.True(proveedor.EsSimulado);
    }

    [Fact]
    public async Task La_respuesta_se_identifica_como_simulada()
    {
        var proveedor = Simulado();

        var respuesta = await proveedor.CompletarAsync(Solicitud, TestContext.Current.CancellationToken);

        // En la bandera Y en el texto. Un proveedor de mentira que devolviera algo
        // verosímil sería peor que uno que falla.
        Assert.True(respuesta.EsSimulada);
        Assert.Contains("simulada", respuesta.Texto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no proviene de ningún modelo", respuesta.Texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task La_misma_solicitud_devuelve_siempre_la_misma_respuesta()
    {
        var ct = TestContext.Current.CancellationToken;

        // Dos instancias distintas, para que el determinismo no dependa de estado
        // guardado en el objeto.
        var primera = await Simulado().CompletarAsync(Solicitud, ct);
        var segunda = await Simulado().CompletarAsync(Solicitud, ct);

        Assert.Equal(primera.Texto, segunda.Texto);
        Assert.Equal(primera.TokensDeEntrada, segunda.TokensDeEntrada);
        Assert.Equal(primera.TokensDeSalida, segunda.TokensDeSalida);
    }

    [Fact]
    public async Task Una_solicitud_distinta_devuelve_una_respuesta_distinta()
    {
        var ct = TestContext.Current.CancellationToken;
        var otra = Solicitud with { Mensaje = "¿Cuántos pedidos hay en revisión?" };

        var primera = await Simulado().CompletarAsync(Solicitud, ct);
        var segunda = await Simulado().CompletarAsync(otra, ct);

        // Sin esto, el test de determinismo pasaría con un cliente que devuelve
        // siempre la misma constante y no mira la solicitud.
        Assert.NotEqual(primera.Texto, segunda.Texto);
    }

    [Fact]
    public async Task La_temperatura_tambien_cambia_la_respuesta()
    {
        var ct = TestContext.Current.CancellationToken;
        var redaccion = Solicitud with { Temperatura = 0.3m };

        var generacion = await Simulado().CompletarAsync(Solicitud, ct);
        var conRedaccion = await Simulado().CompletarAsync(redaccion, ct);

        Assert.NotEqual(generacion.Texto, conRedaccion.Texto);
    }

    [Fact]
    public void El_cliente_simulado_no_tiene_ninguna_dependencia_de_red()
    {
        var fuente = File.ReadAllText(Path.Combine(
            RaizRepositorio.BackendSrc(), "Modules.Asistente", "Infrastructure", "ProveedorSimulado.cs"));

        // La garantía «no hace ninguna llamada de red» no se puede probar
        // ejecutándolo: un test que no ve tráfico no distingue «no llamó» de «no
        // miré». Lo que sí se puede afirmar es que el tipo no nombra nada con lo
        // que se pueda salir a la red.
        Assert.False(TiposDeRed().IsMatch(fuente), "El cliente simulado no debe usar tipos de red.");
    }

    [Fact]
    public void Un_proveedor_desconocido_falla_nombrando_el_valor_configurado()
    {
        using var servicios = Componer("openai-de-verdad").BuildServiceProvider();

        using var turno = servicios.CreateScope();

        var error = Assert.Throws<InvalidOperationException>(
            turno.ServiceProvider.GetRequiredService<IProveedorDeModelo>);

        Assert.Contains("openai-de-verdad", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_proveedor_desconocido_no_impide_componer_el_resto_del_modulo()
    {
        // Contrapartida del test anterior: la configuración del proveedor no puede
        // ser condición de arranque. El módulo carga igual y el error llega recién a
        // quien pide el proveedor.
        using var servicios = Componer("openai-de-verdad").BuildServiceProvider();

        var duena = servicios.GetRequiredService<CadenaDuena>();

        Assert.Equal("app_pr_123", duena.Usuario);
    }

    [Fact]
    public async Task Un_ambiente_sin_ninguna_clave_configurada_levanta_y_responde()
    {
        var ct = TestContext.Current.CancellationToken;

        // El Host real, compuesto sin una sola opción del asistente: es la
        // situación de un ambiente efímero de PR, que no puede tener clave.
        using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting($"ConnectionStrings:{CadenaDuena.Clave}", CadenaDelDueno);
        });
        using var cliente = host.CreateClient();

        using var respuesta = await cliente.GetAsync("/api/asistente/ping", ct);
        using var turno = host.Services.CreateScope();
        var proveedor = turno.ServiceProvider.GetRequiredService<IProveedorDeModelo>();

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(proveedor.EsSimulado);
    }

    // ------------------------------------------------------------------------ apoyo

    /// <summary>
    /// El proveedor se resuelve DENTRO de un scope: el decorador que le cobra al
    /// techo del turno es scoped, y un turno es un request.
    /// </summary>
    private static IProveedorDeModelo Simulado() =>
        Componer().BuildServiceProvider().CreateScope().ServiceProvider
            .GetRequiredService<IProveedorDeModelo>();

    private static ServiceCollection Componer(string? proveedor = null)
    {
        var valores = new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{CadenaDuena.Clave}"] = CadenaDelDueno,
        };

        if (proveedor is not null)
        {
            valores[$"{OpcionesAsistente.Seccion}:{nameof(OpcionesAsistente.Proveedor)}"] = proveedor;
        }

        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddArsDocendiShared(configuracion);
        servicios.AddAsistenteModule(configuracion);
        return servicios;
    }

    [GeneratedRegex(@"\bHttpClient\b|\bHttpRequestMessage\b|\bSocket\b|\bWebRequest\b|\bSystem\.Net\b")]
    private static partial Regex TiposDeRed();
}
