using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Portal;
using Modules.Portal.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Portal;

public sealed class DiagnosticoPortalTests(PostgresFixture postgres) : ClasePostgresAislada(postgres, "portal_diagnostico")
{
    [Fact]
    public async Task Adaptador_local_emite_solo_referencias_tecnicas_sin_escribir()
    {
        var ct = TestContext.Current.CancellationToken;
        var perfil = Guid.NewGuid(); var persona = Guid.NewGuid(); var cv = Guid.NewGuid(); var archivo = Guid.NewGuid();
        await using var conexion = await AbrirConexionAsync();
        await using var insertar = new NpgsqlCommand("INSERT INTO portal.perfiles(id,persona_id) VALUES (@p,@persona); INSERT INTO portal.cvs(id,perfil_id,nombre,archivo_id) VALUES (@cv,@p,'Dato sensible sintético',@archivo)", conexion);
        insertar.Parameters.AddWithValue("p", perfil); insertar.Parameters.AddWithValue("persona", persona);
        insertar.Parameters.AddWithValue("cv", cv); insertar.Parameters.AddWithValue("archivo", archivo);
        await insertar.ExecuteNonQueryAsync(ct);
        var services = new ServiceCollection();
        services.AddPortalModule(new ConfigurationBuilder().Build());
        Assert.Contains(services, d => d.ServiceType == typeof(IDiagnosticoIntegridadModulo));
        await using var db = PostgresFixture.CrearPortal(Cadena);
        services.AddSingleton(db);
        await using var provider = services.BuildServiceProvider();
        var diagnostico = Assert.Single(provider.GetServices<IDiagnosticoIntegridadModulo>());
        var antes = await HuellaAsync(conexion, ct);
        var filas = await diagnostico.LeerAsync(ct);
        Assert.Contains(new RegistroIntegridad("referencia", "portal-persona", perfil, "persona", persona), filas);
        Assert.Contains(new RegistroIntegridad("referencia", "portal-archivo", cv, "archivo", archivo), filas);
        Assert.Equal(antes, await HuellaAsync(conexion, ct));
    }
    private static async Task<long> HuellaAsync(NpgsqlConnection c, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("SELECT count(*) FROM audit.change_log", c);
        return (long)(await cmd.ExecuteScalarAsync(ct))!;
    }
}
