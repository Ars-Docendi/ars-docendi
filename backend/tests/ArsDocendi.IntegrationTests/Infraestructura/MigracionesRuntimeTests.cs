using ArsDocendi.Migraciones;
using ArsDocendi.Shared;
using ArsDocendi.Shared.Persistencia;
using ArsDocendi.Storage;
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
        Assert.Equal(4, estados.Sum(x => x.Pendientes.Count));
        var archivos = await PreviewMigraciones.GenerarAsync(migradores, estados, "runtime-test", ct);
        Assert.Equal(4, archivos.Keys.Count(x => x.EndsWith(".sql", StringComparison.Ordinal)));
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
        Assert.Equal(4L, await Escalar("SELECT count(*) FROM pg_tables WHERE tablename='__EFMigrationsHistory'"));
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
        await Ejecutar("INSERT INTO identity.\"__EFMigrationsHistory\" VALUES ('historia-alpha-retirada', '10.0.11')");
        await Assert.ThrowsAsync<InvalidOperationException>(() => migradores[0].MigrarAsync(ct));
        Assert.Equal(2L, await Escalar("SELECT count(*) FROM identity.\"__EFMigrationsHistory\""));
        Assert.Equal(7L, await Escalar("SELECT count(*) FROM identity.roles"));
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
