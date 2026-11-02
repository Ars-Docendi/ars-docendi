using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Portal;

public sealed class VersionPortalHttpTests(PostgresFixture postgres) : ClasePostgresAislada(postgres, "portal_version")
{
    [Fact]
    public async Task Dos_contactos_con_la_misma_version_no_pierden_el_primero()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var conexion = await AbrirConexionAsync();
        await using var seed = new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(Raiz(), "infra/scripts/seed-data/sintetico.sql"), ct), conexion);
        await seed.ExecuteNonQueryAsync(ct);
        using var fabrica = new WebApplicationFactory<Program>();
        using var host = fabrica.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development"); b.UseSetting("ConnectionStrings:ArsDocendi", Cadena);
            b.UseSetting("DevelopmentAuthentication:Enabled", "true");
        });
        using var cliente = host.CreateClient();
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, "a0000000-0000-4000-8000-000000000001");
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, "docente");
        var perfil = await cliente.GetFromJsonAsync<JsonObject>("/api/portal/perfil", ct);
        var version = perfil!["contacto"]?["version"]?.GetValue<uint>() ?? 0;
        using var primero = await cliente.PutAsJsonAsync("/api/portal/perfil/contacto", new { telefono = "primero", mail = (string?)null, version }, ct);
        Assert.Equal(HttpStatusCode.OK, primero.StatusCode);
        using var segundo = await cliente.PutAsJsonAsync("/api/portal/perfil/contacto", new { telefono = "segundo", mail = (string?)null, version }, ct);
        Assert.Equal(HttpStatusCode.Conflict, segundo.StatusCode);
        Assert.Equal("concurrency-conflict", (await segundo.Content.ReadFromJsonAsync<JsonObject>(ct))!["code"]!.GetValue<string>());
        perfil = await cliente.GetFromJsonAsync<JsonObject>("/api/portal/perfil", ct);
        Assert.Equal("primero", perfil!["contacto"]!["telefono"]!.GetValue<string>());
    }

    [Fact]
    public async Task Reemplazar_habilidades_obsoletas_no_afecta_intereses()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var conexion = await AbrirConexionAsync();
        await using var seed = new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(Raiz(), "infra/scripts/seed-data/sintetico.sql"), ct), conexion);
        await seed.ExecuteNonQueryAsync(ct);
        using var fabrica = new WebApplicationFactory<Program>();
        using var host = fabrica.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development"); b.UseSetting("ConnectionStrings:ArsDocendi", Cadena);
            b.UseSetting("DevelopmentAuthentication:Enabled", "true");
        });
        using var cliente = host.CreateClient();
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, "a0000000-0000-4000-8000-000000000001");
        cliente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, "docente");
        var perfil = await cliente.GetFromJsonAsync<JsonObject>("/api/portal/perfil", ct);
        var version = perfil!["versionHabilidades"]?.GetValue<long>();
        var versionIntereses = perfil["versionIntereses"]?.GetValue<long>();
        using var primera = await cliente.PutAsJsonAsync("/api/portal/perfil/habilidades", new { terminos = new[] { "Nueva primera" }, version }, ct);
        Assert.Equal(HttpStatusCode.NoContent, primera.StatusCode);
        using var otraSeccion = await cliente.PutAsJsonAsync("/api/portal/perfil/intereses", new { terminos = new[] { "Interés independiente" }, version = versionIntereses }, ct);
        Assert.Equal(HttpStatusCode.NoContent, otraSeccion.StatusCode);
        using var obsoleta = await cliente.PutAsJsonAsync("/api/portal/perfil/habilidades", new { terminos = new[] { "Nueva obsoleta" }, version }, ct);
        Assert.Equal(HttpStatusCode.Conflict, obsoleta.StatusCode);
        perfil = await cliente.GetFromJsonAsync<JsonObject>("/api/portal/perfil", ct);
        Assert.Equal("Nueva primera", perfil!["habilidades"]![0]!["termino"]!.GetValue<string>());
        Assert.Equal("Interés independiente", perfil["intereses"]![0]!["termino"]!.GetValue<string>());
    }

    private static string Raiz()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null) { if (File.Exists(Path.Combine(d.FullName, "AGENTS.md"))) return d.FullName; d = d.Parent; }
        throw new DirectoryNotFoundException();
    }
}
