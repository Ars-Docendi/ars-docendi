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
/// Hoy no puede pasar porque los roles se crean DESPUÉS del clon, sobre él. Este
/// test es lo que hace que siga sin poder pasar.
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
            + "clona: si la plantilla pasara a traer los roles, esto quedaría con el "
            + "default y los tests del invariante #14 pasarían a verde sin probar nada.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task El_ajuste_del_search_path_esta_registrado_contra_ESTA_base(
        bool conDatosPersonales)
    {
        // ACÁ ESTÁ LA TRAMPA, mirada de frente. El ajuste vive en
        // `pg_db_role_setting`, cuya clave es el par (OID de base, OID de rol). Un
        // clon tiene OID nuevo, así que ninguna fila de la plantilla lo alcanza:
        // esta consulta es la que distingue «el rol tiene el ajuste en la base que
        // el test está usando» de «el rol tiene el ajuste en alguna base».
        var rol = conDatosPersonales ? RolSoloLecturaPii : RolSoloLectura;

        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT s.setconfig
              FROM pg_db_role_setting s
              JOIN pg_database d ON d.oid = s.setdatabase
              JOIN pg_roles r    ON r.oid = s.setrole
             WHERE d.datname = current_database()
               AND r.rolname = @rol
            """, conexion);
        comando.Parameters.AddWithValue("rol", rol);

        var ajustes = (string[]?)await comando.ExecuteScalarAsync(
            TestContext.Current.CancellationToken);

        Assert.NotNull(ajustes);
        Assert.Contains(ajustes, ajuste => ajuste.StartsWith("search_path=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task La_base_clonada_trae_las_tres_migraciones()
    {
        // El clon tiene que traer TODO lo que traía la base migrada: si la
        // plantilla se creara mal, los tests fallarían de mil formas distintas y
        // ninguna diría «el clonado está roto».
        var esquemas = new List<string>();

        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            "SELECT nspname FROM pg_namespace WHERE nspname = ANY(@esperados) ORDER BY nspname",
            conexion);
        comando.Parameters.AddWithValue(
            "esperados", new[] { "identity", "designaciones", "portal", "audit" });

        await using var lector = await comando.ExecuteReaderAsync(
            TestContext.Current.CancellationToken);
        while (await lector.ReadAsync(TestContext.Current.CancellationToken))
        {
            esquemas.Add(lector.GetString(0));
        }

        Assert.Equal(["audit", "designaciones", "identity", "portal"], esquemas);
    }
}
