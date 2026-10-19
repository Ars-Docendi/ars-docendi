using System.Net;
using System.Net.Http.Json;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Designaciones.Guarani;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Modules.Designaciones.Api;
using Modules.Designaciones.Infrastructure.Guarani;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Designaciones;

public sealed class ComisionesHttpTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "comisiones_http")
{
    private static readonly Guid Docente = Guid.Parse("a0000000-0000-4000-8000-000000000001");
    private static readonly Guid Jefe = Guid.Parse("a0000000-0000-4000-8000-000000000002");
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Secretaria_importa_y_consulta_por_http()
    {
        await SembrarAsync();
        var guarani = GuaraniDePrueba.ConFixtures();
        using var host = CrearHost(guarani);
        using var cliente = Cliente(host, Secretaria, "secretaria");

        var periodosGuarani = await cliente.GetFromJsonAsync<PeriodoLectivoGuaraniDto[]>(
            "/api/designaciones/comisiones/guarani/periodos-lectivos", Ct);
        Assert.Equal(2026, periodosGuarani![0].Anio);

        using var importar = await cliente.PostAsJsonAsync(
            "/api/designaciones/comisiones/importaciones", new ImportarComisionesDto("152"), Ct);
        Assert.Equal(HttpStatusCode.OK, importar.StatusCode);
        Assert.Equal(3, (await importar.Content.ReadFromJsonAsync<ResultadoImportacionDto>(Ct))!.Cantidad);

        var periodos = await cliente.GetFromJsonAsync<PeriodoImportadoDto[]>("/api/designaciones/comisiones/periodos", Ct);
        Assert.Contains(periodos!, p => p.Id == "152");
        var comisiones = await cliente.GetFromJsonAsync<ComisionDto[]>(
            "/api/designaciones/comisiones?periodoLectivoId=152", Ct);
        Assert.Equal(3, comisiones!.Length);
    }

    [Fact]
    public async Task Consultar_no_contacta_a_guarani()
    {
        await SembrarAsync();
        var guarani = GuaraniDePrueba.ConFixtures();
        using var host = CrearHost(guarani);
        using var cliente = Cliente(host, Jefe, "jefe_catedra");

        using var periodos = await cliente.GetAsync("/api/designaciones/comisiones/periodos", Ct);
        using var listado = await cliente.GetAsync("/api/designaciones/comisiones?periodoLectivoId=152", Ct);

        Assert.Equal(HttpStatusCode.OK, periodos.StatusCode);
        Assert.Equal(HttpStatusCode.OK, listado.StatusCode);
        Assert.Empty(guarani.Solicitudes);
    }

    [Fact]
    public async Task Sin_comisiones_ver_no_consulta_y_sin_comisiones_importar_no_importa()
    {
        await SembrarAsync();
        var guarani = GuaraniDePrueba.ConFixtures();
        using var host = CrearHost(guarani);
        using var docente = Cliente(host, Docente, "docente");
        using var jefe = Cliente(host, Jefe, "jefe_catedra");

        using var listado = await docente.GetAsync("/api/designaciones/comisiones/periodos", Ct);
        using var periodosGuarani = await jefe.GetAsync("/api/designaciones/comisiones/guarani/periodos-lectivos", Ct);
        using var importar = await jefe.PostAsJsonAsync(
            "/api/designaciones/comisiones/importaciones", new ImportarComisionesDto("152"), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, listado.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, periodosGuarani.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, importar.StatusCode);
        Assert.Empty(guarani.Solicitudes);
    }

    [Fact]
    public async Task Integracion_no_configurada_responde_503_identificable()
    {
        await SembrarAsync();
        var guarani = GuaraniDePrueba.ConFixtures();
        using var host = CrearHost(guarani, configurada: false);
        using var cliente = Cliente(host, Secretaria, "secretaria");

        using var respuesta = await cliente.PostAsJsonAsync(
            "/api/designaciones/comisiones/importaciones", new ImportarComisionesDto("152"), Ct);
        var cuerpo = await respuesta.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        Assert.Contains("guarani-no-configurado", cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain(GuaraniDePrueba.Opciones().Usuario, cuerpo, StringComparison.Ordinal);
        Assert.Empty(guarani.Solicitudes);
    }

    [Fact]
    public async Task Credenciales_rechazadas_responden_502_sin_exponerlas()
    {
        await SembrarAsync();
        var guarani = GuaraniDePrueba.ConFixtures()
            .Responder(GuaraniDePrueba.Comisiones("152"), "[]", HttpStatusCode.Unauthorized);
        using var host = CrearHost(guarani);
        using var cliente = Cliente(host, Secretaria, "secretaria");

        using var respuesta = await cliente.PostAsJsonAsync(
            "/api/designaciones/comisiones/importaciones", new ImportarComisionesDto("152"), Ct);
        var cuerpo = await respuesta.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.BadGateway, respuesta.StatusCode);
        Assert.Contains("guarani-autenticacion", cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain(GuaraniDePrueba.Opciones().Contrasena, cuerpo, StringComparison.Ordinal);
    }

    private WebApplicationFactory<Program> CrearHost(GuaraniDePrueba guarani, bool configurada = true) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:ArsDocendi", Cadena);
            builder.UseSetting($"{AutenticacionDesarrolloOptions.Seccion}:Enabled", "true");
            builder.ConfigureTestServices(services =>
            {
                var opciones = GuaraniDePrueba.Opciones();
                services.Configure<OpcionesGuarani>(o =>
                {
                    o.UrlBase = opciones.UrlBase;
                    o.Usuario = opciones.Usuario;
                    o.Contrasena = configurada ? opciones.Contrasena : string.Empty;
                    o.RutaPeriodosLectivos = opciones.RutaPeriodosLectivos;
                    o.RutaComisiones = opciones.RutaComisiones;
                });
                services.AddHttpClient<ClienteGuarani>().ConfigurePrimaryHttpMessageHandler(() => guarani);
            });
        });

    private static HttpClient Cliente(WebApplicationFactory<Program> host, Guid usuario, string rol)
    {
        var cliente = host.CreateClient();
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, usuario.ToString());
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, rol);
        return cliente;
    }

    private async Task SembrarAsync()
    {
        var raiz = new DirectoryInfo(AppContext.BaseDirectory);
        while (raiz is not null && !File.Exists(Path.Join(raiz.FullName, "AGENTS.md"))) raiz = raiz.Parent;
        var sql = await File.ReadAllTextAsync(Path.Join(raiz!.FullName, "infra", "scripts", "seed-data", "sintetico.sql"), Ct);
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion) { CommandTimeout = 60 };
        await comando.ExecuteNonQueryAsync(Ct);
    }
}
