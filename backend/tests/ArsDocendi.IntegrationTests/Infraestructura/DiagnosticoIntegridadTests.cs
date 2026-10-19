using System.Diagnostics;
using System.Text.Json;
using ArsDocendi.Migraciones;
using ArsDocendi.Shared.Persistencia;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Infraestructura;

public sealed class DiagnosticoIntegridadTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "diagnostico_integridad")
{
    [Fact]
    public void Diagnostico_es_un_modo_cli_explicito_sin_listener()
    {
        var solicitud = SolicitudMigracion.Parsear(["--diagnosticar-integridad"]);
        Assert.NotEqual(ModoMigracion.Ninguno, solicitud.Modo);
        Assert.NotEqual(ModoMigracion.Migrar, solicitud.Modo);
        Assert.Null(solicitud.Directorio);
    }

    [Fact]
    public async Task Cli_detecta_persona_huerfana_sin_escribir_ni_exponer_valores()
    {
        await using var conexion = await AbrirConexionAsync();
        await using (var insertar = new NpgsqlCommand("""
            INSERT INTO portal.perfiles(persona_id)
            VALUES ('11111111-1111-4111-8111-111111111111');
            """, conexion))
            await insertar.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        var antes = await HuellaAsync(conexion);
        var resultado = await EjecutarCliAsync(Cadena);
        Assert.Equal(1, resultado.Codigo);
        using var json = JsonDocument.Parse(resultado.Salida);
        Assert.False(json.RootElement.GetProperty("compatible").GetBoolean());
        Assert.Contains(json.RootElement.GetProperty("hallazgos").EnumerateArray(),
            h => h.GetProperty("categoria").GetString() == "portal-persona-huerfana"
                && h.GetProperty("cantidad").GetInt64() == 1);
        Assert.DoesNotContain("persona_id", resultado.Error);
        Assert.Equal(antes, await HuellaAsync(conexion));
    }

    [Fact]
    public async Task Cli_detecta_duplicados_y_scope_legados_sin_sanear_ni_emitir_upn()
    {
        await using var conexion = await AbrirConexionAsync();
        // Solo en esta base desechable: simula datos anteriores a las nuevas garantías.
        await using var preparar = new NpgsqlCommand("""
            DROP INDEX identity.users_upn_normalizado_key;
            INSERT INTO identity.users(id, upn, display_name) VALUES
              ('22222222-2222-4222-8222-222222222221', 'Diagnostico@example.invalid', 'Sintetico'),
              ('22222222-2222-4222-8222-222222222222', ' diagnostico@EXAMPLE.invalid ', 'Sintetico');
            INSERT INTO identity.roles(id, code, name, scope) VALUES
              ('22222222-2222-4222-8222-222222222223', 'diagnostico_legacy', 'Diagnostico legacy', 'materia');
            BEGIN;
            SET LOCAL session_replication_role = replica;
            INSERT INTO identity.user_roles(user_id, role_id) VALUES
              ('22222222-2222-4222-8222-222222222221', '22222222-2222-4222-8222-222222222223');
            COMMIT;
            """, conexion);
        await preparar.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        var antes = await HuellaAsync(conexion);
        var resultado = await EjecutarCliAsync(Cadena);
        Assert.Equal(1, resultado.Codigo);
        using var json = JsonDocument.Parse(resultado.Salida);
        var hallazgos = json.RootElement.GetProperty("hallazgos").EnumerateArray().ToArray();
        Assert.Contains(hallazgos, h => h.GetProperty("categoria").GetString() == "identity-upn-duplicado"
            && h.GetProperty("cantidad").GetInt64() == 2);
        Assert.Contains(hallazgos, h => h.GetProperty("categoria").GetString() == "identity-scope-incoherente"
            && h.GetProperty("cantidad").GetInt64() == 1);
        Assert.DoesNotContain("example.invalid", resultado.Salida + resultado.Error);
        Assert.DoesNotContain("Sintetico", resultado.Salida + resultado.Error);
        Assert.Equal(antes, await HuellaAsync(conexion));
    }

    [Fact]
    public async Task Cli_detecta_ciclo_legado_sin_recursion_infinita_ni_reparacion()
    {
        await using var conexion = await AbrirConexionAsync();
        await using var preparar = new NpgsqlCommand("""
            INSERT INTO identity.users(id, upn, display_name) VALUES
              ('33333333-3333-4333-8333-333333333330', 'ciclo@example.invalid', 'Sintetico');
            BEGIN;
            SET LOCAL session_replication_role = replica;
            INSERT INTO tareas.tareas(id, titulo, fecha_inicio, fecha_fin, prioridad_id, tipo_id, estado_id,
              responsable_id, creado_por_id, tarea_padre_id)
            SELECT id, 'Ciclo sintetico', '2026-01-01', '2026-12-31',
              (SELECT id FROM tareas.prioridades WHERE codigo = 'media'),
              (SELECT id FROM tareas.tipos_tarea WHERE codigo = 'administrativa'),
              (SELECT id FROM tareas.estados_tarea WHERE codigo = 'pendiente'),
              '33333333-3333-4333-8333-333333333330', '33333333-3333-4333-8333-333333333330', padre
            FROM (VALUES
              ('33333333-3333-4333-8333-333333333331'::uuid, '33333333-3333-4333-8333-333333333332'::uuid),
              ('33333333-3333-4333-8333-333333333332'::uuid, '33333333-3333-4333-8333-333333333331'::uuid)
            ) nodos(id, padre);
            COMMIT;
            """, conexion);
        await preparar.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        var antes = await HuellaAsync(conexion);
        var resultado = await EjecutarCliAsync(Cadena);
        Assert.Equal(1, resultado.Codigo);
        using var json = JsonDocument.Parse(resultado.Salida);
        Assert.Contains(json.RootElement.GetProperty("hallazgos").EnumerateArray(),
            h => h.GetProperty("categoria").GetString() == "tareas-ciclo"
                && h.GetProperty("cantidad").GetInt64() == 2);
        Assert.DoesNotContain("Ciclo sintetico", resultado.Salida + resultado.Error);
        Assert.Equal(antes, await HuellaAsync(conexion));
    }

    [Fact]
    public async Task Cli_base_limpia_informa_ambiente_release_historial_sin_escrituras()
    {
        await using var conexion = await AbrirConexionAsync();
        var antes = await HuellaAsync(conexion);
        var resultado = await EjecutarCliAsync(Cadena);
        Assert.Equal(0, resultado.Codigo);
        using var json = JsonDocument.Parse(resultado.Salida);
        Assert.True(json.RootElement.GetProperty("compatible").GetBoolean());
        Assert.Equal("Testing", json.RootElement.GetProperty("ambiente").GetString());
        Assert.Equal("fixture-sintetica", json.RootElement.GetProperty("sha").GetString());
        Assert.Empty(json.RootElement.GetProperty("hallazgos").EnumerateArray());
        Assert.NotEmpty(json.RootElement.GetProperty("contextos").EnumerateArray());
        Assert.Equal(antes, await HuellaAsync(conexion));
    }

    [Fact]
    public async Task Composicion_incompleta_no_equivale_a_base_limpia()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DiagnosticoIntegridad.LeerAsync([], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Composicion_conserva_nombres_de_contextos_y_rechaza_duplicados_por_case()
    {
        IDiagnosticoIntegridadModulo[] lectores =
        [new LectorVacio("identity"), new LectorVacio("Storage"), new LectorVacio("Designaciones"),
         new LectorVacio("Portal"), new LectorVacio("Tareas")];
        Assert.Empty(await DiagnosticoIntegridad.LeerAsync(lectores, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => DiagnosticoIntegridad.LeerAsync(
            [.. lectores, new LectorVacio("IDENTITY")], TestContext.Current.CancellationToken));
    }

    private sealed class LectorVacio(string contexto) : IDiagnosticoIntegridadModulo
    {
        public string Contexto => contexto;
        public Task<IReadOnlyList<RegistroIntegridad>> LeerAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<RegistroIntegridad>>([]);
    }

    [Fact]
    public async Task Cli_sin_permisos_falla_cerrado_sin_publicar_sql_o_datos()
    {
        await using var conexion = await AbrirConexionAsync();
        var rol = "diagnostico_" + Guid.NewGuid().ToString("N");
        await using var preparar = new NpgsqlCommand($"""
            CREATE ROLE "{rol}" LOGIN PASSWORD 'postgres';
            GRANT USAGE ON SCHEMA identity, storage, designaciones, portal, tareas TO "{rol}";
            GRANT SELECT ON ALL TABLES IN SCHEMA identity, storage, designaciones, portal, tareas TO "{rol}";
            REVOKE SELECT ON portal.perfiles FROM "{rol}";
            """, conexion);
        await preparar.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        try
        {
            var antes = await HuellaAsync(conexion);
            var cadena = new NpgsqlConnectionStringBuilder(Cadena) { Username = rol }.ConnectionString;
            var resultado = await EjecutarCliAsync(cadena);
            Assert.Equal(1, resultado.Codigo);
            using var json = JsonDocument.Parse(resultado.Error.Trim());
            Assert.False(json.RootElement.GetProperty("compatible").GetBoolean());
            Assert.DoesNotContain("SELECT", resultado.Error);
            Assert.DoesNotContain("perfiles", resultado.Error);
            Assert.DoesNotContain(rol, resultado.Error);
            Assert.Equal(antes, await HuellaAsync(conexion));
        }
        finally
        {
            await using var limpiar = new NpgsqlCommand($"DROP OWNED BY \"{rol}\"; DROP ROLE \"{rol}\";", conexion);
            await limpiar.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
    }

    private static async Task<string> HuellaAsync(NpgsqlConnection conexion)
    {
        var tablas = new List<(string Schema, string Nombre)>();
        await using (var catalogo = new NpgsqlCommand("""
            SELECT schemaname, tablename FROM pg_tables
            WHERE schemaname IN ('identity', 'audit', 'storage', 'designaciones', 'comisiones', 'portal', 'tareas')
            ORDER BY schemaname, tablename
            """, conexion))
        await using (var lector = await catalogo.ExecuteReaderAsync(TestContext.Current.CancellationToken))
        {
            while (await lector.ReadAsync(TestContext.Current.CancellationToken))
                tablas.Add((lector.GetString(0), lector.GetString(1)));
        }
        using var identificadores = new NpgsqlCommandBuilder();
        var consultas = tablas.Select(t =>
            $"SELECT {tablas.IndexOf(t)}::text || ':' || to_jsonb(f)::text AS fila FROM "
            + identificadores.QuoteIdentifier(t.Schema) + "." + identificadores.QuoteIdentifier(t.Nombre) + " f");
        await using var comando = new NpgsqlCommand(
            "SELECT md5(coalesce(string_agg(fila, '|' ORDER BY fila), '')) FROM ("
            + string.Join(" UNION ALL ", consultas) + ") filas", conexion);
        return (string)(await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static async Task<(int Codigo, string Salida, string Error)> EjecutarCliAsync(string cadena)
    {
        var inicio = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        inicio.ArgumentList.Add(Path.Join(AppContext.BaseDirectory, "ArsDocendi.Host.dll"));
        inicio.ArgumentList.Add("--diagnosticar-integridad");
        inicio.Environment["ConnectionStrings__ArsDocendi"] = cadena;
        inicio.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";
        inicio.Environment["RELEASE_SHA"] = "fixture-sintetica";
        using var proceso = Process.Start(inicio)!;
        var salida = proceso.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = proceso.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var plazo = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        plazo.CancelAfter(TimeSpan.FromSeconds(30));
        try { await proceso.WaitForExitAsync(plazo.Token); }
        finally { if (!proceso.HasExited) proceso.Kill(entireProcessTree: true); }
        return (proceso.ExitCode, await salida, await error);
    }
}
