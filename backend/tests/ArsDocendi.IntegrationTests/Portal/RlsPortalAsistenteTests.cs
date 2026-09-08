using ArsDocendi.IntegrationTests.Infraestructura;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Portal;

/// <summary>
/// Las policies de RLS que acotan el portal para el asistente.
/// </summary>
/// <remarks>
/// <b>El predicado se prueba con un rol DESCARTABLE, y el estado desplegado con los
/// roles REALES.</b> Son dos preguntas distintas: la policy se puede verificar sin
/// depender de qué se concedió, y lo que hay que verificar del sistema tal como
/// queda es que el GRANT no haya abierto el padrón. El rol descartable es
/// <c>NOSUPERUSER NOBYPASSRLS</c> a propósito: sin las dos cosas, todo pasaría en
/// verde sin que ninguna policy se evaluara.
/// </remarks>
[Collection(ColeccionPostgres.Nombre)]
public sealed class RlsPortalAsistenteTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "portal_rls")
{
    /// <summary>Docente con cuenta y perfil propio en el seed.</summary>
    private static readonly Guid Docente = Guid.Parse("a0000000-0000-4000-8000-000000000001");

    /// <summary>Secretaría: ámbito global, y sin el permiso nuevo por default.</summary>
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");

    private const string PermisoNuevo = "portal.ver_trayectoria_ajena";

    private static readonly string[] Protegidas =
    [
        "perfiles", "educaciones", "certificaciones",
        "experiencias", "docente_habilidades", "habilidades",
    ];

    private string _lector = string.Empty;

    // ------------------------------------------------------------------ la forma

    [Fact]
    public async Task Las_seis_tablas_tienen_RLS_habilitada_sin_FORCE()
    {
        // FORCE sometería también al dueño, y la aplicación conecta como el dueño:
        // `ServicioPortal` dejaría de ver los perfiles que administra.
        var estado = await LeerFilasAsync(
            """
            SELECT c.relname || '|' || c.relrowsecurity || '|' || c.relforcerowsecurity
              FROM pg_class c
              JOIN pg_namespace n ON n.oid = c.relnamespace
             WHERE n.nspname = 'portal' AND c.relname = ANY(@tablas)
             ORDER BY c.relname
            """, ("tablas", Protegidas));

        Assert.Equal(6, estado.Count);
        Assert.All(estado, fila => Assert.EndsWith("|true|false", fila, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Cada_policy_nombra_al_actor_y_no_se_apoya_en_la_del_padre()
    {
        // ES LA PROPIEDAD QUE HACE QUE CADA POLICY SE PUEDA LEER SOLA. Una policy
        // hija cuya única protección sea que la RLS del padre se aplique dentro de su
        // subconsulta hereda comportamiento, no impone una frontera: el día que
        // alguien toque la del padre se lleva puestas cinco sin enterarse.
        var predicados = await LeerFilasAsync(
            "SELECT qual FROM pg_policies WHERE schemaname = 'portal'");

        Assert.Equal(6, predicados.Count);

        // El predicado se mudó a `identity.asistente_alcanza_a`, así que lo que cada
        // policy tiene que nombrar es ESA función y no las dos de antes. La
        // propiedad no cambió: la policy hija sigue invocando el predicado completo
        // por su cuenta, y no depende de que la del padre se aplique adentro de su
        // subconsulta.
        Assert.All(predicados, qual =>
            Assert.Contains("asistente_alcanza_a", qual, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Las_tablas_que_no_se_conceden_no_llevan_policy()
    {
        // contactos, cvs, proyectos y proyecto_documentos quedan fuera del alcance.
        // Una policy sobre una tabla que nadie puede leer daría la impresión de que
        // está contemplada, cuando lo que la protege es que no hay GRANT.
        var conPolicy = await LeerFilasAsync(
            "SELECT DISTINCT tablename FROM pg_policies WHERE schemaname = 'portal' ORDER BY 1");

        Assert.DoesNotContain("contactos", conPolicy);
        Assert.DoesNotContain("cvs", conPolicy);
        Assert.DoesNotContain("proyectos", conPolicy);
        Assert.DoesNotContain("proyecto_documentos", conPolicy);
    }

    // ------------------------------------------------------------------ el alcance

    [Fact]
    public async Task Sin_el_permiso_el_actor_solo_ve_su_propio_perfil()
    {
        await PrepararAsync();

        var total = await ContarComoDuenoAsync("portal.perfiles");
        var propios = await ContarComoActorAsync(Docente, "portal.perfiles");

        // La premisa asertada: si el seed tuviera un solo perfil, «ve uno» no
        // distinguiría nada.
        Assert.True(total > 1, $"El seed tiene {total} perfiles; hace falta más de uno.");
        Assert.Equal(1, propios);
    }

    [Fact]
    public async Task Sin_el_permiso_la_trayectoria_ajena_no_se_ve()
    {
        await PrepararAsync();

        // Secretaría es GLOBAL y aun así no ve nada de portal sin el permiso: es el
        // punto entero de que el ámbito haya quedado fuera del predicado.
        foreach (var tabla in new[] { "educaciones", "certificaciones", "experiencias" })
        {
            var comoDueno = await ContarComoDuenoAsync($"portal.{tabla}");
            var comoActor = await ContarComoActorAsync(Secretaria, $"portal.{tabla}");

            Assert.True(comoDueno > 0, $"portal.{tabla} está vacía en el seed.");
            Assert.Equal(0, comoActor);
        }
    }

    [Fact]
    public async Task Con_el_permiso_el_actor_ve_todo_el_padron()
    {
        await PrepararAsync();
        await ConcederPermisoAsync(Secretaria);

        foreach (var tabla in Protegidas)
        {
            var comoDueno = await ContarComoDuenoAsync($"portal.{tabla}");
            var comoActor = await ContarComoActorAsync(Secretaria, $"portal.{tabla}");

            Assert.Equal(comoDueno, comoActor);
        }
    }

    [Fact]
    public async Task Sin_actor_fijado_no_hay_ninguna_fila_visible()
    {
        // Falla cerrado: sin ajuste no hay actor, sin actor no hay persona ni
        // permiso, y el predicado es falso para toda fila.
        await PrepararAsync();

        await using var conexion = await AbrirComoLectorAsync();

        foreach (var tabla in Protegidas)
        {
            await using var comando = new NpgsqlCommand(
                $"SELECT count(*) FROM portal.{tabla}", conexion);

            Assert.Equal(
                0L, await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task El_vocabulario_de_habilidades_sigue_al_alcance_de_quien_las_declaro()
    {
        // `portal.habilidades` no tiene dueño propio: se ve un término si alguien a
        // quien el actor ya alcanza lo declaró. Para el actor sin permiso eso son sus
        // propias habilidades, que es coherente — el vocabulario que ve es el suyo.
        await PrepararAsync();
        await SembrarHabilidadDeOtroAsync();

        var sinPermiso = await ContarComoActorAsync(Docente, "portal.habilidades");
        var comoDueno = await ContarComoDuenoAsync("portal.habilidades");

        Assert.True(comoDueno > sinPermiso, "El término del otro docente tiene que quedar afuera.");

        // CON EL PERMISO YA NO ALCANZA TODO, y ése es el cambio. El ámbito del
        // docente es una materia, así que ve el vocabulario de quienes están
        // designados ahí — no el del padrón. El vocabulario se angosta con el
        // alcance a propósito: un término raro identifica a una persona por sí solo,
        // así que verlo ya dice que alguien alcanzable lo declaró.
        await ConcederPermisoAsync(Docente);
        var acotado = await ContarComoActorAsync(Docente, "portal.habilidades");

        Assert.True(
            acotado >= sinPermiso,
            $"El permiso no puede quitarle vocabulario: sin permiso {sinPermiso}, con permiso {acotado}.");
        Assert.True(
            acotado < comoDueno,
            $"Un actor de ámbito de materia no puede ver el vocabulario entero: vio {acotado} de {comoDueno}.");

        // Y el ámbito global sí llega al vocabulario completo, que es la otra mitad
        // de la regla: Secretaría, Administración y Decanato alcanzan a todos.
        await ConcederPermisoAsync(Secretaria);
        Assert.Equal(comoDueno, await ContarComoActorAsync(Secretaria, "portal.habilidades"));
    }

    // ------------------------------------- con los roles REALES del asistente

    [Fact]
    public async Task Con_el_GRANT_puesto_el_asistente_no_ve_ningun_perfil_ajeno()
    {
        // ES LA VERIFICACIÓN DE QUE CONCEDER PORTAL NO ABRIÓ EL PADRÓN, y va con los
        // roles REALES del asistente y no con el descartable: acá lo que se prueba no
        // es el predicado sino el estado del sistema tal como queda desplegado.
        //
        // El GRANT da acceso a las tablas; el permiso `portal.ver_trayectoria_ajena`
        // da acceso a las filas ajenas, y está concedido A NADIE. Mientras siga así,
        // cada actor ve exactamente su propio perfil.
        await SembrarAsync();

        var total = await ContarComoDuenoAsync("portal.perfiles");
        Assert.True(total > 1, $"El seed tiene {total} perfiles; hace falta más de uno.");

        foreach (var actor in new[] { Docente, Secretaria })
        {
            await using var conexion = await AbrirConexionComoAsistenteAsync(false);
            await using var transaccion = await conexion.BeginTransactionAsync(
                TestContext.Current.CancellationToken);

            await using (var ajuste = new NpgsqlCommand(
                "SELECT set_config('app.asistente_user_id', @actor, TRUE)",
                conexion, transaccion))
            {
                ajuste.Parameters.AddWithValue("actor", actor.ToString());
                await ajuste.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            await using var comando = new NpgsqlCommand(
                "SELECT count(*) FROM portal.perfiles", conexion, transaccion);

            var visibles = (long)(await comando.ExecuteScalarAsync(
                TestContext.Current.CancellationToken))!;

            Assert.True(
                visibles <= 1,
                $"El actor {actor} ve {visibles} perfiles con el permiso concedido a nadie. "
                + "Tiene que ver a lo sumo el propio.");
        }
    }

    [Fact]
    public async Task El_asistente_no_llega_a_las_tablas_que_no_se_le_conceden()
    {
        // La otra mitad de la frontera: contacto personal, CV y proyectos no se
        // conceden, así que el motor rechaza antes de evaluar ninguna policy. Es
        // permission denied y no cero filas, que es la diferencia entre una tabla
        // fuera de alcance y una vacía.
        await SembrarAsync();

        foreach (var tabla in new[] { "contactos", "cvs", "proyectos", "proyecto_documentos" })
        {
            await using var conexion = await AbrirConexionComoAsistenteAsync(true);
            await using var comando = new NpgsqlCommand(
                $"SELECT count(*) FROM portal.{tabla}", conexion);

            var error = await Assert.ThrowsAsync<PostgresException>(
                () => comando.ExecuteScalarAsync(TestContext.Current.CancellationToken));

            // 42501 es insufficient_privilege.
            Assert.Equal("42501", error.SqlState);
        }
    }

    // ------------------------------------------------------------------ no regresión

    [Fact]
    public async Task El_dueno_de_las_tablas_sigue_viendo_todas_las_filas()
    {
        // Es la garantía de que esta migración no le saca visibilidad a la API REST,
        // que conecta como el dueño. Con FORCE en vez de ENABLE, esto daría cero.
        await PrepararAsync();

        Assert.True(await ContarComoDuenoAsync("portal.perfiles") > 1);
    }

    // ------------------------------------------------------------------ apoyo

    private async Task PrepararAsync()
    {
        await SembrarAsync();

        _lector = $"lec_t{Guid.NewGuid():N}"[..20];

        await EjecutarAsync(
            $"""
            CREATE ROLE "{_lector}" WITH LOGIN PASSWORD 'lector-de-prueba'
                NOSUPERUSER NOBYPASSRLS NOINHERIT;
            GRANT USAGE ON SCHEMA portal, identity TO "{_lector}";
            GRANT SELECT ON ALL TABLES IN SCHEMA portal TO "{_lector}";
            GRANT EXECUTE ON FUNCTION identity.asistente_actor() TO "{_lector}";
            GRANT EXECUTE ON FUNCTION identity.asistente_persona() TO "{_lector}";
            GRANT EXECUTE ON FUNCTION identity.asistente_tiene_permiso(TEXT) TO "{_lector}";
            GRANT EXECUTE ON FUNCTION identity.asistente_es_global() TO "{_lector}";
            GRANT EXECUTE ON FUNCTION identity.asistente_materias_visibles() TO "{_lector}";
            GRANT EXECUTE ON FUNCTION identity.asistente_alcanza_a(UUID) TO "{_lector}";
            GRANT USAGE ON SCHEMA designaciones TO "{_lector}";
            """);
    }

    private async Task ConcederPermisoAsync(Guid actor) =>
        await EjecutarAsync(
            $"""
            INSERT INTO identity.rol_permisos (rol_id, permiso_id)
            SELECT ur.role_id, p.id
              FROM identity.user_roles ur
             CROSS JOIN identity.permisos p
             WHERE ur.user_id = '{actor}'
               AND ur.deleted_at IS NULL
               AND p.code = '{PermisoNuevo}'
            ON CONFLICT DO NOTHING;
            """);

    /// <summary>Le carga una habilidad a un docente que NO es el del test.</summary>
    private async Task SembrarHabilidadDeOtroAsync() =>
        await EjecutarAsync(
            """
            WITH nueva AS (
                INSERT INTO portal.habilidades (termino, termino_norm)
                VALUES ('Operación de reactor RA-6', 'operacion de reactor ra-6')
                RETURNING id
            )
            INSERT INTO portal.docente_habilidades (perfil_id, habilidad_id, tipo)
            SELECT pf.id, nueva.id, 'habilidad'
              FROM nueva
              JOIN portal.perfiles pf
                ON pf.persona_id = 'd0000000-0000-4000-8000-000000000003';
            """);

    private async Task<long> ContarComoDuenoAsync(string tabla)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand($"SELECT count(*) FROM {tabla}", conexion);
        return (long)(await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private async Task<long> ContarComoActorAsync(Guid actor, string tabla)
    {
        await using var conexion = await AbrirComoLectorAsync();
        await using var transaccion = await conexion.BeginTransactionAsync(
            TestContext.Current.CancellationToken);

        await using (var ajuste = new NpgsqlCommand(
            "SELECT set_config('app.asistente_user_id', @actor, TRUE)", conexion, transaccion))
        {
            ajuste.Parameters.AddWithValue("actor", actor.ToString());
            await ajuste.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await using var comando = new NpgsqlCommand(
            $"SELECT count(*) FROM {tabla}", conexion, transaccion);

        return (long)(await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private async Task<NpgsqlConnection> AbrirComoLectorAsync()
    {
        var cadena = new NpgsqlConnectionStringBuilder(Cadena)
        {
            Username = _lector,
            Password = "lector-de-prueba",
            Pooling = false,
        }.ConnectionString;

        var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync(TestContext.Current.CancellationToken);
        return conexion;
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

        await using var lector = await comando.ExecuteReaderAsync(
            TestContext.Current.CancellationToken);

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
