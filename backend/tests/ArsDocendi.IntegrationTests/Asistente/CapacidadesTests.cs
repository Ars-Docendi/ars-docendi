using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica el catálogo de capacidades por actor (ARS-49). El invariante de privacidad de esta
/// pieza es el más importante de la épica: <b>el catálogo se deriva de los GRANT efectivos y nunca
/// del payload del prompt</b>.
/// </summary>
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
    /// <c>sys_admin</c>: un rol de sistema que el catálogo de presentaciones no nombra.
    /// </summary>
    private static readonly Guid Sistemas = Guid.Parse("a0000000-0000-4000-8000-000000000007");

    /// <summary>
    /// El rol de Secretaría Académica, de alcance global. No es un actor: es el
    /// SEGUNDO rol que el test le suma a un actor que ya tiene uno, para armarse el
    /// caso multirol que el seed compartido dejó de traer.
    /// </summary>
    private static readonly Guid RolSecretaria = Guid.Parse("a1000000-0000-4000-8000-000000000004");
    private static readonly Guid RolJefe = Guid.Parse("a1000000-0000-4000-8000-000000000002");

    private static readonly string[] ColumnasPersonales =
        ["documento", "cuil", "fecha_nacimiento", "telefono", "upn"];

    // ------------------------------------------------- derivado de los GRANT

    [Fact]
    public async Task El_catalogo_de_un_rol_basico_no_cuenta_ninguna_columna_personal()
    {
        // La primera versión de este test miraba el TEXTO redactado buscando «documento», y pasaba
        // aunque el catálogo se armara con la conexión de datos personales: la redacción nunca
        // nombra columnas, así que no había nada que encontrar.
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;
        var catalogo = Catalogo();

        var sinPii = await catalogo.ObtenerAsync(Coordinador, ct);
        var conPii = await catalogo.ObtenerAsync(Secretaria, ct);

        var personasBasica = Assert.Single(
            sinPii.Cubre, a => a.Nombre == "identity.personas");
        var personasConPii = Assert.Single(
            conPii.Cubre, a => a.Nombre == "identity.personas");

        // Cuatro de las cinco columnas personales están en `identity.personas` —documento, cuil,
        // fecha_nacimiento y teléfono—; la quinta, `upn`, está en `identity.users`.
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

        // Aulas is announced by the live permission (Secretaría approves, the coordinator holds
        // neither), not by the read role: leave it out so only the PII difference is measured.
        Assert.True(ColumnasSinAulas(conPii) > ColumnasSinAulas(sinPii),
            $"Con datos personales: {ColumnasSinAulas(conPii)}; sin: {ColumnasSinAulas(sinPii)}.");

        // Cinco columnas exactamente: documento, cuil, fecha_nacimiento, telefono y upn.
        Assert.Equal(5, ColumnasSinAulas(conPii) - ColumnasSinAulas(sinPii));
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

        // The coordinator holds no aulas permission, so the catalog leaves aulas out even though the
        // read role holds the GRANT (same gate as comisiones, by the live permission).
        var sinAulas = legibles.Where(c => c.Esquema != "aulas").ToList();
        Assert.Equal(sinAulas.Count, puede.Columnas);
        Assert.Equal(
            sinAulas.Select(c => $"{c.Esquema}.{c.Tabla}").Distinct().Count(),
            puede.Tablas);
    }

    [Fact]
    public async Task El_catalogo_no_ofrece_el_schema_propio_del_asistente()
    {
        // Los dos registros del asistente están revocados a sus propios roles. Si aparecieran acá,
        // el catálogo estaría ofreciendo consultar el texto de las preguntas de todos los demás.
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
    public void Hoy_ningun_ejemplo_del_catalogo_toca_una_columna_personal()
    {
        // Deja escrito el estado real: el filtro protege de algo que todavía no pasó.
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

        // Aulas follows the live permission (the jefe requests, the coordinator does not), not scope.
        Assert.Equal(ColumnasSinAulas(deCarrera), ColumnasSinAulas(deMateria));
        Assert.Equal(deCarrera.Alcance, deMateria.Alcance);
        Assert.NotEqual(global.Alcance, deCarrera.Alcance);
    }

    // -------------------------------------------------- la presentación por rol

    [Fact]
    public async Task Sin_el_permiso_del_tramite_la_presentacion_no_lo_anuncia()
    {
        // BR-asistente-004. El rol `docente` tiene `portal.ver` y `portal.editar`, y NADA de
        // designaciones: la RLS le devuelve cero filas sobre las cuatro tablas del trámite.
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
        // LA OTRA MITAD, y la que evita arreglar de más: un jefe de cátedra NO es global, así que
        // `AlcanzaDesignaciones` le da falso — pero ve las designaciones de su cátedra y
        // anunciárselas no promete nada que no pueda ejercer.
        await SembrarAsync();

        var puede = await Catalogo().ObtenerAsync(Jefe, TestContext.Current.CancellationToken);

        Assert.Contains("designaciones de tu cátedra", puede.Presentacion, StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_rol_elige_la_presentacion_y_nada_mas()
    {
        // ES EL GATE DE ESTA PIEZA. Docente y Jefe de Cátedra tienen los dos ámbito de materia y el
        // mismo rol de lectura: si el rol se filtrara a los conteos o al alcance, el módulo habría
        // empezado a decidir por rol lo que hasta ahora deriva de los GRANT y de la matriz de permisos.
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;
        var catalogo = Catalogo();

        var deDocente = await catalogo.ObtenerAsync(Docente, ct);
        var deJefe = await catalogo.ObtenerAsync(Jefe, ct);

        Assert.NotEqual(deDocente.Presentacion, deJefe.Presentacion);
        Assert.Equal(deDocente.Alcance, deJefe.Alcance);

        // The only difference in the areas and the examples is comisiones, offered by the live
        // permission and not by the role.
        Assert.Equal(
            deDocente.Cubre,
            deJefe.Cubre.Where(area => !area.Nombre.StartsWith("comisiones.", StringComparison.Ordinal)));

        // Same for the examples: the general ones are the same for both (domain examples take the
        // tail of the list, so compare only the leading general ones).
        Assert.Equal(
            deDocente.Ejemplos.Where(EsDeComisiones),
            []);
        Assert.Equal(
            deDocente.Ejemplos.Where(ejemplo => !EsDeAulas(ejemplo)).Take(3),
            deJefe.Ejemplos.Where(ejemplo => !EsDeComisiones(ejemplo) && !EsDeAulas(ejemplo)).Take(3));
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

        // Menciona ejemplos reales del catálogo verificado, adentro del propio
        // texto («Por ejemplo:») — no hay un campo de sugerencias aparte desde
        // ARS-149 (design.md D12 de asistente-rediseno-v3).
        var puede = await Catalogo().ObtenerAsync(Secretaria, TestContext.Current.CancellationToken);
        Assert.NotEmpty(puede.Ejemplos);
        Assert.Contains(puede.Ejemplos[0], turno.Respuesta, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Con_el_corte_al_proveedor_abierto_la_meta_pregunta_sigue_respondiendo()
    {
        await SembrarAsync();
        var banco = Banco(new OpcionesAsistente
        {
            FallosParaAbrirElBreaker = 1,
        });

        banco.Breaker.Fallo();

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿qué podés hacer?", TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.Respondida, turno.Estado);
        Assert.Equal(0, banco.Proveedor.Llamadas);
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
        // Las dos cosas que el asistente NO hace sobre portal, y que son justo las que alguien
        // esperaría de un «perfil»: el contacto personal está denegado y del CV sólo se puede decir que existe.
        await SembrarAsync();
        await ConcederTrayectoriaAjenaAsync(Secretaria);

        var texto = (await Catalogo().ObtenerAsync(
            Secretaria, TestContext.Current.CancellationToken)).Presentacion;

        foreach (var prohibida in new[] { "teléfono", "mail", "correo", "CV", "currículum" })
        {
            Assert.DoesNotContain(prohibida, texto, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ------------------------------------------------------------- comisiones

    [Fact]
    public async Task Con_comisiones_ver_se_ofrecen_las_comisiones_y_sin_el_permiso_no_se_mencionan()
    {
        // Both read roles hold the GRANT, so the catalog cannot tell them apart: only the live
        // permission can. Without this gate the question would be offered and answered with zero rows.
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;
        var catalogo = Catalogo();

        var conPermiso = await catalogo.ObtenerAsync(Jefe, ct);
        var sinPermiso = await catalogo.ObtenerAsync(Docente, ct);

        Assert.Contains("comisiones", conPermiso.Presentacion, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(conPermiso.Ejemplos, EsDeComisiones);
        Assert.Contains(conPermiso.Cubre, area => area.Nombre.StartsWith("comisiones.", StringComparison.Ordinal));

        Assert.DoesNotContain("comisiones", sinPermiso.Presentacion, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(sinPermiso.Ejemplos, EsDeComisiones);
        Assert.DoesNotContain(sinPermiso.Cubre, area => area.Nombre.StartsWith("comisiones.", StringComparison.Ordinal));

        // The meta-question text is built from the same lists: nothing about comisiones for the actor
        // who does not hold the permission.
        Assert.DoesNotContain(
            "comisiones", RedaccionDeCapacidades.Texto(sinPermiso), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task La_presentacion_de_comisiones_no_promete_aulas_libres_ni_el_registro_de_importaciones()
    {
        await SembrarAsync();

        var texto = (await Catalogo().ObtenerAsync(
            Secretaria, TestContext.Current.CancellationToken)).Presentacion;

        Assert.Contains("comisiones", texto, StringComparison.OrdinalIgnoreCase);
        foreach (var prohibida in new[] { "libres", "disponibles", "importacion", "importación" })
        {
            Assert.DoesNotContain(prohibida, texto, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ------------------------------------------------------------------ aulas

    [Fact]
    public async Task Las_solicitudes_de_aula_se_anuncian_segun_el_permiso_vivo_y_dicen_de_quien_son()
    {
        // Both read roles hold the GRANT; only the live permissions tell who sees what. The approver
        // is told about all requests, who only requests about their own, anyone else nothing.
        await SembrarAsync();
        await EjecutarAsync(
            """
            DELETE FROM identity.rol_permisos rp
             USING identity.permisos p
             WHERE p.id = rp.permiso_id AND p.code IN ('aulas.aprobar', 'aulas.solicitar');

            INSERT INTO identity.rol_permisos (rol_id, permiso_id)
            SELECT @secretaria, p.id FROM identity.permisos p WHERE p.code = 'aulas.aprobar'
            UNION ALL
            SELECT @jefe, p.id FROM identity.permisos p WHERE p.code = 'aulas.solicitar';
            """,
            ("secretaria", RolSecretaria), ("jefe", RolJefe));
        var ct = TestContext.Current.CancellationToken;
        var catalogo = Catalogo();

        var aprueba = await catalogo.ObtenerAsync(Secretaria, ct);
        var solicita = await catalogo.ObtenerAsync(Jefe, ct);
        var ninguno = await catalogo.ObtenerAsync(Coordinador, ct);

        Assert.Contains("solicitudes de reserva de aula, con su estado", aprueba.Presentacion, StringComparison.Ordinal);
        Assert.Contains(aprueba.Cubre, area => area.Nombre == "aulas.solicitudes_reserva");
        Assert.Contains(aprueba.Ejemplos, EsDeAulas);

        Assert.Contains("tus solicitudes de reserva de aula", solicita.Presentacion, StringComparison.Ordinal);
        Assert.DoesNotContain("solicitudes de reserva de aula, con su estado", solicita.Presentacion, StringComparison.Ordinal);
        Assert.Contains(solicita.Cubre, area => area.Nombre == "aulas.solicitudes_reserva");

        Assert.DoesNotContain("reserva de aula", ninguno.Presentacion, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ninguno.Ejemplos, EsDeAulas);
        Assert.DoesNotContain(ninguno.Cubre, area => area.Nombre.StartsWith("aulas.", StringComparison.Ordinal));

        // Nothing announced as answerable that the schema cannot answer.
        foreach (var prohibida in new[] { "libres", "capacidad", "disponibilidad" })
        {
            Assert.DoesNotContain(prohibida, aprueba.Presentacion, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static int ColumnasSinAulas(CapacidadesDelActor capacidades) =>
        capacidades.Cubre
            .Where(area => !area.Nombre.StartsWith("aulas.", StringComparison.Ordinal))
            .Sum(area => area.Columnas);

    private static bool EsDeAulas(string pregunta) =>
        new SelectorDeEjemplos().Catalogo.Any(ejemplo =>
            ejemplo.Pregunta == pregunta
            && DetectorDeDominios.Detectar(ejemplo.Sql).Dominios.HasFlag(DominioConsultado.Aulas));

    private static bool EsDeComisiones(string pregunta) =>
        new SelectorDeEjemplos().Catalogo.Any(ejemplo =>
            ejemplo.Pregunta == pregunta
            && DetectorDeDominios.Detectar(ejemplo.Sql).Dominios.HasFlag(DominioConsultado.Comisiones));

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
        var cadena = new ArsDocendi.Shared.Persistencia.CadenaDuena(Cadena);
        var reloj = TimeProvider.System;
        var identidad = new ArsDocendi.Shared.Identity.ConsultasIdentity(
            PostgresFixture.CrearIdentity(Cadena));
        var cuota = new CuotaPersistente(cadena, identidad, reloj, new DetectorDeUmbrales(), NullLogger<CuotaPersistente>.Instance);
        var presupuestoOrganizacional = new PresupuestoOrganizacionalPersistente(cadena, reloj, new DetectorDeUmbrales(), NullLogger<PresupuestoOrganizacionalPersistente>.Instance);
        var disponibilidadDelModulo = new DisponibilidadDelModuloReal(cadena);
        var disponibilidad = new DisponibilidadDelModeloReal(
            new AccesoPersistente(cadena, identidad),
            cuota,
            presupuestoOrganizacional,
            disponibilidadDelModulo,
            new BreakerDelProveedor(
                Microsoft.Extensions.Options.Options.Create(new OpcionesAsistente()),
                reloj,
                NullLogger<BreakerDelProveedor>.Instance));

        return new CatalogoDeCapacidades(
            Apertura,
            new ConsultorDeAlcance(Apertura),
            new SelectorDeEjemplos(),
            new CacheDeCapacidades(),
            disponibilidad,
            disponibilidadDelModulo,
            cuota,
            identidad,
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
            configuracion ?? new OpcionesAsistente());
    }

    /// <summary>Le suma al actor una segunda asignación de rol vigente.</summary>
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
