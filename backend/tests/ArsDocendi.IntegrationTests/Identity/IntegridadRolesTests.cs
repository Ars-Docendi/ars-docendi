using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Shared.Identity.Administracion;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Identity;

public sealed class IntegridadRolesTests(PostgresFixture postgres) : ClasePostgresAislada(postgres, "roles_integridad")
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    private async Task Sql(string sql)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        await comando.ExecuteNonQueryAsync(Ct);
    }
    private async Task<Guid> CrearRol(string nombre = "Rol sintético")
    {
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        var rol = await new ServicioRoles(db, new RepositorioRoles(db)).CrearAsync(new(nombre, null, "global"), Ct);
        return rol.Id;
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Grant_y_cambio_scope_se_serializan_en_ambos_ordenes(bool ganaGrant)
    {
        var id = await CrearRol();
        var usuario = Guid.NewGuid();
        await Sql($"INSERT INTO identity.users(id,upn,display_name) VALUES ('{usuario}','carrera@example.test','Sintético')");
        await using var a = await AbrirConexionAsync();
        await using var b = await AbrirConexionAsync();
        await using var tx = await a.BeginTransactionAsync(Ct);
        var grant = $"INSERT INTO identity.user_roles(user_id,role_id) VALUES ('{usuario}','{id}')";
        var scope = $"UPDATE identity.roles SET scope='materia' WHERE id='{id}'";
        await using (var ganador = new NpgsqlCommand(ganaGrant ? grant : scope, a, tx))
            await ganador.ExecuteNonQueryAsync(Ct);
        await using var perdedor = new NpgsqlCommand(ganaGrant ? scope : grant, b);
        var pendiente = perdedor.ExecuteNonQueryAsync(Ct);
        await EsperarBloqueoAsync(b.ProcessID);
        Assert.False(pendiente.IsCompleted);
        await tx.CommitAsync(Ct);
        var error = await Assert.ThrowsAsync<PostgresException>(async () => await pendiente);
        Assert.Equal("23514", error.SqlState);
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        Assert.Equal(ganaGrant ? "global" : "materia", (await db.Roles.SingleAsync(r => r.Id == id, Ct)).Ambito);
        Assert.Equal(ganaGrant ? 1 : 0, await db.UsuarioRoles.CountAsync(r => r.RolId == id, Ct));
    }
    [Fact]
    public async Task Renombres_diferentes_concurrentes_traducen_solo_unique_de_nombre()
    {
        var primero = await CrearRol("Nombre inicial uno");
        var segundo = await CrearRol("Nombre inicial dos");
        await using var a = await AbrirConexionAsync();
        await using var tx = await a.BeginTransactionAsync(Ct);
        await using (var rename = new NpgsqlCommand($"UPDATE identity.roles SET name='Destino' WHERE id='{primero}'", a, tx))
            await rename.ExecuteNonQueryAsync(Ct);
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        var repo = new RepositorioRoles(db);
        var rol = (await repo.ObtenerAsync(segundo, true, Ct))!;
        var codigo = rol.Codigo;
        Assert.False(await repo.ExisteNombreAsync(" DESTINO ", segundo, Ct));
        rol.Nombre = " DESTINO ";
        await db.Database.OpenConnectionAsync(Ct);
        var pendiente = repo.GuardarAsync(Ct);
        await EsperarBloqueoAsync(((NpgsqlConnection)db.Database.GetDbConnection()).ProcessID);
        await tx.CommitAsync(Ct);
        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() => pendiente);
        Assert.Equal("identity-role-name-conflict", error.Codigo);
        await using var comprobar = PostgresFixture.CrearIdentity(Cadena);
        var conservado = await comprobar.Roles.SingleAsync(r => r.Id == segundo, Ct);
        Assert.Equal(codigo, conservado.Codigo);
        Assert.Equal("Nombre inicial dos", conservado.Nombre);
    }
    [Fact]
    public async Task Revocacion_autentica_de_legado_incoherente_no_permite_regrant()
    {
        var id = await CrearRol();
        await Sql($"""
            INSERT INTO identity.users(id,upn,display_name) VALUES ('11111111-1111-4111-8111-111111111111','legado@example.test','Sintético');
            INSERT INTO identity.user_roles(user_id,role_id) VALUES ('11111111-1111-4111-8111-111111111111','{id}');
            ALTER TABLE identity.roles DISABLE TRIGGER trg_roles_proteger_ambito;
            UPDATE identity.roles SET scope='materia' WHERE id='{id}';
            ALTER TABLE identity.roles ENABLE TRIGGER trg_roles_proteger_ambito;
            UPDATE identity.user_roles SET deleted_at=now() WHERE role_id='{id}';
            """);
        var error = await Assert.ThrowsAsync<PostgresException>(() => Sql($"UPDATE identity.user_roles SET deleted_at=NULL WHERE role_id='{id}'"));
        Assert.Equal("user_roles_scope_coherente", error.ConstraintName);
    }
    private async Task EsperarBloqueoAsync(int pid)
    {
        await using var observador = await AbrirConexionAsync();
        for (var intento = 0; intento < 400; intento++)
        {
            await using var cmd = new NpgsqlCommand("SELECT cardinality(pg_blocking_pids(@pid)) > 0", observador);
            cmd.Parameters.AddWithValue("pid", pid);
            if ((bool)(await cmd.ExecuteScalarAsync(Ct))!) return;
            await Task.Delay(25, Ct);
        }
        Assert.Fail("La segunda sesión no llegó al bloqueo esperado.");
    }
    [Fact]
    public async Task Nombre_activo_normalizado_es_unico_y_desactivado_no_reserva_nombre()
    {
        var id = await CrearRol();
        var otro = await CrearRol("Otro rol sintético");
        var error = await Assert.ThrowsAsync<PostgresException>(() => Sql($"UPDATE identity.roles SET name=' ROL SINTÉTICO ' WHERE id='{otro}'"));
        Assert.Equal("23505", error.SqlState);
        Assert.Equal("roles_nombre_activo_normalizado_key", error.ConstraintName);
        await Sql($"UPDATE identity.roles SET is_active=false WHERE id='{id}'; UPDATE identity.roles SET name=' ROL SINTÉTICO ' WHERE id='{otro}'");
    }
    [Fact]
    public async Task Sql_scope_con_asignacion_viva_rechazado_y_revocada_conservada()
    {
        var id = await CrearRol();
        await Sql($"INSERT INTO identity.users(id,upn,display_name) VALUES ('11111111-1111-4111-8111-111111111111','rol@example.test','Sintético'); INSERT INTO identity.user_roles(user_id,role_id) VALUES ('11111111-1111-4111-8111-111111111111','{id}');");
        var error = await Assert.ThrowsAsync<PostgresException>(() => Sql($"UPDATE identity.roles SET scope='materia' WHERE id='{id}'"));
        Assert.Equal("roles_scope_asignaciones_vivas", error.ConstraintName);
        await Sql($"UPDATE identity.user_roles SET deleted_at=now() WHERE role_id='{id}'; UPDATE identity.roles SET scope='materia' WHERE id='{id}';");
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        Assert.Equal("materia", (await db.Roles.SingleAsync(r => r.Id == id, Ct)).Ambito);
        var historica = await db.UsuarioRoles.IgnoreQueryFilters().SingleAsync(r => r.RolId == id, Ct);
        Assert.NotNull(historica.EliminadoEn);
        Assert.Null(historica.MateriaId);
    }
    [Fact]
    public async Task Scope_con_asignacion_viva_es_conflicto_sin_mutacion()
    {
        var id = await CrearRol();
        await Sql($"INSERT INTO identity.users(id,upn,display_name) VALUES ('11111111-1111-4111-8111-111111111111','rol@example.test','Sintético'); INSERT INTO identity.user_roles(user_id,role_id) VALUES ('11111111-1111-4111-8111-111111111111','{id}');");
        await using var db = PostgresFixture.CrearIdentity(Cadena);
        var servicio = new ServicioRoles(db, new RepositorioRoles(db));
        var rol = await servicio.ObtenerAsync(id, Ct);
        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() => servicio.EditarAsync(id, new(rol.Nombre, null, "materia", rol.Version), Ct));
        Assert.Equal("identity-role-scope-conflict", error.Codigo);
        Assert.Equal("global", (await servicio.ObtenerAsync(id, Ct)).Ambito);
    }
}
