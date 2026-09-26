using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica el rechazo cooperativo y la consulta detrás de permiso (ARS-47).
/// </summary>
/// <remarks>
/// Las dos cosas comparten un mismo criterio: qué se le devuelve al usuario cuando
/// el turno no salió como esperaba, y qué se le devuelve <b>de más</b> a quien tiene
/// permiso para verlo.
/// </remarks>
public sealed class SugerenciasYConsultaTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_sugerencias")
{
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");

    /// <summary>Un actor de ámbito acotado: su rol de lectura NO ve datos personales.</summary>
    private static readonly Guid Coordinador = Guid.Parse("a0000000-0000-4000-8000-000000000003");
    private const string Industrial = "c0000000-0000-4000-8000-000000000202";

    private const string ContarDocentes =
        "SELECT count(*) AS cantidad FROM designaciones.designaciones";

    // ------------------------------------------------------- las sugerencias

    [Fact]
    public async Task Un_rechazo_trae_sugerencias_y_no_trae_opciones()
    {
        await SembrarAsync();
        var banco = Banco(ProveedorGuionado.NoContestable());

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuál es la temperatura del aula 302?",
            TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.NoContestable, turno.Estado);
        Assert.NotNull(turno.Sugerencias);
        Assert.NotEmpty(turno.Sugerencias!);
        Assert.True(turno.Opciones is null or { Count: 0 });
    }

    [Fact]
    public async Task Una_aclaracion_trae_opciones_y_no_trae_sugerencias()
    {
        // ES LA DISTINCIÓN ENTERA. Las opciones BLOQUEAN el turno esperando una
        // elección; las sugerencias no bloquean nada. Un solo campo para las dos
        // cosas obligaría a la interfaz a adivinar cuál le llegó.
        await SembrarAsync();
        await AgregarColisionesAsync();
        var banco = Banco();

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿Quiénes dan Bases de Datos?",
            TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.NecesitaAclaracion, turno.Estado);
        Assert.NotNull(turno.Opciones);
        Assert.NotEmpty(turno.Opciones!);
        Assert.True(turno.Sugerencias is null or { Count: 0 });
    }

    [Fact]
    public async Task Cada_sugerencia_es_la_pregunta_de_un_ejemplo_verificado()
    {
        // No salen del modelo. Las del catálogo tienen su consulta al lado, ejecutan
        // sin error y pasan el validador: son, por construcción, cosas que el
        // asistente sabe hacer. Una sugerencia que no funciona convierte un rechazo
        // honesto en dos, y el segundo con la pregunta que el propio sistema propuso.
        await SembrarAsync();
        var banco = Banco(ProveedorGuionado.NoContestable());
        var catalogo = new SelectorDeEjemplos().Catalogo.Select(e => e.Pregunta).ToHashSet(
            StringComparer.Ordinal);

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuánto sale el café de la máquina?",
            TestContext.Current.CancellationToken);

        Assert.All(turno.Sugerencias!, sugerencia => Assert.Contains(sugerencia, catalogo));
    }

    [Fact]
    public void Sin_parecido_lexico_igual_hay_sugerencias()
    {
        // El selector devuelve vacío a propósito cuando ninguna se parece lo
        // suficiente. El requisito pide que SIEMPRE haya al menos una, así que
        // ahí se cae a las primeras del catálogo.
        var ejemplos = new SelectorDeEjemplos();

        Assert.Empty(ejemplos.Elegir("zxqwv plffk"));
        Assert.NotEmpty(Sugerencias.Para("zxqwv plffk", ejemplos));
    }

    [Fact]
    public async Task Un_rechazo_del_validador_tambien_sugiere()
    {
        await SembrarAsync();
        var banco = Banco(ProveedorGuionado.Generacion("DELETE FROM designaciones.pedidos"));

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "borrá todos los pedidos",
            TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.NoContestable, turno.Estado);
        Assert.NotEmpty(turno.Sugerencias!);
    }

    [Fact]
    public async Task Un_servicio_degradado_no_sugiere_nada()
    {
        // La pregunta no tiene nada de malo: proponerle otra al usuario le sugeriría
        // que el problema es suyo, cuando el problema es del proveedor.
        await SembrarAsync();
        var banco = Banco(new OpcionesAsistente { FallosParaAbrirElBreaker = 1 });
        banco.Breaker.Fallo();

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes están designados?",
            TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.ServicioDegradado, turno.Estado);
        Assert.True(turno.Sugerencias is null or { Count: 0 });
    }

    // ------------------------------- follow-up suggestions after success (4.x)

    [Fact]
    public void ParaCategoria_matches_by_category_and_excludes_the_just_answered_query()
    {
        EjemploSql[] catalogo =
        [
            new("¿Qué carreras están vigentes?", "SELECT 1", "consulta_simple"),
            new("¿Cuántos pedidos hay?", "SELECT 2", "consulta_simple"),
            new("¿Qué docentes hay?", "SELECT 3", "cruce_de_tablas"),
        ];

        var elegidos = Sugerencias.ParaCategoria("consulta_simple", "SELECT 1", catalogo);

        Assert.Equal(["¿Cuántos pedidos hay?"], elegidos.Select(e => e.Pregunta));
    }

    [Fact]
    public void ParaCategoria_with_no_executed_sql_keeps_every_category_match()
    {
        EjemploSql[] catalogo =
        [
            new("A", "SELECT 1", "agregacion"),
            new("B", "SELECT 2", "agregacion"),
            new("C", "SELECT 3", "cruce_de_tablas"),
        ];

        var elegidos = Sugerencias.ParaCategoria("agregacion", null, catalogo);

        Assert.Equal(["A", "B"], elegidos.Select(e => e.Pregunta));
    }

    [Fact]
    public async Task A_successful_turn_suggests_up_to_three_executable_examples_of_its_category()
    {
        await SembrarAsync();
        var banco = Banco(ProveedorGuionado.Generacion(ContarDocentes));

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes están designados?",
            TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.Respondida, turno.Estado);
        Assert.NotNull(turno.Sugerencias);
        Assert.NotEmpty(turno.Sugerencias!);
        Assert.True(turno.Sugerencias!.Count <= Sugerencias.Cuantas);

        var catalogo = new SelectorDeEjemplos().Catalogo
            .Where(e => e.Categoria == "cruce_de_tablas")
            .Select(e => e.Pregunta)
            .ToHashSet(StringComparer.Ordinal);
        Assert.All(turno.Sugerencias!, sugerencia => Assert.Contains(sugerencia, catalogo));
    }

    [Fact]
    public async Task A_successful_turn_never_suggests_back_the_query_it_just_ran()
    {
        await SembrarAsync();
        var yaRespondida = new SelectorDeEjemplos().Catalogo
            .First(e => e.Categoria == "cruce_de_tablas");
        var banco = Banco(ProveedorGuionado.Generacion(yaRespondida.Sql));

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, yaRespondida.Pregunta, TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.Respondida, turno.Estado);
        Assert.DoesNotContain(yaRespondida.Pregunta, turno.Sugerencias ?? []);
    }

    [Fact]
    public async Task A_successful_turn_in_a_category_with_no_catalog_match_suggests_nothing()
    {
        // "ambigua" has zero entries in the real catalog (see the categories
        // asserted in EjemplosEjecutablesTests-adjacent coverage): there is
        // nothing to fall back to, and the requirement is that nothing does.
        await SembrarAsync();
        const string SinCoincidencias = """
            {"es_contestable": true, "sql": "SELECT count(*) AS cantidad FROM designaciones.pedidos",
             "razonamiento": "x", "categoria": "ambigua"}
            """;
        var banco = Banco(SinCoincidencias);

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos pedidos hay?", TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.Respondida, turno.Estado);
        Assert.True(turno.Sugerencias is null or { Count: 0 });
    }

    [Fact]
    public async Task Post_success_suggestions_exclude_an_example_the_actor_cannot_execute()
    {
        // Synthetic catalog, same reason CapacidadesTests uses one for
        // EjecutableAsync: the real catalog has nothing that touches personal
        // data, so this is the only way to prove the filter actually filters.
        await SembrarAsync();
        var catalogoFalso = new CatalogoFalso(
            new EjemploSql(
                "¿Cuál es el documento de cada persona?",
                "SELECT documento FROM identity.personas",
                "consulta_simple"),
            new EjemploSql(
                "¿Qué carreras están vigentes?",
                "SELECT name FROM identity.carreras WHERE is_active",
                "consulta_simple"));
        var sugeridor = new SugerenciasDeSeguimiento(Apertura, catalogoFalso);
        var ct = TestContext.Current.CancellationToken;

        var conRolBasico = await sugeridor.ObtenerAsync(
            Secretaria, "consulta_simple", ContarDocentes, conDatosPersonales: false, ct);
        var conRolPii = await sugeridor.ObtenerAsync(
            Secretaria, "consulta_simple", ContarDocentes, conDatosPersonales: true, ct);

        Assert.DoesNotContain("¿Cuál es el documento de cada persona?", conRolBasico);
        Assert.Contains("¿Qué carreras están vigentes?", conRolBasico);
        Assert.Contains("¿Cuál es el documento de cada persona?", conRolPii);
    }

    // --------------------------------------------- la consulta tras el permiso

    [Fact]
    public async Task Sin_el_permiso_la_consulta_no_viaja()
    {
        await SembrarAsync();
        var banco = Banco(ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.");

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes están designados?",
            TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.Respondida, turno.Estado);
        Assert.Null(turno.Sql);
    }

    [Fact]
    public async Task Con_el_permiso_viaja_y_es_la_consulta_que_se_ejecuto()
    {
        await SembrarAsync();
        await ConcederVerConsultaAsync();
        var banco = Banco(ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.");

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes están designados?",
            TestContext.Current.CancellationToken);

        Assert.Equal(ContarDocentes, turno.Sql);
    }

    [Fact]
    public async Task Recien_migrada_ningun_rol_tiene_el_permiso()
    {
        // No es prudencia genérica: la consulta generada es superficie de
        // diagnóstico y su WHERE puede llevar un documento. Quién necesita verla es
        // una decisión del Departamento, no de quien escribió la migración.
        var concedido = await EscalarAsync<long>(
            """
            SELECT count(*) FROM identity.rol_permisos rp
              JOIN identity.permisos p ON p.id = rp.permiso_id
             WHERE p.code = 'asistente.ver_consulta'
            """);

        Assert.Equal(0L, concedido);
    }

    [Fact]
    public async Task El_permiso_existe_sembrado()
    {
        var filas = await EscalarAsync<long>(
            "SELECT count(*) FROM identity.permisos WHERE code = 'asistente.ver_consulta'");

        Assert.Equal(1L, filas);
    }

    // ------------------------------------------------ nada crudo hacia afuera

    [Fact]
    public async Task Un_rechazo_del_motor_no_nombra_tablas_ni_codigos_de_error()
    {
        // Con un actor de ámbito acotado, el turno usa el rol de lectura SIN datos
        // personales y el motor rechaza la consulta con 42501. Ese mensaje crudo
        // nombra la tabla, así que va al registro y no a la respuesta.
        await SembrarAsync();
        var banco = Banco(
            ProveedorGuionado.Generacion("SELECT documento FROM identity.personas"));

        var turno = await banco.Capa().ResponderAsync(
            Coordinador, null, "¿cuáles son los documentos?",
            TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.NoContestable, turno.Estado);

        Assert.DoesNotContain("personas", turno.Respuesta, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("42501", turno.Respuesta, StringComparison.Ordinal);
        Assert.DoesNotContain("permission denied", turno.Respuesta, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------ apoyo

    private BancoDelAsistente Banco(params string[] guion) =>
        Banco(new OpcionesAsistente(), guion);

    private BancoDelAsistente Banco(OpcionesAsistente configuracion, params string[] guion)
    {
        var (basica, pii) = CadenasDeLectura();

        return BancoDelAsistente.Armar(
            basica, pii, ClasificadorDeSensibilidad(),
            Apertura, configuracion, guion: guion);
    }

    private async Task ConcederVerConsultaAsync()
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO identity.rol_permisos (rol_id, permiso_id)
            SELECT r.id, p.id
              FROM identity.roles r
             CROSS JOIN identity.permisos p
             WHERE p.code = 'asistente.ver_consulta' AND r.code = 'secretaria'
            ON CONFLICT DO NOTHING;
            """,
            conexion);

        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task AgregarColisionesAsync()
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"""
            INSERT INTO identity.materias (id, code, name, carrera_id, is_active)
            VALUES ('70000000-0000-4000-8000-0000000009f1', '04910', 'Bases de Datos',
                    '{Industrial}', true);
            """,
            conexion);

        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// A stand-in <see cref="ISelectorDeEjemplos"/> with a hand-picked catalog, for
    /// exercising the executability filter with entries the real catalog does not
    /// have (see <c>CapacidadesTests</c>'s own synthetic-query test for why: the
    /// real catalog touches no personal data, so the filter is a no-op against it).
    /// </summary>
    private sealed class CatalogoFalso(params EjemploSql[] catalogo) : ISelectorDeEjemplos
    {
        public string Huella => "catalogo-falso";
        public IReadOnlyList<EjemploSql> Catalogo { get; } = catalogo;
        public IReadOnlyList<EjemploSql> Elegir(string pregunta) => [];
    }
}
