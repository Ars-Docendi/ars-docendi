using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ArsDocendi.Host.Desarrollo;
using Modules.Asistente.Infrastructure;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

public sealed class InferenciaTests(PostgresFixture postgres) : ClasePostgresAislada(postgres, "inferencia")
{
    private const string UrlLocal = "http://traefik:11435";
    private const string UrlTunel = "https://ia.prueba.invalid";
    private static readonly Guid AdministradorSistema = Guid.Parse("a0000000-0000-4000-8000-000000000007");
    private static readonly Guid Docente = Guid.Parse("a0000000-0000-4000-8000-000000000001");

    [Fact]
    public async Task Sin_url_informa_deshabilitado_sin_peticiones_salientes()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        var servicio = new ServicioFalso(HttpStatusCode.OK);
        using var host = new FabricaInferencia(Cadena, servicio, new());

        var estado = await ConsultarEstadoAsync(host, ct);

        Assert.Equal("deshabilitado", estado.GetProperty("estado").GetString());
        Assert.Empty(servicio.Peticiones);
    }

    [Fact]
    public async Task Configuracion_local_de_prod_usa_basic_auth_sin_service_token()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        var servicio = new ServicioFalso(HttpStatusCode.OK);
        using var host = new FabricaInferencia(Cadena, servicio, Configuracion(UrlLocal, "prod"));

        var estado = await ConsultarEstadoAsync(host, ct);

        Assert.Equal("disponible", estado.GetProperty("estado").GetString());
        var peticion = Assert.Single(servicio.Peticiones);
        Assert.Equal($"{UrlLocal}/api/version", peticion.RequestUri!.ToString());
        Assert.Equal(BasicAuth("prod"), peticion.Headers.Authorization!.ToString());
        Assert.False(peticion.Headers.Contains("CF-Access-Client-Id"));
        Assert.False(peticion.Headers.Contains("CF-Access-Client-Secret"));
    }

    [Fact]
    public async Task Configuracion_por_tunel_agrega_cabeceras_de_cloudflare_access()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        var servicio = new ServicioFalso(HttpStatusCode.OK);
        var configuracion = Configuracion(UrlTunel, "staging");
        configuracion["Inferencia:AccessClientId"] = "id-prueba.access";
        configuracion["Inferencia:AccessClientSecret"] = "secreto-prueba";
        using var host = new FabricaInferencia(Cadena, servicio, configuracion);

        var estado = await ConsultarEstadoAsync(host, ct);

        Assert.Equal("disponible", estado.GetProperty("estado").GetString());
        var peticion = Assert.Single(servicio.Peticiones);
        Assert.Equal(BasicAuth("staging"), peticion.Headers.Authorization!.ToString());
        Assert.Equal("id-prueba.access", Assert.Single(peticion.Headers.GetValues("CF-Access-Client-Id")));
        Assert.Equal("secreto-prueba", Assert.Single(peticion.Headers.GetValues("CF-Access-Client-Secret")));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Respuesta_no_exitosa_informa_no_disponible_sin_datos_internos(HttpStatusCode codigo)
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = new FabricaInferencia(Cadena, new ServicioFalso(codigo), Configuracion(UrlTunel, "staging"));

        var estado = await ConsultarEstadoAsync(host, ct);

        Assert.Equal("no_disponible", estado.GetProperty("estado").GetString());
        AssertSinDatosInternos(estado);
    }

    [Fact]
    public async Task Error_de_red_o_tiempo_limite_informa_no_disponible()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var sinConexion = new FabricaInferencia(
            Cadena, new ServicioFalso(error: new HttpRequestException("ia.prueba.invalid:443")), Configuracion(UrlTunel, "staging"));
        using var lento = new FabricaInferencia(
            Cadena, new ServicioFalso(demora: TimeSpan.FromSeconds(30)), Configuracion(UrlTunel, "staging"));

        var estadoSinConexion = await ConsultarEstadoAsync(sinConexion, ct);
        var estadoLento = await ConsultarEstadoAsync(lento, ct);

        Assert.Equal("no_disponible", estadoSinConexion.GetProperty("estado").GetString());
        AssertSinDatosInternos(estadoSinConexion);
        Assert.Equal("no_disponible", estadoLento.GetProperty("estado").GetString());
        Assert.InRange(estadoLento.GetProperty("duracionMs").GetDouble(), 2_900, 10_000);
    }

    [Fact]
    public async Task Sin_permiso_devuelve_403_sin_contactar_el_servicio()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        var servicio = new ServicioFalso(HttpStatusCode.OK);
        using var host = new FabricaInferencia(Cadena, servicio, Configuracion(UrlLocal, "prod"));
        using var docente = Cliente(host, Docente, "docente");

        using var respuesta = await docente.GetAsync("/api/inferencia/estado", ct);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Empty(servicio.Peticiones);
    }

    [Fact]
    public void Configuracion_incompleta_fuera_de_development_impide_el_arranque_sin_mostrar_valores()
    {
        var configuracion = Configuracion("https://ia-secreta.prueba.invalid", "usuario-secreto");
        configuracion["Inferencia:Contrasena"] = "";
        configuracion["Inferencia:AccessClientId"] = "id-secreto.access";
        using var host = new FabricaInferencia(Cadena, new ServicioFalso(HttpStatusCode.OK), configuracion, "Staging");

        var error = Record.Exception(() => host.CreateClient());

        Assert.NotNull(error);
        var texto = error.ToString();
        Assert.Contains("Falta Inferencia:Contrasena", texto);
        Assert.Contains("deben configurarse juntos", texto);
        Assert.DoesNotContain("ia-secreta", texto);
        Assert.DoesNotContain("usuario-secreto", texto);
        Assert.DoesNotContain("id-secreto", texto);
    }

    private static Dictionary<string, string?> Configuracion(string url, string usuario) => new()
    {
        ["Inferencia:Url"] = url,
        ["Inferencia:Usuario"] = usuario,
        ["Inferencia:Contrasena"] = "clave-prueba",
    };

    private static string BasicAuth(string usuario) =>
        "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{usuario}:clave-prueba"));

    private static void AssertSinDatosInternos(JsonElement estado)
    {
        var texto = estado.GetRawText();
        Assert.DoesNotContain("ia.prueba", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("staging", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clave", texto, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<JsonElement> ConsultarEstadoAsync(FabricaInferencia host, CancellationToken ct)
    {
        using var administrador = Cliente(host, AdministradorSistema, "sys_admin");
        using var respuesta = await administrador.GetAsync("/api/inferencia/estado", ct);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var estado = await respuesta.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.True(estado.GetProperty("comprobadoEn").GetDateTimeOffset() > DateTimeOffset.MinValue);
        return estado;
    }

    private static HttpClient Cliente(WebApplicationFactory<Program> host, Guid usuarioId, string rol)
    {
        var cliente = host.CreateClient();
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, usuarioId.ToString());
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, rol);
        return cliente;
    }

    private async Task EjecutarSeedAsync(CancellationToken ct)
    {
        var sql = await File.ReadAllTextAsync(
            Path.Join(BuscarRaizRepositorio(), "infra", "scripts", "seed-data", "sintetico.sql"), ct);
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion) { CommandTimeout = 60 };
        await comando.ExecuteNonQueryAsync(ct);
    }

    private static string BuscarRaizRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null)
        {
            if (File.Exists(Path.Join(directorio.FullName, "AGENTS.md"))) return directorio.FullName;
            directorio = directorio.Parent;
        }
        throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
    }

    private sealed class FabricaInferencia(
        string cadena,
        ServicioFalso servicio,
        Dictionary<string, string?> configuracion,
        string ambiente = "Development") : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(ambiente);
            builder.UseSetting("ConnectionStrings:ArsDocendi", cadena);
            builder.UseSetting($"{AutenticacionDesarrolloOptions.Seccion}:Enabled", bool.TrueString);
            foreach (var (clave, valor) in configuracion) builder.UseSetting(clave, valor);
            builder.ConfigureTestServices(services =>
                services.Configure<HttpClientFactoryOptions>(nameof(ClienteInferencia), opciones =>
                    opciones.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = servicio)));
        }
    }

    /// <summary>Servicio de inferencia simulado que registra las peticiones recibidas.</summary>
    private sealed class ServicioFalso(
        HttpStatusCode codigo = HttpStatusCode.OK,
        Exception? error = null,
        TimeSpan? demora = null) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Peticiones { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Peticiones.Add(request);
            if (demora is { } espera) await Task.Delay(espera, ct);
            if (error is not null) throw error;
            return new HttpResponseMessage(codigo) { Content = new StringContent("""{"version":"0.0.0"}""") };
        }
    }
}
