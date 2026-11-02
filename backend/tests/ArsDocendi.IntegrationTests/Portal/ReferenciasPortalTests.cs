using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Storage.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Portal;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Portal;

public sealed class ReferenciasPortalTests(PostgresFixture postgres) : ClasePostgresAislada(postgres, "portal_referencias")
{
    [Fact]
    public async Task Consultas_y_participante_local_comparten_transaccion_y_rollback_real()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = PostgresFixture.CrearPortal(Cadena);
        var services = new ServiceCollection(); services.AddPortalModule(new ConfigurationBuilder().Build()); services.AddSingleton(db);
        Assert.Contains(services, d => d.ServiceType == typeof(IConsultasReferenciasArchivos));
        Assert.Contains(services, d => d.ServiceType == typeof(IParticipanteTransaccionArchivos));
        await using var provider = services.BuildServiceProvider();
        var consultas = Assert.Single(provider.GetServices<IConsultasReferenciasArchivos>());
        var participante = Assert.Single(provider.GetServices<IParticipanteTransaccionArchivos>());
        var archivo = Guid.NewGuid(); var perfil = Guid.NewGuid();
        await using var conexion = await AbrirConexionAsync(); await using var tx = await conexion.BeginTransactionAsync(ct);
        await participante.EnlistarAsync(conexion, tx, ct);
        db.Perfiles.Add(new() { Id = perfil, PersonaId = Guid.NewGuid() });
        db.Cvs.Add(new() { Id = Guid.NewGuid(), PerfilId = perfil, Nombre = "cv.pdf", ArchivoId = archivo });
        await db.SaveChangesAsync(ct);
        Assert.True(await consultas.TieneReferenciasAsync(archivo, ct));
        await tx.RollbackAsync(ct); await participante.DesenlistarAsync(ct);
        Assert.False(await consultas.TieneReferenciasAsync(archivo, ct));
        await using var verificar = new NpgsqlCommand("SELECT count(*) FROM portal.perfiles WHERE id=@id", conexion);
        verificar.Parameters.AddWithValue("id", perfil);
        Assert.Equal(0L, (long)(await verificar.ExecuteScalarAsync(ct))!);
    }
}
