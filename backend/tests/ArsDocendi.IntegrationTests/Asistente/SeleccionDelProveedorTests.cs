using System.Net;
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
/// Verifica que el adaptador real se elija por ambiente y quede enchufado a la
/// cadena de decoradores que ya existía.
/// </summary>
/// <remarks>
/// Lo que se prueba acá no es el adaptador —eso es
/// <see cref="ProveedorAnthropicTests"/>— sino el cableado: que el
/// <c>switch</c> lo alcance, que consuma el cliente HTTP con el reintento del
/// módulo y que sus fallas lleguen al corte. Nada de esto necesita clave ni red.
/// </remarks>
public sealed class SeleccionDelProveedorTests
{
    private const string CadenaDelDueno =
        "Host=arsdocendi-postgres;Port=5432;Database=arsdocendi_pr_123;Username=app_pr_123;Password=x";

    private static readonly SolicitudAlModelo Solicitud = new()
    {
        PrefijoEstable = "Esquema.",
        Mensaje = "¿Qué docentes dictan Bases de Datos?",
        Temperatura = 0.0m,
        MaximoDeTokens = 256,
    };

    [Fact]
    public void Configurado_con_clave_el_proveedor_resuelto_no_es_simulado()
    {
        using var servicios = Componer(clave: "clave-de-prueba").BuildServiceProvider();
        using var turno = servicios.CreateScope();

        var proveedor = turno.ServiceProvider.GetRequiredService<IProveedorDeModelo>();

        Assert.False(proveedor.EsSimulado);
        Assert.StartsWith("anthropic/", proveedor.Nombre, StringComparison.Ordinal);
    }

    [Fact]
    public void Sin_clave_pedir_el_proveedor_falla_nombrando_el_valor_que_falta()
    {
        using var servicios = Componer(clave: null).BuildServiceProvider();
        using var turno = servicios.CreateScope();

        var error = Assert.Throws<InvalidOperationException>(
            turno.ServiceProvider.GetRequiredService<IProveedorDeModelo>);

        // El mensaje tiene que nombrar el valor: «falta configuración» manda a
        // buscar en veinte lugares.
        Assert.Contains(
            nameof(OpcionesAsistente.ClaveDelProveedor), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sin_clave_el_Host_igual_arranca_y_el_ping_responde()
    {
        var ct = TestContext.Current.CancellationToken;

        // Contrapartida del test anterior, y es la que importa: la configuración
        // del proveedor NO puede ser condición de arranque. Un ambiente a medio
        // configurar tiene que levantar igual, porque el ping es el smoke test que
        // dice si el módulo está vivo.
        using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting($"ConnectionStrings:{CadenaDuena.Clave}", CadenaDelDueno);
            builder.UseSetting(Ajuste(nameof(OpcionesAsistente.Proveedor)), "anthropic");
        });
        using var cliente = host.CreateClient();

        using var respuesta = await cliente.GetAsync("/api/asistente/ping", ct);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public void Un_esfuerzo_desconocido_falla_nombrando_los_aceptados()
    {
        using var servicios = Componer(clave: "clave-de-prueba", esfuerzo: "alto")
            .BuildServiceProvider();
        using var turno = servicios.CreateScope();

        var error = Assert.Throws<InvalidOperationException>(
            turno.ServiceProvider.GetRequiredService<IProveedorDeModelo>);

        // Falla en vez de caer al default en silencio: alguien que escribió «alto»
        // en vez de «high» quiere enterarse, no correr un mes con un esfuerzo que
        // no eligió.
        Assert.Contains("alto", error.Message, StringComparison.Ordinal);
        Assert.Contains("xhigh", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_adaptador_consume_el_cliente_con_el_reintento_del_modulo()
    {
        using var transporte = TransporteFalso.QueFalla(HttpStatusCode.InternalServerError);
        using var servicios = Componer(clave: "clave-de-prueba", transporte: transporte, intentos: 3)
            .BuildServiceProvider();
        using var turno = servicios.CreateScope();

        var proveedor = turno.ServiceProvider.GetRequiredService<IProveedorDeModelo>();

        await Assert.ThrowsAsync<HttpRequestException>(
            () => proveedor.CompletarAsync(Solicitud, TestContext.Current.CancellationToken));

        // Tres intentos, y ni uno más: el adaptador no reintenta y el handler del
        // módulo sí. Es la otra mitad del test que verifica que el SDK está en
        // cero — juntos fijan que hay exactamente UNA autoridad de reintento.
        Assert.Equal(3, transporte.Intentos);
    }

    [Fact]
    public async Task Fallas_repetidas_del_proveedor_abren_el_corte()
    {
        var ct = TestContext.Current.CancellationToken;
        using var transporte = TransporteFalso.QueFalla(HttpStatusCode.InternalServerError);
        using var servicios = Componer(
            clave: "clave-de-prueba", transporte: transporte, intentos: 1, fallosParaAbrir: 2)
            .BuildServiceProvider();

        for (var turno = 0; turno < 2; turno++)
        {
            using var alcance = servicios.CreateScope();
            await Assert.ThrowsAsync<HttpRequestException>(
                () => alcance.ServiceProvider.GetRequiredService<IProveedorDeModelo>()
                    .CompletarAsync(Solicitud, ct));
        }

        var antes = transporte.Intentos;

        using var tercero = servicios.CreateScope();
        await Assert.ThrowsAsync<ProveedorNoDisponible>(
            () => tercero.ServiceProvider.GetRequiredService<IProveedorDeModelo>()
                .CompletarAsync(Solicitud, ct));

        // Es la prueba de que la traducción de fallas del adaptador LLEGA al
        // breaker. Si dejara escapar un tipo del SDK, el corte no contaría nada y
        // esta tercera llamada saldría a la red igual que las dos anteriores: un
        // proveedor caído al cien por ciento seguiría recibiendo llamadas y la
        // degradación no se activaría nunca.
        Assert.Equal(antes, transporte.Intentos);
    }

    // ------------------------------------------------------------------ apoyo

    private static string Ajuste(string nombre) => $"{OpcionesAsistente.Seccion}:{nombre}";

    private static ServiceCollection Componer(
        string? clave,
        string esfuerzo = "high",
        TransporteFalso? transporte = null,
        int? intentos = null,
        int? fallosParaAbrir = null)
    {
        var valores = new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{CadenaDuena.Clave}"] = CadenaDelDueno,
            [Ajuste(nameof(OpcionesAsistente.Proveedor))] = "anthropic",
            [Ajuste(nameof(OpcionesAsistente.Esfuerzo))] = esfuerzo,
            [Ajuste(nameof(OpcionesAsistente.EsperaBaseMs))] = "1",
            [Ajuste(nameof(OpcionesAsistente.EsperaMaximaMs))] = "2",
        };

        if (clave is not null)
        {
            valores[Ajuste(nameof(OpcionesAsistente.ClaveDelProveedor))] = clave;
        }

        if (intentos is { } cuantos)
        {
            valores[Ajuste(nameof(OpcionesAsistente.MaximoDeIntentosDeTransporte))] =
                cuantos.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (fallosParaAbrir is { } cuantosFallos)
        {
            valores[Ajuste(nameof(OpcionesAsistente.FallosParaAbrirElBreaker))] =
                cuantosFallos.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddArsDocendiShared(configuracion);
        servicios.AddAsistenteModule(configuracion);

        if (transporte is not null)
        {
            // Se reemplaza el handler PRIMARIO del cliente con nombre, no el cliente
            // entero: así el reintento del módulo sigue en el pipeline y lo que se
            // mide es el cableado real, no una versión de laboratorio.
            servicios.AddHttpClient(ModuleExtensions.ClienteDelProveedor)
                .ConfigurePrimaryHttpMessageHandler(() => transporte);
        }

        return servicios;
    }
}
