using System.IO.Compression;
using System.Net;
using System.Xml.Linq;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Modules.Designaciones.Domain;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Designaciones;

[Collection(ColeccionPostgres.Nombre)]
public sealed class ExportacionLoteTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "exportacion_lote")
{
    private static readonly Guid Periodo = Guid.Parse("d4000000-0000-4000-8000-000000000001");
    private static readonly Guid Jefe = Guid.Parse("a0000000-0000-4000-8000-000000000002");

    [Theory]
    [InlineData("a0000000-0000-4000-8000-000000000004", RolesCircuito.Secretaria)]
    [InlineData("a0000000-0000-4000-8000-000000000005", RolesCircuito.Decanato)]
    [InlineData("a0000000-0000-4000-8000-000000000006", RolesCircuito.Administrativo)]
    public async Task Rol_departamental_descarga_un_XLSX_de_dos_hojas(string usuario, string rol)
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var cliente = CrearHost().CreateClient();
        Autenticar(cliente, Guid.Parse(usuario), rol);

        using var respuesta = await cliente.GetAsync($"/api/designaciones/periodos/{Periodo}/lote.xlsx", ct);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            respuesta.Content.Headers.ContentType?.MediaType);
        var contenido = await respuesta.Content.ReadAsByteArrayAsync(ct);
        using var zip = new ZipArchive(new MemoryStream(contenido), ZipArchiveMode.Read);
        var libro = LeerXml(zip, "xl/workbook.xml");
        var hojas = libro.Root!.Elements().Single(e => e.Name.LocalName == "sheets").Elements().ToArray();

        Assert.Equal(
            ["Pedidos finalizados", "Designaciones resultantes"],
            hojas.Select(h => h.Attribute("name")?.Value).ToArray());
        var hojaPedidos = LeerXml(zip, "xl/worksheets/sheet1.xml").ToString();
        var hojaDesignaciones = LeerXml(zip, "xl/worksheets/sheet2.xml").ToString();
        var elementosHoja = LeerXml(zip, "xl/worksheets/sheet1.xml")
            .Root!
            .Elements()
            .Select(e => e.Name.LocalName)
            .ToArray();
        Assert.Equal(["dimension", "sheetViews", "sheetData"], elementosHoja);
        var filas = LeerXml(zip, "xl/worksheets/sheet1.xml")
            .Descendants()
            .Single(e => e.Name.LocalName == "sheetData")
            .Elements();
        Assert.All(filas, fila => Assert.Equal("row", fila.Name.LocalName));
        Assert.Contains("2026-9006", hojaPedidos);
        Assert.DoesNotContain("2026-9001", hojaPedidos);
        Assert.Contains("Continuidad", hojaDesignaciones);
        Assert.DoesNotContain("d5000000-0000-4000-8000-000000000006", hojaDesignaciones);

        var celdaNumerica = LeerXml(zip, "xl/worksheets/sheet1.xml")
            .Descendants().Single(e => e.Attribute("r")?.Value == "J6");
        Assert.Equal("n", celdaNumerica.Attribute("t")?.Value);
    }

    [Fact]
    public async Task API_rechaza_sin_autenticacion_y_roles_fuera_del_lote()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost();
        using var sinAutenticar = host.CreateClient();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await sinAutenticar.GetAsync($"/api/designaciones/periodos/{Periodo}/lote.xlsx", ct)).StatusCode);

        using var cliente = host.CreateClient();
        Autenticar(cliente, Jefe, RolesCircuito.JefeCatedra);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await cliente.GetAsync($"/api/designaciones/periodos/{Periodo}/lote.xlsx", ct)).StatusCode);
    }

    [Fact]
    public async Task API_rechaza_período_inexistente_o_que_dejó_de_estar_activo()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        await using (var conexion = await AbrirConexionAsync())
        await using (var comando = new NpgsqlCommand(
            "UPDATE designaciones.periodos SET activo = FALSE WHERE id = $1", conexion))
        {
            comando.Parameters.AddWithValue(Periodo);
            await comando.ExecuteNonQueryAsync(ct);
        }

        using var cliente = CrearHost().CreateClient();
        Autenticar(cliente, Guid.Parse("a0000000-0000-4000-8000-000000000005"), RolesCircuito.Decanato);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await cliente.GetAsync($"/api/designaciones/periodos/{Periodo}/lote.xlsx", ct)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await cliente.GetAsync(
                "/api/designaciones/periodos/aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa/lote.xlsx", ct)).StatusCode);
    }

    [Fact]
    public async Task XLSX_conserva_textos_formula_legajo_con_ceros_y_valores_desconocidos()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        await using (var conexion = await AbrirConexionAsync())
        await using (var comando = new NpgsqlCommand("""
            UPDATE identity.materias SET name = '=Materia de prueba'
             WHERE id = '70000000-0000-4000-8000-000000000102';
            UPDATE identity.personas SET legajo = '0014'
             WHERE id = 'd0000000-0000-4000-8000-000000000014';
            UPDATE designaciones.designaciones
               SET horas_investigacion = NULL, horas_externas = NULL
             WHERE id = 'd6000000-0000-4000-8000-000000000001';
            """, conexion))
        {
            await comando.ExecuteNonQueryAsync(ct);
        }

        using var cliente = CrearHost().CreateClient();
        Autenticar(cliente, Guid.Parse("a0000000-0000-4000-8000-000000000005"), RolesCircuito.Decanato);
        using var respuesta = await cliente.GetAsync($"/api/designaciones/periodos/{Periodo}/lote.xlsx", ct);
        var contenido = await respuesta.Content.ReadAsByteArrayAsync(ct);
        using var zip = new ZipArchive(new MemoryStream(contenido), ZipArchiveMode.Read);
        var pedidos = LeerXml(zip, "xl/worksheets/sheet1.xml").ToString();
        var documentoDesignaciones = LeerXml(zip, "xl/worksheets/sheet2.xml");
        var designaciones = documentoDesignaciones.ToString();

        Assert.Contains("=Materia de prueba", pedidos, StringComparison.Ordinal);
        Assert.Contains("t=\"inlineStr\"", pedidos, StringComparison.Ordinal);
        Assert.Contains("0014", pedidos, StringComparison.Ordinal);
        Assert.True(documentoDesignaciones.Descendants().Single(e => e.Attribute("r")?.Value == "K6").IsEmpty);
        Assert.True(documentoDesignaciones.Descendants().Single(e => e.Attribute("r")?.Value == "L6").IsEmpty);
    }

    private WebApplicationFactory<Program> CrearHost() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:ArsDocendi", Cadena);
            builder.UseSetting($"{AutenticacionDesarrolloOptions.Seccion}:Enabled", "true");
        });

    private static void Autenticar(HttpClient cliente, Guid usuario, string rol)
    {
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, usuario.ToString());
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, rol);
    }

    private async Task EjecutarSeedAsync(CancellationToken ct)
    {
        var sql = await File.ReadAllTextAsync(
            Path.Combine(BuscarRaizRepositorio(), "infra", "scripts", "seed-data", "sintetico.sql"), ct);
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion) { CommandTimeout = 60 };
        await comando.ExecuteNonQueryAsync(ct);
    }

    private static XDocument LeerXml(ZipArchive zip, string nombre)
    {
        using var stream = zip.GetEntry(nombre)!.Open();
        return XDocument.Load(stream);
    }

    private static string BuscarRaizRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null)
        {
            if (File.Exists(Path.Combine(directorio.FullName, "AGENTS.md"))) return directorio.FullName;
            directorio = directorio.Parent;
        }
        throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
    }
}
