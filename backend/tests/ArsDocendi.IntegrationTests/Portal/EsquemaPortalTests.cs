using ArsDocendi.IntegrationTests.Infraestructura;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Portal;

/// <summary>
/// La forma del esquema de portal, en lo que otros módulos dependen de ella.
/// </summary>
/// <remarks>
/// <c>portal.perfiles.persona_id</c> es el único camino del portal hacia el padrón.
/// Es una referencia LÓGICA (spec <c>integridad-referencias-logicas</c>): no hay FK,
/// la persona se valida al escribir y su borrado físico lo frena un trigger de
/// <c>identity</c>. El lector de catálogo del asistente la declara aparte para que
/// el prefijo del prompt siga diciendo cómo cruzar portal con identity.
/// </remarks>
public sealed class EsquemaPortalTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "portal_esquema")
{
    [Fact]
    public async Task El_lector_de_catalogo_ofrece_el_camino_del_perfil_a_la_persona()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var conexion = await AbrirConexionComoAsistenteAsync(conDatosPersonales: false);
        var referencias = await Modules.Asistente.Infrastructure.LectorDeCatalogo.LeerReferenciasAsync(conexion, ct);

        Assert.Contains(referencias, r =>
            r is { Esquema: "portal", Tabla: "perfiles", Columna: "persona_id" }
              and { EsquemaReferido: "identity", TablaReferida: "personas", ColumnaReferida: "id" });
    }

    [Fact]
    public async Task No_se_borra_una_persona_que_tiene_perfil_cargado()
    {
        // Las personas no se borran: se desactivan. El trigger de identity rechaza
        // el borrado físico, así que un perfil cargado nunca queda sin dueño.
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
