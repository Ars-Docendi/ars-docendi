using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Shared.Identity;
using ArsDocendi.Shared.Identity.Administracion;
using ArsDocendi.Shared.Identity.Ingreso;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ArsDocendi.IntegrationTests.Identity;

// PostgreSQL propio, sin binds ni conexiones a ambientes compartidos.
public sealed class IntegridadIdentityTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer contenedor = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("identity_integridad").WithUsername("postgres").WithPassword("postgres").Build();
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    private IdentityDbContext CrearDb() => new(new DbContextOptionsBuilder<IdentityDbContext>()
        .UseNpgsql(contenedor.GetConnectionString(), o => o.MigrationsHistoryTable("__EFMigrationsHistory", "identity"))
        .Options);
    public ValueTask InitializeAsync() => new(contenedor.StartAsync());
    public ValueTask DisposeAsync() => new(contenedor.DisposeAsync().AsTask());
    private async Task Migrar(string? destino = null)
    {
        await using var db = CrearDb();
        // Esta reproducción aísla el upgrade UPN; migraciones posteriores de auditoría
        // agregan columnas y cambian la forma JSON, no los eventos legados.
        await db.GetService<IMigrator>().MigrateAsync(destino ?? "20261013155142_UnicidadUpnNormalizadoIdentity", Ct);
    }
    private async Task Sql(string sql)
    {
        await using var conexion = new NpgsqlConnection(contenedor.GetConnectionString());
        await conexion.OpenAsync(Ct);
        await using var comando = new NpgsqlCommand(sql, conexion);
        await comando.ExecuteNonQueryAsync(Ct);
    }
    [Fact]
    public async Task Ingreso_y_administracion_encuentran_upn_con_espacios_en_la_fila()
    {
        await Migrar();
        await Sql("INSERT INTO identity.users(upn,display_name) VALUES (' Cuenta@Example.test ', 'Sintético');");
        await using var db = CrearDb();
        Assert.NotNull(await new RepositorioIngreso(db).BuscarPorUpnAsync("cuenta@example.test", Ct));
        Assert.True(await new RepositorioUsuarios(db).ExisteUpnAsync("CUENTA@example.test ", null, Ct));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Upgrade_conserva_cuentas_y_bloquea_duplicados_sin_fusion(bool duplicado)
    {
        await Migrar("20261005000500_CuentaMicrosoftUsuarios");
        await Sql("""
            INSERT INTO identity.personas(id,documento,nombre,apellido) VALUES
              ('11111111-1111-4111-8111-111111111111','sintetico','Nombre','Apellido');
            INSERT INTO identity.users(id,upn,display_name,persona_id,azure_oid,azure_tid) VALUES
              ('22222222-2222-4222-8222-222222222222',' Cuenta@Example.test ','Sintético',
               '11111111-1111-4111-8111-111111111111','33333333-3333-4333-8333-333333333333',
               '44444444-4444-4444-8444-444444444444');
            INSERT INTO identity.user_roles(user_id,role_id) VALUES
              ('22222222-2222-4222-8222-222222222222','a1000000-0000-4000-8000-000000000004');
            """);
        if (duplicado) await Sql("INSERT INTO identity.users(upn,display_name) VALUES ('cuenta@example.test','Otro');");
        var antes = await Huella();
        if (duplicado)
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => Migrar());
            Assert.Equal("users_upn_normalizado_key", error.ConstraintName);
            Assert.Equal(antes, await Huella());
        }
        else
        {
            await Migrar();
            Assert.Equal(antes, await Huella());
            await using var db = CrearDb();
            Assert.Contains("20261013155142_UnicidadUpnNormalizadoIdentity", await db.Database.GetAppliedMigrationsAsync(Ct));
        }
    }
    private async Task<string> Huella()
    {
        await using var conexion = new NpgsqlConnection(contenedor.GetConnectionString());
        await conexion.OpenAsync(Ct);
        await using var comando = new NpgsqlCommand("""
            SELECT jsonb_build_object('users',(SELECT jsonb_agg(to_jsonb(u) ORDER BY id) FROM identity.users u),
              'personas',(SELECT jsonb_agg(to_jsonb(p) ORDER BY id) FROM identity.personas p),
              'roles',(SELECT jsonb_agg(to_jsonb(r) ORDER BY id) FROM identity.user_roles r),
              'audit',(SELECT jsonb_agg(to_jsonb(a) ORDER BY id) FROM audit.change_log a))::text
            """, conexion);
        return (string)(await comando.ExecuteScalarAsync(Ct))!;
    }
    [Fact]
    public async Task Sql_rechaza_upn_equivalente_incluso_inactivo()
    {
        await Migrar();
        await Sql("INSERT INTO identity.users(upn,display_name) VALUES (' Cuenta@Example.test ', 'Sintético');");
        var error = await Assert.ThrowsAsync<PostgresException>(() => Sql(
            "INSERT INTO identity.users(upn,display_name,is_active) VALUES ('cuenta@example.test','Otro',false);"));
        Assert.Equal("23505", error.SqlState);
        Assert.Equal("users_upn_normalizado_key", error.ConstraintName);
    }
}
