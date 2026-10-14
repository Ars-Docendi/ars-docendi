using ArsDocendi.Shared.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ArsDocendi.IntegrationTests.Identity;

public sealed class UpgradeRolesIdentityTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer contenedor = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("upgrade_roles").WithUsername("postgres").WithPassword("postgres").Build();
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    public ValueTask InitializeAsync() => new(contenedor.StartAsync());
    public ValueTask DisposeAsync() => new(contenedor.DisposeAsync().AsTask());
    private IdentityDbContext CrearDb() => new(new DbContextOptionsBuilder<IdentityDbContext>()
        .UseNpgsql(contenedor.GetConnectionString(), o => o.MigrationsHistoryTable("__EFMigrationsHistory", "identity")).Options);
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Upgrade_nombre_conserva_identidades_o_aborta_sin_fusion(bool duplicado)
    {
        await using var db = CrearDb();
        await db.GetService<IMigrator>().MigrateAsync("20261013170003_ProtegerAmbitoRolesIdentity", Ct);
        await using var conexion = new NpgsqlConnection(contenedor.GetConnectionString());
        await conexion.OpenAsync(Ct);
        await using var comando = new NpgsqlCommand("""
            INSERT INTO identity.roles(id,code,name,scope) VALUES
              ('11111111-1111-4111-8111-111111111111','sintetico',' Nombre Libre ','global');
            INSERT INTO identity.rol_permisos(rol_id,permiso_id)
              SELECT '11111111-1111-4111-8111-111111111111'::uuid,id FROM identity.permisos ORDER BY code LIMIT 1;
            """, conexion);
        await comando.ExecuteNonQueryAsync(Ct);
        if (duplicado)
        {
            comando.CommandText = "INSERT INTO identity.roles(code,name,scope) VALUES ('otro','nombre libre','global')";
            await comando.ExecuteNonQueryAsync(Ct);
        }
        var antes = await Huella(conexion);
        if (duplicado)
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>()
                .MigrateAsync("20261013170527_UnicidadNombreRolActivoIdentity", Ct));
            Assert.Equal("roles_nombre_activo_normalizado_key", error.ConstraintName);
            Assert.DoesNotContain("20261013170527_UnicidadNombreRolActivoIdentity", await db.Database.GetAppliedMigrationsAsync(Ct));
        }
        else
        {
            await db.GetService<IMigrator>().MigrateAsync("20261013170527_UnicidadNombreRolActivoIdentity", Ct);
            Assert.Contains("20261013170527_UnicidadNombreRolActivoIdentity", await db.Database.GetAppliedMigrationsAsync(Ct));
        }
        Assert.Equal(antes, await Huella(conexion));
    }
    private async Task<string> Huella(NpgsqlConnection conexion)
    {
        await using var cmd = new NpgsqlCommand("""
            SELECT jsonb_build_object(
                'roles',(SELECT jsonb_agg(to_jsonb(r) ORDER BY id) FROM identity.roles r),
                'permisos',(SELECT jsonb_agg(to_jsonb(p) ORDER BY rol_id,permiso_id) FROM identity.rol_permisos p),
                'audit',(SELECT jsonb_agg(to_jsonb(a) ORDER BY id) FROM audit.change_log a))::text
            """, conexion);
        return (string)(await cmd.ExecuteScalarAsync(Ct))!;
    }
}
