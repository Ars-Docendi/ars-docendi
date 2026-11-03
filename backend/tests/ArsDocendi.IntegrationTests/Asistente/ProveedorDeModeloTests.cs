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

/// <summary>Verifica la abstracción del proveedor de modelo y su cliente simulado.</summary>
public sealed partial class ProveedorDeModeloTests
{
    private const string CadenaDelDueno =
        "Host=arsdocendi-postgres;Port=5432;Database=arsdocendi_pr_123;Username=app_pr_123;Password=x";

    private static readonly SolicitudAlModelo Solicitud = new()
    {
        PrefijoEstable = "Esquema de identity y designaciones.",
        Mensaje = "¿Qué docentes dictan Bases de Datos?",
        Temperatura = 0.0m,
        Esfuerzo = EsfuerzoDelModelo.Medio,
        MaximoDeTokens = 512,
    };

    [Fact]
    public void El_default_sin_ninguna_configuracion_es_el_cliente_simulado()
    {
        using var servicios = Componer().BuildServiceProvider();

        using var turno = servicios.CreateScope();
        var proveedor = turno.ServiceProvider.GetRequiredService<IProveedorDeModelo>();

        // Usar un proveedor real tiene que exigir configuración explícita. Si el default fuese el
        // real, un ambiente mal configurado gastaría plata —o fallaría— sin que nadie lo hubiera pedido.
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
    public async Task Un_proveedor_desconocido_falla_nombrando_el_valor_configurado()
    {
        using var servicios = Componer("openai-de-verdad").BuildServiceProvider();

        using var turno = servicios.CreateScope();

        // The adapter is resolved by the turn's resolver at the first call.
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => turno.ServiceProvider.GetRequiredService<IProveedorDeModelo>()
                .CompletarAsync(Solicitud, TestContext.Current.CancellationToken));

        Assert.Contains("openai-de-verdad", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_proveedor_desconocido_no_impide_componer_el_resto_del_modulo()
    {
        // Contrapartida del test anterior: la configuración del proveedor no puede ser condición de arranque.
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
            builder.UseSetting("Integridad:ClavesSellado:1", Convert.ToBase64String(new byte[32]));
            // En Production el Host exige el ingreso con Microsoft configurado.
            builder.UseSetting("AutenticacionMicrosoft:Habilitada", "true");
            builder.UseSetting("AutenticacionMicrosoft:ClientId", "cliente-pruebas");
            builder.UseSetting("AutenticacionMicrosoft:ClientSecret", "secreto-pruebas");
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
        servicios.SinSeleccionGuardada();
        return servicios;
    }

    [GeneratedRegex(@"\bHttpClient\b|\bHttpRequestMessage\b|\bSocket\b|\bWebRequest\b|\bSystem\.Net\b")]
    private static partial Regex TiposDeRed();
}
