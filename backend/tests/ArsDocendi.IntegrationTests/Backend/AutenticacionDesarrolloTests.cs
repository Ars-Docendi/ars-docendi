using System.Net;
using System.Net.Http.Json;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Identity.Desarrollo;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Backend;

[Collection(ColeccionPostgres.Nombre)]
public sealed class AutenticacionDesarrolloTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "auth_dev")
{
    private static readonly Guid Jefe = Guid.Parse("a0000000-0000-4000-8000-000000000002");
    private static readonly Guid Inactivo = Guid.Parse("a0000000-0000-4000-8000-000000000008");

    [Fact]
    public async Task Catalogo_y_handler_aceptan_usuario_activo_con_rol_asignado()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost("Development", true);
        using var cliente = host.CreateClient();

        var identidades = await cliente.GetFromJsonAsync<IdentidadDesarrolloDto[]>(
            "/api/desarrollo/identidades", ct);

        Assert.NotNull(identidades);
        Assert.DoesNotContain(identidades, i => i.UsuarioId == Inactivo);
        var jefe = Assert.Single(identidades, i => i.UsuarioId == Jefe);
        Assert.Contains(jefe.Roles, r => r.Codigo == "jefe_catedra" && r.Materias.Count > 0);

        using var solicitud = new HttpRequestMessage(HttpMethod.Get, "/api/designaciones/catalogos");
        solicitud.Headers.Add(AutenticacionDesarrolloHandler.HeaderUsuario, Jefe.ToString());
        solicitud.Headers.Add(AutenticacionDesarrolloHandler.HeaderRol, "jefe_catedra");
        using var respuesta = await cliente.SendAsync(solicitud, ct);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Theory]
    [InlineData("a0000000-0000-4000-8000-000000000002", "rol_inventado")]
    [InlineData("a0000000-0000-4000-8000-000000000008", "docente")]
    [InlineData("ffffffff-ffff-4fff-8fff-ffffffffffff", "jefe_catedra")]
    public async Task Handler_rechaza_rol_inventado_usuario_inactivo_o_inexistente(
        string usuarioId,
        string rol)
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost("Development", true);
        using var cliente = host.CreateClient();
        using var solicitud = new HttpRequestMessage(HttpMethod.Get, "/api/designaciones/catalogos");
        solicitud.Headers.Add(AutenticacionDesarrolloHandler.HeaderUsuario, usuarioId);
        solicitud.Headers.Add(AutenticacionDesarrolloHandler.HeaderRol, rol);

        using var respuesta = await cliente.SendAsync(solicitud, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Handler_rechaza_usuario_activo_ajeno_al_dataset()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        var usuario = Guid.NewGuid();
        await using (var conexion = await AbrirConexionAsync())
        await using (var comando = new NpgsqlCommand("""
            INSERT INTO identity.personas (id, documento, nombre, apellido)
            VALUES (@id, @documento, 'Fuera', 'Del seed');
            INSERT INTO identity.users (id, azure_oid, upn, display_name, is_active, persona_id)
            VALUES (@id, @oid, @upn, 'Fuera del seed', TRUE, @id);
            INSERT INTO identity.user_roles (id, user_id, role_id, materia_id, carrera_id)
            VALUES (@asignacion, @id, 'a1000000-0000-4000-8000-000000000002',
                    '70000000-0000-4000-8000-000000000101',
                    'c0000000-0000-4000-8000-000000000201');
            """, conexion))
        {
            comando.Parameters.AddWithValue("id", usuario);
            comando.Parameters.AddWithValue("documento", $"T-{usuario:N}");
            comando.Parameters.AddWithValue("oid", Guid.NewGuid());
            comando.Parameters.AddWithValue("upn", $"{usuario:N}@example.test");
            comando.Parameters.AddWithValue("asignacion", Guid.NewGuid());
            await comando.ExecuteNonQueryAsync(ct);
        }
        using var host = CrearHost("Development", true);
        using var cliente = host.CreateClient();
        using var solicitud = new HttpRequestMessage(HttpMethod.Get, "/api/designaciones/catalogos");
        solicitud.Headers.Add(AutenticacionDesarrolloHandler.HeaderUsuario, usuario.ToString());
        solicitud.Headers.Add(AutenticacionDesarrolloHandler.HeaderRol, "jefe_catedra");

        using var respuesta = await cliente.SendAsync(solicitud, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Production_no_registra_endpoint_ni_esquema_de_desarrollo()
    {
        using var host = CrearHost("Production", true);
        using var cliente = host.CreateClient();
        using var solicitud = new HttpRequestMessage(HttpMethod.Get, "/api/desarrollo/identidades");
        solicitud.Headers.Add(AutenticacionDesarrolloHandler.HeaderUsuario, Jefe.ToString());
        solicitud.Headers.Add(AutenticacionDesarrolloHandler.HeaderRol, "jefe_catedra");

        using var respuesta = await cliente.SendAsync(
            solicitud, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Staging_con_opt_in_registra_catalogo_y_esquema_de_desarrollo()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
        using var host = CrearHost("Staging", true);
        using var cliente = host.CreateClient();

        var identidades = await cliente.GetFromJsonAsync<IdentidadDesarrolloDto[]>(
            "/api/desarrollo/identidades", ct);

        Assert.NotNull(identidades);
        Assert.Contains(identidades, identidad => identidad.UsuarioId == Jefe);

        using var solicitud = new HttpRequestMessage(HttpMethod.Get, "/api/designaciones/catalogos");
        solicitud.Headers.Add(AutenticacionDesarrolloHandler.HeaderUsuario, Jefe.ToString());
        solicitud.Headers.Add(AutenticacionDesarrolloHandler.HeaderRol, "jefe_catedra");
        using var respuesta = await cliente.SendAsync(solicitud, ct);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    private WebApplicationFactory<Program> CrearHost(string ambiente, bool habilitada) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(ambiente);
            builder.UseSetting("ConnectionStrings:ArsDocendi", Cadena);
            builder.UseSetting(
                $"{AutenticacionDesarrolloOptions.Seccion}:Enabled",
                habilitada.ToString());
        });

    private async Task EjecutarSeedAsync(CancellationToken ct)
    {
        var sql = await File.ReadAllTextAsync(
            Path.Combine(BuscarRaizRepositorio(), "infra", "scripts", "seed-data", "sintetico.sql"), ct);
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion) { CommandTimeout = 60 };
        await comando.ExecuteNonQueryAsync(ct);
    }

    private static string BuscarRaizRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null)
        {
            if (File.Exists(Path.Combine(directorio.FullName, "CLAUDE.md"))) return directorio.FullName;
            directorio = directorio.Parent;
        }
        throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
    }
}
