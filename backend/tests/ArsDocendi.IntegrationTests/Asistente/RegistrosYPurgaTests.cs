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
/// Verifica los dos registros desvinculados y la purga que los acota.
/// </summary>
/// <remarks>
/// Va contra una base real porque lo que se prueba es una <b>propiedad del
/// esquema</b>: que las dos tablas no compartan ninguna columna que permita
/// cruzarlas, que ninguna tenga disparador de auditoría, y que el rol de solo
/// lectura del asistente no pueda leerlas. Nada de eso se puede afirmar mirando
/// código.
/// </remarks>
[Collection(ColeccionPostgres.Nombre)]
public sealed class RegistrosYPurgaTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_registros")
{
    /// <summary>
    /// El ancla del reloj falso, deliberadamente lejos de la fecha real.
    /// </summary>
    /// <remarks>
    /// No es un detalle. Con un ancla cercana a hoy, una purga que usara el reloj
    /// del sistema en lugar del inyectado daría verde igual, y el test no probaría
    /// nada de lo que dice probar. Con el ancla en el futuro, las filas «viejas» del
    /// reloj falso todavía no ocurrieron para el reloj real, así que confundirlos
    /// hace fallar el test.
    /// </remarks>
    private static readonly DateTimeOffset Ancla = new(2027, 6, 15, 14, 37, 12, TimeSpan.Zero);
    private static readonly Guid Alguien = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private static readonly Guid Otro = Guid.Parse("a0000000-0000-4000-8000-000000000003");

    // ------------------------------------------------- lo que guarda cada uno

    [Fact]
    public async Task El_operativo_no_guarda_el_texto_de_la_pregunta()
    {
        await RegistrarAsync(Turno(pregunta: "¿cuál es el documento de Gómez?"));

        var columnas = await ColumnasDeAsync("registro_operativo");

        Assert.DoesNotContain("pregunta", columnas);
        Assert.Empty(await BuscarTextoAsync("registro_operativo", "Gómez"));
    }

    [Fact]
    public async Task El_operativo_guarda_el_nombre_del_proveedor_y_ninguna_credencial()
    {
        // El nombre del proveedor es lo que hace atribuible el costo: sin él, un
        // cambio de modelo mezcla dos series de precios distintos en la misma tabla.
        // La credencial es lo contrario —no aporta nada y todo lo que puede hacer es
        // filtrarse—, y la única forma de que no esté es que nunca llegue hasta acá.
        await RegistrarAsync(Turno());

        var proveedores = await LeerAsync<string>(
            "SELECT proveedor FROM asistente.registro_operativo");

        Assert.Equal(["anthropic/claude-sonnet-5"], proveedores);

        // Ninguna de las dos tablas guarda nada con forma de credencial. Se busca la
        // forma y no un valor concreto: un test contra la clave de un ambiente daría
        // verde en cualquier otro.
        foreach (var tabla in (string[])["registro_operativo", "registro_analitico"])
        {
            Assert.Empty(await BuscarTextoAsync(tabla, "sk-ant"));
            Assert.Empty(await BuscarTextoAsync(tabla, "api_key"));
        }
    }

    [Fact]
    public async Task El_nombre_del_proveedor_no_viaja_al_registro_analitico()
    {
        // Es la mitad que importa de la columna nueva. En el analítico sería una
        // dimensión más por la cual agrupar preguntas, y con treinta usuarios cada
        // dimensión que se agrega achica el conjunto en el que alguien se esconde.
        await RegistrarAsync(Turno());

        Assert.DoesNotContain("proveedor", await ColumnasDeAsync("registro_analitico"));
    }

    [Fact]
    public async Task El_analitico_no_guarda_actor_ni_hora()
    {
        await RegistrarAsync(Turno());

        var columnas = await ColumnasDeAsync("registro_analitico");

        Assert.DoesNotContain("actor_id", columnas);
        Assert.DoesNotContain("ocurrido_en", columnas);
        Assert.Contains("dia", columnas);
    }

    [Fact]
    public async Task La_fecha_analitica_es_de_tipo_date_y_no_timestamp()
    {
        // No alcanza con truncar el valor al escribir: una columna timestamptz
        // invita a que la próxima inserción guarde la hora y nadie lo note.
        var tipo = await EscalarAsync<string>(
            """
            SELECT data_type FROM information_schema.columns
             WHERE table_schema = 'asistente' AND table_name = 'registro_analitico'
               AND column_name = 'dia'
            """);

        Assert.Equal("date", tipo);
    }

    [Fact]
    public async Task Dos_turnos_del_mismo_dia_son_indistinguibles_en_el_tiempo()
    {
        await RegistrarAsync(Turno(Alguien, Ancla, "¿cuántos docentes hay?"));
        await RegistrarAsync(
            Turno(Otro, Ancla.AddSeconds(4), "¿qué pedidos están pendientes?"));

        var dias = await LeerAsync<DateTime>("SELECT dia FROM asistente.registro_analitico");

        Assert.Equal(2, dias.Count);
        Assert.Single(dias.Distinct());
    }

    [Fact]
    public async Task Cruzar_los_dos_registros_no_reconstruye_quien_pregunto_que()
    {
        // Un join necesita una columna que coincida en nombre Y en tipo. Las dos
        // tablas comparten el nombre `id`, y ahí termina el parecido: en el
        // operativo es un bigint secuencial y en el analítico un uuid aleatorio.
        await RegistrarAsync(Turno(Alguien, Ancla, "¿cuántos docentes hay?"));
        await RegistrarAsync(Turno(Otro, Ancla.AddSeconds(4), "¿qué pedidos hay?"));

        var operativo = await ColumnasConTipoAsync("registro_operativo");
        var analitico = await ColumnasConTipoAsync("registro_analitico");

        var joineables = operativo.Intersect(analitico).OrderBy(c => c).ToList();

        // «estado» es lo único que queda, y es una categoría de cuatro valores
        // repetidos en todas las filas: no identifica a nadie.
        Assert.Equal(["estado:text"], joineables);
    }

    [Fact]
    public async Task La_clave_del_analitico_es_aleatoria_y_no_secuencial()
    {
        // ÉSTA ES LA TRAMPA QUE HABÍA QUE EVITAR. Con una identidad autoincremental
        // en las dos tablas, la fila n de una y la fila n de la otra serían el mismo
        // turno: el orden de inserción sería, él mismo, la clave del join que quitar
        // la hora existe para impedir.
        var tipo = await EscalarAsync<string>(
            """
            SELECT data_type FROM information_schema.columns
             WHERE table_schema = 'asistente' AND table_name = 'registro_analitico'
               AND column_name = 'id'
            """);

        var esIdentidad = await EscalarAsync<string>(
            """
            SELECT is_identity FROM information_schema.columns
             WHERE table_schema = 'asistente' AND table_name = 'registro_analitico'
               AND column_name = 'id'
            """);

        Assert.Equal("uuid", tipo);
        Assert.Equal("NO", esIdentidad);

        // Y los valores efectivamente no salen en orden.
        await RegistrarAsync(Turno(Alguien, Ancla, "primera"));
        await RegistrarAsync(Turno(Otro, Ancla.AddSeconds(1), "segunda"));

        var claves = await LeerAsync<Guid>("SELECT id FROM asistente.registro_analitico");

        Assert.Equal(2, claves.Distinct().Count());
    }

    [Fact]
    public async Task Ningun_registro_guarda_filas_ni_consultas()
    {
        // La consulta y las filas ni siquiera llegan al escritor: no están en el
        // tipo que recibe. Este test cierra la otra mitad —que tampoco haya dónde
        // ponerlas— mirando el esquema.
        var columnas = (await ColumnasDeAsync("registro_operativo"))
            .Concat(await ColumnasDeAsync("registro_analitico"))
            .ToList();

        Assert.DoesNotContain("sql", columnas);
        Assert.DoesNotContain("consulta", columnas);
        Assert.DoesNotContain("filas", columnas);
        Assert.DoesNotContain("resultado", columnas);
    }

    [Fact]
    public async Task El_turno_que_llega_al_registro_no_trae_filas_ni_consulta()
    {
        // La contracara del anterior, del lado del contrato: lo que la capa le
        // entrega al registro no incluye nada de lo que el enmascaramiento protegió.
        var propiedades = typeof(TurnoParaRegistrar)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        Assert.DoesNotContain("Filas", propiedades);
        Assert.DoesNotContain("Columnas", propiedades);
        Assert.DoesNotContain("Sql", propiedades);
        Assert.DoesNotContain("Respuesta", propiedades);

        await Task.CompletedTask;
    }

    // -------------------------------------------------------- sin auditoría

    [Fact]
    public async Task Ninguna_de_las_dos_tablas_tiene_disparador_de_auditoria()
    {
        // Todas las tablas de identity y designaciones llaman a audit.attach al
        // final de su archivo. Acá NO, porque audit.change_log guarda la fila entera
        // en JSON y no tiene retención: el texto de cada pregunta sobreviviría a la
        // purga en otro lado.
        var disparadores = await LeerAsync<string>(
            """
            SELECT tgname FROM pg_catalog.pg_trigger t
              JOIN pg_catalog.pg_class c ON c.oid = t.tgrelid
              JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
             WHERE n.nspname = 'asistente' AND NOT t.tgisinternal
            """);

        Assert.Empty(disparadores);
    }

    [Fact]
    public async Task Escribir_un_turno_no_hace_crecer_la_bitacora_de_auditoria()
    {
        var antes = await EscalarAsync<long>("SELECT count(*) FROM audit.change_log");

        await RegistrarAsync(Turno());

        var despues = await EscalarAsync<long>("SELECT count(*) FROM audit.change_log");

        Assert.Equal(antes, despues);
    }

    // ------------------------------------------------------- quién escribe

    [Fact]
    public async Task El_rol_de_solo_lectura_no_puede_leer_los_registros()
    {
        // El registro analítico tiene el texto de las preguntas de TODOS. Un
        // asistente que pudiera consultarlo respondería «qué preguntó fulano» a
        // cualquiera con el permiso de consulta.
        await RegistrarAsync(Turno());

        var (basica, _) = CadenasDeLectura();

        await using var conexion = new NpgsqlConnection(basica.Valor);
        await conexion.OpenAsync(TestContext.Current.CancellationToken);

        await using var comando = new NpgsqlCommand(
            "SELECT count(*) FROM asistente.registro_analitico", conexion);

        var falla = await Assert.ThrowsAsync<PostgresException>(() =>
            comando.ExecuteScalarAsync(TestContext.Current.CancellationToken));

        // 42501 insufficient_privilege · 3F000 invalid_schema_name, según si el
        // motor llega a resolver el schema.
        Assert.Contains(falla.SqlState, new[] { "42501", "3F000" });
    }

    [Fact]
    public async Task El_rol_de_solo_lectura_no_puede_escribirlos()
    {
        var (basica, _) = CadenasDeLectura();

        await using var conexion = new NpgsqlConnection(basica.Valor);
        await conexion.OpenAsync(TestContext.Current.CancellationToken);

        await using var comando = new NpgsqlCommand(
            "INSERT INTO asistente.registro_analitico (pregunta, categoria, estado, dia) "
            + "VALUES ('x', 'y', 'z', current_date)",
            conexion);

        await Assert.ThrowsAsync<PostgresException>(() =>
            comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
    }

    // ------------------------------------------------- tolerancia a fallos

    [Fact]
    public async Task Sin_tablas_de_registro_el_turno_responde_igual()
    {
        // Un registro que rompe el turno que estaba registrando convierte la
        // observabilidad en una fuente de indisponibilidad.
        await using (var conexion = await AbrirConexionAsync())
        {
            await using var drop = new NpgsqlCommand("DROP SCHEMA asistente CASCADE", conexion);
            await drop.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var registro = new RegistroDelTurno(
            new CadenaDuena(Cadena), NullLogger<RegistroDelTurno>.Instance);

        // No tira: se loguea y sigue.
        await registro.RegistrarAsync(Turno(), TestContext.Current.CancellationToken);
    }

    // --------------------------------------- lo que la capa manda a registrar

    [Fact]
    public async Task Un_turno_deja_una_fila_en_cada_registro()
    {
        await SembrarAsync();

        await Capa().ResponderAsync(
            Alguien, null, "¿cuántos docentes están designados?",
            TestContext.Current.CancellationToken);

        Assert.Equal(1L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.registro_operativo"));
        Assert.Equal(1L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.registro_analitico"));
    }

    [Fact]
    public async Task El_analitico_guarda_lo_que_escribio_el_usuario_y_no_la_reescritura()
    {
        // El registro analítico existe para saber CÓMO PREGUNTA LA GENTE. Guardar la
        // versión reescrita mediría al reescritor en vez de a los usuarios, y la
        // conclusión —«nadie pregunta así»— sería sobre el prompt, no sobre nadie.
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;
        var capa = Capa(
            ProveedorGuionado.Generacion(ContarDocentes),
            "Hay 4 docentes.",
            "¿cuántos docentes están designados en Sistemas?",
            ProveedorGuionado.Generacion(ContarDocentes),
            "Hay 2 docentes.");

        var primero = await capa.ResponderAsync(
            Alguien, null, "¿cuántos docentes están designados?", ct);

        await capa.ResponderAsync(Alguien, primero.Hilo, "¿y en Sistemas?", ct);

        var preguntas = await LeerAsync<string>(
            "SELECT pregunta FROM asistente.registro_analitico ORDER BY pregunta");

        Assert.Contains("¿y en Sistemas?", preguntas);
        Assert.DoesNotContain(preguntas, p => p.Contains("designados en Sistemas", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Un_turno_con_filas_sensibles_no_deja_ningun_valor_en_ningun_registro()
    {
        // Son exactamente los datos que el enmascaramiento acaba de sacar del camino
        // de salida hacia el proveedor. Volver a escribirlos acá los devolvería por
        // la puerta de la observabilidad, y sin retención en la bitácora.
        await SembrarAsync();

        await Capa(
            ProveedorGuionado.Generacion(
                "SELECT apellido, documento FROM identity.personas ORDER BY legajo"),
            "Encontré los docentes.")
            .ResponderAsync(
                Alguien, null, "¿cuál es el documento de los docentes?",
                TestContext.Current.CancellationToken);

        Assert.Empty(await BuscarTextoAsync("registro_operativo", DocumentoSembrado));
        Assert.Empty(await BuscarTextoAsync("registro_analitico", DocumentoSembrado));
    }

    [Fact]
    public async Task El_operativo_informa_las_llamadas_y_los_tokens_del_turno()
    {
        await SembrarAsync();

        await Capa(ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.")
            .ResponderAsync(
                Alguien, null, "¿cuántos docentes están designados?",
                TestContext.Current.CancellationToken);

        var llamadas = await EscalarAsync<int>(
            "SELECT llamadas_al_modelo FROM asistente.registro_operativo");
        var entrada = await EscalarAsync<int>(
            "SELECT tokens_de_entrada FROM asistente.registro_operativo");

        Assert.Equal(2, llamadas);
        Assert.True(entrada > 0, "El turno facturó tokens de entrada y el registro los perdió.");
    }

    [Fact]
    public async Task Un_saludo_queda_registrado_en_el_carril_sin_datos()
    {
        await SembrarAsync();

        await Capa().ResponderAsync(
            Alguien, null, "hola", TestContext.Current.CancellationToken);

        Assert.Equal(
            nameof(CarrilDelTurno.SinDatos),
            await EscalarAsync<string>("SELECT carril FROM asistente.registro_operativo"));
    }

    // ------------------------------------------------------------- la purga

    [Fact]
    public async Task La_purga_borra_lo_que_supero_la_ventana()
    {
        var reloj = new RelojFijo(Ancla);

        await RegistrarAsync(Turno(Alguien, Ancla.AddDays(-120), "vieja"));
        await RegistrarAsync(Turno(Alguien, Ancla.AddDays(-10), "reciente"));

        var borradas = await Purga(reloj, dias: 90).PurgarAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(2, borradas);
        Assert.Equal(1L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.registro_operativo"));
        Assert.Equal(1L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.registro_analitico"));
    }

    [Fact]
    public async Task La_purga_conserva_lo_que_esta_dentro_de_la_ventana()
    {
        var reloj = new RelojFijo(Ancla);

        await RegistrarAsync(Turno(Alguien, Ancla.AddDays(-89), "al borde"));

        var borradas = await Purga(reloj, dias: 90).PurgarAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(0, borradas);
        Assert.Equal(1L, await EscalarAsync<long>(
            "SELECT count(*) FROM asistente.registro_operativo"));
    }

    [Fact]
    public async Task La_purga_es_idempotente()
    {
        var reloj = new RelojFijo(Ancla);
        var purga = Purga(reloj, dias: 90);
        var ct = TestContext.Current.CancellationToken;

        await RegistrarAsync(Turno(Alguien, Ancla.AddDays(-120), "vieja"));

        Assert.Equal(2, await purga.PurgarAsync(ct));
        Assert.Equal(0, await purga.PurgarAsync(ct));
        Assert.Equal(0, await purga.PurgarAsync(ct));
    }

    [Fact]
    public async Task La_ventana_es_configurable()
    {
        var reloj = new RelojFijo(Ancla);

        await RegistrarAsync(Turno(Alguien, Ancla.AddDays(-20), "de hace veinte días"));

        Assert.Equal(0, await Purga(reloj, dias: 90).PurgarAsync(
            TestContext.Current.CancellationToken));

        Assert.Equal(2, await Purga(reloj, dias: 7).PurgarAsync(
            TestContext.Current.CancellationToken));
    }

    // ------------------------------------------------------------------ apoyo

    private const string ContarDocentes =
        "SELECT count(*) AS cantidad FROM designaciones.designaciones";

    /// <summary>Un documento sembrado por el fixture sintético.</summary>
    private const string DocumentoSembrado = "28341567";

    /// <summary>La capa completa, escribiendo en los registros de verdad.</summary>
    private CapaConversacional Capa(params string[] guion)
    {
        var (basica, pii) = CadenasDeLectura();

        return BancoDelAsistente.Armar(
            basica,
            pii,
            ClasificadorDeSensibilidad(),
            new OpcionesAsistente { CupoDeLlamadasPorActor = 0 },
            reloj: new RelojFijo(Ancla),
            registro: new RegistroDelTurno(
                new CadenaDuena(Cadena), NullLogger<RegistroDelTurno>.Instance),
            guion: guion).Capa();
    }

    private async Task SembrarAsync()
    {
        var sql = await File.ReadAllTextAsync(
            Path.Combine(RaizRepositorio.Ruta(), "infra", "scripts", "seed-data", "sintetico.sql"),
            TestContext.Current.CancellationToken);

        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion) { CommandTimeout = 60 };
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static TurnoParaRegistrar Turno(
        Guid? actor = null, DateTimeOffset? cuando = null, string pregunta = "¿cuántos docentes hay?") =>
        new(actor ?? Alguien,
            cuando ?? Ancla,
            CarrilDelTurno.Sql,
            EstadoDelTurno.Respondida,
            LlamadasAlModelo: 2,
            TokensDeEntrada: 1200,
            TokensDeSalida: 80,
            TokensDeCache: 900,
            LatenciaMs: 1450,
            HuboReintento: false,
            Truncado: false,
            pregunta,
            "cruce_de_tablas",
            Proveedor: "anthropic/claude-sonnet-5");

    private async Task RegistrarAsync(TurnoParaRegistrar turno)
    {
        var registro = new RegistroDelTurno(
            new CadenaDuena(Cadena), NullLogger<RegistroDelTurno>.Instance);

        await registro.RegistrarAsync(turno, TestContext.Current.CancellationToken);
    }

    private PurgaDeRegistros Purga(TimeProvider reloj, int dias) =>
        new(new CadenaDuena(Cadena),
            Options.Create(new OpcionesAsistente { RetencionDeRegistrosDias = dias }),
            reloj,
            NullLogger<PurgaDeRegistros>.Instance);

    private async Task<List<string>> ColumnasConTipoAsync(string tabla) =>
        await LeerAsync<string>(
            $"""
            SELECT column_name || ':' || data_type FROM information_schema.columns
             WHERE table_schema = 'asistente' AND table_name = '{tabla}'
             ORDER BY column_name
            """);

    private async Task<List<string>> ColumnasDeAsync(string tabla) =>
        await LeerAsync<string>(
            $"""
            SELECT column_name FROM information_schema.columns
             WHERE table_schema = 'asistente' AND table_name = '{tabla}'
             ORDER BY column_name
            """);

    private async Task<List<string>> BuscarTextoAsync(string tabla, string aguja)
    {
        // Busca la aguja en TODAS las columnas de texto de la tabla, para que el
        // test no dependa de acertar en cuál se habría filtrado.
        var columnas = await LeerAsync<string>(
            $"""
            SELECT column_name FROM information_schema.columns
             WHERE table_schema = 'asistente' AND table_name = '{tabla}'
               AND data_type IN ('text', 'character varying')
            """);

        var encontradas = new List<string>();

        foreach (var columna in columnas)
        {
            var cuantas = await EscalarAsync<long>(
                $"SELECT count(*) FROM asistente.{tabla} WHERE {columna} LIKE '%{aguja}%'");

            if (cuantas > 0)
            {
                encontradas.Add(columna);
            }
        }

        return encontradas;
    }

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

    private async Task<T> EscalarAsync<T>(string sql)
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);

        return (T)(await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}
