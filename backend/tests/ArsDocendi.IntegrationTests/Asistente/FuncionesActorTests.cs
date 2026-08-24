using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Auth;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica las cuatro funciones de resolución del actor del asistente.
/// </summary>
/// <remarks>
/// Cada llamada corre dentro de su propia transacción, con el ajuste
/// <c>app.asistente_user_id</c> fijado como transaction-local, igual que en
/// producción: el ajuste muere en el COMMIT y no sobrevive al pool de conexiones.
/// </remarks>
[Collection(ColeccionPostgres.Nombre)]
public sealed class FuncionesActorTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_funciones")
{
    private static readonly Guid Docente = Guid.Parse("a0000000-0000-4000-8000-000000000001");
    private static readonly Guid Jefe = Guid.Parse("a0000000-0000-4000-8000-000000000002");
    private static readonly Guid Coordinador = Guid.Parse("a0000000-0000-4000-8000-000000000003");
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private static readonly Guid MateriaDelJefe = Guid.Parse("70000000-0000-4000-8000-000000000101");
    private static readonly Guid CarreraDelCoordinador = Guid.Parse("c0000000-0000-4000-8000-000000000201");

    private static readonly string[] Funciones =
    [
        "asistente_actor", "asistente_es_global",
        "asistente_materias_visibles", "asistente_tiene_permiso",
    ];

    /// <summary>Los siete códigos de rol de sistema del catálogo.</summary>
    private static readonly string[] CodigosDeRol =
    [
        "docente", "jefe_catedra", "coordinador_carrera",
        "secretaria", "decanato", "administrativo", "sys_admin",
    ];

    // -------------------------------------------------------------------- la forma

    [Fact]
    public async Task Las_cuatro_funciones_existen_y_son_security_definer()
    {
        var declaradas = await LeerColumnaAsync(
            """
            SELECT p.proname
              FROM pg_proc p
              JOIN pg_namespace n ON n.oid = p.pronamespace
             WHERE n.nspname = 'identity'
               AND p.proname = ANY(@nombres)
               AND p.prosecdef
             ORDER BY p.proname
            """, ("nombres", Funciones));

        Assert.Equal(Funciones.Order(), declaradas.Order());
    }

    [Fact]
    public async Task Ninguna_funcion_lleva_un_codigo_de_rol_embebido()
    {
        // Una lista negra (`code <> 'docente'`) falla ABIERTA: cualquier rol nuevo
        // pasaría por default. Una blanca falla cerrada pero obliga a desplegar cada
        // vez que el cliente crea un rol. Por eso se pregunta por el permiso.
        var cuerpos = await LeerColumnaAsync(
            """
            SELECT p.proname || ' :: ' || p.prosrc
              FROM pg_proc p
              JOIN pg_namespace n ON n.oid = p.pronamespace
             WHERE n.nspname = 'identity' AND p.proname = ANY(@nombres)
            """, ("nombres", Funciones));

        Assert.NotEmpty(cuerpos);
        var infracciones = cuerpos
            .Where(cuerpo => CodigosDeRol.Any(codigo =>
                cuerpo.Contains($"'{codigo}'", StringComparison.Ordinal)))
            .ToArray();

        Assert.True(infracciones.Length == 0, string.Join("\n", infracciones));
    }

    [Fact]
    public async Task PUBLIC_no_puede_ejecutar_ninguna_de_las_cuatro()
    {
        string[] firmas =
        [
            "identity.asistente_actor()",
            "identity.asistente_es_global()",
            "identity.asistente_materias_visibles()",
            "identity.asistente_tiene_permiso(text)",
        ];

        foreach (var firma in firmas)
        {
            var puede = await EscalarAsync<bool>(
                $"SELECT has_function_privilege('public', '{firma}', 'EXECUTE')");

            Assert.False(puede, $"PUBLIC no debería poder ejecutar {firma}.");
        }
    }

    [Fact]
    public async Task Los_dos_roles_del_asistente_si_pueden_ejecutarlas()
    {
        foreach (var rol in new[] { RolSoloLectura, RolSoloLecturaPii })
        {
            var puede = await EscalarAsync<bool>(
                $"SELECT has_function_privilege('{rol}', 'identity.asistente_tiene_permiso(text)', 'EXECUTE')");

            Assert.True(puede, $"{rol} debería poder ejecutar la función de permiso.");
        }
    }

    // ------------------------------------------------------------------ el actor

    [Fact]
    public async Task Sin_ajuste_el_actor_es_nulo()
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand("SELECT identity.asistente_actor()", conexion);

        var actor = await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        // Sin actor no hay filas visibles: es el default correcto.
        Assert.Equal(DBNull.Value, actor);
    }

    [Fact]
    public async Task Un_ajuste_que_no_es_UUID_rompe_con_un_mensaje_claro()
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            ComoActorCrudoAsync("no-es-un-uuid", "SELECT identity.asistente_actor()"));

        Assert.Contains("app.asistente_user_id", error.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_UUID_que_no_es_un_usuario_activo_rompe_en_vez_de_devolver_vacio()
    {
        // Es el caso del `oid` de Azure AD: también es un UUID válido. Devolver cero
        // filas en silencio sería una respuesta falsa, y la métrica del asistente es
        // corrección con abstención.
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            ComoActorCrudoAsync(Guid.NewGuid().ToString(), "SELECT identity.asistente_actor()"));

        Assert.Contains("Azure AD", error.MessageText, StringComparison.Ordinal);
    }

    // ----------------------------------------------------------------- el alcance

    [Fact]
    public async Task El_alcance_global_distingue_a_secretaria_del_jefe_de_catedra()
    {
        await SembrarAsync();

        Assert.True(await ComoActorAsync<bool>(Secretaria, "SELECT identity.asistente_es_global()"));
        Assert.False(await ComoActorAsync<bool>(Jefe, "SELECT identity.asistente_es_global()"));
    }

    [Fact]
    public async Task Las_materias_visibles_dependen_del_alcance()
    {
        await SembrarAsync();
        var todas = await EscalarAsync<long>("SELECT count(*) FROM identity.materias");

        var deSecretaria = await ComoActorAsync<long>(
            Secretaria, "SELECT count(*) FROM identity.asistente_materias_visibles()");
        var delJefe = await ComoActorListaAsync(
            Jefe, "SELECT * FROM identity.asistente_materias_visibles()");
        var delCoordinador = await ComoActorListaAsync(
            Coordinador, "SELECT * FROM identity.asistente_materias_visibles()");
        var materiasDeLaCarrera = await LeerColumnaAsync(
            "SELECT id::text FROM identity.materias WHERE carrera_id = @carrera",
            ("carrera", CarreraDelCoordinador));

        Assert.Equal(todas, deSecretaria);
        Assert.Equal([MateriaDelJefe.ToString()], delJefe);
        Assert.Equal(materiasDeLaCarrera.Order(), delCoordinador.Order());
    }

    [Fact]
    public async Task Una_asignacion_dada_de_baja_no_suma_alcance()
    {
        await SembrarAsync();
        Assert.NotEmpty(await ComoActorListaAsync(
            Jefe, "SELECT * FROM identity.asistente_materias_visibles()"));

        await EjecutarAsync(
            "UPDATE identity.user_roles SET deleted_at = now() WHERE user_id = @usuario",
            ("usuario", Jefe));

        Assert.Empty(await ComoActorListaAsync(
            Jefe, "SELECT * FROM identity.asistente_materias_visibles()"));
    }

    // ---------------------------------------------------------------- el permiso

    [Fact]
    public async Task Revocar_el_permiso_al_rol_cambia_el_resultado_sin_reiniciar()
    {
        await SembrarAsync();
        Assert.True(await TienePermisoAsync(Jefe, Permisos.AsistenteConsultar));

        await EjecutarAsync(
            """
            DELETE FROM identity.rol_permisos
             WHERE rol_id = (SELECT id FROM identity.roles WHERE code = 'jefe_catedra')
               AND permiso_id = (SELECT id FROM identity.permisos WHERE code = @code)
            """, ("code", Permisos.AsistenteConsultar));

        // Mismo proceso, misma conexión de test: la matriz se lee en cada llamada.
        Assert.False(await TienePermisoAsync(Jefe, Permisos.AsistenteConsultar));
    }

    [Fact]
    public async Task El_docente_queda_excluido_por_no_tener_el_permiso()
    {
        await SembrarAsync();

        Assert.False(await TienePermisoAsync(Docente, Permisos.AsistenteConsultar));
    }

    [Fact]
    public async Task Un_rol_creado_por_la_administracion_con_el_permiso_queda_habilitado()
    {
        await SembrarAsync();
        var usuario = await CrearUsuarioConRolNuevoAsync("coordinador_adjunto", conPermiso: true);

        Assert.True(await TienePermisoAsync(usuario, Permisos.AsistenteConsultar));
    }

    [Fact]
    public async Task Un_rol_creado_por_la_administracion_sin_el_permiso_queda_excluido()
    {
        await SembrarAsync();
        var usuario = await CrearUsuarioConRolNuevoAsync("observador_externo", conPermiso: false);

        // El caso que una lista negra de roles dejaría pasar: un rol que nadie
        // conocía al escribir el código, sin el permiso, tendría acceso igual.
        Assert.False(await TienePermisoAsync(usuario, Permisos.AsistenteConsultar));
    }

    // ------------------------------------------------------------- el plan

    [Fact]
    public async Task El_predicado_se_resuelve_una_vez_por_consulta_y_no_por_fila()
    {
        var plan = string.Join(
            "\n",
            await LeerColumnaAsync(
                "EXPLAIN SELECT id FROM identity.materias WHERE identity.asistente_es_global()"));

        // STABLE hace que un predicado sin columnas sea pseudo-constante y el
        // ejecutor lo resuelva UNA vez por consulta.
        Assert.Contains("One-Time Filter", plan, StringComparison.Ordinal);
    }

    [Fact]
    public async Task La_misma_funcion_declarada_VOLATILE_se_reevalua_por_fila()
    {
        // Contraprueba del test anterior: sin STABLE, el mismo predicado deja de ser
        // pseudo-constante y pasa a evaluarse fila por fila. Es la diferencia que
        // justifica la marca, no una preferencia de estilo.
        await EjecutarAsync(
            """
            CREATE FUNCTION identity.asistente_es_global_volatil()
            RETURNS BOOLEAN LANGUAGE sql VOLATILE SECURITY DEFINER SET search_path = ''
            AS 'SELECT TRUE';
            """);

        var plan = string.Join(
            "\n",
            await LeerColumnaAsync(
                "EXPLAIN SELECT id FROM identity.materias WHERE identity.asistente_es_global_volatil()"));

        Assert.DoesNotContain("One-Time Filter", plan, StringComparison.Ordinal);
        Assert.Contains("Filter", plan, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------------ apoyo

    private Task<bool> TienePermisoAsync(Guid actor, string permiso) =>
        ComoActorAsync<bool>(actor, $"SELECT identity.asistente_tiene_permiso('{permiso}')");

    /// <summary>
    /// Corre una consulta dentro de una transacción con el ajuste del actor fijado
    /// como transaction-local, igual que la aplicación en cada turno.
    /// </summary>
    private async Task<T> ComoActorAsync<T>(Guid actor, string sql)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var transaccion = await conexion.BeginTransactionAsync(
            TestContext.Current.CancellationToken);
        await FijarActorAsync(conexion, transaccion, actor.ToString());

        await using var comando = new NpgsqlCommand(sql, conexion, transaccion);
        return (T)(await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private async Task<IReadOnlyList<string>> ComoActorListaAsync(Guid actor, string sql)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var transaccion = await conexion.BeginTransactionAsync(
            TestContext.Current.CancellationToken);
        await FijarActorAsync(conexion, transaccion, actor.ToString());

        await using var comando = new NpgsqlCommand(sql, conexion, transaccion);
        await using var lector = await comando.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        var valores = new List<string>();
        while (await lector.ReadAsync(TestContext.Current.CancellationToken))
        {
            valores.Add(lector.GetGuid(0).ToString());
        }

        return valores;
    }

    private async Task ComoActorCrudoAsync(string crudo, string sql)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var transaccion = await conexion.BeginTransactionAsync(
            TestContext.Current.CancellationToken);
        await FijarActorAsync(conexion, transaccion, crudo);

        await using var comando = new NpgsqlCommand(sql, conexion, transaccion);
        await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }

    private static async Task FijarActorAsync(
        NpgsqlConnection conexion, NpgsqlTransaction transaccion, string valor)
    {
        await using var comando = new NpgsqlCommand(
            "SELECT set_config('app.asistente_user_id', @actor, true)", conexion, transaccion);
        comando.Parameters.AddWithValue("actor", valor);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Guid> CrearUsuarioConRolNuevoAsync(string codigoDeRol, bool conPermiso)
    {
        var usuario = Guid.NewGuid();
        var rol = Guid.NewGuid();
        await EjecutarAsync(
            """
            INSERT INTO identity.users (id, azure_oid, upn, display_name)
                VALUES (@usuario, @oid, @upn, 'Rol nuevo de prueba');
            INSERT INTO identity.roles (id, code, name, scope, es_sistema)
                VALUES (@rol, @codigo, 'Rol creado por administración', 'global', FALSE);
            INSERT INTO identity.user_roles (user_id, role_id)
                VALUES (@usuario, @rol);
            """,
            ("usuario", usuario), ("oid", Guid.NewGuid()),
            ("upn", $"{usuario:N}@unlam.edu.ar"), ("rol", rol), ("codigo", codigoDeRol));

        if (conPermiso)
        {
            await EjecutarAsync(
                """
                INSERT INTO identity.rol_permisos (rol_id, permiso_id)
                SELECT @rol, id FROM identity.permisos WHERE code = @code
                """, ("rol", rol), ("code", Permisos.AsistenteConsultar));
        }

        return usuario;
    }

    private async Task SembrarAsync()
    {
        var sql = await File.ReadAllTextAsync(
            Path.Combine(RaizRepositorio.Ruta(), "infra", "scripts", "seed-data", "sintetico.sql"),
            TestContext.Current.CancellationToken);
        await EjecutarAsync(sql);
    }

    private async Task<IReadOnlyList<string>> LeerColumnaAsync(
        string sql, params (string Nombre, object Valor)[] parametros)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = Preparar(conexion, sql, parametros);
        await using var lector = await comando.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        var valores = new List<string>();
        while (await lector.ReadAsync(TestContext.Current.CancellationToken))
        {
            valores.Add(lector.GetString(0));
        }

        return valores;
    }

    private async Task<T> EscalarAsync<T>(string sql, params (string Nombre, object Valor)[] parametros)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = Preparar(conexion, sql, parametros);
        return (T)(await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private async Task EjecutarAsync(string sql, params (string Nombre, object Valor)[] parametros)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = Preparar(conexion, sql, parametros);
        comando.CommandTimeout = 60;
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static NpgsqlCommand Preparar(
        NpgsqlConnection conexion, string sql, (string Nombre, object Valor)[] parametros)
    {
        var comando = new NpgsqlCommand(sql, conexion);
        foreach (var (nombre, valor) in parametros)
        {
            comando.Parameters.AddWithValue(nombre, valor);
        }

        return comando;
    }
}
