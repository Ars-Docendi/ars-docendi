using ArsDocendi.Migraciones;
using ArsDocendi.Shared;
using ArsDocendi.Shared.Persistencia;
using ArsDocendi.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Aulas;
using Modules.Designaciones;
using Modules.Portal;
using Modules.Tareas;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ArsDocendi.IntegrationTests.Infraestructura;

public sealed class MigracionesRuntimeTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("runtime_prueba").WithUsername("postgres").WithPassword("synthetic-runtime").Build();
    private ServiceProvider servicios = null!;
    private IServiceScope scope = null!;
    private IMigradorModulo[] migradores = [];

    public async ValueTask InitializeAsync()
    {
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ArsDocendi"] = postgres.GetConnectionString(),
        }).Build();
        servicios = new ServiceCollection().AddLogging().AddArsDocendiShared(config).AddMigracionesIdentity()
            .AddAlmacenamientoModule(config).AddDesignacionesModule(config).AddAulasModule(config)
            .AddPortalModule(config).AddTareasModule(config).BuildServiceProvider();
        scope = servicios.CreateScope();
        migradores = scope.ServiceProvider.GetServices<IMigradorModulo>().ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        scope?.Dispose();
        if (servicios is not null) await servicios.DisposeAsync();
        await postgres.DisposeAsync();
    }

    [Fact]
    public async Task Consulta_y_preview_de_base_vacia_no_crean_schemas_ni_histories()
    {
        var ct = TestContext.Current.CancellationToken;
        var estados = new List<EstadoMigracionesModulo>();
        foreach (var migrador in migradores) estados.Add(await migrador.ConsultarAsync(ct));
        Assert.All(estados, estado => Assert.Empty(estado.Aplicadas));
        Assert.True(estados.Sum(x => x.Disponibles.Count) >= 9);
        Assert.Equal(estados.Sum(x => x.Disponibles.Count), estados.Sum(x => x.Pendientes.Count));
        var archivos = await PreviewMigraciones.GenerarAsync(migradores, estados, "runtime-test", ct);
        Assert.Equal(5, archivos.Keys.Count(x => x.EndsWith(".sql", StringComparison.Ordinal)));
        Assert.Equal(0L, await Escalar("SELECT count(*) FROM pg_namespace WHERE nspname IN ('identity','audit','storage','designaciones','portal','aulas','tareas')"));
    }

    [Fact]
    public async Task Migracion_real_y_noop_conservan_datos_y_auditoria()
    {
        var ct = TestContext.Current.CancellationToken;
        foreach (var migrador in migradores) await migrador.MigrarAsync(ct);
        await Ejecutar("INSERT INTO identity.carreras (code, name) VALUES ('TEST-PERSIST', 'Edición persistente')");
        var auditoria = await Escalar("SELECT count(*) FROM audit.change_log");
        foreach (var migrador in migradores) await migrador.MigrarAsync(ct);
        Assert.Equal(1L, await Escalar("SELECT count(*) FROM identity.carreras WHERE code='TEST-PERSIST' AND name='Edición persistente'"));
        Assert.Equal(auditoria, await Escalar("SELECT count(*) FROM audit.change_log"));
        foreach (var migrador in migradores) Assert.Empty((await migrador.ConsultarAsync(ct)).Pendientes);
        Assert.Equal(5L, await Escalar("SELECT count(*) FROM pg_tables WHERE tablename='__EFMigrationsHistory'"));
    }

    [Fact]
    public async Task Schema_preexistente_sin_historial_se_rechaza_sin_writes()
    {
        await Ejecutar("CREATE SCHEMA identity; CREATE TABLE identity.testigo(id int); INSERT INTO identity.testigo VALUES(1)");
        await Assert.ThrowsAsync<InvalidOperationException>(() => migradores[0].MigrarAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1L, await Escalar("SELECT count(*) FROM identity.testigo"));
        Assert.Equal(0L, await Escalar("SELECT count(*) FROM pg_tables WHERE tablename='__EFMigrationsHistory'"));
    }

    [Fact]
    public async Task Historia_desconocida_se_rechaza_sin_modificar_tablas()
    {
        var ct = TestContext.Current.CancellationToken;
        foreach (var migrador in migradores) await migrador.MigrarAsync(ct);
        var cantidadAnterior = await Escalar("SELECT count(*) FROM identity.\"__EFMigrationsHistory\"");
        await Ejecutar("INSERT INTO identity.\"__EFMigrationsHistory\" VALUES ('historia-alpha-retirada', '10.0.11')");
        await Assert.ThrowsAsync<InvalidOperationException>(() => migradores[0].MigrarAsync(ct));
        Assert.Equal(cantidadAnterior + 1, await Escalar("SELECT count(*) FROM identity.\"__EFMigrationsHistory\""));
        Assert.Equal(7L, await Escalar("SELECT count(*) FROM identity.roles"));
    }

    [Fact]
    public async Task Incrementales_identity_se_aplican_despues_del_baseline_sin_duplicar_permisos()
    {
        var ct = TestContext.Current.CancellationToken;
        await using (var identity = PostgresFixture.CrearIdentity(postgres.GetConnectionString()))
            await identity.GetService<IMigrator>().MigrateAsync("20261005000000_BaselineIdentityYAudit", ct);
        await Ejecutar("""
            INSERT INTO identity.carreras (code, name) VALUES ('TEST-UPGRADE', 'Dato anterior');
            INSERT INTO identity.users (azure_oid, upn, display_name)
            VALUES ('11111111-2222-4333-8444-555555555555', 'upgrade@example.test', 'Usuario anterior');
            """);

        var identityMigrador = Assert.Single(migradores, migrador => migrador.Contexto == "identity");
        var estado = await identityMigrador.ConsultarAsync(ct);
        Assert.Equal(["20261005000000_BaselineIdentityYAudit"], estado.Aplicadas);
        Assert.Equal([
            "20261005000400_PermisosTareas",
            "20261005000500_CuentaMicrosoftUsuarios",
        ], estado.Pendientes.Take(2));
        Assert.Equal(estado.Disponibles.Count - estado.Aplicadas.Count, estado.Pendientes.Count);
        await identityMigrador.MigrarAsync(ct);

        Assert.Equal(1L, await Escalar("SELECT count(*) FROM identity.carreras WHERE code='TEST-UPGRADE'"));
        Assert.Equal(1L, await Escalar("SELECT count(*) FROM identity.users WHERE upn='upgrade@example.test' AND azure_oid IS NULL AND azure_tid IS NULL"));
        // 25 del baseline + incrementales de Tareas, más los seis del asistente
        // (asistente.consultar, asistente.ver_consulta, portal.ver_trayectoria_ajena,
        // asistente.leer_historial_ajeno, asistente.administrar y asistente.revisar_calidad).
        Assert.Equal(31L, await Escalar("SELECT count(*) FROM identity.permisos"));
        Assert.Equal(7L, await Escalar("SELECT count(*) FROM identity.rol_permisos rp JOIN identity.permisos p ON p.id=rp.permiso_id WHERE p.code='tareas.ver'"));
        Assert.Equal(4L, await Escalar("SELECT count(*) FROM identity.rol_permisos rp JOIN identity.permisos p ON p.id=rp.permiso_id WHERE p.code='tareas.gestionar'"));
        Assert.Equal(3L, await Escalar("SELECT count(*) FROM identity.rol_permisos rp JOIN identity.permisos p ON p.id=rp.permiso_id WHERE p.code='proyectos.gestionar'"));
        Assert.Equal((long)estado.Disponibles.Count, await Escalar("SELECT count(*) FROM identity.\"__EFMigrationsHistory\""));
        var auditoria = await Escalar("SELECT count(*) FROM audit.change_log");
        await identityMigrador.MigrarAsync(ct);
        Assert.Empty((await identityMigrador.ConsultarAsync(ct)).Pendientes);
        Assert.Equal(auditoria, await Escalar("SELECT count(*) FROM audit.change_log"));
    }

    private async Task Ejecutar(string sql)
    {
        await using var conn = new NpgsqlConnection(postgres.GetConnectionString());
        await conn.OpenAsync(TestContext.Current.CancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task<long> Escalar(string sql)
    {
        await using var conn = new NpgsqlConnection(postgres.GetConnectionString());
        await conn.OpenAsync(TestContext.Current.CancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        return (long)(await cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}
