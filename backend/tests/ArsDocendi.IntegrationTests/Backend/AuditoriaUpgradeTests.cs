using ArsDocendi.Host.Administracion;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace ArsDocendi.IntegrationTests.Backend;

public sealed class AuditoriaUpgradeTests : IAsyncLifetime
{
    private const string Incremental = "20261013165425_AuditoriaPrecisaIdentity";
    private readonly PostgreSqlContainer contenedor = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("auditoria_upgrade").WithUsername("postgres").WithPassword("postgres").Build();
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    public ValueTask InitializeAsync() => new(contenedor.StartAsync());
    public ValueTask DisposeAsync() => new(contenedor.DisposeAsync().AsTask());
    private async Task Sql(string sql)
    {
        await using var c = new NpgsqlConnection(contenedor.GetConnectionString());
        await c.OpenAsync(Ct);
        await using var cmd = new NpgsqlCommand(sql, c);
        await cmd.ExecuteNonQueryAsync(Ct);
    }
    private async Task<T> Escalar<T>(string sql)
    {
        await using var c = new NpgsqlConnection(contenedor.GetConnectionString());
        await c.OpenAsync(Ct);
        await using var cmd = new NpgsqlCommand(sql, c);
        return (T)(await cmd.ExecuteScalarAsync(Ct))!;
    }
    private static string Raiz([System.Runtime.CompilerServices.CallerFilePath] string archivo = "")
    {
        for (var dir = new DirectoryInfo(Path.GetDirectoryName(archivo)!); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "database", "README.md"))) return dir.FullName;
        throw new InvalidOperationException("No se encontró la fixture sintética en el checkout de la prueba.");
    }
    [Fact]
    public async Task Upgrade_y_no_op_conservan_bytes_legacy_y_historial_y_minimizan_solo_eventos_nuevos()
    {
        var cadena = contenedor.GetConnectionString();
        await using (var db = PostgresFixture.CrearIdentity(cadena))
            await db.GetService<IMigrator>().MigrateAsync("20261005000500_CuentaMicrosoftUsuarios", Ct);
        await using (var db = PostgresFixture.CrearAlmacenamiento(cadena))
            await db.GetService<IMigrator>().MigrateAsync("20261005000100_BaselineStorage", Ct);
        await using (var db = PostgresFixture.CrearDesignaciones(cadena))
            await db.GetService<IMigrator>().MigrateAsync("20261005000200_BaselineDesignaciones", Ct);
        await using (var db = PostgresFixture.CrearPortal(cadena))
            await db.GetService<IMigrator>().MigrateAsync("20261005000300_BaselinePortal", Ct);
        await using (var db = PostgresFixture.CrearTareas(cadena))
            await db.GetService<IMigrator>().MigrateAsync("20260926000000_CatalogosTareasId", Ct);
        await Sql(await File.ReadAllTextAsync(Path.Combine(Raiz(), "infra/scripts/seed-data/sintetico.sql"), Ct));
        // Eventos de las dos relaciones conocidas aún tienen su captura histórica.
        await Sql("""
            INSERT INTO identity.personas(id,documento,nombre,apellido) VALUES
              ('44444444-4444-4444-8444-444444444444','upgrade-audit-fixture','Sintético','Upgrade');
            INSERT INTO portal.perfiles(id,persona_id) VALUES
              ('11111111-1111-4111-8111-111111111111','44444444-4444-4444-8444-444444444444');
            INSERT INTO portal.habilidades(id,termino,termino_norm) VALUES
              ('22222222-2222-4222-8222-222222222222','Término sintético audit','TERMINO SINTETICO AUDIT');
            INSERT INTO portal.docente_habilidades(perfil_id,habilidad_id,tipo) VALUES
              ('11111111-1111-4111-8111-111111111111','22222222-2222-4222-8222-222222222222','habilidad'),
              ('11111111-1111-4111-8111-111111111111','22222222-2222-4222-8222-222222222222','interes');
            """);
        var maxId = await Escalar<long>("SELECT max(id) FROM audit.change_log");
        const string huella = "SELECT md5(jsonb_agg(to_jsonb(c)-'row_key'-'row_pk_format' ORDER BY id)::text) FROM audit.change_log c";
        var antes = await Escalar<string>(huella);
        const string historial = "SELECT md5(jsonb_agg(to_jsonb(h) ORDER BY id)::text) FROM designaciones.pedido_historial h";
        var tramites = await Escalar<string>(historial);
        Assert.True(await Escalar<bool>("SELECT EXISTS(SELECT 1 FROM audit.change_log WHERE new_row ? 'documento')"));
        await using (var db = PostgresFixture.CrearIdentity(cadena))
        {
            await db.GetService<IMigrator>().MigrateAsync(Incremental, Ct);
            await db.GetService<IMigrator>().MigrateAsync(Incremental, Ct);
        }
        Assert.Equal(antes, await Escalar<string>(huella));
        Assert.Equal(tramites, await Escalar<string>(historial));
        Assert.True(await Escalar<bool>("SELECT bool_and(row_pk_format='legacy' AND row_key IS NULL) FROM audit.change_log"));
        await Sql("""
            UPDATE identity.personas SET telefono='telefono-nuevo-sintetico'
              WHERE id='d0000000-0000-4000-8000-000000000001';
            UPDATE designaciones.designaciones SET horas=horas+1,horas_investigacion=COALESCE(horas_investigacion,0)+1,
              horas_externas=COALESCE(horas_externas,0)+1,
              origen_pedido_id=(SELECT id FROM designaciones.pedidos ORDER BY id LIMIT 1)
              WHERE id=(SELECT id FROM designaciones.designaciones ORDER BY id LIMIT 1);
            DELETE FROM portal.docente_habilidades WHERE perfil_id='11111111-1111-4111-8111-111111111111' AND tipo='interes';
            """);
        Assert.False(await Escalar<bool>($"SELECT EXISTS(SELECT 1 FROM audit.change_log WHERE id>{maxId} AND (old_row ? 'telefono' OR new_row ? 'telefono'))"));
        Assert.True(await Escalar<bool>($"""
            SELECT old_row ?& ARRAY['cargo_id','dedicacion_id','horas','horas_investigacion','horas_externas','vigente_desde','origen_pedido_id']
              AND new_row ?& ARRAY['cargo_id','dedicacion_id','horas','horas_investigacion','horas_externas','vigente_desde','origen_pedido_id']
              AND old_row->'horas'<>new_row->'horas' AND old_row->'origen_pedido_id' IS DISTINCT FROM new_row->'origen_pedido_id'
            FROM audit.change_log WHERE id>{maxId} AND table_name='designaciones'
            """));
        Assert.Equal("composite-v1", await Escalar<string>($"SELECT row_pk_format FROM audit.change_log WHERE id>{maxId} AND table_name='docente_habilidades'"));
        Assert.Equal(antes, await Escalar<string>(huella + $" WHERE id<={maxId}"));
        Assert.Equal(tramites, await Escalar<string>(historial));
        await using var consulta = PostgresFixture.CrearIdentity(cadena);
        var pagina = await ServicioAuditoriaDePrueba.Crear(new RepositorioAuditoria(consulta)).ListarAsync(new ConsultaAuditoriaDto(
            Modulo: "identity", Tabla: "personas", TamanoPagina: 100), Ct);
        Assert.DoesNotContain("telefono-nuevo-sintetico", System.Text.Json.JsonSerializer.Serialize(pagina));
    }
}
