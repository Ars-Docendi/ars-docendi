using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Shared.Identity;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Identity;

public sealed class DestinosLogicosIdentityTests(PostgresFixture postgres) : ClasePostgresAislada(postgres, "destinos_identity")
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task Destino_protegido_serializa_desactivacion_y_rechaza_nuevos_inactivos()
    {
        await using var a = await AbrirConexionAsync();
        await using var b = await AbrirConexionAsync();
        var id = Guid.NewGuid();
        await using (var crear = new NpgsqlCommand($"INSERT INTO identity.users(id,upn,display_name) VALUES ('{id}','protegido@example.test','Sintético')", a))
            await crear.ExecuteNonQueryAsync(Ct);
        await using var tx = await a.BeginTransactionAsync(Ct);
        await ProteccionDestinosIdentity.ValidarAsync(a, tx, [id], [], Ct);
        await using var cambio = new NpgsqlCommand($"UPDATE identity.users SET is_active=false WHERE id='{id}'", b);
        var pendiente = cambio.ExecuteNonQueryAsync(Ct);
        await EsperarBloqueoAsync(b.ProcessID);
        Assert.False(pendiente.IsCompleted);
        await tx.CommitAsync(Ct);
        Assert.Equal(1, await pendiente);
        await using var invalido = new NpgsqlCommand("SELECT identity.validar_destinos_logicos(@usuarios, ARRAY[]::uuid[])", b);
        invalido.Parameters.AddWithValue("usuarios", new[] { id });
        var error = await Assert.ThrowsAsync<PostgresException>(() => invalido.ExecuteNonQueryAsync(Ct));
        Assert.Equal("identity_destino_no_elegible", error.ConstraintName);
    }
    [Fact]
    public async Task Desactivacion_que_gana_rechaza_referencia_y_guard_no_confirma_transaccion()
    {
        await using var a = await AbrirConexionAsync();
        await using var b = await AbrirConexionAsync();
        var id = Guid.NewGuid();
        await using (var crear = new NpgsqlCommand($"INSERT INTO identity.users(id,upn,display_name) VALUES ('{id}','primero@example.test','Sintético'); CREATE TABLE public.referencias_sinteticas(usuario_id uuid)", a))
            await crear.ExecuteNonQueryAsync(Ct);
        await using var txA = await a.BeginTransactionAsync(Ct);
        await using (var cambiar = new NpgsqlCommand($"UPDATE identity.users SET is_active=false WHERE id='{id}'", a, txA))
            await cambiar.ExecuteNonQueryAsync(Ct);
        await using var txB = await b.BeginTransactionAsync(Ct);
        var pendiente = ProteccionDestinosIdentity.ValidarAsync(b, txB, [id], [], Ct);
        await EsperarBloqueoAsync(b.ProcessID);
        await txA.CommitAsync(Ct);
        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() => pendiente);
        Assert.Equal("identity-destination-conflict", error.Codigo);
        await txB.RollbackAsync(Ct);
        await using var cantidad = new NpgsqlCommand("SELECT count(*) FROM public.referencias_sinteticas", b);
        Assert.Equal(0L, await cantidad.ExecuteScalarAsync(Ct));
    }
    [Fact]
    public async Task Persona_protegida_espera_mantenimiento_y_guard_conserva_rollback_del_consumidor()
    {
        await using var a = await AbrirConexionAsync();
        await using var b = await AbrirConexionAsync();
        var id = Guid.NewGuid();
        await using (var crear = new NpgsqlCommand($"INSERT INTO identity.personas(id,documento,nombre,apellido) VALUES ('{id}','destino','Nombre','Apellido'); CREATE TABLE public.referencias_sinteticas(persona_id uuid)", a))
            await crear.ExecuteNonQueryAsync(Ct);
        await using var tx = await a.BeginTransactionAsync(Ct);
        await ProteccionDestinosIdentity.ValidarAsync(a, tx, [], [id, id], Ct);
        await using (var referencia = new NpgsqlCommand($"INSERT INTO public.referencias_sinteticas VALUES ('{id}')", a, tx))
            await referencia.ExecuteNonQueryAsync(Ct);
        await using var mantenimiento = new NpgsqlCommand($"UPDATE identity.personas SET nombre='Modificado' WHERE id='{id}'", b);
        var pendiente = mantenimiento.ExecuteNonQueryAsync(Ct);
        await EsperarBloqueoAsync(b.ProcessID);
        await tx.RollbackAsync(Ct);
        Assert.Equal(1, await pendiente);
        await using var cantidad = new NpgsqlCommand("SELECT count(*) FROM public.referencias_sinteticas", b);
        Assert.Equal(0L, await cantidad.ExecuteScalarAsync(Ct));
        await using var inexistente = await b.BeginTransactionAsync(Ct);
        var error = await Assert.ThrowsAsync<ExcepcionAplicacion>(() => ProteccionDestinosIdentity.ValidarAsync(b, inexistente, [], [Guid.NewGuid()], Ct));
        Assert.Equal("identity-destination-conflict", error.Codigo);
    }
    private async Task EsperarBloqueoAsync(int pid)
    {
        await using var observador = await AbrirConexionAsync();
        for (var intento = 0; intento < 200; intento++)
        {
            await using var cmd = new NpgsqlCommand("SELECT cardinality(pg_blocking_pids(@pid)) > 0", observador);
            cmd.Parameters.AddWithValue("pid", pid);
            if ((bool)(await cmd.ExecuteScalarAsync(Ct))!) return;
            await Task.Delay(25, Ct);
        }
        Assert.Fail("La segunda sesión no llegó al bloqueo esperado.");
    }
    [Fact]
    public async Task Borrado_fisico_falla_cerrado_y_desactivacion_conserva_identidad()
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand("""
            INSERT INTO identity.personas(id,documento,nombre,apellido)
            VALUES ('11111111-1111-4111-8111-111111111111','sintetico','Nombre','Apellido');
            INSERT INTO identity.users(id,upn,display_name)
            VALUES ('22222222-2222-4222-8222-222222222222','destino@example.test','Sintético');
            """, conexion);
        await comando.ExecuteNonQueryAsync(Ct);
        foreach (var tabla in new[] { "users", "personas" })
        {
            comando.CommandText = $"DELETE FROM identity.{tabla}";
            var error = await Assert.ThrowsAsync<PostgresException>(() => comando.ExecuteNonQueryAsync(Ct));
            Assert.Equal("identity_destino_borrado_protegido", error.ConstraintName);
        }
        comando.CommandText = "UPDATE identity.users SET is_active=false";
        Assert.Equal(1, await comando.ExecuteNonQueryAsync(Ct));
    }
}
