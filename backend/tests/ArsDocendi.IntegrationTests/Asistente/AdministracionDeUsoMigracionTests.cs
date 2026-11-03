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

}
