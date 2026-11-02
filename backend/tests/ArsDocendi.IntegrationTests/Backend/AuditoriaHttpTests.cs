using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ArsDocendi.Host.Administracion;
using ArsDocendi.Host.Desarrollo;
using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Xunit;

namespace ArsDocendi.IntegrationTests.Backend;

public sealed class AuditoriaHttpTests(PostgresFixture postgres) : ClasePostgresAislada(postgres, "auditoria_http")
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    private static string Raiz([CallerFilePath] string archivo = "")
    {
        for (var dir = new DirectoryInfo(Path.GetDirectoryName(archivo)!); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) return dir.FullName;
        throw new DirectoryNotFoundException("No se encontró el checkout de la prueba.");
    }
    private sealed class Fabrica(string cadena) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseContentRoot(Path.Combine(Raiz(), "backend/src/ArsDocendi.Host"));
            builder.UseSetting("ConnectionStrings:ArsDocendi", cadena);
            builder.UseSetting($"{AutenticacionDesarrolloOptions.Seccion}:Enabled", bool.TrueString);
        }
    }
    [Fact]
    public async Task Endpoint_conserva_permisos_limites_y_filtro_compuesto_sin_escribir_al_leer()
    {
        await using var conexion = await AbrirConexionAsync();
        await using (var seed = new NpgsqlCommand(await File.ReadAllTextAsync(
            Path.Combine(Raiz(), "infra/scripts/seed-data/sintetico.sql"), Ct), conexion))
            await seed.ExecuteNonQueryAsync(Ct);
        await using (var insertar = new NpgsqlCommand("""
            INSERT INTO identity.roles(id,code,name,scope) VALUES
              ('11111111-1111-4111-8111-111111111111','audit_http_fixture','HTTP sintético','global');
            INSERT INTO identity.rol_permisos(rol_id,permiso_id)
              SELECT '11111111-1111-4111-8111-111111111111',id FROM identity.permisos ORDER BY id LIMIT 2;
            """, conexion))
            await insertar.ExecuteNonQueryAsync(Ct);
        await using var total = new NpgsqlCommand("SELECT count(*) FROM audit.change_log", conexion);
        var antes = (long)(await total.ExecuteScalarAsync(Ct))!;
        using var host = new Fabrica(Cadena);
        using var anonimo = host.CreateClient();
        using var denegado = await anonimo.GetAsync("/api/administracion/auditoria", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, denegado.StatusCode);
        using var docente = host.CreateClient();
        docente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, "a0000000-0000-4000-8000-000000000001");
        docente.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, "docente");
        using var sinPermiso = await docente.GetAsync("/api/administracion/auditoria", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, sinPermiso.StatusCode);
        using var admin = host.CreateClient();
        admin.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderUsuario, "a0000000-0000-4000-8000-000000000007");
        admin.DefaultRequestHeaders.Add(AutenticacionDesarrolloHandler.HeaderRol, "sys_admin");
        foreach (var filtro in new[] { "tamanoPagina=101", "pagina=0", "rowPk=%7B" })
        {
            using var invalido = await admin.GetAsync("/api/administracion/auditoria?" + filtro, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);
        }
        await using var clave = new NpgsqlCommand("SELECT row_pk FROM audit.change_log WHERE table_name='rol_permisos' AND row_pk_format='composite-v1' ORDER BY id DESC LIMIT 1", conexion);
        var pk = (string)(await clave.ExecuteScalarAsync(Ct))!;
        using var respuesta = await admin.GetAsync("/api/administracion/auditoria?schema=identity&tabla=rol_permisos&rowPk=" + Uri.EscapeDataString(pk), Ct);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var pagina = await respuesta.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(1, pagina.GetProperty("total").GetInt64());
        var evento = Assert.Single(pagina.GetProperty("elementos").EnumerateArray());
        Assert.Equal(pk, evento.GetProperty("rowPk").GetString());
        Assert.False(evento.TryGetProperty("old_row", out _));
        Assert.False(evento.TryGetProperty("new_row", out _));
        Assert.Equal(antes, (long)(await total.ExecuteScalarAsync(Ct))!);
    }
    private sealed class Demora : DbCommandInterceptor
    {
        public int Timeout { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Timeout = command.CommandTimeout;
            command.CommandText = "SELECT pg_sleep(8); " + command.CommandText;
            return ValueTask.FromResult(result);
        }
    }
    [Fact]
    public async Task Timeout_se_aplica_realmente_a_una_consulta_lenta()
    {
        var demora = new Demora();
        await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(Cadena).AddInterceptors(demora).Options);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new RepositorioAuditoria(db).ListarAsync(new ConsultaAuditoriaDto(), Ct));
        Assert.IsType<NpgsqlException>(error.InnerException);
        Assert.IsType<TimeoutException>(error.GetBaseException());
        Assert.Equal(5, demora.Timeout);
    }
}
