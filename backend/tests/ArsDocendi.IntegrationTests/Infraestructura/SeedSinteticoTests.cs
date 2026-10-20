using System.Diagnostics;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Infraestructura;

public sealed class SeedSinteticoTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "seed")
{
    [Fact]
    public async Task Seed_es_idempotente_restaura_fixtures_y_preserva_filas_ajenas()
    {
        var ct = TestContext.Current.CancellationToken;
        await EjecutarSeedAsync(ct);
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
        await EjecutarSeedAsync(ct);

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

            await EjecutarSeedAsync(ct);

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
    public async Task Script_rechaza_prod_y_origen_productivo_antes_de_invocar_docker()
    {
        var prod = await EjecutarScriptSeedAsync("prod");
        var copia = await EjecutarScriptSeedAsync("staging", "arsdocendi_prod");

        Assert.NotEqual(0, prod.ExitCode);
        Assert.Contains("PROHIBIDO", prod.Error, StringComparison.Ordinal);
        Assert.NotEqual(0, copia.ExitCode);
        Assert.Contains("PROHIBIDO", copia.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reconstruccion_local_reinicia_una_base_y_preserva_la_vecina()
    {
        var ct = TestContext.Current.CancellationToken;
        var baseRecreable = string.Empty;
        var baseVecina = string.Empty;
        try
        {
            baseRecreable = await Postgres.CrearBaseMigradaAsync("reconstruible");
            baseVecina = await Postgres.CrearBaseMigradaAsync("vecina");
            await EjecutarSeedAsync(baseRecreable, ct);
            await EjecutarSeedAsync(baseVecina, ct);

            await using (var conexion = new NpgsqlConnection(baseRecreable))
            {
                await conexion.OpenAsync(ct);
                await EjecutarAsync(conexion, """
                    INSERT INTO identity.personas (id, documento, nombre, apellido)
                    VALUES ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeed', 'TEST-RESET', 'Fila', 'Temporal')
                    """);
            }

            await using (var conexion = new NpgsqlConnection(baseVecina))
            {
                await conexion.OpenAsync(ct);
                await EjecutarAsync(conexion, """
                    INSERT INTO identity.personas (id, documento, nombre, apellido)
                    VALUES ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee', 'TEST-VECINA', 'Fila', 'Vecina')
                    """);
            }

            await Postgres.EliminarBaseAsync(baseRecreable);
            baseRecreable = await Postgres.CrearBaseMigradaAsync("reconstruible");
            await EjecutarSeedAsync(baseRecreable, ct);

            await using var recreada = new NpgsqlConnection(baseRecreable);
            await recreada.OpenAsync(ct);
            Assert.Equal(0L, await EscalarAsync<long>(recreada,
                "SELECT count(*) FROM identity.personas WHERE documento = 'TEST-RESET'"));
            Assert.Equal(8L, await EscalarAsync<long>(recreada,
                "SELECT count(*) FROM designaciones.pedidos WHERE numero LIKE '2026-90%'"));
            Assert.Equal("2026.09.1", await EscalarAsync<string>(recreada,
                "SELECT valor FROM public.seed_metadata WHERE clave = 'dataset_version'"));

            await using var vecina = new NpgsqlConnection(baseVecina);
            await vecina.OpenAsync(ct);
            Assert.Equal(1L, await EscalarAsync<long>(vecina,
                "SELECT count(*) FROM identity.personas WHERE documento = 'TEST-VECINA'"));
        }
        finally
        {
            if (baseRecreable.Length > 0) await Postgres.EliminarBaseAsync(baseRecreable);
            if (baseVecina.Length > 0) await Postgres.EliminarBaseAsync(baseVecina);
        }
    }

    private Task EjecutarSeedAsync(CancellationToken ct) => EjecutarSeedAsync(Cadena, ct);

    private async Task EjecutarSeedAsync(string cadena, CancellationToken ct)
    {
        var sql = await File.ReadAllTextAsync(
            Path.Combine(BuscarRaizRepositorio(), "infra", "scripts", "seed-data", "sintetico.sql"), ct);
        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync(ct);
        await using var comando = new NpgsqlCommand(sql, conexion) { CommandTimeout = 60 };
        await comando.ExecuteNonQueryAsync(ct);
    }

    private static async Task<ResultadoProceso> EjecutarScriptSeedAsync(
        string ambiente,
        string? origen = null)
    {
        var inicio = new ProcessStartInfo("bash")
        {
            WorkingDirectory = BuscarRaizRepositorio(),
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        inicio.ArgumentList.Add("infra/scripts/seed.sh");
        inicio.ArgumentList.Add(ambiente);
        if (origen is not null)
        {
            inicio.Environment["SEED_FROM_DB"] = origen;
        }

        using var proceso = Process.Start(inicio)
            ?? throw new InvalidOperationException("No se pudo iniciar seed.sh.");
        var error = await proceso.StandardError.ReadToEndAsync();
        await proceso.WaitForExitAsync();
        return new ResultadoProceso(proceso.ExitCode, error);
    }

    private static string BuscarRaizRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null)
        {
            if (File.Exists(Path.Combine(directorio.FullName, "AGENTS.md")))
            {
                return directorio.FullName;
            }
            directorio = directorio.Parent;
        }
        throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
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

    private sealed record ResultadoProceso(int ExitCode, string Error);
}
