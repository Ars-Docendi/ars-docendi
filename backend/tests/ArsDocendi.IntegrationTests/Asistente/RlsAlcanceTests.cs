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
/// Los conteos esperados NO se fijan a mano: se derivan del seed con la conexión
/// del dueño —exenta de las policies— y el predicado de ámbito reescrito acá. Un
/// literal ata el test a la edición de turno del seed y lo pone rojo cada vez que
/// alguien mueve una materia, sin que el alcance haya cambiado en nada.
/// </remarks>
[Collection(ColeccionPostgres.Nombre)]
public sealed class RlsAlcanceTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_rls")
{
    private static readonly Guid Docente = Guid.Parse("a0000000-0000-4000-8000-000000000001");
    private static readonly Guid Jefe = Guid.Parse("a0000000-0000-4000-8000-000000000002");
    private static readonly Guid Coordinador = Guid.Parse("a0000000-0000-4000-8000-000000000003");
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");

    /// <summary>Ámbito del coordinador y del jefe de cátedra en el seed.</summary>
    /// <remarks>
    /// Los identificadores de fixture son estables y reservados por diseño; los
    /// conteos no lo son. Por eso acá se fija el ámbito y el número se cuenta
    /// contra la base.
    /// </remarks>
    private static readonly Guid CarreraInformatica = Guid.Parse("c0000000-0000-4000-8000-000000000201");
    private static readonly Guid MateriaIngenieriaDeSoftware = Guid.Parse("70000000-0000-4000-8000-000000000101");

    /// <summary>Designaciones de la carrera que el coordinador NO tiene.</summary>
    private const string SqlAjenas =
        """
        SELECT count(*)
          FROM designaciones.designaciones d
         WHERE d.materia_id IN (
               SELECT m.id FROM identity.materias m
                WHERE m.carrera_id = 'c0000000-0000-4000-8000-000000000202')
        """;

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

        Assert.Equal(await ContarPedidosDelSeedAsync("TRUE"), global);
        Assert.Equal(
            await ContarPedidosDelSeedAsync("m.carrera_id = @ambito", CarreraInformatica),
            deCarrera);
        Assert.Equal(
            await ContarPedidosDeLasMateriasDeAsync(Jefe),
            deMateria);

        // ACÁ ESTABA EL AGUJERO. La desigualdad `deMateria < global` dependía de que
        // el seed tuviera pedidos fuera del alcance del jefe, y dejó de tenerlos
        // cuando le dieron una tercera materia: con las tres, el jefe ve los ocho
        // pedidos y la desigualdad se cae SIN que la RLS esté rota.
        //
        // Un test cuyo poder de detección depende de cuántas filas tenga el seed no
        // detecta nada de forma confiable. Así que el pedido ajeno se fabrica acá:
        // una materia que el jefe no dicta, un pedido en ella, y la afirmación de
        // que no lo alcanza. Eso vale con cualquier seed.
        var ajeno = await SembrarPedidoFueraDelAlcanceDeAsync(Jefe);

        Assert.NotEqual(Guid.Empty, ajeno);

        // El conteo NO se movió: el pedido nuevo existe y el jefe no lo alcanza. Si
        // la policy dejara de filtrar, acá habría uno más.
        Assert.Equal(deMateria, await ContarComoActorAsync(Jefe, "designaciones.pedidos"));
        Assert.Equal(global + 1, await ContarComoActorAsync(Secretaria, "designaciones.pedidos"));
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
        // LA SONDA VA CONTRA designaciones.designaciones Y NO CONTRA pedidos, y el
        // motivo importa: el seed dejó de tener pedidos fuera de Informática, así que
        // pedirle a esta consulta filas ajenas devolvería cero aunque RLS estuviera
        // apagada. Un test que no puede fallar es peor que uno rojo, porque no avisa.
        // designaciones sí conserva una fila de la otra carrera.
        var ajenasQueExisten = await EscalarAsync<long>(SqlAjenas);
        var ajenasVisibles = await EscalarComoActorAsync<long>(Coordinador, SqlAjenas);

        // La premisa, asertada y no supuesta: si el seed se quedara sin filas ajenas
        // el test volvería a vaciarse en silencio, que es exactamente lo que pasó acá.
        Assert.True(ajenasQueExisten > 0);
        Assert.True(deCarrera > 0);
        Assert.Equal(0, ajenasVisibles);
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

    /// <summary>
    /// Cuenta pedidos con la conexión del dueño —exenta de las policies— y el
    /// predicado de ámbito escrito acá, para usarlo como expectativa.
    /// </summary>
    /// <remarks>
    /// No es tautológico: el predicado se reescribe desde la definición del ámbito
    /// en vez de pasar por <c>identity.asistente_materias_visibles()</c>, que es
    /// justamente lo que se está probando. Si alguien le cambia el ámbito al actor
    /// en el seed, la expectativa deja de coincidir y el test lo dice.
    /// </remarks>
    private async Task<int> ContarPedidosDelSeedAsync(string filtro, Guid? ambito = null)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"""
            SELECT count(*)
              FROM designaciones.pedidos p
              JOIN identity.materias m ON m.id = p.materia_id
             WHERE {filtro}
            """, conexion);

        if (ambito is { } valor)
        {
            comando.Parameters.AddWithValue("ambito", valor);
        }

        return (int)(long)(await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
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

    /// <summary>
    /// Los pedidos de TODAS las materias donde el actor tiene un rol vigente.
    /// </summary>
    /// <remarks>
    /// Reemplaza a clavar una materia. El seed le da al jefe de cátedra más de una,
    /// y contar sólo la primera hacía que el test midiera menos de lo que la policy
    /// deja ver — o sea que fallara con la RLS funcionando perfecto. La pregunta
    /// correcta es la que hace la policy: «los pedidos de las materias del actor».
    /// </remarks>
    private Task<int> ContarPedidosDeLasMateriasDeAsync(Guid actor) =>
        ContarPedidosDelSeedAsync(
            "m.id IN (SELECT ur.materia_id FROM identity.user_roles ur "
            + "WHERE ur.user_id = @ambito AND ur.materia_id IS NOT NULL "
            + "AND ur.deleted_at IS NULL)",
            actor);

    /// <summary>
    /// Crea un pedido en una materia que el actor NO dicta, y devuelve su id.
    /// </summary>
    /// <remarks>
    /// Es lo que le devuelve al test su poder de detección. Antes se apoyaba en que
    /// el seed tuviera filas fuera del alcance; fabricarlo acá hace que la
    /// afirmación valga con cualquier seed, que es lo único que un guard de RLS
    /// puede prometer.
    /// </remarks>
    private async Task<Guid> SembrarPedidoFueraDelAlcanceDeAsync(Guid actor)
    {
        var id = Guid.NewGuid();

        await EjecutarAsync(
            """
            INSERT INTO designaciones.pedidos
                (id, numero, periodo_id, persona_id, materia_id, novedad, estado, prioritario)
            SELECT @id, 'AJENO-' || left(@id::text, 8), per.id, sin_pedido.id,
                   ajena.id, 'Sin novedad', 'borrador', FALSE
              FROM designaciones.periodos per
              -- Una persona SIN pedido en ese período: hay un único pedido por
              -- docente y período, así que reusar una cualquiera choca.
              CROSS JOIN LATERAL (
                  SELECT pe.id
                    FROM identity.personas pe
                   WHERE NOT EXISTS (SELECT FROM designaciones.pedidos x
                                      WHERE x.persona_id = pe.id AND x.periodo_id = per.id)
                   LIMIT 1) sin_pedido
              -- Una materia que el actor NO dicta.
              CROSS JOIN LATERAL (
                  SELECT mm.id
                    FROM identity.materias mm
                   WHERE mm.id NOT IN (SELECT ur.materia_id
                                         FROM identity.user_roles ur
                                        WHERE ur.user_id = @actor
                                          AND ur.materia_id IS NOT NULL
                                          AND ur.deleted_at IS NULL)
                   LIMIT 1) ajena
             WHERE per.activo
             LIMIT 1
            """,
            ("id", id),
            ("actor", actor));

        return id;
    }
}
