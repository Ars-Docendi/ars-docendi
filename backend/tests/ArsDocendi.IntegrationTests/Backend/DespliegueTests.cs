using System.Net;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ArsDocendi.IntegrationTests.Backend;

/// <summary>Guard de arranque: en Production el ingreso con Microsoft es obligatorio.</summary>
public sealed class DespliegueTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "despliegue")
{
    [Fact]
    public void Production_sin_ingreso_con_Microsoft_no_arranca_y_no_expone_secretos()
    {
        using var host = CrearHost(clientSecret: "valor-que-no-debe-aparecer", habilitada: false);

        var error = Assert.Throws<InvalidOperationException>(() => host.CreateClient());

        Assert.Contains("AutenticacionMicrosoft", error.Message);
        Assert.DoesNotContain("valor-que-no-debe-aparecer", error.Message);
    }

    [Fact]
    public void Production_sin_client_secret_no_arranca()
    {
        using var host = CrearHost(clientSecret: "", habilitada: true);

        Assert.Throws<InvalidOperationException>(() => host.CreateClient());
    }

    [Fact]
    public async Task Production_configurado_arranca()
    {
        using var host = CrearHost(clientSecret: "secreto-pruebas", habilitada: true);
        using var cliente = host.CreateClient();

        using var respuesta = await cliente.GetAsync("/api/portal/ping", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    private WebApplicationFactory<Program> CrearHost(string clientSecret, bool habilitada) =>
        new HostPruebas(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:ArsDocendi", Cadena);
            builder.UseSetting("AutenticacionMicrosoft:Habilitada", habilitada.ToString());
            builder.UseSetting("AutenticacionMicrosoft:ClientId", "cliente-pruebas");
            builder.UseSetting("AutenticacionMicrosoft:ClientSecret", clientSecret);
        });

    private sealed class HostPruebas(Action<IWebHostBuilder> configurar) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => configurar(builder);
    }
}
