using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica el catálogo de capacidades por actor (ARS-49).
/// </summary>
/// <remarks>
/// El invariante de privacidad de esta pieza es el más importante de la épica:
/// <b>el catálogo se deriva de los GRANT efectivos y nunca del payload del
/// prompt</b>. El prefijo trae el esquema entero, columnas personales incluidas; un
/// catálogo derivado de ahí le ofrecería a cualquiera preguntas sobre columnas que
/// su rol no puede leer.
/// </remarks>
public sealed class CapacidadesTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_capacidades")
{
    /// <summary>Alcance global y acceso a datos personales.</summary>
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");

    /// <summary>Ámbito de carrera: sin acceso a datos personales.</summary>
    private static readonly Guid Coordinador = Guid.Parse("a0000000-0000-4000-8000-000000000003");

    /// <summary>Ámbito de materia: mismo acceso a datos que el coordinador.</summary>
    private static readonly Guid Jefe = Guid.Parse("a0000000-0000-4000-8000-000000000002");

    /// <summary>Ámbito de materia también, y con eso el mismo alcance que el jefe.</summary>
    private static readonly Guid Docente = Guid.Parse("a0000000-0000-4000-8000-000000000001");

    /// <summary>
    /// <c>sys_admin</c>: un rol de sistema que el catálogo de presentaciones no
    /// nombra. Es el caso «rol desconocido» con datos reales, sin fixture propia.
    /// </summary>
    private static readonly Guid Sistemas = Guid.Parse("a0000000-0000-4000-8000-000000000007");

    /// <summary>
    /// El rol de Secretaría Académica, de alcance global. No es un actor: es el
    /// SEGUNDO rol que el test le suma a un actor que ya tiene uno, para armarse el
    /// caso multirol que el seed compartido dejó de traer.
    /// </summary>
    private static readonly Guid RolSecretaria = Guid.Parse("a1000000-0000-4000-8000-000000000004");

    private static readonly string[] ColumnasPersonales =
        ["documento", "cuil", "fecha_nacimiento", "telefono", "upn"];

    // ------------------------------------------------- derivado de los GRANT

    [Fact]
    public async Task El_catalogo_de_un_rol_basico_no_cuenta_ninguna_columna_personal()
    {
        // La primera versión de este test miraba el TEXTO redactado buscando
        // «documento», y pasaba aunque el catálogo se armara con la conexión de datos
        // personales: la redacción nunca nombra columnas, así que no había nada que
        // encontrar. Ahora se mira el conteo de la tabla que tiene las columnas
        // personales, que es lo único que se movería si el catálogo dejara de
        // derivarse del rol del actor.
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;
        var catalogo = Catalogo();

        var sinPii = await catalogo.ObtenerAsync(Coordinador, ct);
        var conPii = await catalogo.ObtenerAsync(Secretaria, ct);

        var personasBasica = Assert.Single(
            sinPii.Cubre, a => a.Nombre == "identity.personas");
        var personasConPii = Assert.Single(
            conPii.Cubre, a => a.Nombre == "identity.personas");

        // Cuatro de las cinco columnas personales están en `identity.personas`
        // —documento, cuil, fecha_nacimiento y teléfono—; la quinta, `upn`, está en
        // `identity.users`.
        Assert.Equal(4, personasConPii.Columnas - personasBasica.Columnas);

        var usuariosBasica = Assert.Single(sinPii.Cubre, a => a.Nombre == "identity.users");
        var usuariosConPii = Assert.Single(conPii.Cubre, a => a.Nombre == "identity.users");

        Assert.Equal(1, usuariosConPii.Columnas - usuariosBasica.Columnas);
    }

    [Fact]
    public async Task Dos_actores_con_acceso_distinto_reciben_conteos_distintos()
    {
        // ES EL GATE DE LA ÉPICA. Si los dos vieran lo mismo, el catálogo no estaría
        // derivándose de los privilegios sino de algo compartido.
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;
        var catalogo = Catalogo();

        var conPii = await catalogo.ObtenerAsync(Secretaria, ct);
        var sinPii = await catalogo.ObtenerAsync(Coordinador, ct);

        Assert.True(conPii.Columnas > sinPii.Columnas,
            $"Con datos personales: {conPii.Columnas}; sin: {sinPii.Columnas}.");

        // Cinco columnas exactamente: documento, cuil, fecha_nacimiento, telefono y upn.
        Assert.Equal(5, conPii.Columnas - sinPii.Columnas);
    }

    [Fact]
    public async Task Los_conteos_coinciden_con_lo_que_el_rol_puede_leer()
    {
        await SembrarAsync();

        var puede = await Catalogo().ObtenerAsync(
            Coordinador, TestContext.Current.CancellationToken);

        var (basica, _) = CadenasDeLectura();
        await using var conexion = new NpgsqlConnection(basica.Valor);
        await conexion.OpenAsync(TestContext.Current.CancellationToken);

        var legibles = await LectorDeCatalogo.LeerColumnasAsync(
            conexion, TestContext.Current.CancellationToken);

        Assert.Equal(legibles.Count, puede.Columnas);
        Assert.Equal(
            legibles.Select(c => $"{c.Esquema}.{c.Tabla}").Distinct().Count(),
            puede.Tablas);
    }

    [Fact]
    public async Task El_catalogo_no_ofrece_el_schema_propio_del_asistente()
    {
        // Los dos registros del asistente están revocados a sus propios roles. Si
        // aparecieran acá, el catálogo estaría ofreciendo consultar el texto de las
        // preguntas de todos los demás.
        await SembrarAsync();

        var puede = await Catalogo().ObtenerAsync(
            Secretaria, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(puede.Cubre, area =>
            area.Nombre.StartsWith("asistente.", StringComparison.Ordinal));
        Assert.DoesNotContain(puede.Cubre, area =>
            area.Nombre.StartsWith("audit.", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------- los ejemplos

    [Fact]
    public async Task Los_ejemplos_salen_del_catalogo_verificado()
    {
        await SembrarAsync();
        var verificados = new SelectorDeEjemplos().Catalogo
            .Select(e => e.Pregunta)
            .ToHashSet(StringComparer.Ordinal);

        var puede = await Catalogo().ObtenerAsync(
            Secretaria, TestContext.Current.CancellationToken);

        Assert.NotEmpty(puede.Ejemplos);
        Assert.All(puede.Ejemplos, ejemplo => Assert.Contains(ejemplo, verificados));
    }

    [Fact]
    public async Task Se_ofrecen_entre_cuatro_y_seis_ejemplos()
    {
        await SembrarAsync();

        var puede = await Catalogo().ObtenerAsync(
            Secretaria, TestContext.Current.CancellationToken);

        Assert.InRange(puede.Ejemplos.Count, 4, 6);
    }

    [Fact]
    public async Task Una_consulta_con_una_columna_personal_no_es_ejecutable_por_el_rol_basico()
    {
        // EL FILTRO, PROBADO CON UNA CONSULTA SINTÉTICA. El catálogo de ejemplos real
        // no tiene ninguna que toque datos personales, así que sobre datos reales el
        // filtro es hoy un no-op — y sin este test nadie sabría si funciona el día
        // que alguien agregue el primero.
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;
        var (basica, pii) = CadenasDeLectura();
        const string ConDatosPersonales = "SELECT documento FROM identity.personas";

        await using var conBasica = new NpgsqlConnection(basica.Valor);
        await conBasica.OpenAsync(ct);

        await using var conPii = new NpgsqlConnection(pii.Valor);
        await conPii.OpenAsync(ct);

        Assert.False(await CatalogoDeCapacidades.EjecutableAsync(
            conBasica, Secretaria, ConDatosPersonales, ct));

        Assert.True(await CatalogoDeCapacidades.EjecutableAsync(
            conPii, Secretaria, ConDatosPersonales, ct));
    }

    [Fact]
    public void Hoy_ningun_ejemplo_del_catalogo_toca_una_columna_personal()
    {
        // Deja escrito el estado real: el filtro protege de algo que todavía no
        // pasó. Si este test empieza a fallar, quiere decir que se agregó un ejemplo
        // con datos personales, y entonces el filtro pasa a tener efecto — que es lo
        // que el test de arriba ya verificó que funciona.
        var conPersonales = new SelectorDeEjemplos().Catalogo
            .Where(e => ColumnasPersonales.Any(c =>
                e.Sql.Contains(c, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.Empty(conPersonales);
    }

    // ------------------------------------------------- alcance, límites, caché

    [Fact]
    public async Task El_ambito_no_altera_los_conteos_pero_si_el_alcance_informado()
    {
        // El ámbito cambia QUÉ FILAS ve, no QUÉ PUEDE PREGUNTAR. Meterlo en los
        // conteos los haría mentir en las dos direcciones.
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;
        var catalogo = Catalogo();

        var deCarrera = await catalogo.ObtenerAsync(Coordinador, ct);
        var deMateria = await catalogo.ObtenerAsync(Jefe, ct);
        var global = await catalogo.ObtenerAsync(Secretaria, ct);

        Assert.Equal(deCarrera.Columnas, deMateria.Columnas);
        Assert.Equal(deCarrera.Alcance, deMateria.Alcance);
        Assert.NotEqual(global.Alcance, deCarrera.Alcance);
    }

    [Fact]
    public async Task El_catalogo_dice_que_no_escribe_y_que_no_sale_del_sistema()
    {
        await SembrarAsync();

        var puede = await Catalogo().ObtenerAsync(
            Secretaria, TestContext.Current.CancellationToken);

        Assert.NotEmpty(puede.NoPuede);
        Assert.Contains(puede.NoPuede, l =>
            l.Contains("No modifica", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(puede.NoPuede, l =>
            l.Contains("Guaraní", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task El_catalogo_se_cachea_por_rol()
    {
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;
        var catalogo = Catalogo();

        await catalogo.ObtenerAsync(Coordinador, ct);
        await catalogo.ObtenerAsync(Jefe, ct);

        // Los dos usan el rol básico: una sola lectura del catálogo de PostgreSQL.
        Assert.Equal(1, catalogo.Lecturas);

        await catalogo.ObtenerAsync(Secretaria, ct);

        // El de datos personales es otra variante.
        Assert.Equal(2, catalogo.Lecturas);
    }

    // -------------------------------------------------- la presentación por rol

    [Fact]
    public async Task Sin_el_permiso_del_tramite_la_presentacion_no_lo_anuncia()
    {
        // BR-asistente-004. El rol `docente` tiene `portal.ver` y `portal.editar`, y
        // NADA de designaciones: la RLS le devuelve cero filas sobre las cuatro
        // tablas del trámite. La copy vieja le prometía «preguntá por tus
        // designaciones», que es exactamente la capacidad que no puede ejercer.
        //
        // Se consulta la presentación de un perfil armado a mano y no la del actor:
        // el rol `docente` ni siquiera tiene `asistente.consultar`, así que el
        // catálogo no llega a construirse para él.
        var perfil = new PerfilDelActor(
            EsGlobal: false, VeDatosPersonales: false, CodigoDeRol: "docente",
            VeDesignaciones: false);

        var presentacion = PresentacionPorRol.Texto(perfil);

        Assert.DoesNotContain("designaciones", presentacion, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("las materias", presentacion, StringComparison.Ordinal);
        Assert.Contains("tu propio perfil profesional", presentacion, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Con_el_permiso_del_tramite_un_rol_acotado_si_lo_anuncia()
    {
        // LA OTRA MITAD, y la que evita arreglar de más: un jefe de cátedra NO es
        // global, así que `AlcanzaDesignaciones` le da falso — pero ve las
        // designaciones de su cátedra y anunciárselas no promete nada que no pueda
        // ejercer. Si alguien "simplificara" mirando el conjugado, este test cae.
        await SembrarAsync();

        var puede = await Catalogo().ObtenerAsync(Jefe, TestContext.Current.CancellationToken);

        Assert.Contains("designaciones de tu cátedra", puede.Presentacion, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("a0000000-0000-4000-8000-000000000002", "designaciones de tu cátedra")]
    [InlineData("a0000000-0000-4000-8000-000000000003", "designaciones de tu carrera")]
    [InlineData("a0000000-0000-4000-8000-000000000004", "designaciones de todo el Departamento")]
    [InlineData("a0000000-0000-4000-8000-000000000005", "designaciones de todo el Departamento")]
    [InlineData("a0000000-0000-4000-8000-000000000006", "designaciones de todo el Departamento")]
    public async Task Cada_rol_conocido_recibe_su_propia_presentacion(string actor, string fragmento)
    {
        await SembrarAsync();

        var puede = await Catalogo().ObtenerAsync(
            Guid.Parse(actor), TestContext.Current.CancellationToken);

        Assert.Contains(fragmento, puede.Presentacion, StringComparison.Ordinal);

        // Y no cayó al ámbito genérico. Se afirma sobre la cláusula entera y no
        // sobre «del sistema» suelto: los catálogos se nombran «del sistema» para
        // todos, así que buscar esa frase a secas encontraría siempre algo.
        Assert.DoesNotContain(
            $"designaciones {PresentacionPorRol.AmbitoGenerico}",
            puede.Presentacion,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_rol_que_la_tabla_no_conoce_cae_a_la_presentacion_generica()
    {
        // `sys_admin` existe en identity.roles y no está en la tabla de
        // presentaciones. El default correcto es el texto que no promete nada de
        // más: acá el rol elige un saludo, no un permiso, así que no conocerlo no
        // abre nada.
        await SembrarAsync();

        var puede = await Catalogo().ObtenerAsync(
            Sistemas, TestContext.Current.CancellationToken);

        // Cae al ámbito genérico: no se le inventa uno. El resto de la frase —las
        // áreas— se deriva de sus permisos igual que para cualquier otro actor.
        Assert.Contains(
            $"designaciones {PresentacionPorRol.AmbitoGenerico}",
            puede.Presentacion,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Con_varios_roles_a_la_vez_la_presentacion_es_la_generica()
    {
        // SIN TABLA DE PRECEDENCIA, y a propósito: elegir que «secretaria gana a
        // jefe_catedra» sería inventar una jerarquía que nadie pidió para decidir un
        // saludo. Un genérico correcto es mejor que un específico adivinado.
        //
        // EL ACTOR MULTIROL LO ARMA EL TEST, y ya no lo trae el seed compartido. El
        // usuario «Demo Multirol» se dio de baja cuando el seed pasó a exigir que
        // cada actor del historial tuviera de verdad el rol con el que actuó, y seis
        // roles sobre dos materias no entraban en esa coherencia. Concederlo acá deja
        // la condición a la vista —dos roles vigentes sobre el mismo actor— en vez de
        // esconderla en una fila de fixture que cualquier otro cambio se lleva puesta.
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;
        await SumarleUnSegundoRolAsync(Jefe, ct);

        var puede = await Catalogo().ObtenerAsync(Jefe, ct);

        // Cae al ámbito genérico: no se le inventa uno. El resto de la frase —las
        // áreas— se deriva de sus permisos igual que para cualquier otro actor.
        Assert.Contains(
            $"designaciones {PresentacionPorRol.AmbitoGenerico}",
            puede.Presentacion,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_rol_elige_la_presentacion_y_nada_mas()
    {
        // ES EL GATE DE ESTA PIEZA. Docente y Jefe de Cátedra tienen los dos ámbito
        // de materia y el mismo rol de lectura: si el rol se filtrara a los conteos
        // o al alcance, el módulo habría empezado a decidir por rol lo que hasta
        // ahora deriva de los GRANT y de la matriz de permisos.
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;
        var catalogo = Catalogo();

        var deDocente = await catalogo.ObtenerAsync(Docente, ct);
        var deJefe = await catalogo.ObtenerAsync(Jefe, ct);

        Assert.NotEqual(deDocente.Presentacion, deJefe.Presentacion);
        Assert.Equal(deDocente.Alcance, deJefe.Alcance);
        Assert.Equal(deDocente.Columnas, deJefe.Columnas);
        Assert.Equal(deDocente.Tablas, deJefe.Tablas);
        Assert.Equal(deDocente.Ejemplos, deJefe.Ejemplos);
    }

    // ------------------------------------------------------ la meta-pregunta

    [Fact]
    public async Task La_meta_pregunta_responde_con_el_catalogo_real_y_cero_llamadas()
    {
        await SembrarAsync();
        var banco = Banco();

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿qué podés hacer?", TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.Respondida, turno.Estado);
        Assert.Equal(0, banco.Proveedor.Llamadas);
        Assert.Equal(0, turno.LlamadasAlModelo);

        // Menciona ejemplos reales del catálogo verificado, y viajan como
        // sugerencias para que la interfaz los pueda hacer clicables.
        Assert.NotEmpty(turno.Sugerencias!);
        Assert.Contains(turno.Sugerencias![0], turno.Respuesta, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Con_el_corte_al_proveedor_abierto_la_meta_pregunta_sigue_respondiendo()
    {
        await SembrarAsync();
        var banco = Banco(new OpcionesAsistente
        {
            FallosParaAbrirElBreaker = 1,
            CupoDeLlamadasPorActor = 0,
        });

        banco.Breaker.Fallo();

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿qué podés hacer?", TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.Respondida, turno.Estado);
        Assert.NotEmpty(turno.Sugerencias!);
        Assert.Equal(0, banco.Proveedor.Llamadas);
    }

    [Fact]
    public async Task La_meta_pregunta_abre_con_la_misma_presentacion_que_la_pantalla_inicial()
    {
        // Las dos superficies contestan «¿qué podés hacer?». Si cada una redactara
        // la suya, el sistema se contradiría sobre sí mismo en el lugar más visible.
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;

        var puede = await Catalogo().ObtenerAsync(Jefe, ct);
        var texto = RedaccionDeCapacidades.Texto(puede);

        Assert.StartsWith(puede.Presentacion, texto, StringComparison.Ordinal);
        Assert.DoesNotContain(
            $"designaciones {PresentacionPorRol.AmbitoGenerico}",
            puede.Presentacion,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task La_redaccion_no_nombra_tablas_ni_schemas()
    {
        // Son etiquetas internas (RNF-18). Lo que se muestra son los comentarios del
        // catálogo, que están escritos para leerse.
        await SembrarAsync();

        var texto = RedaccionDeCapacidades.Texto(
            await Catalogo().ObtenerAsync(Secretaria, TestContext.Current.CancellationToken));

        Assert.DoesNotContain("identity.", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("designaciones.", texto, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------- portal docente

    [Fact]
    public async Task Sin_el_permiso_la_presentacion_ofrece_el_perfil_propio_y_no_la_busqueda()
    {
        // INVARIANTE #7 POR OTRO CAMINO. Anunciarle a alguien que busque docentes por
        // habilidad cuando el permiso no lo tiene nadie es prometer una capacidad que
        // no va a poder ejercer: el mismo defecto que un botón que no anda, sin botón.
        await SembrarAsync();

        var puede = await Catalogo().ObtenerAsync(
            Secretaria, TestContext.Current.CancellationToken);

        Assert.Contains("tu propio perfil profesional", puede.Presentacion, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("perfiles profesionales de los docentes", puede.Presentacion, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Con_el_permiso_la_presentacion_ofrece_la_busqueda_por_perfil()
    {
        // La otra mitad: el día que Secretaría conceda el permiso, la capacidad se
        // anuncia sola. No hace falta desplegar nada — el catálogo lo lee en vivo.
        await SembrarAsync();
        await ConcederTrayectoriaAjenaAsync(Secretaria);

        var puede = await Catalogo().ObtenerAsync(
            Secretaria, TestContext.Current.CancellationToken);

        Assert.Contains("los perfiles profesionales de los docentes", puede.Presentacion, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tu propio perfil", puede.Presentacion, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task La_presentacion_no_promete_el_contacto_ni_el_archivo_del_CV()
    {
        // Las dos cosas que el asistente NO hace sobre portal, y que son justo las
        // que alguien esperaría de un «perfil»: el contacto personal está denegado y
        // del CV sólo se puede decir que existe.
        await SembrarAsync();
        await ConcederTrayectoriaAjenaAsync(Secretaria);

        var texto = (await Catalogo().ObtenerAsync(
            Secretaria, TestContext.Current.CancellationToken)).Presentacion;

        foreach (var prohibida in new[] { "teléfono", "mail", "correo", "CV", "currículum" })
        {
            Assert.DoesNotContain(prohibida, texto, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ------------------------------------------------------------------ apoyo

    /// <summary>Le concede al actor el permiso de leer la trayectoria ajena.</summary>
    private async Task ConcederTrayectoriaAjenaAsync(Guid actor)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO identity.rol_permisos (rol_id, permiso_id)
            SELECT ur.role_id, p.id
              FROM identity.user_roles ur
             CROSS JOIN identity.permisos p
             WHERE ur.user_id = @actor
               AND ur.deleted_at IS NULL
               AND p.code = 'portal.ver_trayectoria_ajena'
            ON CONFLICT DO NOTHING
            """, conexion);

        comando.Parameters.AddWithValue("actor", actor);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private CatalogoDeCapacidades Catalogo()
    {
        var (basica, pii) = CadenasDeLectura();

        return new CatalogoDeCapacidades(
            Apertura,
            new ConsultorDeAlcance(Apertura),
            new SelectorDeEjemplos(),
            new CacheDeCapacidades(),
            NullLogger<CatalogoDeCapacidades>.Instance);
    }

    private BancoDelAsistente Banco(OpcionesAsistente? configuracion = null)
    {
        var (basica, pii) = CadenasDeLectura();

        return BancoDelAsistente.Armar(
            basica,
            pii,
            ClasificadorDeSensibilidad(),
            Apertura,
            configuracion ?? new OpcionesAsistente { CupoDeLlamadasPorActor = 0 });
    }

    /// <summary>
    /// Le suma al actor una segunda asignación de rol vigente.
    /// </summary>
    /// <remarks>
    /// La fila va sin materia ni carrera porque <c>secretaria</c> es de alcance
    /// global: el trigger <c>identity.enforce_role_scope</c> rechaza cualquier otra
    /// combinación. El id, <c>granted_at</c> y <c>created_at</c> los pone el default
    /// de la tabla, y <c>granted_by</c> queda nulo porque acá no hay quién conceda.
    /// </remarks>
    private async Task SumarleUnSegundoRolAsync(Guid actor, CancellationToken ct)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            "INSERT INTO identity.user_roles (user_id, role_id) VALUES (@actor, @rol)",
            conexion);

        comando.Parameters.AddWithValue("actor", actor);
        comando.Parameters.AddWithValue("rol", RolSecretaria);
        await comando.ExecuteNonQueryAsync(ct);
    }

}
