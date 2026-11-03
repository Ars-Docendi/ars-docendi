using Npgsql;

namespace ArsDocendi.IntegrationTests.Infraestructura;

/// <summary>
/// Custodia el clonado de la base de prueba.
/// </summary>
/// <remarks>
/// Las bases de test se crean con <c>CREATE DATABASE … TEMPLATE</c> en vez de
/// correr las tres migraciones cada vez. PostgreSQL clona el contenido de la base
/// —esquemas, tablas, funciones, policies— pero <b>no</b> clona
/// <c>pg_db_role_setting</c>, que es donde vive el <c>search_path</c> vacío que el
/// módulo le impone a sus roles de lectura: su clave es el OID de la base.
///
/// EL MODO DE FALLA ES SILENCIOSO Y ES EL PEOR. Con el <c>search_path</c> por
/// defecto, una consulta sin calificar resuelve igual, así que ningún test se
/// pone rojo — sólo dejan de probar lo que dicen probar. Los que sostienen el
/// invariante #14 pasarían a verde sin verificar nada.
///
/// Los roles y sus GRANT sí vienen en la plantilla, pero el ajuste se fija sobre
/// cada clon ya creado. Este test es lo que hace que no se pierda.
/// </remarks>
public sealed class PlantillaDeBaseTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "plantilla")
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task El_search_path_efectivo_del_rol_es_vacio_sobre_la_base_clonada(
        bool conDatosPersonales)
    {
        await using var conexion = await AbrirConexionComoAsistenteAsync(conDatosPersonales);
        await using var comando = new NpgsqlCommand("SHOW search_path", conexion);

        var efectivo = (string?)await comando.ExecuteScalarAsync(
            TestContext.Current.CancellationToken);

        // PostgreSQL devuelve la cadena vacía o comillas vacías según cómo se haya
        // fijado; lo que importa es que no nombre ningún esquema.
        Assert.True(
            string.IsNullOrWhiteSpace(efectivo) || efectivo == "\"\"",
            $"El search_path efectivo del rol es «{efectivo}» y tiene que ser vacío. "
            + "Vive en pg_db_role_setting, cuya clave es el OID de la base, y NO se "
            + "clona: si dejara de fijarse sobre cada clon, esto quedaría con el "
            + "default y los tests del invariante #14 pasarían a verde sin probar nada.");
    }

}
