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
