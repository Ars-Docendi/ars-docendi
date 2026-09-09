using System.Diagnostics;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Infraestructura;

[Collection(ColeccionPostgres.Nombre)]
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
    public async Task Seed_cubre_roles_ambitos_estados_y_persona_sin_cuenta()
    {
        await SembrarAsync(TestContext.Current.CancellationToken);
        await using var conexion = await AbrirConexionAsync();

        Assert.Equal(7L, await EscalarAsync<long>(conexion, """
            SELECT count(DISTINCT r.code)
            FROM identity.user_roles ur
            JOIN identity.roles r ON r.id = ur.role_id
            JOIN public.seed_identities si ON si.user_id = ur.user_id
            WHERE r.es_sistema AND ur.deleted_at IS NULL
            """));
        Assert.Equal(3L, await EscalarAsync<long>(conexion, """
            SELECT count(DISTINCT r.scope)
            FROM identity.user_roles ur
            JOIN identity.roles r ON r.id = ur.role_id
            JOIN public.seed_identities si ON si.user_id = ur.user_id
            WHERE ur.deleted_at IS NULL
            """));
        Assert.Equal(8L, await EscalarAsync<long>(conexion,
            "SELECT count(DISTINCT estado) FROM designaciones.pedidos WHERE numero LIKE '2026-90%'"));
        Assert.Equal(1L, await EscalarAsync<long>(conexion,
            "SELECT count(*) FROM identity.users u JOIN public.seed_identities s ON s.user_id=u.id WHERE NOT u.is_active"));
        Assert.True(await EscalarAsync<long>(conexion, """
            SELECT count(*) FROM identity.personas p
            WHERE NOT EXISTS (SELECT 1 FROM identity.users u WHERE u.persona_id = p.id)
            """) > 0);
        Assert.Equal("sintetico", await EscalarAsync<string>(conexion,
            "SELECT valor FROM public.seed_metadata WHERE clave = 'origen_datos'"));
        Assert.Equal(
            await EscalarAsync<long>(conexion, "SELECT count(*) FROM identity.personas"),
            await EscalarAsync<long>(conexion, "SELECT count(*) FROM portal.perfiles"));
        Assert.Equal(
            await EscalarAsync<long>(conexion, "SELECT count(*) FROM identity.personas"),
            await EscalarAsync<long>(conexion, "SELECT count(*) FROM portal.contactos"));
        Assert.Equal(0L, await EscalarAsync<long>(conexion, """
            SELECT count(*)
            FROM designaciones.pedido_historial h
            JOIN designaciones.pedidos p ON p.id = h.pedido_id
            WHERE h.accion IN ('crear', 'enviar', 'cancelar')
              AND NOT EXISTS (
                  SELECT 1
                  FROM identity.user_roles ur
                  WHERE ur.user_id = h.actor_id
                    AND ur.role_id = h.rol_id
                    AND ur.materia_id = p.materia_id
                    AND ur.deleted_at IS NULL
              )
            """));
        Assert.Equal(0L, await EscalarAsync<long>(conexion, """
            SELECT count(*)
            FROM designaciones.pedido_historial h
            WHERE h.actor_id IS NOT NULL
              AND NOT EXISTS (
                  SELECT 1
                  FROM identity.user_roles ur
                  WHERE ur.user_id = h.actor_id
                    AND ur.role_id = h.rol_id
                    AND ur.deleted_at IS NULL
              )
            """));
        Assert.Equal(0L, await EscalarAsync<long>(conexion, """
            SELECT count(*)
            FROM designaciones.pedido_historial h
            JOIN designaciones.pedidos p ON p.id = h.pedido_id
            WHERE h.accion = 'crear'
              AND NOT EXISTS (
                  SELECT 1
                  FROM identity.user_roles ur
                  JOIN identity.roles r ON r.id = ur.role_id
                  WHERE ur.user_id = h.actor_id
                    AND ur.role_id = h.rol_id
                    AND r.code = 'jefe_catedra'
                    AND ur.materia_id = p.materia_id
                    AND ur.deleted_at IS NULL
              )
            """));
        Assert.Equal(0L, await EscalarAsync<long>(conexion, """
            SELECT count(*)
            FROM designaciones.pedidos p
            WHERE p.estado IN ('en_revision_coordinador', 'en_revision_secretaria', 'en_revision_decanato')
              AND NOT EXISTS (
                  SELECT 1 FROM designaciones.pedido_historial h
                  WHERE h.pedido_id = p.id AND h.accion = 'enviar'
              )
            """));
        Assert.Equal(0L, await EscalarAsync<long>(conexion, """
            SELECT count(*)
            FROM designaciones.pedidos p
            WHERE p.novedad = 'Alta'
              AND EXISTS (
                  SELECT 1 FROM designaciones.designaciones d
                  WHERE d.persona_id = p.persona_id AND d.materia_id = p.materia_id
              )
            """));
    }

    [Fact]
    public async Task Seed_cubre_seis_dedicaciones_cargas_y_continuidad()
    {
        await SembrarAsync(TestContext.Current.CancellationToken);
        await using var conexion = await AbrirConexionAsync();

        Assert.Equal(6L, await EscalarAsync<long>(conexion,
            "SELECT count(*) FROM designaciones.dedicaciones WHERE codigo BETWEEN 1 AND 6"));
        Assert.Equal(0L, await EscalarAsync<long>(conexion,
            "SELECT count(*) FROM designaciones.pedidos WHERE numero LIKE '2026-90%' AND novedad = 'Sin novedad'"));
        Assert.Equal(12, await EscalarAsync<int>(conexion, """
            SELECT horas FROM designaciones.pedidos WHERE numero = '2026-9006'
            """));
        Assert.Equal(4, await EscalarAsync<int>(conexion, """
            SELECT horas_investigacion FROM designaciones.pedidos WHERE numero = '2026-9006'
            """));
        Assert.Equal(3, await EscalarAsync<int>(conexion, """
            SELECT horas_externas FROM designaciones.pedidos WHERE numero = '2026-9006'
            """));
        Assert.Equal(10, await EscalarAsync<int>(conexion, """
            SELECT (snapshot->>'horas')::INTEGER FROM designaciones.pedidos WHERE numero = '2026-9006'
            """));
        Assert.Equal(3, await EscalarAsync<int>(conexion, """
            SELECT (snapshot->>'horas_investigacion')::INTEGER FROM designaciones.pedidos WHERE numero = '2026-9006'
            """));
        Assert.Equal(2, await EscalarAsync<int>(conexion, """
            SELECT (snapshot->>'horas_externas')::INTEGER FROM designaciones.pedidos WHERE numero = '2026-9006'
            """));
        Assert.Equal(1L, await EscalarAsync<long>(conexion, """
            SELECT count(*) FROM designaciones.designaciones
            WHERE origen_pedido_id = 'd5000000-0000-4000-8000-000000000006'
            """));
        Assert.True(await EscalarAsync<long>(conexion, """
            SELECT count(*) FROM designaciones.designaciones
            WHERE persona_id = 'd0000000-0000-4000-8000-000000000014'
              AND vigente_hasta IS NULL
            """ ) > 1);
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
    public async Task Spin_up_reconstruye_descartables_en_orden_y_no_resetea_prod()
    {
        var ct = TestContext.Current.CancellationToken;
        var raiz = RaizRepositorio.Ruta();
        var ruta = Path.Combine(raiz, "infra", "scripts", "spin-up.sh");
        var sintaxis = new ProcessStartInfo("bash")
        {
            WorkingDirectory = raiz,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        sintaxis.ArgumentList.Add("-n");
        sintaxis.ArgumentList.Add(ruta);
        using var proceso = Process.Start(sintaxis)!;
        var error = await proceso.StandardError.ReadToEndAsync(ct);
        await proceso.WaitForExitAsync(ct);

        Assert.True(proceso.ExitCode == 0, error);
        var script = await File.ReadAllTextAsync(ruta, ct);
        var reset = script.IndexOf("drop-db.sh", StringComparison.Ordinal);
        var provision = script.IndexOf("provision-db.sh", StringComparison.Ordinal);
        var migraciones = script.IndexOf("run --rm backend", StringComparison.Ordinal);
        var seed = script.IndexOf("seed.sh", StringComparison.Ordinal);
        var servicio = script.IndexOf("up -d", StringComparison.Ordinal);
        Assert.True(reset >= 0 && reset < provision && provision < migraciones && migraciones < seed && seed < servicio);
        Assert.Contains("if [[ \"$ambiente\" != \"prod\" ]]", script, StringComparison.Ordinal);
        Assert.Contains("flock 9", script, StringComparison.Ordinal);
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

    private static async Task<ResultadoProceso> EjecutarScriptSeedAsync(
        string ambiente,
        string? origen = null)
    {
        var inicio = new ProcessStartInfo("bash")
        {
            WorkingDirectory = RaizRepositorio.Ruta(),
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
