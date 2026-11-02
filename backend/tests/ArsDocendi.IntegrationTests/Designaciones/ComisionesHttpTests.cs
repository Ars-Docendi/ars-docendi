using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Xml.Linq;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Designaciones.Guarani;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Modules.Designaciones.Api;
using Modules.Designaciones.Infrastructure.Guarani;
using Modules.Designaciones.Services;
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
        var resultado = await importar.Content.ReadAsStringAsync(Ct);
        ContratoFrontend.Verificar("comisiones-importacion.json", resultado);
        Assert.Equal(3, JsonDocument.Parse(resultado).RootElement.GetProperty("cantidad").GetInt32());

        var periodos = await cliente.GetStringAsync("/api/designaciones/comisiones/periodos", Ct);
        ContratoFrontend.Verificar("comisiones-periodos.json", periodos);
        Assert.Contains(JsonDocument.Parse(periodos).RootElement.EnumerateArray(), p => p.GetProperty("id").GetString() == "152");
        var comisiones = await cliente.GetStringAsync("/api/designaciones/comisiones?periodoLectivoId=152", Ct);
        ContratoFrontend.Verificar("comisiones-listado.json", comisiones);
        Assert.Equal(3, JsonDocument.Parse(comisiones).RootElement.GetArrayLength());
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
    public async Task Exportacion_incluye_solo_lo_pedido_del_periodo_sin_contactar_a_guarani()
    {
        await SembrarAsync();
        var guarani = GuaraniDePrueba.ConFixtures();
        using var host = CrearHost(guarani);
        using var cliente = Cliente(host, Secretaria, "secretaria");
        using var importar = await cliente.PostAsJsonAsync(
            "/api/designaciones/comisiones/importaciones", new ImportarComisionesDto("152"), Ct);
        importar.EnsureSuccessStatusCode();
        var comisiones = await cliente.GetFromJsonAsync<ComisionDto[]>("/api/designaciones/comisiones?periodoLectivoId=152", Ct);
        var (elegida, excluida) = (comisiones![0], comisiones[1]);
        var llamadas = guarani.Solicitudes.Count;

        // SINT-0001 es de otro período: pedirla no la agrega al archivo.
        using var respuesta = await cliente.PostAsJsonAsync(
            "/api/designaciones/comisiones/exportacion",
            new ExportarComisionesDto("152", [elegida.Id, "SINT-0001"]), Ct);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("comisiones-2026-2do-cuatrimestre.xlsx", respuesta.Content.Headers.ContentDisposition?.FileName);
        var hoja = LeerHoja(await respuesta.Content.ReadAsByteArrayAsync(Ct));
        Assert.Contains(elegida.Nombre, hoja, StringComparison.Ordinal);
        Assert.DoesNotContain($">{excluida.Nombre}<", hoja, StringComparison.Ordinal);
        Assert.DoesNotContain("01-0001", hoja, StringComparison.Ordinal);
        Assert.DoesNotContain("28341567", hoja, StringComparison.Ordinal);
        Assert.DoesNotContain("28.341.567", hoja, StringComparison.Ordinal);
        Assert.Equal(llamadas, guarani.Solicitudes.Count);
    }

    [Fact]
    public async Task Exportacion_exige_comisiones_ver_y_rechaza_pedidos_invalidos()
    {
        await SembrarAsync();
        using var host = CrearHost(GuaraniDePrueba.ConFixtures());
        using var docente = Cliente(host, Docente, "docente");
        using var secretaria = Cliente(host, Secretaria, "secretaria");
        const string Ruta = "/api/designaciones/comisiones/exportacion";

        using var sinPermiso = await docente.PostAsJsonAsync(Ruta, new ExportarComisionesDto("sintetico-2026-2", ["SINT-0001"]), Ct);
        using var sinIds = await secretaria.PostAsJsonAsync(Ruta, new ExportarComisionesDto("sintetico-2026-2", []), Ct);
        using var ajena = await secretaria.PostAsJsonAsync(Ruta, new ExportarComisionesDto("sintetico-2026-2", ["no-existe"]), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, sinPermiso.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, sinIds.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, ajena.StatusCode);
    }

    [Fact]
    public void Libro_exportado_es_xml_valido_con_caracteres_especiales()
    {
        var comision = new ComisionDto(
            "1", "=SUMA(A1) & <x>\u0001", new MateriaComisionDto("03500", "Materia", null), [], [], [], null, null, null);

        var hoja = LeerHoja(ExportadorComisionesExcel.Generar([comision]));

        var celda = XDocument.Parse(hoja).Descendants().Single(e => e.Attribute("r")?.Value == "A2");
        Assert.Equal("inlineStr", celda.Attribute("t")?.Value);
        Assert.Equal("=SUMA(A1) & <x>", celda.Value);
    }

    private static string LeerHoja(byte[] libro)
    {
        using var zip = new ZipArchive(new MemoryStream(libro), ZipArchiveMode.Read);
        using var lector = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        return lector.ReadToEnd();
    }

    [Theory]
    [InlineData("Development", false)]
    [InlineData("Staging", true)]
    public void Credenciales_salen_de_las_variables_y_se_exigen_fuera_de_development(string ambiente, bool exige)
    {
        using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(ambiente);
            builder.UseSetting("ConnectionStrings:ArsDocendi", Cadena);
            builder.UseSetting(OpcionesGuarani.VariableUsuario, "usuario-variable");
            builder.UseSetting(OpcionesGuarani.VariableContrasena, "clave-variable");
        });

        var opciones = host.Services.GetRequiredService<IOptions<OpcionesGuarani>>().Value;

        Assert.Equal("usuario-variable", opciones.Usuario);
        Assert.Equal("clave-variable", opciones.Contrasena);
        Assert.Equal(exige, opciones.ExigeCredenciales);
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
                    // Development no exige credenciales: "sin configurar" reproduce a producción sin ellas.
                    o.ExigeCredenciales = !configurada;
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
