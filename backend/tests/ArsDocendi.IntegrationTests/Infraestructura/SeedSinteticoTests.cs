using Npgsql;

namespace ArsDocendi.IntegrationTests.Infraestructura;

public sealed class SeedSinteticoTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "seed")
{
    [Fact]
    public async Task Seed_es_idempotente_restaura_fixtures_y_preserva_filas_ajenas()
    {
        var ct = TestContext.Current.CancellationToken;
        await SembrarAsync(ct);
        await using var conexion = await AbrirConexionAsync();
        var personaAjena = Guid.NewGuid();
        await EjecutarAsync(conexion, """
            INSERT INTO identity.personas (id, documento, nombre, apellido)
            VALUES (@id, @documento, 'Persona', 'Ajena');
            UPDATE identity.personas SET nombre = 'Alterada'
            WHERE id = 'd0000000-0000-4000-8000-000000000001';
            """, new NpgsqlParameter("id", personaAjena), new NpgsqlParameter("documento", $"TEST-{personaAjena:N}"));

        var pedidosAntes = await EscalarAsync<long>(conexion,
            "SELECT count(*) FROM designaciones.pedidos WHERE numero LIKE '2026-90%'");
        await SembrarAsync(ct);

        Assert.Equal(pedidosAntes, await EscalarAsync<long>(conexion,
            "SELECT count(*) FROM designaciones.pedidos WHERE numero LIKE '2026-90%'"));
        Assert.Equal("Carla", await EscalarAsync<string>(conexion,
            "SELECT nombre FROM identity.personas WHERE id = 'd0000000-0000-4000-8000-000000000001'"));
        Assert.Equal(1L, await EscalarAsync<long>(conexion,
            "SELECT count(*) FROM identity.personas WHERE id = @id", new NpgsqlParameter("id", personaAjena)));
    }

    [Fact]
    public async Task Seed_concede_lectura_de_identidades_al_rol_dueno_de_la_base()
    {
        var ct = TestContext.Current.CancellationToken;
        var rolAplicacion = $"app_seed_{Guid.NewGuid():N}";
        var baseActual = new NpgsqlConnectionStringBuilder(Cadena).Database
            ?? throw new InvalidOperationException("La cadena no contiene una base de datos.");

        await using var conexion = await AbrirConexionAsync();
        try
        {
            await EjecutarAsync(conexion, $"""
                CREATE ROLE "{rolAplicacion}" NOLOGIN;
                ALTER DATABASE "{baseActual}" OWNER TO "{rolAplicacion}";
                """);

            await SembrarAsync(ct);

            await EjecutarAsync(conexion, $"SET ROLE \"{rolAplicacion}\";");
            Assert.True(await EscalarAsync<bool>(conexion,
                "SELECT has_table_privilege(current_user, 'public.seed_identities', 'SELECT')"));
            Assert.True(await EscalarAsync<long>(conexion,
                "SELECT count(*) FROM public.seed_identities") > 0);
        }
        finally
        {
            await EjecutarAsync(conexion, "RESET ROLE;");
            await EjecutarAsync(conexion, $"""
                ALTER DATABASE "{baseActual}" OWNER TO postgres;
                REVOKE SELECT ON TABLE public.seed_identities FROM "{rolAplicacion}";
                DROP ROLE IF EXISTS "{rolAplicacion}";
                """);
        }
    }

    [Fact]
    public async Task Reconstruccion_local_reinicia_una_base_y_preserva_la_vecina()
    {
        var ct = TestContext.Current.CancellationToken;
        BaseDePrueba? baseRecreable = null;
        BaseDePrueba? baseVecina = null;
        try
        {
            baseRecreable = await Postgres.CrearBaseMigradaAsync("reconstruible");
            baseVecina = await Postgres.CrearBaseMigradaAsync("vecina");
            await SembrarAsync(baseRecreable.Cadena, ct);
            await SembrarAsync(baseVecina.Cadena, ct);

            await using (var conexion = new NpgsqlConnection(baseRecreable.Cadena))
            {
                await conexion.OpenAsync(ct);
                await EjecutarAsync(conexion, """
                    INSERT INTO identity.personas (id, documento, nombre, apellido)
                    VALUES ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeed', 'TEST-RESET', 'Fila', 'Temporal')
                    """);
            }

            await using (var conexion = new NpgsqlConnection(baseVecina.Cadena))
            {
                await conexion.OpenAsync(ct);
                await EjecutarAsync(conexion, """
                    INSERT INTO identity.personas (id, documento, nombre, apellido)
                    VALUES ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee', 'TEST-VECINA', 'Fila', 'Vecina')
                    """);
            }

            await Postgres.EliminarBaseAsync(baseRecreable);
            baseRecreable = await Postgres.CrearBaseMigradaAsync("reconstruible");
            await SembrarAsync(baseRecreable.Cadena, ct);

            await using var recreada = new NpgsqlConnection(baseRecreable.Cadena);
            await recreada.OpenAsync(ct);
            Assert.Equal(0L, await EscalarAsync<long>(recreada,
                "SELECT count(*) FROM identity.personas WHERE documento = 'TEST-RESET'"));
            Assert.Equal(8L, await EscalarAsync<long>(recreada,
                "SELECT count(*) FROM designaciones.pedidos WHERE numero LIKE '2026-90%'"));
            Assert.Equal("2026.09.1", await EscalarAsync<string>(recreada,
                "SELECT valor FROM public.seed_metadata WHERE clave = 'dataset_version'"));

            await using var vecina = new NpgsqlConnection(baseVecina.Cadena);
            await vecina.OpenAsync(ct);
            Assert.Equal(1L, await EscalarAsync<long>(vecina,
                "SELECT count(*) FROM identity.personas WHERE documento = 'TEST-VECINA'"));
        }
        finally
        {
            if (baseRecreable is not null) await Postgres.EliminarBaseAsync(baseRecreable);
            if (baseVecina is not null) await Postgres.EliminarBaseAsync(baseVecina);
        }
    }

    private static async Task EjecutarAsync(
        NpgsqlConnection conexion,
        string sql,
        params NpgsqlParameter[] parametros)
    {
        await using var comando = new NpgsqlCommand(sql, conexion);
        comando.Parameters.AddRange(parametros);
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task<T> EscalarAsync<T>(
        NpgsqlConnection conexion,
        string sql,
        params NpgsqlParameter[] parametros)
    {
        await using var comando = new NpgsqlCommand(sql, conexion);
        comando.Parameters.AddRange(parametros);
        return (T)(await comando.ExecuteScalarAsync())!;
    }
}
