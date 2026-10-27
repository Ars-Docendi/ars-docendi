using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// With no <c>Asistente__ClaveMaestra</c> the Host starts and the module's ping answers
/// (asistente-modelo-en-caliente, task 1.11).
/// </summary>
public sealed class ModeloEnCalienteSinClaveMaestraTests
{
    private const string CadenaInalcanzable =
        "Host=127.0.0.1;Port=1;Database=no_existe;Username=nadie;Password=nada;Timeout=1";

    private static WebApplicationFactory<Program> CrearHost() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("AutenticacionMicrosoft:Habilitada", "true");
            builder.UseSetting("AutenticacionMicrosoft:ClientId", "cliente-pruebas");
            builder.UseSetting("AutenticacionMicrosoft:ClientSecret", "secreto-pruebas");
            builder.UseSetting("ConnectionStrings:ArsDocendi", CadenaInalcanzable);
            builder.UseSetting("Asistente:ClaveMaestra", string.Empty);
            builder.UseSetting("Asistente:ClaveMaestraAnterior", string.Empty);
        });

    [Fact]
    public async Task The_host_starts_and_the_ping_answers_with_no_master_key()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = CrearHost();
        using var cliente = host.CreateClient();

        using var respuesta = await cliente.GetAsync("/api/asistente/ping", ct);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task The_key_services_resolve_but_report_the_unavailable_key_only_when_used()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = CrearHost();
        using var cliente = host.CreateClient();
        await using var alcance = host.Services.CreateAsyncScope();

        var cifrado = alcance.ServiceProvider.GetRequiredService<CifradoDeCredenciales>();
        _ = alcance.ServiceProvider.GetRequiredService<RepositorioDeCredenciales>();
        _ = alcance.ServiceProvider.GetRequiredService<RepositorioDelModelo>();

        Assert.False(cifrado.Disponible);
        await Assert.ThrowsAsync<ClaveMaestraNoDisponibleException>(() =>
            alcance.ServiceProvider.GetRequiredService<RepositorioDeCredenciales>()
                .GuardarAsync("openai", "sk-live-0123456789", Guid.NewGuid(), ct));
    }
}
