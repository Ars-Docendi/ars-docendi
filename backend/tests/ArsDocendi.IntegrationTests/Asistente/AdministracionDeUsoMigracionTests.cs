using ArsDocendi.IntegrationTests.Infraestructura;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// La forma de <c>database/asistente/006_asistente_administracion.sql</c> (y de
/// <c>008_asistente_presupuesto_usuario_vigente_unico.sql</c>, que la completa) contra una base
/// migrada de verdad (asistente-administracion-de-uso, tarea 2.1/2.2).
/// </summary>
public sealed class AdministracionDeUsoMigracionTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_administracion_migracion")
{
    [Fact]
    public async Task Un_segundo_intento_de_fila_en_modo_mantenimiento_es_rechazado()
    {
        await using var conexion = await AbrirConexionAsync();

        await using var comando = new NpgsqlCommand(
            "INSERT INTO asistente.modo_mantenimiento (id, activo, actualizado_en) VALUES (2, false, now())",
            conexion);
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

        // check_violation: el CHECK id = 1 es lo que impide una segunda fila.
        Assert.Equal("23514", error.SqlState);
    }

    [Fact]
    public async Task Una_segunda_fila_vigente_del_mismo_actor_es_rechazada()
    {
        await using var conexion = await AbrirConexionAsync();
        var actor = Guid.NewGuid();

        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO asistente.presupuesto_usuario (actor_id, cupo_diario_turnos, vigente_desde)
            VALUES (@actor, 10, now()), (@actor, 20, now())
            """, conexion);
        comando.Parameters.AddWithValue("actor", actor);
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

        // unique_violation: el índice parcial de 008 es lo que lo impide.
        Assert.Equal("23505", error.SqlState);
    }

    [Fact]
    public async Task Reaplicar_cierra_las_filas_vigentes_duplicadas_y_deja_la_mas_reciente()
    {
        // Una base donde 006 corrió sin el índice: así quedan los ambientes que
        // ya tenían duplicados antes de 008.
        await using var conexion = await AbrirConexionAsync();
        var ct = TestContext.Current.CancellationToken;
        var actor = Guid.NewGuid();
        await using (var preparar = new NpgsqlCommand(
            """
            DROP INDEX asistente.ux_presupuesto_usuario_actor_vigente;
            INSERT INTO asistente.presupuesto_usuario (actor_id, cupo_diario_turnos, vigente_desde)
            VALUES (@actor, 10, now()), (@actor, 20, now());
            """, conexion))
        {
            preparar.Parameters.AddWithValue("actor", actor);
            await preparar.ExecuteNonQueryAsync(ct);
        }

        await Modules.Asistente.Infrastructure.AdministracionAsistente.AplicarAsync(conexion, ct);

        await using var comando = new NpgsqlCommand(
            """
            SELECT cupo_diario_turnos FROM asistente.presupuesto_usuario
             WHERE actor_id = @actor AND vigente_hasta IS NULL
            """, conexion);
        comando.Parameters.AddWithValue("actor", actor);
        await using var lector = await comando.ExecuteReaderAsync(ct);
        var vigentes = new List<int>();
        while (await lector.ReadAsync(ct))
        {
            vigentes.Add(lector.GetInt32(0));
        }

        Assert.Equal([20], vigentes);
    }

    [Fact]
    public async Task Reaplicar_la_migracion_no_duplica_ni_falla()
    {
        await using var conexion = await AbrirConexionAsync();

        await Modules.Asistente.Infrastructure.AdministracionAsistente.AplicarAsync(
            conexion, TestContext.Current.CancellationToken);
        await Modules.Asistente.Infrastructure.AdministracionAsistente.AplicarAsync(
            conexion, TestContext.Current.CancellationToken);

        await using var comando = new NpgsqlCommand(
            "SELECT count(*) FROM asistente.presupuesto_rol", conexion);
        var filas = (long)(await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;

        Assert.Equal(7, filas);
    }
}
