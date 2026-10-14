using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Identity;

public sealed class ConsultasSelectivasIdentityTests(PostgresFixture postgres) : ClasePostgresAislada(postgres, "identity_selectiva")
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task Filtra_palabras_tildes_y_jerarquia_antes_de_limitar_con_historia_inactiva()
    {
        await using var conexion = await AbrirConexionAsync();
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO identity.users(upn, display_name)
            SELECT 'usuario' || n || '@example.test', 'A Usuario ' || n FROM generate_series(1,70) n;
            INSERT INTO identity.user_roles(user_id,role_id)
            SELECT id,'a1000000-0000-4000-8000-000000000007'::uuid FROM identity.users;
            INSERT INTO identity.personas(id,documento,legajo,nombre,apellido) VALUES
              ('11111111-1111-4111-8111-111111111111','DOC001','LEG001','José','Álvarez');
            INSERT INTO identity.users(id,upn,display_name,persona_id) VALUES
              ('22222222-2222-4222-8222-222222222222','jose@example.test','Z José Álvarez','11111111-1111-4111-8111-111111111111');
            INSERT INTO identity.user_roles(user_id,role_id) VALUES
              ('22222222-2222-4222-8222-222222222222','a1000000-0000-4000-8000-000000000004');
            """, conexion);
        await cmd.ExecuteNonQueryAsync(Ct);
        var captura = new CapturaConsultasIdentity();
        await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(Cadena, o => o.MigrationsHistoryTable("__EFMigrationsHistory", "identity"))
            .AddInterceptors(captura).Options);
        IConsultasIdentity consultas = new ConsultasIdentity(db);
        var id = Guid.Parse("22222222-2222-4222-8222-222222222222");
        Assert.Equal(id, Assert.Single(await consultas.BuscarCandidatosAsync(new("ALVAREZ jose", 1), Ct)).Id);
        var jerarquia = await consultas.BuscarCandidatosAsync(new(null, 1, ["secretaria"], ["sys_admin"]), Ct);
        Assert.Equal(id, Assert.Single(jerarquia).Id);
        Assert.Equal(2, captura.Comandos.Count);
        Assert.All(captura.Comandos, sql => Assert.Contains("LIMIT", sql));
        cmd.CommandText = """
            INSERT INTO identity.users(upn,display_name)
            SELECT 'otro' || n || '@example.test', 'A Ajeno ' || n FROM generate_series(1,700) n;
            INSERT INTO identity.user_roles(user_id,role_id)
            SELECT id,'a1000000-0000-4000-8000-000000000007'::uuid FROM identity.users WHERE upn LIKE 'otro%';
            """;
        await cmd.ExecuteNonQueryAsync(Ct);
        captura.Comandos.Clear();
        Assert.Equal(id, Assert.Single(await consultas.BuscarCandidatosAsync(new("ALVAREZ jose", 1), Ct)).Id);
        Assert.Single(captura.Comandos);
        cmd.CommandText = $"INSERT INTO identity.user_roles(user_id,role_id) VALUES ('{id}','a1000000-0000-4000-8000-000000000007')";
        await cmd.ExecuteNonQueryAsync(Ct);
        Assert.Empty(await consultas.BuscarCandidatosAsync(new("jose", 50, ["secretaria"], ["sys_admin"]), Ct));
        Assert.Equal(id, Assert.Single(await consultas.BuscarCandidatosAsync(new("jose", 50, ["secretaria"], ["sys_admin"], [id]), Ct)).Id);
        Assert.Empty(await consultas.BuscarCandidatosAsync(new(null, 50, []), Ct));
        cmd.CommandText = $"UPDATE identity.users SET is_active=false WHERE id='{id}'";
        await cmd.ExecuteNonQueryAsync(Ct);
        Assert.Empty(await consultas.BuscarCandidatosAsync(new("jose"), Ct));
        Assert.False((await consultas.ObtenerUsuarioAsync(id, Ct))!.Activo);
        captura.Comandos.Clear();
        var historico = Assert.Single(await consultas.ObtenerUsuariosAsync([id, Guid.NewGuid()], Ct));
        Assert.Equal("José", historico.Persona!.Nombre);
        Assert.Single(await consultas.ObtenerPersonasAsync([historico.PersonaId!.Value], Ct));
        Assert.Equal(2, captura.Comandos.Count);
        Assert.All(captura.Comandos, sql => Assert.Contains("WHERE", sql));
        Assert.Empty(db.ChangeTracker.Entries());
    }
}
