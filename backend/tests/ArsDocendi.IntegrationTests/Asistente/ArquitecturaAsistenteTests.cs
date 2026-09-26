using System.Text.RegularExpressions;
using System.Xml.Linq;
using ArsDocendi.IntegrationTests.Infraestructura;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Guards de arquitectura propios del módulo del asistente.
/// </summary>
/// <remarks>
/// Los guards generales de <c>ArquitecturaIdentityTests</c> ya barren
/// <c>Modules.Asistente</c> junto con el resto. Acá van los que solo tienen sentido
/// para este módulo, porque es el único con una excepción declarada: consulta
/// schemas ajenos sin pasar por Contracts. Esa excepción es sostenible únicamente
/// si el resto de sus fronteras está verificado en vez de supuesto.
///
/// Cada guard viene en par: uno corre sobre el código real y el otro alimenta al
/// mismo detector con una violación sintética. Sin el segundo, un detector roto
/// —una regex que no matchea nada— pasaría en verde para siempre.
/// </remarks>
public sealed partial class ArquitecturaAsistenteTests
{
    /// <summary>
    /// Únicos archivos que pueden nombrar <c>CadenaDuena</c>, y por qué cada uno.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>El migrador: conceder privilegios exige ser dueño de la tabla.</item>
    /// <item>La composición: deriva de ella las dos cadenas de solo lectura.</item>
    /// <item>El escritor de los registros y su purga: los dos registros los escribe
    /// la aplicación, y los roles del asistente tienen su schema revocado entero
    /// (definición §3.4).</item>
    /// <item><c>RegistroDeRetroalimentacion.cs</c>: same reason as the registro
    /// writer above — the feedback table lives in the same wholesale-revoked
    /// `asistente` schema, so the two read-only roles could not write there even
    /// if this code tried to use them.</item>
    /// <item><c>RegistroDeHistorial.cs</c>: writes `hilo_historico`/
    /// `turno_historico`, both inside the same wholesale-revoked `asistente`
    /// schema.</item>
    /// <item><c>ConsultasDeHistorial.cs</c>: the READ side of own history
    /// (list/search/rename/delete/resume/re-execution's turn lookup). The two
    /// read-only roles have the whole `asistente` schema revoked, so reading it
    /// at all — not just writing it — needs the owner connection.</item>
    /// <item><c>ConsultasDeAuditoriaDeSoporte.cs</c>: the support-read path
    /// (list/read another actor's history) plus the append-only audit write,
    /// both against tables in the same wholesale-revoked schema.</item>
    /// </list>
    ///
    /// La lista es corta a propósito y crece solo con un motivo escrito. Lo que
    /// sigue afuera es lo que importa: <c>EjecutorDeConsulta</c>,
    /// <c>ProveedorDeEsquema</c>, <c>ConsultorDeAlcance</c> y todo lo que toque la
    /// consulta generada.
    /// </remarks>
    private static readonly string[] PuedenUsarLaCadenaDelDueno =
    [
        "MigradorAsistente.cs",
        "ModuleExtensions.cs",
        "RegistroDelTurno.cs",
        "PurgaDeRegistros.cs",
        "RegistroDeRetroalimentacion.cs",
        "RegistroDeHistorial.cs",
        "ConsultasDeHistorial.cs",
        "ConsultasDeAuditoriaDeSoporte.cs",
        // BarridoDeBorradosPendientes.cs: BarridoDePendientes, la parte
        // scoped y testeable de ese archivo, abre su propia conexión con la
        // cadena dueña para el DELETE físico del backstop de deshacer
        // (asistente-rediseno-v3, design.md D4) — misma tabla, mismo schema
        // revocado entero que ConsultasDeHistorial ya necesita leer y
        // escribir con esta conexión.
        "BarridoDeBorradosPendientes.cs",
        // CuotaPersistente.cs: same reason as ConsultasDeHistorial.cs — reads
        // presupuesto_rol/presupuesto_usuario/registro_operativo, all inside
        // the wholesale-revoked `asistente` schema, so even reading needs the
        // owner connection (asistente-administracion-de-uso).
        "CuotaPersistente.cs",
        // Same reason as CuotaPersistente.cs above — reads
        // tope_organizacional/consumo_organizacional_mensual/tabla_de_precios
        // and writes the accumulator, all inside the same revoked schema.
        "PresupuestoOrganizacionalPersistente.cs",
        // CandadoDelTurnoReal.cs takes CadenaDuena to hand it to
        // CandadoDelTurno, which needs the owner's credentials to open its
        // own dedicated (unpooled) connection for the advisory lock —
        // design.md D5 of asistente-administracion-de-uso.
        "CandadoDelTurnoReal.cs",
        // CandadoDelTurno.cs itself: same reason, it is the class that
        // actually opens the dedicated connection with CadenaDuena.
        "CandadoDelTurno.cs",
        // Reads/writes modo_mantenimiento, inside the wholesale-revoked
        // `asistente` schema — same reason as CuotaPersistente.cs above.
        "DisponibilidadDelModuloReal.cs",
        // Writes the append-only auditoria_administracion, same schema.
        "AuditoriaDeAdministracionReal.cs",
        // Aggregates registro_operativo/tabla_de_precios for the usage panel,
        // same wholesale-revoked schema (tarea 9.1-9.4).
        "ConsultasDeUso.cs",
        // Reads/writes presupuesto_rol/presupuesto_usuario/tope_organizacional,
        // same wholesale-revoked schema (tareas 9.5/9.6).
        "PresupuestosAdministrablesReal.cs",
    ];

    // ------------------------------------------------- la cadena del dueño no se filtra

    [Fact]
    public void Solo_el_migrador_y_la_composicion_usan_la_cadena_del_dueno()
    {
        var archivos = CodigoDelModulo();

        // Es el guard más importante del módulo. Todo el trabajo de privilegios por
        // columna se evapora si el motor de consulta recibe la conexión del dueño:
        // seguiría funcionando, leyendo de más y sin fallar.
        Assert.NotEmpty(archivos);
        var infracciones = Detectar(
            archivos.Where(a => !PuedenUsarLaCadenaDelDueno.Contains(Path.GetFileName(a.Ruta))),
            UsoDeCadenaDuena());

        Assert.True(infracciones.Count == 0,
            "Solo el migrador y la composición pueden nombrar CadenaDuena. Detectado en: "
            + string.Join(", ", infracciones));
    }

    [Fact]
    public void El_detector_reconoce_un_uso_de_la_cadena_del_dueno()
    {
        var sintetico = new Archivo(
            "Application/MotorDeConsulta.cs",
            "public sealed class MotorDeConsulta(CadenaDuena cadena) { }");

        var infracciones = Detectar([sintetico], UsoDeCadenaDuena());

        Assert.Single(infracciones);
    }

    // --------------------------------------------------------- el módulo no escribe

    [Fact]
    public void El_codigo_del_modulo_no_muta_datos_de_otro_schema()
    {
        var archivos = CodigoDelModulo();

        Assert.NotEmpty(archivos);
        var infracciones = DetectarMutacionAjena(archivos, MutacionEnCodigo());

        Assert.True(infracciones.Count == 0,
            "El asistente es de solo lectura sobre los datos del sistema. Lo único que "
            + "escribe es su propio schema `asistente`. Detectado en: "
            + string.Join(", ", infracciones));
    }

    [Fact]
    public void El_detector_reconoce_una_sentencia_de_mutacion_ajena_en_codigo()
    {
        Archivo[] sinteticos =
        [
            new("Application/Guardar.cs", "var sql = \"INSERT INTO designaciones.pedidos VALUES (1)\";"),
            new("Application/Borrar.cs", "var sql = \"DELETE FROM identity.personas\";"),
            new("Application/Vaciar.cs", "var sql = \"TRUNCATE designaciones.pedidos\";"),
            new("Application/VaciarTabla.cs", "var sql = \"TRUNCATE TABLE identity.personas\";"),
            new("Application/Persistir.cs", "await db.SaveChangesAsync(ct);"),
        ];

        var infracciones = DetectarMutacionAjena(sinteticos, MutacionEnCodigo());

        Assert.Equal(5, infracciones.Count);
    }

    [Fact]
    public void Escribir_el_schema_propio_del_asistente_no_es_una_infraccion()
    {
        // LA LÍNEA EXACTA DEL GUARD, y conviene que esté escrita como test y no como
        // comentario. Los dos registros del propio asistente son telemetría suya, no
        // datos del sistema: la definición pide explícitamente que los escriba la
        // conexión dueña (§3.4). Lo que el invariante prohíbe es tocar los datos de
        // los módulos, y eso sigue prohibido en la línea de abajo.
        Archivo[] sinteticos =
        [
            new("Infrastructure/Propio.cs",
                "var sql = \"INSERT INTO asistente.registro_operativo (actor_id) VALUES (@a)\";"),
            new("Infrastructure/Purga.cs",
                "var sql = \"DELETE FROM asistente.registro_analitico WHERE dia < @corte\";"),
        ];

        Assert.Empty(DetectarMutacionAjena(sinteticos, MutacionEnCodigo()));

        Assert.Single(DetectarMutacionAjena(
            [new("Infrastructure/Ajeno.cs",
                "var sql = \"INSERT INTO designaciones.pedidos (id) VALUES (@a)\";")],
            MutacionEnCodigo()));
    }

    [Fact]
    public void Una_lista_de_palabras_prohibidas_no_cuenta_como_mutacion()
    {
        // El validador de la SQL generada tiene que enumerar lo que rechaza. Una
        // enumeración de prohibiciones es lo contrario de una mutación, y el
        // detector no puede confundirlas: si lo hiciera, la salida sería quitar el
        // guard o quitar la enumeración, y las dos son peores.
        Archivo[] sinteticos =
        [
            new("Application/ValidadorFicticio.cs",
                """
                private static readonly HashSet<string> Prohibidas =
                    ["insert", "update", "delete", "truncate", "merge"];
                """),
        ];

        Assert.Empty(Detectar(sinteticos, MutacionEnCodigo()));
    }

    // ------------------------------------------------------------- el DDL no muta

    [Fact]
    public void El_DDL_del_asistente_no_toca_ningun_schema_ajeno()
    {
        var archivos = DdlDelAsistente();

        // El DDL del módulo concede lectura sobre los schemas de los otros y crea
        // el suyo propio. Lo que no puede hacer, en ninguna forma, es crear, alterar,
        // borrar o sembrar algo de identity, designaciones o audit: ahí su papel es
        // pedir permiso, no modificar.
        Assert.NotEmpty(archivos);
        var infracciones = DetectarMutacionAjena(archivos, MutacionEnSql());

        Assert.True(infracciones.Count == 0,
            "El DDL del asistente solo concede privilegios y crea su propio schema. "
            + "Detectado en: " + string.Join(", ", infracciones));
    }

    [Fact]
    public void El_DDL_del_asistente_no_borra_ni_reescribe_nada_de_lo_que_ya_existe()
    {
        // LO PROHIBIDO ES LO DESTRUCTIVO Y LO QUE DEPENDE DEL ORDEN: `DROP`,
        // `RENAME` y `ALTER COLUMN ... TYPE`. Este módulo no lleva historial de
        // migraciones, así que una sentencia que reescribe lo que otro archivo creó
        // deja el esquema dependiendo del orden de aplicación.
        //
        // LO PERMITIDO ES EXACTAMENTE UNO: `ALTER TABLE <tabla propia> ADD COLUMN
        // IF NOT EXISTS`. No es una excepción de conveniencia — es la única forma
        // que no tiene ninguno de los dos problemas: agrega lo que falta, no toca lo
        // que está, y aplicada dos veces da lo mismo que aplicada una. Sin ella el
        // `CREATE TABLE IF NOT EXISTS` es un no-op contra una base que ya tiene la
        // tabla, la columna nueva nunca aparece y el INSERT que la nombra falla en
        // cada turno. Eso pasó de verdad, y en la columna sin `COMMENT` pasó en
        // silencio.
        //
        // La guarda `IF NOT EXISTS` es parte de lo permitido y no un detalle de
        // estilo: sin ella la segunda corrida aborta, que es justo lo que
        // `IMigradorModulo` prohíbe.
        //
        // La única excepción son los reemplazos de CHECK ratificados por nombre
        // (ver ReemplazosDeCheckRatificados).
        var archivos = DdlDelAsistente();

        Assert.NotEmpty(archivos);
        Assert.Empty(Detectar(archivos.Select(SinReemplazosDeCheckRatificados), DestruccionEnSql()));
    }

    [Fact]
    public void Solo_el_reemplazo_de_un_CHECK_ratificado_escapa_a_la_prohibicion_de_DROP()
    {
        const string Reemplazo =
            """
            ALTER TABLE asistente.retroalimentacion_turno
                DROP CONSTRAINT IF EXISTS retroalimentacion_turno_razon_valida;
            ALTER TABLE asistente.retroalimentacion_turno
                ADD CONSTRAINT retroalimentacion_turno_razon_valida
                CHECK (razon IS NULL OR razon IN ('a', 'b'));
            """;
        Archivo[] sinteticos =
        [
            new("017_check_no_ratificado.sql",
                "ALTER TABLE asistente.registro_operativo DROP CONSTRAINT IF EXISTS otro_check;"),
            new("018_ratificado_pero_otra_accion.sql",
                """
                ALTER TABLE asistente.retroalimentacion_turno
                    DROP CONSTRAINT retroalimentacion_turno_razon_valida;
                """),
            new("019_ratificado_y_algo_mas.sql",
                Reemplazo + "\nALTER TABLE asistente.retroalimentacion_turno DROP COLUMN razon;"),
        ];

        Assert.Equal(3, Detectar(sinteticos.Select(SinReemplazosDeCheckRatificados), DestruccionEnSql()).Count);
        Assert.Empty(Detectar(
            [SinReemplazosDeCheckRatificados(new("020_ratificado.sql", Reemplazo))],
            DestruccionEnSql()));
    }

    [Fact]
    public void El_detector_reconoce_un_DDL_que_toca_lo_ajeno()
    {
        Archivo[] sinteticos =
        [
            new("003_tabla_ajena.sql", "CREATE TABLE designaciones.consultas (id uuid);"),
            new("004_semilla.sql", "INSERT INTO identity.roles (code) VALUES ('x');"),
            new("005_indice.sql", "CREATE INDEX ix_x ON identity.personas (legajo);"),
        ];

        Assert.Equal(3, DetectarMutacionAjena(sinteticos, MutacionEnSql()).Count);

        // Y el propio, no.
        Assert.Empty(DetectarMutacionAjena(
            [new("006_propio.sql", "CREATE TABLE asistente.consultas (id uuid);")],
            MutacionEnSql()));
    }

    [Fact]
    public void El_detector_reconoce_un_DDL_destructivo()
    {
        Archivo[] sinteticos =
        [
            new("007_baja.sql", "DROP TABLE designaciones.pedidos;"),
            new("008_baja_propia.sql", "DROP TABLE asistente.registro_operativo;"),
            new("009_columna_sin_guarda.sql",
                "ALTER TABLE asistente.registro_analitico ADD COLUMN x text;"),
            new("010_baja_de_columna.sql",
                "ALTER TABLE asistente.registro_operativo DROP COLUMN proveedor;"),
            new("011_cambio_de_tipo.sql",
                "ALTER TABLE asistente.registro_operativo ALTER COLUMN latencia_ms TYPE bigint;"),
            new("012_renombre.sql",
                "ALTER TABLE asistente.registro_operativo RENAME COLUMN carril TO ruta;"),
            new("013_columna_ajena.sql",
                "ALTER TABLE designaciones.pedidos ADD COLUMN IF NOT EXISTS x text;"),
            // Con coma, la sentencia lleva más de una acción y la segunda queda
            // fuera de lo que el patrón puede leer. Se rechaza entera: una columna
            // por sentencia es lo que hace verificable la forma permitida.
            new("014_dos_acciones.sql",
                """
                ALTER TABLE asistente.registro_operativo
                    ADD COLUMN IF NOT EXISTS a text, ADD COLUMN b text;
                """),
        ];

        Assert.Equal(8, Detectar(sinteticos, DestruccionEnSql()).Count);
    }

    [Fact]
    public void Agregar_una_columna_con_guarda_al_schema_propio_no_es_una_infraccion()
    {
        // LA LÍNEA EXACTA DEL GUARD NUEVO. Es la única sentencia que repara una base
        // que ya tenía la tabla, y es idempotente y sin orden: agrega lo que falta y
        // no toca lo que está.
        Archivo[] sinteticos =
        [
            new("015_columna_propia.sql",
                "ALTER TABLE asistente.registro_operativo ADD COLUMN IF NOT EXISTS x text;"),
            new("016_columna_propia_en_dos_lineas.sql",
                """
                ALTER TABLE asistente.registro_operativo
                    ADD COLUMN IF NOT EXISTS tokens_de_cache integer;
                """),
        ];

        Assert.Empty(Detectar(sinteticos, DestruccionEnSql()));
    }

    // ------------------------------------------- el actor se fija en un solo lugar

    /// <summary>El único archivo que puede armar el preámbulo de lectura.</summary>
    private const string ArchivoDelPreambulo = "PreambuloDelActor.cs";

    /// <summary>
    /// Archivos que abren transacción <b>sin</b> actor, y por qué.
    /// </summary>
    /// <remarks>
    /// Los dos corren DDL de migración con la conexión del dueño que les pasa el
    /// migrador: no hay actor que fijar, y RLS no aplica al dueño de la tabla.
    /// La lista crece solo con un motivo escrito.
    /// </remarks>
    private static readonly string[] TransaccionesSinActor =
    [
        "PrivilegiosAsistente.cs",
        "RegistrosAsistente.cs",
        // Same reason as RegistrosAsistente.cs: migration DDL run with the owner
        // connection, no actor to fix and RLS does not apply to the owner.
        "RetroalimentacionAsistente.cs",
        // Same reason: DDL runners for the two new tables, no actor involved.
        "HistorialAsistente.cs",
        "AuditoriaDeSoporteAsistente.cs",
        // Same reason: DDL runner for the seven administration-of-use tables
        // (asistente-administracion-de-uso), no actor involved.
        "AdministracionAsistente.cs",
        // Writes presupuesto_usuario in its own short transaction (close the
        // previous version, open the new one) — no RLS-scoped read of
        // anything keyed to identity.asistente_actor(), so no actor to fix.
        "PresupuestosAdministrablesReal.cs",
    ];

    [Fact]
    public void El_preambulo_de_lectura_se_arma_en_un_solo_archivo()
    {
        var archivos = CodigoDelModulo();

        // El ajuste del actor y la declaración de solo lectura viven juntos en un
        // solo archivo porque juntos son la afirmación que sostiene el invariante
        // #14: toda transacción que fija el actor es de solo lectura. Escritos
        // sueltos, esa frase es una intención; escritos acá, no queda forma de
        // fijar el actor sin declarar la transacción.
        Assert.NotEmpty(archivos);
        var culpables = Detectar(archivos, PreambuloDeLectura())
            .Where(ruta => !Path.GetFileName(ruta).Equals(ArchivoDelPreambulo, StringComparison.Ordinal))
            .ToList();

        Assert.True(culpables.Count == 0,
            "El ajuste del actor y el SET TRANSACTION READ ONLY solo pueden estar en "
            + $"{ArchivoDelPreambulo}. Detectado en: " + string.Join(", ", culpables));
    }

    [Fact]
    public void El_detector_reconoce_un_preambulo_suelto()
    {
        Archivo[] sinteticos =
        [
            new("Infrastructure/ConsultorNuevo.cs",
                "var c = new NpgsqlCommand(\"SELECT set_config('app.asistente_user_id', @a, true)\");"),
            new("Infrastructure/OtroConsultor.cs",
                "var c = new NpgsqlCommand(\"SET TRANSACTION READ ONLY\", conexion, transaccion);"),
        ];

        Assert.Equal(2, Detectar(sinteticos, PreambuloDeLectura()).Count);
    }

    [Fact]
    public void Toda_transaccion_de_lectura_pasa_por_el_preambulo()
    {
        var archivos = CodigoDelModulo();

        // ESTE ES EL GUARD QUE ATRAPA AL QUINTO CONSULTOR. El de arriba impide
        // escribir el ajuste en otro lado; este impide OLVIDARLO, que es el modo de
        // falla que importa y el único que es silencioso: sin el ajuste,
        // identity.asistente_actor() devuelve NULL, la policy da falso, y el
        // asistente contesta «no hay datos» en vez de «no podés verlos». Eso no
        // tira error: produce una respuesta falsa.
        Assert.NotEmpty(archivos);
        var culpables = archivos
            .Where(a => !TransaccionesSinActor.Contains(Path.GetFileName(a.Ruta)))
            .Where(a => !Path.GetFileName(a.Ruta).Equals(ArchivoDelPreambulo, StringComparison.Ordinal))
            .Where(SinPreambulo)
            .Select(a => a.Ruta)
            .ToList();

        Assert.True(culpables.Count == 0,
            "Hay una transacción de lectura que no aplica el preámbulo del actor. "
            + "Detectado en: " + string.Join(", ", culpables));
    }

    [Fact]
    public void El_detector_reconoce_una_transaccion_sin_preambulo()
    {
        var olvidadizo = new Archivo(
            "Infrastructure/ConsultorNuevo.cs",
            """
            await using var transaccion = await conexion.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, ct);
            await using var leer = new NpgsqlCommand("SELECT 1", conexion, transaccion);
            """);

        Assert.True(SinPreambulo(olvidadizo));

        // Y el que sí lo aplica, no.
        var correcto = olvidadizo with
        {
            Contenido = olvidadizo.Contenido
                + "\nawait PreambuloDelActor.AplicarAsync(conexion, transaccion, actor, ct);",
        };

        Assert.False(SinPreambulo(correcto));
    }

    // --------------------------------------------- el ping no arrastra dependencias

    [Fact]
    public void El_controller_del_ping_no_tiene_ninguna_dependencia()
    {
        // ESTO YA SE ROMPIÓ UNA VEZ. El ping vivía junto al endpoint del turno; el
        // día que ese controller ganó dependencias, construirlo pasó a exigir las
        // cadenas de solo lectura —cuya fábrica falla si el ambiente no las
        // configuró— y el ping devolvió 500 sin base.
        //
        // Un ping que necesita configuración de base deja de poder distinguir «el
        // módulo está cargado» de «la base responde», que es lo único que el
        // invariante #3 le pide. La separación tiene que ser estructural: sin
        // constructor con parámetros, no hay nada que resolver.
        var tipo = typeof(Modules.Asistente.Api.PingAsistenteController);

        Assert.All(
            tipo.GetConstructors(),
            constructor => Assert.Empty(constructor.GetParameters()));
    }

    [Fact]
    public void El_ping_no_esta_en_el_controller_del_turno()
    {
        var tipo = typeof(Modules.Asistente.Api.AsistenteController);

        Assert.DoesNotContain(
            tipo.GetMethods().Select(m => m.Name),
            nombre => nombre.Equals("Ping", StringComparison.Ordinal));
    }

    // ------------------------------------------- una sola puerta a las bases

    /// <summary>
    /// Los únicos archivos que pueden construir una conexión a mano, y por qué.
    /// </summary>
    /// <remarks>
    /// Los tres escriben el schema PROPIO del asistente con la conexión del
    /// dueño: no son lectura y no pasan por <c>AperturaDeLectura</c>. La lista es
    /// cerrada y encoge, no crece — cada entrada nueva necesita un motivo escrito
    /// acá adentro.
    /// </remarks>
    private static readonly string[] PuedenConstruirConexion =
    [
        "AperturaDeLectura.cs",
        "MigradorAsistente.cs",
        "RegistroDelTurno.cs",
        "PurgaDeRegistros.cs",
        // Same reason as RegistroDelTurno.cs above: writes the feedback table
        // with the owner connection, since both read-only roles have the whole
        // `asistente` schema revoked.
        "RegistroDeRetroalimentacion.cs",
        // Same reason: writes hilo_historico/turno_historico with the owner
        // connection, same wholesale-revoked schema.
        "RegistroDeHistorial.cs",
        // Reads (not writes) hilo_historico/turno_historico — the read-only
        // roles cannot reach the schema at all, so even reading it needs the
        // owner connection.
        "ConsultasDeHistorial.cs",
        // Reads another actor's history and writes the append-only audit row,
        // both against the same wholesale-revoked schema.
        "ConsultasDeAuditoriaDeSoporte.cs",
        // BarridoDeBorradosPendientes.cs: BarridoDePendientes physically
        // deletes rows of the same wholesale-revoked schema (the deferred
        // delete's backstop, design.md D4) — same reason as PurgaDeRegistros.cs.
        "BarridoDeBorradosPendientes.cs",
        // Reads presupuesto_rol/presupuesto_usuario and counts
        // registro_operativo (asistente-administracion-de-uso) — all three
        // inside the same wholesale-revoked `asistente` schema, so even
        // reading needs the owner connection, same reason as
        // ConsultasDeHistorial.cs above.
        "CuotaPersistente.cs",
        // Same reason as CuotaPersistente.cs above.
        "PresupuestoOrganizacionalPersistente.cs",
        // CandadoDelTurno.cs needs its OWN dedicated, unpooled connection for
        // the session-level advisory lock (design.md D5): AperturaDeLectura's
        // pooled connections cannot own a session-scoped lock, since the
        // pool can hand that same physical connection to someone else the
        // moment it is returned.
        "CandadoDelTurno.cs",
        // Same reason as CuotaPersistente.cs above.
        "DisponibilidadDelModuloReal.cs",
        "AuditoriaDeAdministracionReal.cs",
        "ConsultasDeUso.cs",
        "PresupuestosAdministrablesReal.cs",
    ];

    [Fact]
    public void Toda_lectura_del_sistema_pasa_por_la_apertura()
    {
        var archivos = CodigoDelModulo();

        // Con doce constructores de conexión repartidos, «con qué techo de comando
        // lee el asistente» no tenía respuesta: dependía de cuál corriera. Sólo el
        // ejecutor ponía el suyo; los demás heredaban el default de Npgsql, 30 s —
        // el doble del que el módulo eligió. Que haya UNA puerta es lo que hace que
        // esa pregunta tenga una respuesta y no cuatro.
        Assert.NotEmpty(archivos);
        var culpables = Detectar(archivos, ConexionConstruidaAMano())
            .Where(ruta => !PuedenConstruirConexion.Contains(Path.GetFileName(ruta)))
            .ToList();

        Assert.True(culpables.Count == 0,
            "Hay código del módulo que construye su propia conexión. Para leer del sistema "
            + "se usa `AperturaDeLectura`, que decide el rol y el techo de comando en un solo "
            + "lugar. Detectado en: " + string.Join(", ", culpables));
    }

    [Fact]
    public void El_detector_reconoce_una_conexion_construida_a_mano()
    {
        var sintetico = new Archivo(
            "Infrastructure/LectorNuevo.cs",
            "await using var conexion = new NpgsqlConnection(cadena.Valor);");

        Assert.Single(Detectar([sintetico], ConexionConstruidaAMano()));
    }

    // ------------------------------------------------ la superficie pública

    /// <summary>
    /// Cuántos tipos de <c>Application</c> pueden ser públicos hoy.
    /// </summary>
    /// <remarks>
    /// El número no es una meta: es el estado que el compilador impone. Casi toda
    /// la superficie la arrastra el constructor de <c>CapaConversacional</c>, que
    /// se registra con <c>AddScoped&lt;T&gt;()</c> y por eso necesita constructor
    /// público, y el controller que la recibe. Bajarlo de acá exige rediseñar esa
    /// activación, y eso es otro renglón.
    ///
    /// Lo que este guard impide es lo otro: que vuelva a subir por costumbre.
    /// Estaba en 93 sin que nadie lo hubiera decidido.
    /// </remarks>
    /// <remarks>
    /// Raised from 61 to 66 for asistente-feedback-export-seguimiento: five new
    /// declarations (<c>IValidezDeRetroalimentacion</c>,
    /// <c>ISugerenciasDeSeguimiento</c>, <c>IRegistroDeRetroalimentacion</c>,
    /// <c>ServicioDeRetroalimentacion</c>, <c>ResultadoDeRetroalimentacion</c>),
    /// none of them optional: each is a constructor parameter type or a public
    /// method's return type on an already-public class
    /// (<c>CapaConversacional</c>, <c>CarrilSql</c>, or <c>AsistenteController</c>
    /// by way of <c>ServicioDeRetroalimentacion</c>), and the compiler rejects a
    /// less-accessible type there. <c>RazonesDeRetroalimentacion</c> stayed
    /// <c>internal</c> for the same reason the others could not: nothing public
    /// references it in a signature.
    /// </remarks>
    /// <remarks>
    /// Raised from 66 to 68, then to 73, then to 74, for
    /// asistente-historial-conversaciones. First two (<c>IRegistroDeHistorial</c>,
    /// <c>TurnoParaHistorial</c>): <c>CapaConversacional</c>'s constructor now
    /// takes an <c>IRegistroDeHistorial</c>, and that interface's own method
    /// carries <c>TurnoParaHistorial</c> as a parameter. Then five more
    /// (<c>IConsultasDeHistorial</c>, <c>ConversacionResumen</c>,
    /// <c>TurnoDeHistorial</c>, <c>ConversacionDetalle</c>,
    /// <c>TurnoParaReejecutar</c>): <c>HistorialController</c>'s constructor
    /// takes an <c>IConsultasDeHistorial</c>, and that interface's own methods
    /// carry the other four as parameters or return types. Then one more
    /// (<c>IConsultasDeAuditoriaDeSoporte</c>): <c>SoporteHistorialController</c>'s
    /// constructor takes it — it reuses <c>ConversacionResumen</c>/
    /// <c>ConversacionDetalle</c> rather than adding new record types. None
    /// optional — in every case the compiler rejects a less-accessible type on
    /// an already-public constructor or interface member. <c>TituloDeConversacion</c>
    /// stayed <c>internal</c>: nothing public references it in a signature.
    ///
    /// Raised once more to 75 for asistente-administracion-de-uso:
    /// <c>IPresupuestoOrganizacional</c>, matching the same public visibility
    /// as its sibling ports <c>ICuotaDelActor</c>/<c>IDisponibilidadDelModelo</c>
    /// (also injected into <c>CapaConversacional</c>'s already-public
    /// constructor). <c>CalculadoraDeCosto</c> and its three record types
    /// (<c>FilaDeConsumo</c>, <c>PrecioVigente</c>, <c>ResultadoDeCosteo</c>)
    /// stayed <c>internal</c>: nothing public references them, since
    /// <c>PresupuestoOrganizacionalPersistente</c> (Infrastructure, same
    /// assembly) is their only caller.
    ///
    /// Raised once more to 76, same change: <c>ICandadoDelTurno</c>, same
    /// reason (also a constructor parameter of <c>CapaConversacional</c>).
    ///
    /// Raised once more to 80, same change (groups 6/7/8): <c>EstadoDeMantenimiento</c>
    /// and <c>IDisponibilidadDelModulo</c> (kill switch, constructor parameters
    /// of <c>CapaConversacional</c>/<c>CatalogoDeCapacidades</c>);
    /// <c>IAuditoriaDeAdministracion</c> (constructor parameter of the new
    /// admin controller); <c>EstadoDelCupoDelActor</c> (a field of the
    /// already-public <c>CapacidadesDelActor</c>, tarea 7.1).
    ///
    /// Raised once more to 85, same change (group 9): <c>IConsultasDeUso</c>
    /// and <c>IPresupuestosAdministrables</c> (constructor parameters of the
    /// admin controller); <c>RangoDePeriodo</c> (a parameter of
    /// <c>IConsultasDeUso.ObtenerAsync</c>) and <c>PanelDeUso</c>/<c>UsoAgregado</c>
    /// (its return type and one of that type's own fields) — all three forced
    /// public by the same "no less accessible than the public interface"
    /// rule the compiler already enforces for the others in this list.
    /// </remarks>
    private const int SuperficiePublicaDeApplication = 85;

    [Fact]
    public void La_superficie_publica_de_Application_no_crece_sin_que_nadie_lo_note()
    {
        var publicos = TiposDeclarados(EsPublico());

        // `public` es una promesa de estabilidad: dice «podés depender de mí, no te
        // voy a romper». El módulo no le prometió nada a nadie —lo que ofrece a los
        // otros módulos va en `Modules.Asistente.Contracts`, hoy vacío— así que cada
        // tipo público de acá es una promesa que alguien tiene que poder justificar.
        Assert.True(
            publicos.Count <= SuperficiePublicaDeApplication,
            $"La superficie pública de Application subió a {publicos.Count} (el tope es "
            + $"{SuperficiePublicaDeApplication}). Un tipo nuevo va `internal` salvo que la "
            + "superficie HTTP del módulo lo exija, y en ese caso se sube el tope con el "
            + "motivo escrito. Públicos hoy: " + string.Join(", ", publicos));
    }

    [Fact]
    public void El_detector_de_superficie_publica_reconoce_las_dos_formas()
    {
        // EL PAR SINTÉTICO. Una expresión regular que dejara de matchear reportaría
        // cero públicos y este guard pasaría en verde para siempre.
        Archivo[] sinteticos =
        [
            new("Application/Turno/Uno.cs", "public sealed class Uno { }"),
            new("Application/Modelo/Dos.cs", "public interface Dos { }"),
            new("Application/Lexico/Tres.cs", "public readonly record struct Tres(int A);"),
            new("Application/Turno/Cuatro.cs", "internal sealed class Cuatro { }"),
        ];

        var publicos = sinteticos
            .SelectMany(a => EsPublico().Matches(a.Contenido).Select(m => m.Groups["nombre"].Value))
            .ToList();

        Assert.Equal(["Uno", "Dos", "Tres"], publicos);
    }

    // ------------------------------------------------------------- las referencias

    [Fact]
    public void El_modulo_solo_referencia_ArsDocendi_Shared()
    {
        var proyecto = Path.Combine(
            RaizRepositorio.BackendSrc(), "Modules.Asistente", "Modules.Asistente.csproj");
        Assert.True(File.Exists(proyecto), $"No se encontró {proyecto}.");

        // Los .csproj escriben las rutas con separador de Windows. Path.* no lo
        // parte en Linux, así que hay que normalizar antes de quedarse con el nombre.
        var referencias = XDocument.Load(proyecto)
            .Descendants("ProjectReference")
            .Select(nodo => (nodo.Attribute("Include")?.Value ?? string.Empty).Replace('\\', '/'))
            .Select(ruta => Path.GetFileNameWithoutExtension(ruta))
            .ToArray();

        // Los edges hacia Contracts ajenos llegan con el carril determinista de API.
        // Hasta entonces, cualquier referencia nueva es un error, no una decisión.
        Assert.Equal(["ArsDocendi.Shared"], referencias);
    }

    [Fact]
    public void El_SDK_del_proveedor_se_nombra_en_un_solo_archivo()
    {
        var culpables = Detectar(CodigoDelModulo(), SdkDelProveedor())
            .Where(ruta => !ruta.EndsWith("ProveedorAnthropic.cs", StringComparison.Ordinal))
            .ToList();

        // Es lo que hace cierta la promesa de que el puerto es agnóstico. Mientras
        // el SDK viva en un archivo, cambiar de proveedor —o sumar un segundo, o
        // pasarse a un modelo propio— es escribir otra clase al lado y otro brazo
        // del switch. En cuanto sus tipos se filtran a la composición o al
        // pipeline, esa promesa deja de ser verificable y pasa a ser una intención.
        Assert.Empty(culpables);
    }

    // -------------------------------------------------- higiene de la credencial

    [Fact]
    public void Ningun_archivo_de_configuracion_versionado_trae_la_clave_del_proveedor()
    {
        var archivos = ConfiguracionVersionada();

        // El default de `ClaveDelProveedor` es la cadena vacía y la clave real entra
        // por ambiente. Lo que este test cuida es que nadie la «deje puesta un rato»
        // en un appsettings para probar: una credencial commiteada no se arregla
        // borrándola después, porque queda en el historial para siempre.
        Assert.NotEmpty(archivos);
        var culpables = Detectar(archivos, ClaveConValor());

        Assert.True(culpables.Count == 0,
            "Hay una clave del proveedor escrita en configuración versionada. "
            + "Va por variable de ambiente. Detectado en: " + string.Join(", ", culpables));
    }

    [Fact]
    public void El_detector_reconoce_una_clave_escrita_en_configuracion()
    {
        Archivo[] sinteticos =
        [
            new("a/appsettings.json", "{\"Asistente\":{\"ClaveDelProveedor\":\"sk-ant-api03-x\"}}"),
            new("b/appsettings.json", "{\"Asistente\":{\"ClaveDelProveedor\": \"a-mano\"}}"),
        ];

        Assert.Equal(2, Detectar(sinteticos, ClaveConValor()).Count);

        // Declararla vacía es exactamente lo que el repositorio SÍ puede hacer:
        // documenta que el valor existe sin traer ningún secreto.
        Assert.Empty(Detectar(
            [new("c/appsettings.json", "{\"Asistente\":{\"ClaveDelProveedor\": \"\"}}")],
            ClaveConValor()));
    }

    // ------------------------------------------------------------------------ apoyo

    private sealed record Archivo(string Ruta, string Contenido);

    private static IReadOnlyList<string> Detectar(IEnumerable<Archivo> archivos, Regex patron) =>
        archivos.Where(a => patron.IsMatch(a.Contenido)).Select(a => a.Ruta).ToList();

    /// <summary>
    /// Si el archivo abre una transacción y no nombra el preámbulo del actor.
    /// </summary>
    private static bool SinPreambulo(Archivo archivo) =>
        AperturaDeTransaccion().IsMatch(archivo.Contenido)
        && !UsoDelPreambulo().IsMatch(archivo.Contenido);

    /// <summary>
    /// Igual que <see cref="Detectar"/>, pero perdona lo que apunta al schema propio.
    /// </summary>
    /// <remarks>
    /// La excepción es angosta y explícita: solo <c>asistente</c> y solo cuando la
    /// sentencia nombra su objetivo. Una mutación sin objetivo reconocible
    /// —<c>SaveChangesAsync</c>, un <c>DROP</c>— no puede acogerse a ella, porque el
    /// detector no tiene con qué comprobar a quién le pega.
    /// </remarks>
    private static IReadOnlyList<string> DetectarMutacionAjena(
        IEnumerable<Archivo> archivos, Regex patron) =>
        archivos
            .Where(a => patron.Matches(a.Contenido).Any(m => !EsDelSchemaPropio(m)))
            .Select(a => a.Ruta)
            .ToList();

    private static bool EsDelSchemaPropio(Match coincidencia)
    {
        var objetivo = coincidencia.Groups["objetivo"];

        if (!objetivo.Success)
        {
            return false;
        }

        var nombre = objetivo.Value.Replace("\"", string.Empty, StringComparison.Ordinal);

        return nombre.Equals("asistente", StringComparison.OrdinalIgnoreCase)
            || nombre.StartsWith("asistente.", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Los tipos de <c>Application</c> que declara el patrón, ordenados.</summary>
    private static IReadOnlyList<string> TiposDeclarados(Regex patron) =>
        [.. CodigoDelModulo()
            .Where(a => a.Ruta.Contains($"Application{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(a => patron.Matches(a.Contenido).Select(m => m.Groups["nombre"].Value))
            .Order(StringComparer.Ordinal)];

    private static Archivo[] CodigoDelModulo()
    {
        var raiz = RaizRepositorio.Ruta();
        var modulo = Path.Combine(RaizRepositorio.BackendSrc(), "Modules.Asistente");

        return [.. Directory.EnumerateFiles(modulo, "*.cs", SearchOption.AllDirectories)
            .Where(ruta => !ruta.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(ruta => !ruta.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(ruta => new Archivo(
                Path.GetRelativePath(raiz, ruta),
                SinComentariosDeCodigo(File.ReadAllText(ruta))))];
    }

    /// <summary>
    /// Los archivos de configuración que el repositorio versiona.
    /// </summary>
    /// <remarks>
    /// Barre el repo entero y no solo el módulo: la clave se puede filtrar tanto en
    /// el appsettings del Host como en el de los tests, y el daño es el mismo.
    ///
    /// Los archivos por ambiente y los locales quedan afuera porque no se versionan:
    /// un appsettings.Development.json con la clave puesta en la máquina de alguien
    /// es correcto, y es justamente el lugar donde el README dice que vaya.
    /// </remarks>
    private static Archivo[] ConfiguracionVersionada()
    {
        var raiz = RaizRepositorio.Ruta();

        return [.. Directory
            .EnumerateFiles(raiz, "appsettings*.json", SearchOption.AllDirectories)
            .Where(ruta => !EnCarpeta(ruta, "obj") && !EnCarpeta(ruta, "bin"))
            .Where(ruta => !EnCarpeta(ruta, "node_modules"))
            .Where(EstaVersionado)
            .Select(ruta => new Archivo(Path.GetRelativePath(raiz, ruta), File.ReadAllText(ruta)))];
    }

    private static bool EnCarpeta(string ruta, string carpeta) =>
        ruta.Contains(
            $"{Path.DirectorySeparatorChar}{carpeta}{Path.DirectorySeparatorChar}",
            StringComparison.Ordinal);

    private static bool EstaVersionado(string ruta)
    {
        var nombre = Path.GetFileName(ruta);

        return !nombre.Contains(".Development.", StringComparison.OrdinalIgnoreCase)
            && !nombre.Contains(".Local.", StringComparison.OrdinalIgnoreCase);
    }

    private static Archivo[] DdlDelAsistente()
    {
        var raiz = RaizRepositorio.Ruta();
        var directorio = Path.Combine(raiz, "database", "asistente");

        return [.. Directory.EnumerateFiles(directorio, "*.sql", SearchOption.AllDirectories)
            .Select(ruta => new Archivo(
                Path.GetRelativePath(raiz, ruta),
                SinComentariosDeSql(File.ReadAllText(ruta))))];
    }

    private static string SinComentariosDeCodigo(string codigo) =>
        ComentariosDeCodigo().Replace(codigo, string.Empty);

    private static string SinComentariosDeSql(string sql) =>
        ComentariosDeSql().Replace(sql, string.Empty);

    [GeneratedRegex(@"//.*?$|/\*.*?\*/", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex ComentariosDeCodigo();

    [GeneratedRegex(@"--.*?$", RegexOptions.Multiline)]
    private static partial Regex ComentariosDeSql();

    [GeneratedRegex(@"\bCadenaDuena\b")]
    private static partial Regex UsoDeCadenaDuena();

    // Declaraciones públicas de nivel superior. El ancla de línea es lo que las
    // distingue de los MIEMBROS públicos de un tipo, que van indentados y no son
    // superficie del módulo sino de su tipo.
    [GeneratedRegex(
        @"^public (?:sealed |abstract |static |readonly |partial )*"
        + @"(?:class|record struct|record|interface|enum|struct) (?<nombre>\w+)",
        RegexOptions.Multiline)]
    private static partial Regex EsPublico();

    // Las dos mitades del preámbulo, en un solo patrón: el nombre del ajuste del
    // actor y la declaración de solo lectura. Que estén juntas es lo que hace
    // verificable la frase que sostiene el invariante #14.
    [GeneratedRegex(@"app\.asistente_user_id|SET\s+TRANSACTION\s+READ\s+ONLY", RegexOptions.IgnoreCase)]
    private static partial Regex PreambuloDeLectura();

    [GeneratedRegex(@"\bBeginTransactionAsync\s*\(")]
    private static partial Regex AperturaDeTransaccion();

    [GeneratedRegex(@"\bnew\s+NpgsqlConnection\s*\(")]
    private static partial Regex ConexionConstruidaAMano();

    [GeneratedRegex(@"\bPreambuloDelActor\b")]
    private static partial Regex UsoDelPreambulo();

    // TRUNCATE exige un objetivo —igual que las otras tres formas de este patrón—
    // y no aparece suelto. El validador de la SQL generada tiene que ENUMERAR las
    // palabras prohibidas para poder rechazarlas, y una lista de prohibiciones es
    // lo contrario de una mutación. Exigir el objetivo conserva todos los
    // verdaderos positivos: una sentencia real siempre nombra qué trunca.
    [GeneratedRegex(
        @"\bINSERT\s+INTO\s+(?<objetivo>[\w"".]+)|\bUPDATE\s+(?<objetivo>[\w"".]+)\s+SET\b|" +
        @"\bDELETE\s+FROM\s+(?<objetivo>[\w"".]+)|\bTRUNCATE\s+(?:TABLE\s+)?(?<objetivo>[\w"".]+)|" +
        @"\bSaveChanges(?:Async)?\s*\(|\bExecuteUpdate\w*\s*\(|\bExecuteDelete\w*\s*\(",
        RegexOptions.IgnoreCase)]
    private static partial Regex MutacionEnCodigo();

    [GeneratedRegex(
        @"\bINSERT\s+INTO\s+(?<objetivo>[\w"".]+)|\bUPDATE\s+(?<objetivo>[\w"".]+)\s+SET\b|" +
        @"\bDELETE\s+FROM\s+(?<objetivo>[\w"".]+)|\bTRUNCATE\s+(?:TABLE\s+)?(?<objetivo>[\w"".]+)|" +
        @"\bCREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?(?<objetivo>[\w"".]+)|" +
        @"\bCREATE\s+SCHEMA\s+(?:IF\s+NOT\s+EXISTS\s+)?(?<objetivo>[\w""]+)|" +
        @"\bCREATE\s+(?:UNIQUE\s+)?INDEX\s+(?:CONCURRENTLY\s+)?(?:IF\s+NOT\s+EXISTS\s+)?" +
        @"[\w"".]+\s+ON\s+(?<objetivo>[\w"".]+)",
        RegexOptions.IgnoreCase)]
    private static partial Regex MutacionEnSql();

    // Lo destructivo y lo que depende del orden, con UNA forma permitida.
    //
    // `DROP` y `RENAME` no tienen excepción de schema: no hay caso en que este
    // módulo tenga que borrar o renombrar algo, ni siquiera lo propio.
    //
    // De `ALTER TABLE` se permite exactamente una forma, y la lista de lo que la
    // sentencia tiene que decir es la lista de sus propiedades:
    //   · `asistente.` — sobre una tabla del schema propio, no de otro módulo;
    //   · `ADD COLUMN` — solo agrega, así que no reescribe lo que otro archivo creó
    //     y no depende del orden de aplicación (un `ALTER COLUMN ... TYPE` sí);
    //   · `IF NOT EXISTS` — la segunda corrida es un no-op en vez de un aborto;
    //   · `[^;,]+;` — una acción por sentencia. Con coma la sentencia lleva más de
    //     una y el patrón solo puede leer la primera, así que se rechaza entera:
    //     una forma permitida que no se puede verificar no es una forma permitida.
    [GeneratedRegex(
        @"\bDROP\s+\w+\b|\bRENAME\b|" +
        @"\bALTER\s+TABLE\s+(?!""?asistente""?\.""?\w+""?\s+ADD\s+COLUMN\s+IF\s+NOT\s+EXISTS\s+[^;,]+;)",
        RegexOptions.IgnoreCase)]
    private static partial Regex DestruccionEnSql();

    /// <summary>
    /// CHECK de tablas propias que su archivo puede reemplazar, y por qué.
    /// </summary>
    /// <remarks>
    /// Un CHECK sólo se ensancha borrándolo y recreándolo, y contra una base que ya
    /// tiene la tabla el <c>CREATE TABLE IF NOT EXISTS</c> no lo toca. Se admite
    /// únicamente el par <c>DROP CONSTRAINT IF EXISTS</c> / <c>ADD CONSTRAINT ...
    /// CHECK</c> sobre el nombre listado acá; cualquier otro DROP sigue prohibido. La
    /// lista crece sólo con un motivo escrito.
    /// </remarks>
    private static readonly string[] ReemplazosDeCheckRatificados =
    [
        // 003: `faltan_datos` entra al set de razones del 👎 (asistente-rediseno-v3,
        // design.md D7). El reemplazo está guardado por la definición vigente, así
        // que es idempotente, y sólo ensancha: ninguna fila existente lo viola.
        "retroalimentacion_turno_razon_valida",
    ];

    /// <summary>
    /// El contenido del archivo sin los reemplazos de CHECK ratificados, para que
    /// <see cref="DestruccionEnSql"/> juzgue todo lo demás.
    /// </summary>
    private static Archivo SinReemplazosDeCheckRatificados(Archivo archivo) =>
        archivo with
        {
            Contenido = ReemplazosDeCheckRatificados.Aggregate(archivo.Contenido, (contenido, nombre) =>
                Regex.Replace(
                    contenido,
                    @"\bALTER\s+TABLE\s+asistente\.\w+\s+(?:DROP\s+CONSTRAINT\s+IF\s+EXISTS\s+"
                        + Regex.Escape(nombre) + @"|ADD\s+CONSTRAINT\s+" + Regex.Escape(nombre)
                        + @"\s+CHECK\b[^;]*)\s*;",
                    string.Empty,
                    RegexOptions.IgnoreCase)),
        };

    // El namespace raíz del SDK y sus tipos propios. Alcanza con el namespace: no
    // hay forma de usar el SDK sin nombrarlo, porque el módulo no tiene ningún
    // using global que lo traiga.
    [GeneratedRegex(@"\bAnthropic\.[A-Z]|\bAnthropicClient\b")]
    private static partial Regex SdkDelProveedor();

    // La clave declarada CON un valor. La cadena vacía se perdona a propósito: es la
    // forma en que un appsettings documenta que el valor existe sin traer el secreto.
    [GeneratedRegex("\"ClaveDelProveedor\"\\s*:\\s*\"[^\"]+\"")]
    private static partial Regex ClaveConValor();
}
