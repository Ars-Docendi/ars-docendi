using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El migrador del asistente contra bases que NO están vacías.
/// </summary>
/// <remarks>
/// Es el hueco que dejaba pasar el defecto: Testcontainers arranca siempre de una
/// base vacía, así que todo el resto de la suite ejercita el único caso en que un
/// <c>CREATE TABLE IF NOT EXISTS</c> alcanza. La promesa de
/// <see cref="IMigradorModulo"/> —«re-ejecutar sobre una base ya migrada no produce
/// cambios ni error»— y la del encabezado del DDL hablan del otro caso, que hasta
/// ahora nadie corría.
///
/// Los tres escenarios de acá son los tres estados en que una base real puede
/// llegar al arranque: al día, vieja de una versión, y rota a mano.
/// </remarks>
public sealed class MigracionDelAsistenteTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_migracion")
{
    private static readonly Guid Alguien = Guid.Parse("a0000000-0000-4000-8000-000000000009");
    private static readonly DateTimeOffset Ancla = new(2027, 6, 15, 14, 37, 12, TimeSpan.Zero);

    /// <summary>
    /// Las columnas del registro operativo que existían antes de que se le agregara
    /// ninguna: la forma exacta de una base aprovisionada con la primera versión.
    /// </summary>
    private const string TablaVieja =
        """
        CREATE SCHEMA asistente;

        CREATE TABLE asistente.registro_operativo (
            id                 bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            actor_id           uuid        NOT NULL,
            ocurrido_en        timestamptz NOT NULL,
            carril             text        NOT NULL,
            estado             text        NOT NULL,
            llamadas_al_modelo integer     NOT NULL,
            tokens_de_entrada  integer     NOT NULL,
            tokens_de_salida   integer     NOT NULL,
            latencia_ms        integer     NOT NULL,
            hubo_reintento     boolean     NOT NULL,
            truncado           boolean     NOT NULL
        );
        """;

    // ------------------------------------------------- el contrato de IMigradorModulo

    [Fact]
    public async Task Correr_el_migrador_dos_veces_sobre_la_misma_base_no_falla_ni_cambia_nada()
    {
        // Es el test que el contrato pide con todas las letras y que nadie había
        // escrito. La base del fixture ya viene migrada, así que estas dos corridas
        // son la segunda y la tercera.
        var antes = await FormaDelSchemaAsync();

        await Migrador().MigrarAsync(TestContext.Current.CancellationToken);
        var despuesDeUna = await FormaDelSchemaAsync();

        await Migrador().MigrarAsync(TestContext.Current.CancellationToken);
        var despuesDeDos = await FormaDelSchemaAsync();

        Assert.Equal(antes, despuesDeUna);
        Assert.Equal(antes, despuesDeDos);
    }

    // ---------------------------------------------- the feedback table (task 1.1)

    [Fact]
    public async Task The_feedback_table_is_created_with_its_foreign_key_to_the_analytic_row()
    {
        await Migrador().MigrarAsync(TestContext.Current.CancellationToken);

        var columnas = await ColumnasDeAsync("retroalimentacion_turno");
        Assert.Equal(["actualizado_en", "analitico_id", "razon", "voto"], columnas);

        // The foreign key: analitico_id references registro_analitico(id), and the
        // migration's own idempotent re-run (covered above) already proves it
        // converges on a second application.
        Assert.Equal(1L, await EscalarAsync<long>(
            """
            SELECT count(*)
              FROM information_schema.table_constraints tc
              JOIN information_schema.constraint_column_usage ccu
                ON ccu.constraint_name = tc.constraint_name
               AND ccu.table_schema = tc.table_schema
             WHERE tc.table_schema = 'asistente'
               AND tc.table_name = 'retroalimentacion_turno'
               AND tc.constraint_type = 'FOREIGN KEY'
               AND ccu.table_name = 'registro_analitico'
               AND ccu.column_name = 'id'
            """));
    }

    // --------------------------------------- own history tables (task 1.1/2.1)

    [Fact]
    public async Task El_historial_se_crea_con_la_cascada_de_turno_a_hilo()
    {
        await Migrador().MigrarAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                "actor_id", "archivada_en", "borrado_pendiente_desde", "creado_en", "id",
                "lote_de_borrado", "titulo", "ultima_actividad",
            ],
            await ColumnasDeAsync("hilo_historico"));

        Assert.Equal(
            ["estado", "hilo_id", "id", "ocurrido_en", "pregunta", "referencias", "sql_resuelto"],
            await ColumnasDeAsync("turno_historico"));

        // ON DELETE CASCADE from turno_historico to hilo_historico.
        Assert.Equal(1L, await EscalarAsync<long>(
            """
            SELECT count(*)
              FROM information_schema.referential_constraints rc
              JOIN information_schema.table_constraints tc
                ON tc.constraint_name = rc.constraint_name
               AND tc.table_schema = rc.constraint_schema
              JOIN information_schema.constraint_column_usage ccu
                ON ccu.constraint_name = rc.unique_constraint_name
               AND ccu.table_schema = rc.unique_constraint_schema
             WHERE tc.table_schema = 'asistente'
               AND tc.table_name = 'turno_historico'
               AND ccu.table_name = 'hilo_historico'
               AND rc.delete_rule = 'CASCADE'
            """));
    }

    [Fact]
    public async Task La_auditoria_de_soporte_no_lleva_clave_foranea_al_historial()
    {
        await Migrador().MigrarAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            ["hilo_historico_id", "id", "lector_id", "ocurrido_en", "razon", "sujeto_id"],
            await ColumnasDeAsync("auditoria_acceso_historial"));

        // Deliberately no foreign key at all from this table (design.md D10):
        // the audit row has to survive the subject deleting the conversation
        // it describes.
        Assert.Equal(0L, await EscalarAsync<long>(
            """
            SELECT count(*)
              FROM information_schema.table_constraints
             WHERE table_schema = 'asistente'
               AND table_name = 'auditoria_acceso_historial'
               AND constraint_type = 'FOREIGN KEY'
            """));
    }

    // --------------------------- D7 (asistente-rediseno-v3): retiring `lento`
    //
    // `lento` stays valid at the database so an existing vote is never rewritten;
    // the API (`RazonesDeRetroalimentacion.Todas`) is what rejects it on new
    // submissions. `faltan_datos` has to reach bases provisioned before it existed,
    // which is what the guarded CHECK replacement in 003 does (the one ratified
    // exception to the no-DROP rule, see ArquitecturaAsistenteTests).

    [Fact]
    public async Task Una_base_con_el_check_viejo_acepta_faltan_datos_y_conserva_sus_votos()
    {
        await Migrador().MigrarAsync(TestContext.Current.CancellationToken);
        await EjecutarAsync(
            """
            ALTER TABLE asistente.retroalimentacion_turno
                DROP CONSTRAINT retroalimentacion_turno_razon_valida;
            ALTER TABLE asistente.retroalimentacion_turno
                ADD CONSTRAINT retroalimentacion_turno_razon_valida
                CHECK (razon IS NULL OR razon IN ('datos_incorrectos', 'no_entendio_la_pregunta', 'lento', 'otro'));
            """);
        var votoViejo = await SembrarRegistroAnaliticoAsync();
        await EjecutarAsync(
            "INSERT INTO asistente.retroalimentacion_turno (analitico_id, voto, razon, actualizado_en) "
            + "VALUES (@id, false, 'lento', now())",
            ("id", votoViejo));

        await Migrador().MigrarAsync(TestContext.Current.CancellationToken);
        var despuesDeUna = await FormaDelSchemaAsync();
        await Migrador().MigrarAsync(TestContext.Current.CancellationToken);

        var votoNuevo = await SembrarRegistroAnaliticoAsync();
        await EjecutarAsync(
            "INSERT INTO asistente.retroalimentacion_turno (analitico_id, voto, razon, actualizado_en) "
            + "VALUES (@id, false, 'faltan_datos', now())",
            ("id", votoNuevo));

        Assert.Equal("lento", await EscalarAsync<string>(
            "SELECT razon FROM asistente.retroalimentacion_turno WHERE analitico_id = @id",
            ("id", votoViejo)));
        Assert.Equal(despuesDeUna, await FormaDelSchemaAsync());
    }

    [Fact]
    public async Task Una_fila_lento_sobrevive_sin_que_nada_la_toque()
    {
        await Migrador().MigrarAsync(TestContext.Current.CancellationToken);
        var analiticoId = await SembrarRegistroAnaliticoAsync();
        await EjecutarAsync(
            "INSERT INTO asistente.retroalimentacion_turno (analitico_id, voto, razon, actualizado_en) "
            + "VALUES (@id, false, 'lento', now())",
            ("id", analiticoId));

        await Migrador().MigrarAsync(TestContext.Current.CancellationToken);

        Assert.Equal("lento", await EscalarAsync<string>(
            "SELECT razon FROM asistente.retroalimentacion_turno WHERE analitico_id = @id",
            ("id", analiticoId)));
    }

    [Fact]
    public async Task Faltan_datos_entra_como_razon_nueva()
    {
        await Migrador().MigrarAsync(TestContext.Current.CancellationToken);
        var analiticoId = await SembrarRegistroAnaliticoAsync();

        await EjecutarAsync(
            "INSERT INTO asistente.retroalimentacion_turno (analitico_id, voto, razon, actualizado_en) "
            + "VALUES (@id, false, 'faltan_datos', now())",
            ("id", analiticoId));

        Assert.Equal("faltan_datos", await EscalarAsync<string>(
            "SELECT razon FROM asistente.retroalimentacion_turno WHERE analitico_id = @id",
            ("id", analiticoId)));
    }

    // ------------------------------------------------------------ la base vieja

    [Fact]
    public async Task Una_base_que_ya_tenia_la_tabla_recibe_las_columnas_que_le_faltan()
    {
        await RehacerLaBaseViejaAsync();

        await Migrador().MigrarAsync(TestContext.Current.CancellationToken);

        var columnas = await ColumnasDeAsync("registro_operativo");

        Assert.Contains("proveedor", columnas);
        Assert.Contains("tokens_de_cache", columnas);
        Assert.Contains("intencion_sombra", columnas);
    }

    // ------------------------------------ la base vieja del historial (tasks.md 2.1)

    [Fact]
    public async Task Una_base_vieja_del_historial_recibe_las_columnas_de_archivo_y_borrado()
    {
        // MISMO DEFECTO QUE `Una_base_que_ya_tenia_la_tabla_recibe_las_columnas_que_le_faltan`,
        // esta vez sobre `hilo_historico`/`turno_historico`: contra una base
        // que ya las tenía SIN las tres columnas nuevas, el CREATE TABLE IF
        // NOT EXISTS de 004 es un no-op y sólo el ALTER las repone.
        await EjecutarAsync("DROP SCHEMA asistente CASCADE");
        await EjecutarAsync(
            """
            CREATE SCHEMA asistente;

            CREATE TABLE asistente.hilo_historico (
                id               uuid        PRIMARY KEY,
                actor_id         uuid        NOT NULL,
                titulo           text        NOT NULL,
                creado_en        timestamptz NOT NULL,
                ultima_actividad timestamptz NOT NULL
            );

            CREATE TABLE asistente.turno_historico (
                id            uuid        PRIMARY KEY,
                hilo_id       uuid        NOT NULL REFERENCES asistente.hilo_historico(id) ON DELETE CASCADE,
                pregunta      text        NOT NULL,
                sql_resuelto  text        NULL,
                estado        text        NOT NULL,
                ocurrido_en   timestamptz NOT NULL
            );
            """);

        await Migrador().MigrarAsync(TestContext.Current.CancellationToken);

        var columnasDelHilo = await ColumnasDeAsync("hilo_historico");
        Assert.Contains("archivada_en", columnasDelHilo);
        Assert.Contains("borrado_pendiente_desde", columnasDelHilo);
        Assert.Contains("lote_de_borrado", columnasDelHilo);
        Assert.Contains("referencias", await ColumnasDeAsync("turno_historico"));
    }

    [Fact]
    public async Task La_fila_vieja_sobrevive_y_sobre_la_base_migrada_el_registro_vuelve_a_guardar()
    {
        // LA MITAD QUE IMPORTA. Un `ADD COLUMN ... NOT NULL` sin `DEFAULT` sobre una
        // tabla VACÍA pasa; sobre una con filas, PostgreSQL lo rechaza. Sin esta
        // fila, el test daría verde con una migración que en producción —donde la
        // tabla tiene noventa días de registros— aborta.
        await RehacerLaBaseViejaAsync();
        await SembrarFilaViejaAsync();

        await Migrador().MigrarAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.registro_operativo"));

        // Y el modo de falla que este change cierra, que no era el ruidoso: con la
        // columna faltante el INSERT reventaba, el fallo se tragaba, el endpoint
        // devolvía 200 y el registro dejaba de guardar sin que nada avisara. La única
        // forma de afirmar que eso se terminó es contar filas después de escribir una.
        var registro = new RegistroDelTurno(
            new CadenaDuena(Cadena), NullLogger<RegistroDelTurno>.Instance);
        await registro.RegistrarAsync(Turno(), TestContext.Current.CancellationToken);

        Assert.Equal(2L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.registro_operativo"));
        Assert.Equal(1L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.registro_analitico"));
    }

    // -------------------------------------------------- el chequeo de arranque

    [Theory]
    [InlineData("registro_operativo", "latencia_ms")]
    [InlineData("registro_analitico", "categoria")]
    public async Task El_migrador_no_deja_arrancar_si_falta_una_columna_que_el_modulo_escribe(
        string tabla, string columna)
    {
        // Las dos columnas de acá NO las repone ningún `ADD COLUMN IF NOT EXISTS`:
        // son originales de sus tablas. Es a propósito — lo que se prueba es la red
        // que hay DEBAJO de los ALTER, para el día en que alguien agregue una columna
        // al `CREATE TABLE` y se olvide del ALTER.
        await EjecutarAsync($"ALTER TABLE asistente.{tabla} DROP COLUMN {columna}");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Migrador().MigrarAsync(TestContext.Current.CancellationToken));

        Assert.Contains(columna, error.Message, StringComparison.Ordinal);
        Assert.Contains(tabla, error.Message, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ apoyo

    private MigradorAsistente Migrador() =>
        new(Options.Create(new OpcionesAsistente
        {
            RolSoloLectura = RolSoloLectura,
            RolSoloLecturaPii = RolSoloLecturaPii,
        }),
            new CadenaDuena(Cadena),
            NullLogger<MigradorAsistente>.Instance);

    /// <summary>Deja la base en la forma que tenía antes de las tres columnas nuevas.</summary>
    private async Task RehacerLaBaseViejaAsync()
    {
        await EjecutarAsync("DROP SCHEMA asistente CASCADE");
        await EjecutarAsync(TablaVieja);
    }

    private Task<Guid> SembrarRegistroAnaliticoAsync() =>
        EscalarAsync<Guid>(
            "INSERT INTO asistente.registro_analitico (pregunta, categoria, estado, dia) "
            + "VALUES ('¿cuántos docentes hay?', 'cruce_de_tablas', 'respondida', current_date) "
            + "RETURNING id");

    private Task SembrarFilaViejaAsync() =>
        EjecutarAsync(
            """
            INSERT INTO asistente.registro_operativo
                (actor_id, ocurrido_en, carril, estado, llamadas_al_modelo,
                 tokens_de_entrada, tokens_de_salida, latencia_ms, hubo_reintento, truncado)
            VALUES ('a0000000-0000-4000-8000-000000000009', now(), 'Sql', 'Respondida',
                    2, 1200, 80, 1450, false, false)
            """);

    private static TurnoParaRegistrar Turno() =>
        new(Alguien,
            Guid.NewGuid(),
            Ancla,
            CarrilDelTurno.Sql,
            EstadoDelTurno.Respondida,
            LlamadasAlModelo: 2,
            TokensDeEntrada: 1200,
            TokensDeSalida: 80,
            TokensDeCache: 900,
            LatenciaMs: 1450,
            HuboReintento: false,
            Truncado: false,
            "¿cuántos docentes hay?",
            "cruce_de_tablas",
            Proveedor: "anthropic/claude-sonnet-5",
            IntencionSombra: null);

    /// <summary>
    /// Columnas, índices y privilegios del schema, en una lista comparable.
    /// </summary>
    /// <remarks>
    /// «No produce cambios» se afirma contra el catálogo y no contra la ausencia de
    /// excepción: una segunda corrida que agregara un default o revocara de más
    /// terminaría sin error igual.
    /// </remarks>
    private Task<List<string>> FormaDelSchemaAsync() =>
        LeerAsync<string>(
            """
            SELECT 'columna · ' || table_name || '.' || column_name || ' · ' || data_type
                   || ' · ' || is_nullable || ' · ' || coalesce(column_default, 'sin default')
              FROM information_schema.columns
             WHERE table_schema = 'asistente'
            UNION ALL
            SELECT 'indice · ' || indexname || ' · ' || indexdef
              FROM pg_indexes WHERE schemaname = 'asistente'
            UNION ALL
            SELECT 'privilegio · ' || grantee || ' · ' || table_name || ' · ' || privilege_type
              FROM information_schema.role_table_grants WHERE table_schema = 'asistente'
             ORDER BY 1
            """);

    private Task<List<string>> ColumnasDeAsync(string tabla) =>
        LeerAsync<string>(
            $"""
            SELECT column_name FROM information_schema.columns
             WHERE table_schema = 'asistente' AND table_name = '{tabla}'
             ORDER BY column_name
            """);

    private async Task<List<T>> LeerAsync<T>(string sql)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        await using var lector = await comando.ExecuteReaderAsync(
            TestContext.Current.CancellationToken);

        var valores = new List<T>();

        while (await lector.ReadAsync(TestContext.Current.CancellationToken))
        {
            valores.Add(lector.GetFieldValue<T>(0));
        }

        return valores;
    }

}
