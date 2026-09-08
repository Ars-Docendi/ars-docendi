using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Persistencia;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Declarar cuánta gente cargó el dato, para no confundir el vacío con el hecho.
/// </summary>
/// <remarks>
/// EL MODO DE FALLA QUE ESTO EVITA no es de privacidad ni de permisos: es el sistema
/// afirmando más de lo que sabe. «Ningún docente sabe Python» y «nadie cargó sus
/// habilidades» son la MISMA consulta con el mismo cero, y el design spec del portal
/// dice que el segundo caso es el normal —«el problema del Departamento es que los
/// docentes no cargan nada»—.
/// </remarks>
public sealed class CoberturaDelPortalTests
{
    // ------------------------------------------------------ qué toca la consulta

    [Theory]
    [InlineData("SELECT 1 FROM portal.educaciones", "educaciones")]
    [InlineData("select * from PORTAL.CERTIFICACIONES c", "certificaciones")]
    [InlineData("SELECT 1 FROM portal.docente_habilidades dh", "docente_habilidades")]
    public void Detecta_la_tabla_de_portal_que_la_consulta_nombra(string sql, string esperada) =>
        Assert.Equal([esperada], CoberturaDelPortal.TablasQueToca(sql));

    [Theory]
    [InlineData("SELECT p.nombre FROM identity.personas p")]
    [InlineData("SELECT count(*) FROM designaciones.pedidos")]
    [InlineData("")]
    [InlineData(null)]
    public void Una_consulta_que_no_toca_portal_no_declara_nada(string? sql) =>
        Assert.Empty(CoberturaDelPortal.TablasQueToca(sql));

    [Fact]
    public void Una_consulta_que_cruza_dos_tablas_de_portal_declara_las_dos()
    {
        var tablas = CoberturaDelPortal.TablasQueToca(
            """
            SELECT p.apellido
              FROM portal.educaciones e
              JOIN portal.certificaciones c ON c.perfil_id = e.perfil_id
              JOIN portal.perfiles pf ON pf.id = e.perfil_id
              JOIN identity.personas p ON p.id = pf.persona_id
            """);

        Assert.Equal(["certificaciones", "educaciones"], tablas);
    }

    [Fact]
    public void Las_tablas_que_no_carga_el_docente_no_se_declaran()
    {
        // `perfiles` no está en el catálogo: su cobertura es la de tener perfil, no la
        // de tener un dato, y eso no explica el vacío de una pregunta concreta.
        Assert.Empty(CoberturaDelPortal.TablasQueToca("SELECT id FROM portal.perfiles"));
    }

    // ----------------------------------------------------- cuál explica el vacío

    [Fact]
    public void Se_declara_la_cobertura_mas_escasa_y_no_todas()
    {
        // Es la que EXPLICA el vacío. Si la pregunta cruzó formación con
        // certificaciones y nadie cargó certificaciones, decir además cuánta
        // formación hay no aclara nada y alarga una mala noticia.
        var elegida = CoberturaDelPortal.LaQueExplicaElVacio(
        [
            new CoberturaDeUnDato("educaciones", 14, 120),
            new CoberturaDeUnDato("certificaciones", 2, 120),
        ]);

        Assert.Equal("certificaciones", elegida?.Tabla);
    }

    [Fact]
    public void Con_el_padron_vacio_no_se_declara_nada()
    {
        // «0 de 0 cargaron» no informa: es un cociente sin significado, y decirlo
        // sólo agrega ruido a una respuesta que ya no encontró nada.
        Assert.Null(CoberturaDelPortal.LaQueExplicaElVacio(
            [new CoberturaDeUnDato("educaciones", 0, 0)]));
    }

    [Fact]
    public void Sin_coberturas_no_hay_nada_que_explicar() =>
        Assert.Null(CoberturaDelPortal.LaQueExplicaElVacio([]));

    // ------------------------------------------------------------ cómo se dice

    [Fact]
    public void La_frase_da_el_numerador_y_el_denominador()
    {
        var frase = new CoberturaDeUnDato("docente_habilidades", 14, 120).Frase();

        Assert.Contains("120", frase, StringComparison.Ordinal);
        Assert.Contains("14", frase, StringComparison.Ordinal);
        Assert.Contains("habilidades", frase, StringComparison.OrdinalIgnoreCase);
    }

    // --------------------------------------------- el texto del resultado vacío

    [Fact]
    public void El_texto_del_vacio_sin_cobertura_queda_como_estaba()
    {
        // No se toca el caso de designaciones: la cobertura es opcional y sólo se
        // suma cuando hay algo que declarar.
        Assert.Equal(
            "No encontré ningún registro que responda esa pregunta.",
            PoliticaDeAbstencion.TextoDeResultadoVacio(true));
    }

    [Fact]
    public void El_texto_del_vacio_suma_la_cobertura_sin_reemplazar_el_alcance()
    {
        // LOS DOS LÍMITES PUEDEN SER CIERTOS A LA VEZ, y quien pregunta necesita los
        // dos para saber qué hacer: pedir acceso, o pedirle a la gente que cargue.
        var texto = PoliticaDeAbstencion.TextoDeResultadoVacio(
            alcanzaTodo: false, new CoberturaDeUnDato("educaciones", 3, 120));

        Assert.Contains("fuera de tu alcance", texto, StringComparison.Ordinal);
        Assert.Contains("3 cargaron", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void Con_cobertura_el_actor_que_alcanza_todo_ya_no_afirma_ausencia_a_secas()
    {
        var texto = PoliticaDeAbstencion.TextoDeResultadoVacio(
            alcanzaTodo: true, new CoberturaDeUnDato("docente_habilidades", 0, 120));

        // Sigue diciendo que no encontró —es cierto— pero ahora explica por qué el
        // cero no significa que nadie sepa lo que se preguntó.
        Assert.Contains("No encontré", texto, StringComparison.Ordinal);
        Assert.Contains("120 docentes", texto, StringComparison.Ordinal);
        Assert.Contains("0 cargaron", texto, StringComparison.Ordinal);
    }

    // ------------------------------------------------- la regla que ve el modelo

    [Fact]
    public void La_regla_de_redaccion_le_prohibe_al_modelo_narrar_el_total()
    {
        // Con filas devueltas el texto lo escribe el modelo, así que el límite tiene
        // que viajar en el prompt. Es lo único que se puede hacer para restringir una
        // narración, y por eso se dice explícito además de estar en el COMMENT.
        var reglas = PoliticaDeAbstencion.ReglasDeRedaccion(
            alcanzaTodo: true,
            truncado: false,
            [new CoberturaDeUnDato("educaciones", 14, 120)]);

        var regla = Assert.Single(reglas);
        Assert.Contains("14 cargaron", regla, StringComparison.Ordinal);
        Assert.Contains("NO afirmes que nadie", regla, StringComparison.Ordinal);
    }

    [Fact]
    public void Sin_cobertura_las_reglas_de_redaccion_no_cambian()
    {
        Assert.Empty(PoliticaDeAbstencion.ReglasDeRedaccion(
            alcanzaTodo: true, truncado: false));
    }

    [Fact]
    public void La_regla_de_cobertura_llega_al_prompt_de_redaccion()
    {
        // Verificarlo a través de la respuesta del modelo probaría al modelo; acá se
        // afirma que el texto salió del armado del mensaje, que es nuestro.
        var mensaje = RedactorDeRespuesta.ArmarMensaje(
            "¿quiénes tienen posgrado?",
            new ResultadoDeConsulta(["apellido"], [new object?[] { "Díaz" }], false),
            alcanzaTodo: true,
            [new CoberturaDeUnDato("educaciones", 14, 120)]);

        Assert.Contains("14 cargaron su formación", mensaje, StringComparison.Ordinal);
    }
}

/// <summary>
/// El conteo de cobertura contra la base, con el alcance del actor.
/// </summary>
/// <remarks>
/// <b>Con un rol DESCARTABLE y no con los del asistente</b>, que todavía no tienen
/// <c>USAGE</c> sobre <c>portal</c>: el <c>GRANT</c> es una decisión aparte con su
/// propio gate de finalidad. Probar con ellos mediría la ausencia del grant.
///
/// Que hoy falte ese grant es además lo que hace visible por qué el carril envuelve
/// esta consulta en un <c>try</c>: sin él, un turno que tocara portal moriría con
/// «permission denied for schema portal» en vez de responder sin el denominador.
/// </remarks>
[Collection(ColeccionPostgres.Nombre)]
public sealed class ConsultorDeCoberturaTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_cobertura")
{
    private static readonly Guid Docente = Guid.Parse("a0000000-0000-4000-8000-000000000001");

    private string _lector = string.Empty;

    [Fact]
    public async Task Cuenta_cuantos_perfiles_tienen_el_dato_y_cuantas_personas_hay()
    {
        await PrepararAsync();

        var coberturas = await Consultor().ObtenerAsync(
            ["educaciones"], Docente, TestContext.Current.CancellationToken);

        var cobertura = Assert.Single(coberturas);

        Assert.Equal("educaciones", cobertura.Tabla);

        // La premisa asertada: con el padrón vacío el cociente no diría nada y este
        // test pasaría sin verificar el conteo.
        Assert.True(cobertura.Total > 1, "El padrón sintético tiene que tener personas.");

        // Y el numerador es estrictamente menor: es el hecho que hace falta declarar,
        // porque es justo lo que el usuario no sabe al leer un resultado vacío.
        Assert.True(
            cobertura.ConDato < cobertura.Total,
            "El seed tiene que dejar gente SIN formación cargada, o esto no prueba nada.");
    }

    [Fact]
    public async Task El_conteo_respeta_la_RLS_del_actor()
    {
        // SIN EL PERMISO, EL NUMERADOR ES LO QUE EL ACTOR ALCANZA. Declararle una
        // cobertura calculada sobre filas que no puede ver le diría cuánta gente hay
        // detrás de una puerta cerrada, que es información que no le corresponde.
        await PrepararAsync();

        var suyo = await Consultor().ObtenerAsync(
            ["educaciones"], Docente, TestContext.Current.CancellationToken);

        await ConcederPermisoAsync(Docente);

        var todo = await Consultor().ObtenerAsync(
            ["educaciones"], Docente, TestContext.Current.CancellationToken);

        Assert.True(
            suyo[0].ConDato < todo[0].ConDato,
            "Con el permiso el actor tiene que alcanzar más perfiles que sin él.");

        // El denominador NO cambia: el tamaño del padrón no es un dato del portal y
        // sale de identity.personas, que no tiene RLS.
        Assert.Equal(suyo[0].Total, todo[0].Total);
    }

    [Fact]
    public async Task Sin_tablas_no_consulta_nada()
    {
        // El caso mayoritario: la pregunta no tocó portal. La consulta extra se paga
        // sólo en los turnos que la necesitan — y ni siquiera se abre la conexión.
        Assert.Empty(await new ConsultorDeCobertura(new CadenaSoloLectura("no-se-usa"))
            .ObtenerAsync([], Docente, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Una_tabla_fuera_del_catalogo_no_se_interpola()
    {
        // EL NOMBRE SE INTERPOLA porque un identificador no se puede parametrizar,
        // así que la única defensa es que salga del catálogo cerrado. Este test es
        // esa defensa hecha comprobable.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ConsultorDeCobertura(new CadenaSoloLectura("no-se-usa")).ObtenerAsync(
                ["contactos; DROP TABLE portal.perfiles --"],
                Docente,
                TestContext.Current.CancellationToken));
    }

    // ------------------------------------------------------------------ apoyo

    private IConsultorDeCobertura Consultor() =>
        new ConsultorDeCobertura(new CadenaSoloLectura(
            new NpgsqlConnectionStringBuilder(Cadena)
            {
                Username = _lector,
                Password = "lector-de-prueba",
                Pooling = false,
            }.ConnectionString));

    private async Task PrepararAsync()
    {
        await SembrarAsync();

        _lector = $"cob_t{Guid.NewGuid():N}"[..20];

        await EjecutarAsync(
            $"""
            CREATE ROLE "{_lector}" WITH LOGIN PASSWORD 'lector-de-prueba'
                NOSUPERUSER NOBYPASSRLS NOINHERIT;
            GRANT USAGE ON SCHEMA portal, identity TO "{_lector}";
            GRANT SELECT ON ALL TABLES IN SCHEMA portal TO "{_lector}";
            GRANT SELECT ON identity.personas TO "{_lector}";
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
               AND p.code = 'portal.ver_trayectoria_ajena'
            ON CONFLICT DO NOTHING;
            """);

    private async Task SembrarAsync() => await EjecutarAsync(
        await File.ReadAllTextAsync(
            Path.Combine(RaizRepositorio.Ruta(), "infra", "scripts", "seed-data", "sintetico.sql"),
            TestContext.Current.CancellationToken));
}
