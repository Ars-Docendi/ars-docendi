using System.IO.Compression;
using System.Net;
using System.Xml.Linq;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Designaciones;

[Collection(ColeccionPostgres.Nombre)]
public sealed class ExportacionLoteTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "exportacion_lote")
{
    private static readonly Guid Periodo = Guid.Parse("d4000000-0000-4000-8000-000000000001");
    private static readonly Guid Jefe = Guid.Parse("a0000000-0000-4000-8000-000000000002");

    [Theory]
    [InlineData("a0000000-0000-4000-8000-000000000004", "secretaria")]
    [InlineData("a0000000-0000-4000-8000-000000000005", "decanato")]
    [InlineData("a0000000-0000-4000-8000-000000000006", "administrativo")]
    public async Task Rol_departamental_descarga_la_planilla_institucional(string usuario, string rol)
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
        using var zip = new ZipArchive(
            new MemoryStream(await respuesta.Content.ReadAsByteArrayAsync(ct)), ZipArchiveMode.Read);
        var libro = LeerXml(zip, "xl/workbook.xml");
        Assert.Equal(
            ["PROPUESTA COMPLETA", "ALTAS", "BAJAS"],
            libro.Descendants().Single(e => e.Name.LocalName == "sheets")
                .Elements().Select(e => e.Attribute("name")?.Value).ToArray());
        Assert.NotNull(zip.GetEntry("xl/theme/theme1.xml"));
        Assert.NotNull(zip.GetEntry("xl/styles.xml"));

        var propuesta = LeerXml(zip, "xl/worksheets/sheet1.xml");
        var filas = FilasDeDatos(propuesta, 5);
        Assert.Contains("Primer cuatrimestre 2026", Valor(propuesta, "D1"));
        Assert.Contains("Segundo cuatrimestre 2026", Valor(propuesta, "E1"));
        Assert.Contains("Profesor Adjunto / Categoría 2", Valores(filas, "D"));
        Assert.Contains("Ayudante de Primera / Categoría 4", Valores(filas, "E"));
        Assert.Contains("Actualización de designación", Valores(filas, "F"));
        var docentes = filas.Select(f => Valor(f, "B")).ToArray();
        Assert.Equal(docentes.Order(StringComparer.Ordinal), docentes);
        var pares = filas.Select(f => $"{Valor(f, "B")}\u001f{Valor(f, "H")}").ToArray();
        Assert.Equal(pares.Length, pares.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain("2026-9001", Texto(zip));
        Assert.DoesNotContain("MAYO-AGOSTO", Texto(zip));
        Assert.DoesNotContain("d5000000-0000-4000-8000-000000000006", Texto(zip));
        Assert.Equal("n", Celda(filas.First(), "G")?.Attribute("t")?.Value);
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
        Autenticar(cliente, Jefe, "jefe_catedra");
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await cliente.GetAsync($"/api/designaciones/periodos/{Periodo}/lote.xlsx", ct)).StatusCode);
    }

    [Fact]
    public async Task API_rechaza_período_inexistente_o_que_dejó_de_estar_activo()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        await EjecutarAsync("UPDATE designaciones.periodos SET activo = FALSE WHERE id = @id", ct, new NpgsqlParameter("id", Periodo));

        using var cliente = CrearHost().CreateClient();
        Autenticar(cliente, Guid.Parse("a0000000-0000-4000-8000-000000000005"), "decanato");
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await cliente.GetAsync($"/api/designaciones/periodos/{Periodo}/lote.xlsx", ct)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await cliente.GetAsync(
                "/api/designaciones/periodos/aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa/lote.xlsx", ct)).StatusCode);
    }

    [Fact]
    public async Task Alta_y_baja_se_separan_y_no_mutan_el_estado()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        await EjecutarAsync("""
            UPDATE designaciones.pedidos SET estado = 'en_lote'
             WHERE id = 'd5000000-0000-4000-8000-000000000004';
            UPDATE designaciones.pedidos SET estado = 'en_lote'
             WHERE id = 'd5000000-0000-4000-8000-000000000008';
            UPDATE designaciones.designaciones
               SET vigente_hasta = DATE '2026-07-31'
             WHERE id = 'd6000000-0000-4000-8000-000000000010';
            """, ct);
        var estadoAntes = await EscalarAsync<string>(
            "SELECT estado FROM designaciones.pedidos WHERE id = 'd5000000-0000-4000-8000-000000000008'", ct);

        using var cliente = CrearHost().CreateClient();
        Autenticar(cliente, Guid.Parse("a0000000-0000-4000-8000-000000000005"), "decanato");
        using var respuesta = await cliente.GetAsync($"/api/designaciones/periodos/{Periodo}/lote.xlsx", ct);
        using var zip = new ZipArchive(
            new MemoryStream(await respuesta.Content.ReadAsByteArrayAsync(ct)), ZipArchiveMode.Read);
        var propuesta = LeerXml(zip, "xl/worksheets/sheet1.xml");
        var altas = LeerXml(zip, "xl/worksheets/sheet2.xml");
        var bajas = LeerXml(zip, "xl/worksheets/sheet3.xml");

        Assert.Contains("Ortiz, Brenda", Texto(altas));
        Assert.Contains("27-40333012-4", Texto(altas));
        Assert.Contains("brenda.ortiz@example.com", Texto(altas));
        Assert.Equal("n", Celda(FilasDeDatos(altas, 2).Single(), "E")?.Attribute("t")?.Value);
        Assert.Contains("Giménez, Laura", Texto(bajas));
        Assert.Contains("Profesor Titular / Categoría 1", Texto(bajas));
        Assert.Contains("Renuncia", Texto(bajas));
        Assert.Single(FilasDeDatos(propuesta, 5), f => Valor(f, "B") == "Ortiz, Brenda");
        Assert.Single(FilasDeDatos(propuesta, 5), f => Valor(f, "B") == "Giménez, Laura");
        Assert.Contains("Ayudante de Segunda / Categoría 5", Valores(FilasDeDatos(propuesta, 5), "E"));
        Assert.Equal(estadoAntes, await EscalarAsync<string>(
            "SELECT estado FROM designaciones.pedidos WHERE id = 'd5000000-0000-4000-8000-000000000008'", ct));
        Assert.Equal(1L, await EscalarAsync<long>(
            "SELECT count(*) FROM designaciones.designaciones WHERE id = 'd6000000-0000-4000-8000-000000000010' AND vigente_hasta = DATE '2026-07-31'", ct));
    }

    [Fact]
    public async Task XLSX_conserva_textos_literales_CUIL_y_celdas_sin_fuente()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        await EjecutarAsync("""
            UPDATE identity.materias SET name = '=Materia de prueba'
             WHERE id = '70000000-0000-4000-8000-000000000102';
            UPDATE identity.personas SET cuil = '0014', legajo = '0014'
             WHERE id = 'd0000000-0000-4000-8000-000000000014';
            """, ct);

        using var cliente = CrearHost().CreateClient();
        Autenticar(cliente, Guid.Parse("a0000000-0000-4000-8000-000000000005"), "decanato");
        using var respuesta = await cliente.GetAsync($"/api/designaciones/periodos/{Periodo}/lote.xlsx", ct);
        using var zip = new ZipArchive(
            new MemoryStream(await respuesta.Content.ReadAsByteArrayAsync(ct)), ZipArchiveMode.Read);
        var propuesta = LeerXml(zip, "xl/worksheets/sheet1.xml");
        var filaNatalia = FilasDeDatos(propuesta, 5).Single(f =>
            Valor(f, "B") == "Castro, Natalia" && Valor(f, "H") == "=Materia de prueba");

        Assert.Equal("0014", Valor(filaNatalia, "C"));
        Assert.Equal("inlineStr", Celda(filaNatalia, "C")?.Attribute("t")?.Value);
        Assert.Contains("=Materia de prueba", Texto(propuesta));
        Assert.All(FilasDeDatos(propuesta, 5), fila =>
        {
            Assert.True(Celda(fila, "I")?.Elements().SingleOrDefault() is null);
            Assert.True(Celda(fila, "J")?.Elements().SingleOrDefault() is null);
            Assert.True(Celda(fila, "K")?.Elements().SingleOrDefault() is null);
        });
    }

    [Fact]
    public async Task Sin_período_anterior_no_reutiliza_el_encabezado_del_modelo()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        await EjecutarAsync("DELETE FROM designaciones.periodos WHERE id <> @id", ct, new NpgsqlParameter("id", Periodo));

        using var cliente = CrearHost().CreateClient();
        Autenticar(cliente, Guid.Parse("a0000000-0000-4000-8000-000000000005"), "decanato");
        using var respuesta = await cliente.GetAsync($"/api/designaciones/periodos/{Periodo}/lote.xlsx", ct);
        using var zip = new ZipArchive(
            new MemoryStream(await respuesta.Content.ReadAsByteArrayAsync(ct)), ZipArchiveMode.Read);
        var propuesta = LeerXml(zip, "xl/worksheets/sheet1.xml");

        Assert.Equal("CARGO /DEDICACIÓN\nDESIGNACIÓN", Valor(propuesta, "D1"));
        Assert.DoesNotContain("ENERO-ABRIL", Texto(propuesta));
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
        await EjecutarAsync(sql, ct);
    }

    private async Task EjecutarAsync(string sql, CancellationToken ct, params NpgsqlParameter[] parametros)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion) { CommandTimeout = 60 };
        comando.Parameters.AddRange(parametros);
        await comando.ExecuteNonQueryAsync(ct);
    }

    private async Task<T> EscalarAsync<T>(string sql, CancellationToken ct)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        return (T)(await comando.ExecuteScalarAsync(ct))!;
    }

    private static XDocument LeerXml(ZipArchive zip, string nombre)
    {
        using var stream = zip.GetEntry(nombre)!.Open();
        return XDocument.Load(stream);
    }

    private static XElement[] FilasDeDatos(XDocument documento, int desde) =>
        documento.Descendants().Single(e => e.Name.LocalName == "sheetData")
            .Elements().Where(e => int.Parse(e.Attribute("r")!.Value) >= desde).ToArray();

    private static string Valores(IEnumerable<XElement> filas, string columna) =>
        string.Join("\n", filas.Select(f => Valor(f, columna)));

    private static string Valor(XDocument documento, string referencia) =>
        documento.Descendants().Single(e => e.Attribute("r")?.Value == referencia)
            .Descendants().FirstOrDefault(e => e.Name.LocalName is "t" or "v")?.Value ?? string.Empty;

    private static string Valor(XElement fila, string columna) =>
        Celda(fila, columna)?.Descendants().FirstOrDefault(e => e.Name.LocalName is "t" or "v")?.Value ?? string.Empty;

    private static XElement? Celda(XElement fila, string columna) =>
        fila.Elements().FirstOrDefault(e => e.Name.LocalName == "c" && e.Attribute("r")?.Value.StartsWith(columna, StringComparison.Ordinal) == true);

    private static string Texto(XDocument documento) => documento.ToString(SaveOptions.DisableFormatting);

    private static string Texto(ZipArchive zip) =>
        string.Join("\n", zip.Entries.Where(e => e.FullName.EndsWith(".xml", StringComparison.Ordinal))
            .Select(e => { using var s = e.Open(); using var reader = new StreamReader(s); return reader.ReadToEnd(); }));

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
