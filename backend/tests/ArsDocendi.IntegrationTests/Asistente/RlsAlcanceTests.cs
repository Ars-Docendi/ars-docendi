using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Auth;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica las policies de RLS sobre las cuatro tablas del trámite.
/// </summary>
/// <remarks>
/// Todas las consultas de alcance corren conectadas como el rol de solo lectura
/// del asistente, no como el dueño. Es la única forma de probar un límite que
/// impone el motor: el dueño está exento de las policies, así que consultarlo
/// desde su conexión no probaría nada.
///
/// Datos del seed sintético: la carrera INF tiene cuatro materias y siete pedidos;
/// la carrera IND tiene una materia y un pedido. Ocho en total.
/// </remarks>
[Collection(ColeccionPostgres.Nombre)]
public sealed class RlsAlcanceTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_rls")
{
    private static readonly Guid Docente = Guid.Parse("a0000000-0000-4000-8000-000000000001");
    private static readonly Guid Jefe = Guid.Parse("a0000000-0000-4000-8000-000000000002");
    private static readonly Guid Coordinador = Guid.Parse("a0000000-0000-4000-8000-000000000003");
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");

    private static readonly string[] Protegidas =
    [
        "pedidos", "designaciones", "pedido_historial", "pedido_adjuntos",
    ];

    // ------------------------------------------------------------------- la forma

    [Fact]
    public async Task Las_cuatro_tablas_tienen_RLS_habilitada_sin_FORCE()
    {
        var estado = await LeerFilasAsync(
            """
            SELECT c.relname || '|' || c.relrowsecurity || '|' || c.relforcerowsecurity
              FROM pg_class c
              JOIN pg_namespace n ON n.oid = c.relnamespace
             WHERE n.nspname = 'designaciones' AND c.relname = ANY(@tablas)
             ORDER BY c.relname
            """, ("tablas", Protegidas));

        // FORCE sometería también al dueño. La aplicación conecta como el dueño y
        // estas policies son FOR SELECT y están escritas para el actor del
        // asistente: forzarlo no endurece nada, tira el backend entero.
        Assert.Equal(4, estado.Count);
        Assert.All(estado, fila => Assert.EndsWith("|true|false", fila, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Cada_tabla_protegida_tiene_su_policy_de_solo_lectura()
    {
        var policies = await LeerFilasAsync(
            """
            SELECT tablename || '|' || policyname || '|' || cmd
              FROM pg_policies
             WHERE schemaname = 'designaciones'
             ORDER BY tablename
            """);

        Assert.Equal(4, policies.Count);
        Assert.All(policies, fila => Assert.EndsWith("|SELECT", fila, StringComparison.Ordinal));
    }

    [Fact]
    public async Task El_predicado_conjunta_el_permiso_con_el_alcance()
    {
        var predicados = await LeerFilasAsync(
            "SELECT qual FROM pg_policies WHERE schemaname = 'designaciones'");

        // Sin el permiso adentro del predicado, un rol con ámbito de materia pero
        // sin designaciones.ver recibiría pedidos e historial que la API le niega.
        Assert.Equal(4, predicados.Count);
        Assert.All(predicados, qual =>
        {
            Assert.Contains("asistente_tiene_permiso", qual, StringComparison.Ordinal);
            Assert.Contains("asistente_materias_visibles", qual, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void La_migracion_explica_por_que_no_se_usa_FORCE()
    {
        var sql = File.ReadAllText(Path.Combine(
            RaizRepositorio.Ruta(), "database", "designaciones",
            "009_designaciones_rls_asistente.sql"));

        Assert.Contains("FORCE ROW LEVEL SECURITY", sql, StringComparison.Ordinal);
        Assert.Contains("dueño", sql, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ el alcance

    [Fact]
    public async Task El_alcance_global_ve_todos_los_pedidos_y_el_de_carrera_solo_los_suyos()
    {
        await SembrarAsync();

        var global = await ContarComoActorAsync(Secretaria, "designaciones.pedidos");
        var deCarrera = await ContarComoActorAsync(Coordinador, "designaciones.pedidos");
        var deMateria = await ContarComoActorAsync(Jefe, "designaciones.pedidos");

        Assert.Equal(8, global);
        Assert.Equal(7, deCarrera);   // INF: cuatro materias, siete pedidos
        Assert.Equal(2, deMateria);   // solo Ingeniería de Software
    }

    [Fact]
    public async Task Un_actor_con_ambito_pero_sin_el_permiso_de_dominio_no_ve_nada()
    {
        await SembrarAsync();

        // El docente tiene ámbito sobre Ingeniería de Software, donde hay pedidos.
        // Sus permisos son portal.ver y portal.editar: nada de designaciones.
        Assert.False(await TienePermisoAsync(Docente, Permisos.DesignacionesVer));

        foreach (var tabla in Protegidas)
        {
            Assert.Equal(0, await ContarComoActorAsync(Docente, $"designaciones.{tabla}"));
        }
    }

    [Fact]
    public async Task Sin_actor_fijado_no_hay_ninguna_fila_visible()
    {
        await SembrarAsync();

        // Falla cerrado: sin el ajuste no hay actor, sin actor no hay permiso, y sin
        // permiso el predicado es falso para toda fila.
        await using var conexion = await AbrirConexionComoAsistenteAsync(false);
        foreach (var tabla in Protegidas)
        {
            await using var comando = new NpgsqlCommand(
                $"SELECT count(*) FROM designaciones.{tabla}", conexion);
            Assert.Equal(0L, await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task Una_consulta_que_une_las_cuatro_tablas_respeta_el_alcance_en_todas()
    {
        await SembrarAsync();

        var deCarrera = await EscalarComoActorAsync<long>(
            Coordinador,
            """
            SELECT count(*)
              FROM designaciones.pedidos p
              LEFT JOIN designaciones.pedido_historial h ON h.pedido_id = p.id
              LEFT JOIN designaciones.pedido_adjuntos a ON a.pedido_id = p.id
              LEFT JOIN designaciones.designaciones d ON d.materia_id = p.materia_id
             WHERE p.materia_id NOT IN (
                   SELECT m.id FROM identity.materias m
                    WHERE m.carrera_id = 'c0000000-0000-4000-8000-000000000202')
            """);
        var fueraDeAlcance = await EscalarComoActorAsync<long>(
            Coordinador,
            """
            SELECT count(*)
              FROM designaciones.pedidos p
             WHERE p.materia_id IN (
                   SELECT m.id FROM identity.materias m
                    WHERE m.carrera_id = 'c0000000-0000-4000-8000-000000000202')
            """);

        // Nada de la otra carrera se cuela por ninguna de las cuatro puertas.
        Assert.True(deCarrera > 0);
        Assert.Equal(0, fueraDeAlcance);
    }

    // ----------------------------------------------------------------- no regresión

    [Fact]
    public async Task El_rol_dueno_de_las_tablas_sigue_viendo_todas_las_filas()
    {
        await SembrarAsync();
        var duenoDePrueba = $"app_t{Guid.NewGuid():N}"[..20];
        await CrearDuenoNoSuperusuarioAsync(duenoDePrueba);

        var cadena = new NpgsqlConnectionStringBuilder(Cadena)
        {
            Username = duenoDePrueba,
            Password = "dueno-de-prueba",
            Pooling = false,
        }.ConnectionString;

        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync(TestContext.Current.CancellationToken);

        // Sin fijar ningún actor: el dueño está exento de las policies porque las
        // tablas llevan ENABLE y no FORCE. Es la garantía de que esta migración no
        // le saca visibilidad a la aplicación.
        await using var comando = new NpgsqlCommand(
            "SELECT count(*) FROM designaciones.pedidos", conexion);

        Assert.Equal(8L, await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    // ------------------------------------------------------------------------ apoyo

    private async Task CrearDuenoNoSuperusuarioAsync(string rol)
    {
        await EjecutarAsync(
            $"""
            CREATE ROLE "{rol}" WITH LOGIN PASSWORD 'dueno-de-prueba' NOSUPERUSER NOBYPASSRLS;
            GRANT USAGE, CREATE ON SCHEMA designaciones TO "{rol}";
            GRANT USAGE ON SCHEMA identity TO "{rol}";
            ALTER TABLE designaciones.pedidos OWNER TO "{rol}";
            """);
    }

    private Task<bool> TienePermisoAsync(Guid actor, string permiso) =>
        EscalarComoActorAsync<bool>(actor, $"SELECT identity.asistente_tiene_permiso('{permiso}')");

    private async Task<int> ContarComoActorAsync(Guid actor, string tabla) =>
        (int)await EscalarComoActorAsync<long>(actor, $"SELECT count(*) FROM {tabla}");

    /// <summary>
    /// Corre la consulta como el rol de solo lectura del asistente, con el actor
    /// fijado transaction-local, igual que un turno real.
    /// </summary>
    private async Task<T> EscalarComoActorAsync<T>(Guid actor, string sql)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var conexion = await AbrirConexionComoAsistenteAsync(false);
        await using var transaccion = await conexion.BeginTransactionAsync(ct);

        await using (var guc = new NpgsqlCommand(
            "SELECT set_config('app.asistente_user_id', @actor, true)", conexion, transaccion))
        {
            guc.Parameters.AddWithValue("actor", actor.ToString());
            await guc.ExecuteNonQueryAsync(ct);
        }

        await using var comando = new NpgsqlCommand(sql, conexion, transaccion);
        return (T)(await comando.ExecuteScalarAsync(ct))!;
    }

    private async Task SembrarAsync()
    {
        var sql = await File.ReadAllTextAsync(
            Path.Combine(RaizRepositorio.Ruta(), "infra", "scripts", "seed-data", "sintetico.sql"),
            TestContext.Current.CancellationToken);
        await EjecutarAsync(sql);
    }

    private async Task<IReadOnlyList<string>> LeerFilasAsync(
        string sql, params (string Nombre, object Valor)[] parametros)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        foreach (var (nombre, valor) in parametros)
        {
            comando.Parameters.AddWithValue(nombre, valor);
        }

        await using var lector = await comando.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var filas = new List<string>();
        while (await lector.ReadAsync(TestContext.Current.CancellationToken))
        {
            filas.Add(lector.GetString(0));
        }

        return filas;
    }

    private async Task EjecutarAsync(string sql)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion) { CommandTimeout = 60 };
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
