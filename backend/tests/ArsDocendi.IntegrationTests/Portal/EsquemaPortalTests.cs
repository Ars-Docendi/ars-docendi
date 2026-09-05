using ArsDocendi.IntegrationTests.Infraestructura;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Portal;

/// <summary>
/// La forma del esquema de portal, en lo que otros módulos dependen de ella.
/// </summary>
/// <remarks>
/// <c>portal.perfiles.persona_id</c> es el único camino del portal hacia el padrón,
/// y nació sin <c>REFERENCES</c>. La constraint no es cosmética: además de la
/// integridad, es lo que hace que el lector de catálogo del asistente pueda emitir
/// la relación entre los dos esquemas, porque la lee de <c>pg_constraint</c> con
/// <c>contype = 'f'</c>. Sin ella habría que escribir el camino a mano en un
/// comentario, que es poner una barrera de prosa donde puede haber una del motor.
/// </remarks>
[Collection(ColeccionPostgres.Nombre)]
public sealed class EsquemaPortalTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "portal_esquema")
{
    [Fact]
    public async Task El_perfil_referencia_a_la_persona_con_una_clave_foranea()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT confrelid::REGCLASS::TEXT
              FROM pg_constraint
             WHERE conrelid = 'portal.perfiles'::REGCLASS
               AND contype = 'f'
               AND conname = 'perfiles_persona_fk'
            """, conexion);

        Assert.Equal("identity.personas", await comando.ExecuteScalarAsync(ct));
    }

    [Fact]
    public async Task Un_perfil_que_apunta_a_una_persona_inexistente_no_entra()
    {
        // Es la propiedad, no la constraint: lo que importa es que el motor lo
        // impida, no que exista una fila en un catálogo del sistema.
        var ct = TestContext.Current.CancellationToken;

        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            "INSERT INTO portal.perfiles (persona_id) VALUES (gen_random_uuid())", conexion);

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => comando.ExecuteNonQueryAsync(ct));

        // 23503 es violación de clave foránea. Se afirma el código y no el mensaje
        // porque el mensaje cambia con la versión y con el locale del servidor.
        Assert.Equal("23503", error.SqlState);
    }

    [Fact]
    public async Task No_se_borra_una_persona_que_tiene_perfil_cargado()
    {
        // SIN `ON DELETE CASCADE`, y es deliberado. Las FK internas de portal
        // cascadean porque un perfil sin dueño no significa nada; ésta no, porque
        // borrar una persona con perfil cargado tiene que fallar y que alguien lo
        // mire. En el flujo normal las personas no se borran: se desactivan.
        var ct = TestContext.Current.CancellationToken;

        await using var conexion = await AbrirConexionAsync();

        await using (var alta = new NpgsqlCommand(
            """
            INSERT INTO identity.personas (id, documento, nombre, apellido)
            VALUES ('11111111-1111-4111-8111-111111111111', '99999999', 'Prueba', 'Borrado');
            INSERT INTO portal.perfiles (persona_id)
            VALUES ('11111111-1111-4111-8111-111111111111');
            """, conexion))
        {
            await alta.ExecuteNonQueryAsync(ct);
        }

        await using var baja = new NpgsqlCommand(
            "DELETE FROM identity.personas WHERE id = '11111111-1111-4111-8111-111111111111'",
            conexion);

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => baja.ExecuteNonQueryAsync(ct));

        Assert.Equal("23503", error.SqlState);
    }
}
