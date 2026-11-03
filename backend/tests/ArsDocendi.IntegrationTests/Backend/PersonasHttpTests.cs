using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Backend;

/// <summary>Autorización de la API de personas con los usuarios del seed sintético.</summary>
public sealed class PersonasHttpTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "personas_http")
{
    private static readonly Guid UsuarioDocente = Guid.Parse("a0000000-0000-4000-8000-000000000001");
    private static readonly Guid UsuarioJefe = Guid.Parse("a0000000-0000-4000-8000-000000000002");
    private static readonly Guid UsuarioSecretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private static readonly Guid UsuarioDecanato = Guid.Parse("a0000000-0000-4000-8000-000000000005");
    private static readonly Guid PersonaSecretaria = Guid.Parse("d0000000-0000-4000-8000-000000000004");

    [Fact]
    public async Task Ver_personas_respeta_permiso_y_ambito()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = new Fabrica(Cadena);
        using var docente = Cliente(host, UsuarioDocente, "docente");
        using var jefe = Cliente(host, UsuarioJefe, "jefe_catedra");
        await ElegirJefaturaAsync(jefe);
        using var secretaria = Cliente(host, UsuarioSecretaria, "secretaria");

        using var denegada = await docente.GetAsync("/api/administracion/personas", ct);
        Assert.Equal(HttpStatusCode.Forbidden, denegada.StatusCode);

        var todas = await secretaria.GetFromJsonAsync<JsonElement[]>("/api/administracion/personas", ct);
        var delAmbito = await jefe.GetFromJsonAsync<JsonElement[]>("/api/administracion/personas", ct);
        Assert.NotNull(todas);
        Assert.NotNull(delAmbito);
        Assert.NotEmpty(delAmbito);
        Assert.True(delAmbito.Length < todas.Length);
        Assert.DoesNotContain(delAmbito, p => p.GetProperty("personaId").GetGuid() == PersonaSecretaria);

        using var fueraDelAmbito = await jefe.GetAsync($"/api/administracion/personas/{PersonaSecretaria}", ct);
        Assert.Equal(HttpStatusCode.NotFound, fueraDelAmbito.StatusCode);
    }

    [Fact]
    public async Task Mutar_personas_requiere_administrar()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = new Fabrica(Cadena);
        using var jefe = Cliente(host, UsuarioJefe, "jefe_catedra");
        await ElegirJefaturaAsync(jefe);
        using var decanato = Cliente(host, UsuarioDecanato, "decanato");
        var sinRol = new
        {
            nombre = "Ada", apellido = "Lovelace", documento = "70000099", legajo = "L-99",
            fechaNacimiento = "1985-12-10", upn = "ada.http@unlam.edu.ar",
            membresias = Array.Empty<object>(), designaciones = Array.Empty<object>(),
        };

        using var prohibida = await jefe.PostAsJsonAsync("/api/administracion/personas", sinRol, ct);
        Assert.Equal(HttpStatusCode.Forbidden, prohibida.StatusCode);
        using var documento = await jefe.PostAsJsonAsync(
            "/api/administracion/personas/documento/verificar", new { documento = "70000099" }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, documento.StatusCode);

        // Decanato pasa a administrar: llega a la validación (sin rol → 400).
        using var invalida = await decanato.PostAsJsonAsync("/api/administracion/personas", sinRol, ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalida.StatusCode);
        using var respuesta = await decanato.PostAsJsonAsync(
            "/api/administracion/personas/documento/verificar", new { documento = "70000099" }, ct);
        var verificacion = await respuesta.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.False(verificacion.GetProperty("existe").GetBoolean());
    }

    [Fact]
    public async Task Portal_de_otra_persona_se_corrige_con_permiso_y_ambito()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = new Fabrica(Cadena);
        using var docente = Cliente(host, UsuarioDocente, "docente");
        using var jefe = Cliente(host, UsuarioJefe, "jefe_catedra");
        await ElegirJefaturaAsync(jefe);
        using var secretaria = Cliente(host, UsuarioSecretaria, "secretaria");
        var personaDocente = Guid.Parse("d0000000-0000-4000-8000-000000000001");
        var url = $"/api/administracion/personas/{personaDocente}/portal";

        using var sinPermiso = await docente.PutAsJsonAsync($"{url}/contacto", new { telefono = "11-1111-1111" }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, sinPermiso.StatusCode);

        var actual = await secretaria.GetFromJsonAsync<JsonElement>(url, ct);
        var version = actual.GetProperty("contacto").TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetUInt32() : (uint?)null;
        using var corregido = await secretaria.PutAsJsonAsync($"{url}/contacto",
            new { telefono = "11-2222-3333", mail = "contacto@example.com", version }, ct);
        Assert.Equal(HttpStatusCode.OK, corregido.StatusCode);
        var perfil = await jefe.GetFromJsonAsync<JsonElement>(url, ct);
        Assert.Equal("11-2222-3333", perfil.GetProperty("contacto").GetProperty("telefono").GetString());

        // La corrección queda auditada con el operador como autor.
        await using (var conexion = await AbrirConexionAsync())
        {
            await using var comando = new NpgsqlCommand(
                "SELECT count(*) FROM audit.change_log WHERE schema_name='portal' AND table_name='contactos' AND changed_by=@actor", conexion);
            comando.Parameters.AddWithValue("actor", UsuarioSecretaria);
            Assert.True((long)(await comando.ExecuteScalarAsync(ct))! > 0);
        }

        using var fueraDelAmbito = await jefe.PutAsJsonAsync(
            $"/api/administracion/personas/{PersonaSecretaria}/portal/contacto", new { telefono = "11-0000-0000" }, ct);
        Assert.Equal(HttpStatusCode.NotFound, fueraDelAmbito.StatusCode);
    }

    private sealed class Fabrica(string cadena) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:ArsDocendi", cadena);
            builder.UseSetting($"{AutenticacionDesarrolloOptions.Seccion}:Enabled", bool.TrueString);
        }
    }

    /// <summary>El Jefe sembrado lo es en varias materias: opera con la del docente sembrado.</summary>
    private async Task ElegirJefaturaAsync(HttpClient cliente) =>
        cliente.DefaultRequestHeaders.Add(
            AutenticacionDesarrolloHandler.HeaderAsignacion,
            (await AsignacionEnMateriaAsync(
                UsuarioJefe, "jefe_catedra", Guid.Parse("70000000-0000-4000-8000-000000000101"))).ToString());

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
}
