using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArsDocendi.IntegrationTests.Identity;

/// <summary>Efficient reads that resolve the recipients of a notification.</summary>
public sealed class DestinatariosIdentityTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "identity_destinatarios")
{
    private static readonly Guid Materia = Guid.Parse("70000000-0000-4000-8000-000000000101");
    private static readonly Guid OtraMateria = Guid.Parse("70000000-0000-4000-8000-000000000102");
    private static readonly Guid Carrera = Guid.Parse("c0000000-0000-4000-8000-000000000201");
    private static readonly Guid Jefe = Guid.Parse("a0000000-0000-4000-8000-000000000002");
    private static readonly Guid Coordinador = Guid.Parse("a0000000-0000-4000-8000-000000000003");
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task PrepararAsync()
    {
        var raiz = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(raiz.FullName, "AGENTS.md"))) raiz = raiz.Parent!;
        var sql = await File.ReadAllTextAsync(
            Path.Combine(raiz.FullName, "infra", "scripts", "seed-data", "sintetico.sql"), Ct);
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new Npgsql.NpgsqlCommand(sql, conexion) { CommandTimeout = 60 };
        await comando.ExecuteNonQueryAsync(Ct);
    }

    private async Task EjecutarAsync(string sql)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new Npgsql.NpgsqlCommand(sql, conexion);
        await comando.ExecuteNonQueryAsync(Ct);
    }

    private IdentityDbContext CrearContexto() => new(new DbContextOptionsBuilder<IdentityDbContext>()
        .UseNpgsql(Cadena, o => o.MigrationsHistoryTable("__EFMigrationsHistory", "identity")).Options);

    [Fact]
    public async Task Devuelve_los_usuarios_activos_con_el_rol_en_cada_tipo_de_ambito()
    {
        await PrepararAsync();
        await using var db = CrearContexto();
        IConsultasIdentity consultas = new ConsultasIdentity(db);

        Assert.Equal(Jefe, Assert.Single(await consultas.ObtenerUsuarioIdsConRolAsync("jefe_catedra", Materia, null, Ct)));
        Assert.Equal(Coordinador, Assert.Single(await consultas.ObtenerUsuarioIdsConRolAsync("coordinador_carrera", null, Carrera, Ct)));
        Assert.Equal(Secretaria, Assert.Single(await consultas.ObtenerUsuarioIdsConRolAsync("secretaria", null, null, Ct)));
        // A scoped assignment is not a global one, and a global assignment is not a scoped one.
        Assert.Empty(await consultas.ObtenerUsuarioIdsConRolAsync("jefe_catedra", null, null, Ct));
        Assert.Empty(await consultas.ObtenerUsuarioIdsConRolAsync("secretaria", Materia, null, Ct));
        Assert.Empty(await consultas.ObtenerUsuarioIdsConRolAsync("jefe_catedra", Guid.NewGuid(), null, Ct));
        // The seeded jefe holds 101, 102 and 103 and nobody else holds the rol on 104.
        Assert.Empty(await consultas.ObtenerUsuarioIdsConRolAsync(
            "jefe_catedra", Guid.Parse("70000000-0000-4000-8000-000000000104"), null, Ct));
        Assert.Contains(Jefe, await consultas.ObtenerUsuarioIdsConRolAsync("jefe_catedra", OtraMateria, null, Ct));
        Assert.Empty(await consultas.ObtenerUsuarioIdsConRolAsync("no_existe", null, null, Ct));
    }

    [Fact]
    public async Task Descarta_usuarios_inactivos_asignaciones_revocadas_y_roles_inactivos()
    {
        await PrepararAsync();
        var otro = Guid.NewGuid();
        await EjecutarAsync($"""
            INSERT INTO identity.users(id, upn, display_name) VALUES ('{otro}', 'otro@example.test', 'Otro');
            INSERT INTO identity.user_roles(user_id, role_id, materia_id)
            VALUES ('{otro}', 'a1000000-0000-4000-8000-000000000002', '{Materia}');
            """);
        await using var db = CrearContexto();
        IConsultasIdentity consultas = new ConsultasIdentity(db);
        Assert.Equal(2, (await consultas.ObtenerUsuarioIdsConRolAsync("jefe_catedra", Materia, null, Ct)).Count);

        await EjecutarAsync($"UPDATE identity.user_roles SET deleted_at = now() WHERE user_id = '{otro}'");
        Assert.Equal(Jefe, Assert.Single(await consultas.ObtenerUsuarioIdsConRolAsync("jefe_catedra", Materia, null, Ct)));

        await EjecutarAsync($"UPDATE identity.users SET is_active = false WHERE id = '{Jefe}'");
        Assert.Empty(await consultas.ObtenerUsuarioIdsConRolAsync("jefe_catedra", Materia, null, Ct));
    }

    [Fact]
    public async Task Resuelve_el_usuario_de_cada_persona_y_omite_a_las_que_no_tienen_cuenta()
    {
        await PrepararAsync();
        await using var db = CrearContexto();
        IConsultasIdentity consultas = new ConsultasIdentity(db);
        var conCuenta = Guid.Parse("d0000000-0000-4000-8000-000000000002");
        var sinCuenta = Guid.Parse("d0000000-0000-4000-8000-000000000010");

        var resultado = await consultas.ObtenerUsuarioIdsDePersonasAsync([conCuenta, sinCuenta, Guid.NewGuid()], Ct);

        Assert.Equal(Jefe, resultado[conCuenta]);
        Assert.Single(resultado);

        // Inactive usuarios are still resolved: activity is the caller's decision.
        await EjecutarAsync($"UPDATE identity.users SET is_active = false WHERE id = '{Jefe}'");
        Assert.Equal(Jefe, (await consultas.ObtenerUsuarioIdsDePersonasAsync([conCuenta], Ct))[conCuenta]);
    }
}
