using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica la frontera de salida contra una base real: que la clasificación se
/// resuelva por los identificadores del motor y que ningún dato personal llegue al
/// prompt de redacción.
/// </summary>
/// <remarks>
/// Lo que <see cref="EnmascaradorTests"/> no puede probar en memoria es
/// justamente lo que hace que el mecanismo funcione: que el motor reporte el
/// origen de cada columna, y que lo siga reportando a través de la envoltura en
/// subconsulta con la que el ejecutor impone el límite de filas. Si eso no
/// valiera, el enmascarador estaría clasificando todo como desconocido y tapando
/// nada.
/// </remarks>
public sealed class EnmascaramientoDelTurnoTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_mascara")
{
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private static readonly Guid Coordinador = Guid.Parse("a0000000-0000-4000-8000-000000000003");

    /// <summary>Un documento sembrado por el fixture sintético.</summary>
    private const string DocumentoSembrado = "28341567";

    private const string TelefonoSembrado = "11-4000-0001";

    // --------------------------------------------- el origen sobrevive al motor

    [Fact]
    public async Task Una_columna_sensible_con_alias_queda_clasificada_como_sensible()
    {
        // EL TEST QUE SOSTIENE TODO EL DISEÑO. El alias no está en ningún
        // manifiesto: lo que la tapa es el identificador que reporta el motor. Con
        // una comparación por nombre, esta columna pasaría entera.
        await SembrarAsync();

        var resultado = await EjecutarAsync(
            "SELECT documento AS codigo_interno FROM identity.personas ORDER BY legajo",
            Secretaria,
            conDatosPersonales: true);

        Assert.Equal(["codigo_interno"], resultado.Columnas);
        Assert.Equal(
            ClasificacionDeSensibilidad.SensibleValor,
            resultado.SensibilidadDe(0).Clasificacion);
    }

    [Fact]
    public async Task El_origen_sobrevive_a_la_envoltura_y_a_una_expresion_comun()
    {
        // El ejecutor envuelve la consulta en una subconsulta para imponer el
        // límite. Si esa envoltura borrara el origen, todo quedaría desconocido.
        await SembrarAsync();

        var resultado = await EjecutarAsync(
            """
            WITH visibles AS (SELECT * FROM identity.personas)
            SELECT v.apellido, v.telefono AS contacto FROM visibles v ORDER BY v.legajo
            """,
            Secretaria,
            conDatosPersonales: true);

        Assert.Equal(ClasificacionDeSensibilidad.Publica, resultado.SensibilidadDe(0).Clasificacion);
        Assert.Equal(
            ClasificacionDeSensibilidad.SensibleValor,
            resultado.SensibilidadDe(1).Clasificacion);
    }

    [Fact]
    public async Task Una_columna_calculada_queda_con_origen_desconocido()
    {
        // El riesgo residual declarado, fijado en un test para que se vea. Se trata
        // como pública porque enmascarar todo origen desconocido rompería count(*).
        await SembrarAsync();

        var resultado = await EjecutarAsync(
            "SELECT count(*) AS total FROM identity.personas", Secretaria, conDatosPersonales: true);

        Assert.Equal(
            ClasificacionDeSensibilidad.Desconocida,
            resultado.SensibilidadDe(0).Clasificacion);
    }

    [Fact]
    public async Task El_catalogo_se_resuelve_una_sola_vez()
    {
        await SembrarAsync();

        var catalogo = new CatalogoDeSensibilidad(Apertura, ManifiestoDeSensibilidad.Cargar());
        var ct = TestContext.Current.CancellationToken;

        await catalogo.PrepararAsync(ct);
        await catalogo.PrepararAsync(ct);

        Assert.Equal(1, catalogo.Lecturas);
    }

    [Fact]
    public async Task La_resolucion_encuentra_las_columnas_personales()
    {
        await SembrarAsync();

        var catalogo = new CatalogoDeSensibilidad(Apertura, ManifiestoDeSensibilidad.Cargar());
        await catalogo.PrepararAsync(TestContext.Current.CancellationToken);

        // Se comprueba a través de una consulta real: pedir las cuatro columnas y
        // ver que las cuatro salen clasificadas. Afirmar sobre el diccionario
        // interno probaría la estructura, no el efecto.
        var resultado = await EjecutarAsync(
            "SELECT documento, cuil, telefono, fecha_nacimiento FROM identity.personas",
            Secretaria,
            conDatosPersonales: true);

        Assert.All(
            Enumerable.Range(0, 4),
            indice => Assert.Equal(
                ClasificacionDeSensibilidad.SensibleValor,
                resultado.SensibilidadDe(indice).Clasificacion));
    }

    [Fact]
    public async Task Un_manifiesto_que_nombra_una_columna_inexistente_falla_nombrandola()
    {
        // Un manifiesto viejo es exactamente el que deja de clasificar la columna
        // nueva que ocupó el lugar de la que se fue. Falla en vez de ignorarla.
        await SembrarAsync();

        var catalogo = new CatalogoDeSensibilidad(
            Apertura,
            ManifiestoDeSensibilidad.Interpretar(
                """
                {"tablas":[{"schema":"identity","tabla":"personas","columnas":[
                  {"columna":"columna_fantasma","clasificacion":"publica"}]}]}
                """));

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => catalogo.PrepararAsync(TestContext.Current.CancellationToken));

        Assert.Contains(
            "identity.personas.columna_fantasma", excepcion.Message, StringComparison.Ordinal);
    }

    // ------------------------------------------------- el turno de punta a punta

    [Fact]
    public async Task El_documento_no_llega_al_prompt_de_redaccion_pero_si_al_llamador()
    {
        await SembrarAsync();

        var (turno, proveedor) = await PreguntarAsync(
            Secretaria,
            "¿Cuál es el documento de los docentes?",
            "SELECT apellido, documento FROM identity.personas ORDER BY legajo");

        var redaccion = proveedor.Recibidas[^1].Mensaje;

        // Al modelo, tapado.
        Assert.DoesNotContain(DocumentoSembrado, redaccion, StringComparison.Ordinal);
        Assert.Contains("documento", redaccion, StringComparison.Ordinal);

        // Al llamador, real: el dato lo renderiza la interfaz, no la narración.
        Assert.Contains(
            turno.Filas,
            fila => fila.Any(valor => Equals(valor, DocumentoSembrado)));
    }

    [Fact]
    public async Task El_turno_declara_que_columnas_son_sensibles()
    {
        await SembrarAsync();

        var (turno, _) = await PreguntarAsync(
            Secretaria,
            "¿Cuál es el documento de los docentes?",
            "SELECT apellido, documento FROM identity.personas ORDER BY legajo");

        Assert.Equal(
            [ClasificacionDeSensibilidad.Publica, ClasificacionDeSensibilidad.SensibleValor],
            turno.Sensibilidad.Select(columna => columna.Clasificacion));
    }

    [Fact]
    public async Task El_comentario_del_historial_no_llega_al_modelo()
    {
        await SembrarAsync();

        // La pregunta evita a propósito la palabra «comentario»: el prompt lleva la
        // pregunta del usuario tal cual, así que buscarla en todo el texto daría un
        // falso positivo por el eco de la propia pregunta.
        var (turno, proveedor) = await PreguntarAsync(
            Secretaria,
            "¿Qué pasó con los pedidos?",
            "SELECT h.accion, h.comentario FROM designaciones.pedido_historial h ORDER BY h.created_at");

        var redaccion = proveedor.Recibidas[^1].Mensaje;

        // El valor no viaja...
        Assert.DoesNotContain("Documentación completa", redaccion, StringComparison.Ordinal);
        Assert.DoesNotContain("Cobertura urgente", redaccion, StringComparison.Ordinal);

        // ...y el nombre de la columna tampoco: dejarlo invitaría al modelo a
        // mencionar un comentario que no puede leer.
        Assert.DoesNotContain("comentario", LineaDeColumnas(redaccion), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("accion", LineaDeColumnas(redaccion), StringComparison.Ordinal);

        // Y el llamador sí la recibe, con su valor real.
        Assert.Contains("comentario", turno.Columnas);
        Assert.Contains(
            turno.Filas, fila => fila.Any(valor => Equals(valor, "Documentación completa")));
    }

    [Fact]
    public async Task El_log_del_turno_no_contiene_valores_de_filas()
    {
        await SembrarAsync();

        var registro = new RegistroDeCapturas();
        var proveedor = new ProveedorGuionado(ProveedorGuionado.Generacion(
            "SELECT apellido, documento FROM identity.personas ORDER BY legajo"));

        await CarrilCon(proveedor, registro).ResponderAsync(
            Secretaria, "¿Cuál es el documento?", null, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(DocumentoSembrado, registro.Todo(), StringComparison.Ordinal);
        Assert.DoesNotContain("López", registro.Todo(), StringComparison.Ordinal);
    }

    // ------------------- redaction without masking (asistente-redaccion-sin-enmascarado-local)

    private static OpcionesAsistente SinEnmascarar(
        string provider = "local", string cassetteDirectory = "", bool templates = false) =>
        new()
        {
            RedaccionSinEnmascarar = true,
            Proveedor = provider,
            DirectorioDeCassettes = cassetteDirectory,
            RedaccionConPlantillas = templates,
        };

    private const string SqlDeDocumentos =
        "SELECT apellido, documento FROM identity.personas ORDER BY legajo";

    [Fact]
    public async Task Unmasked_option_in_effect_puts_the_document_in_the_redaction_prompt()
    {
        await SembrarAsync();

        var (turno, proveedor) = await PreguntarAsync(
            Secretaria, "¿Cuál es el documento de los docentes?", SqlDeDocumentos, SinEnmascarar());

        var redaccion = proveedor.Recibidas[^1].Mensaje;

        Assert.Contains(DocumentoSembrado, redaccion, StringComparison.Ordinal);
        Assert.DoesNotContain("«documento", redaccion, StringComparison.Ordinal);

        // The caller still gets the real rows.
        Assert.Contains(turno.Filas, fila => fila.Any(valor => Equals(valor, DocumentoSembrado)));
    }

    [Fact]
    public async Task Unmasked_option_with_a_remote_provider_keeps_the_document_out_of_the_prompt()
    {
        await SembrarAsync();

        var (_, proveedor) = await PreguntarAsync(
            Secretaria,
            "¿Cuál es el documento de los docentes?",
            SqlDeDocumentos,
            SinEnmascarar(provider: "anthropic"));

        Assert.DoesNotContain(
            DocumentoSembrado, proveedor.Recibidas[^1].Mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unmasked_option_with_a_cassette_directory_keeps_the_document_out_of_the_prompt()
    {
        await SembrarAsync();

        var (_, proveedor) = await PreguntarAsync(
            Secretaria,
            "¿Cuál es el documento de los docentes?",
            SqlDeDocumentos,
            SinEnmascarar(cassetteDirectory: "cassettes"));

        Assert.DoesNotContain(
            DocumentoSembrado, proveedor.Recibidas[^1].Mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unmasked_option_in_effect_still_suppresses_the_history_comment()
    {
        await SembrarAsync();

        var (turno, proveedor) = await PreguntarAsync(
            Secretaria,
            "¿Qué pasó con los pedidos?",
            "SELECT h.accion, h.comentario FROM designaciones.pedido_historial h ORDER BY h.created_at",
            SinEnmascarar());

        var redaccion = proveedor.Recibidas[^1].Mensaje;

        Assert.DoesNotContain("Documentación completa", redaccion, StringComparison.Ordinal);
        Assert.DoesNotContain("Cobertura urgente", redaccion, StringComparison.Ordinal);
        Assert.DoesNotContain("comentario", LineaDeColumnas(redaccion), StringComparison.OrdinalIgnoreCase);

        Assert.Contains("comentario", turno.Columnas);
    }

    [Fact]
    public async Task A_scoped_actor_asking_for_phones_gets_none_with_the_option_in_effect()
    {
        await SembrarAsync();

        var (turno, proveedor) = await PreguntarAsync(
            Coordinador,
            "¿Cuáles son los teléfonos de los docentes?",
            "SELECT apellido, telefono FROM identity.personas ORDER BY legajo",
            SinEnmascarar());

        Assert.NotEqual(EstadoDelTurno.Respondida, turno.Estado);
        Assert.DoesNotContain(TelefonoSembrado, turno.Respuesta, StringComparison.Ordinal);
        Assert.DoesNotContain(
            TelefonoSembrado,
            string.Join("\n", proveedor.Recibidas.Select(r => r.Mensaje)),
            StringComparison.Ordinal);
        Assert.Empty(turno.Filas);
    }

    [Fact]
    public async Task The_turn_log_carries_no_row_value_with_the_option_in_effect()
    {
        await SembrarAsync();

        var registro = new RegistroDeCapturas();
        var proveedor = new ProveedorGuionado(ProveedorGuionado.Generacion(SqlDeDocumentos));

        await CarrilCon(proveedor, registro, SinEnmascarar()).ResponderAsync(
            Secretaria, "¿Cuál es el documento?", null, TestContext.Current.CancellationToken);

        // The value does reach the prompt, but never a log line.
        Assert.Contains(
            DocumentoSembrado, proveedor.Recibidas[^1].Mensaje, StringComparison.Ordinal);
        Assert.DoesNotContain(DocumentoSembrado, registro.Todo(), StringComparison.Ordinal);
        Assert.DoesNotContain("López", registro.Todo(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("local", true)]
    [InlineData("anthropic", false)]
    public async Task A_one_column_template_states_the_real_value_only_when_the_option_is_in_effect(
        string provider, bool statesTheValue)
    {
        await SembrarAsync();

        var (turno, proveedor) = await PreguntarAsync(
            Secretaria,
            "¿Cuál es el documento del docente?",
            $"SELECT documento FROM identity.personas WHERE documento = '{DocumentoSembrado}'",
            SinEnmascarar(provider: provider, templates: true));

        // The template answers without a model call, so only the generation was sent.
        Assert.Equal(1, proveedor.Recibidas.Count);
        Assert.Equal(statesTheValue, turno.Respuesta.Contains(DocumentoSembrado, StringComparison.Ordinal));
        Assert.Equal(!statesTheValue, turno.Respuesta.Contains("«documento", StringComparison.Ordinal));
    }

    // ------------------------------------------ selección de conexión (ARS-37)

    [Fact]
    public async Task Un_actor_global_con_permiso_resuelve_a_la_conexion_con_datos_personales()
    {
        await SembrarAsync();

        var perfil = await new ConsultorDeAlcance(Apertura)
            .ObtenerAsync(Secretaria, TestContext.Current.CancellationToken);

        Assert.True(perfil.EsGlobal);
        Assert.True(perfil.VeDatosPersonales);
    }

    [Fact]
    public async Task Un_actor_de_ambito_acotado_resuelve_a_la_conexion_basica()
    {
        // La política de la aplicación es la puerta; el acotamiento se aplica
        // después, en el controller. Sin exigir alcance global ADEMÁS del permiso,
        // el asistente heredaría la puerta sin el acotamiento.
        await SembrarAsync();

        var perfil = await new ConsultorDeAlcance(Apertura)
            .ObtenerAsync(Coordinador, TestContext.Current.CancellationToken);

        Assert.False(perfil.EsGlobal);
        Assert.False(perfil.VeDatosPersonales);
    }

    [Fact]
    public async Task Con_la_conexion_basica_el_motor_rechaza_la_columna_personal()
    {
        // La restricción la impone el motor, no el código: cualquier camino nuevo
        // que se olvide del filtro falla igual.
        await SembrarAsync();

        var excepcion = await Assert.ThrowsAsync<ConsultaSinPrivilegio>(
            () => EjecutarAsync(
                "SELECT telefono FROM identity.personas", Secretaria, conDatosPersonales: false));

        // Se afirma el SQLSTATE sobre la causa y no sobre el tipo del módulo: lo
        // que este test prueba es que el rechazo lo produjo el MOTOR por falta de
        // privilegio, y eso sigue estando aunque el ejecutor lo traduzca.
        var delMotor = Assert.IsType<PostgresException>(excepcion.InnerException);
        Assert.Equal("42501", delMotor.SqlState);
    }

    [Fact]
    public async Task Un_actor_acotado_que_pregunta_por_telefonos_no_los_recibe()
    {
        await SembrarAsync();

        var (turno, proveedor) = await PreguntarAsync(
            Coordinador,
            "¿Cuáles son los teléfonos de los docentes?",
            "SELECT apellido, telefono FROM identity.personas ORDER BY legajo");

        Assert.NotEqual(EstadoDelTurno.Respondida, turno.Estado);
        Assert.DoesNotContain(TelefonoSembrado, turno.Respuesta, StringComparison.Ordinal);
        Assert.DoesNotContain(
            TelefonoSembrado,
            string.Join("\n", proveedor.Recibidas.Select(r => r.Mensaje)),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_rechazo_del_motor_no_se_filtra_al_usuario()
    {
        await SembrarAsync();

        var (turno, _) = await PreguntarAsync(
            Coordinador,
            "¿Cuáles son los teléfonos?",
            "SELECT telefono FROM identity.personas");

        Assert.DoesNotContain("permission denied", turno.Respuesta, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("42501", turno.Respuesta, StringComparison.Ordinal);
        Assert.DoesNotContain("identity.personas", turno.Respuesta, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ apoyo

    /// <summary>
    /// La línea del prompt que enumera las columnas. Afirmar sobre ella y no sobre
    /// todo el texto evita el falso positivo del eco de la pregunta del usuario.
    /// </summary>
    private static string LineaDeColumnas(string prompt) =>
        prompt.Split('\n').Single(linea => linea.StartsWith("Columnas:", StringComparison.Ordinal));

    private async Task<ResultadoDeConsulta> EjecutarAsync(
        string sql, Guid actor, bool conDatosPersonales)
    {
        var (basica, pii) = CadenasDeLectura();
        var ejecutor = new EjecutorDeConsulta(Apertura,ClasificadorDeSensibilidad(),
            Options.Create(new OpcionesAsistente()));

        return await ejecutor.EjecutarAsync(
            sql, actor, conDatosPersonales, TestContext.Current.CancellationToken);
    }

    private async Task<(ResultadoDelTurno Turno, ProveedorGuionado Proveedor)> PreguntarAsync(
        Guid actor, string pregunta, string sql, OpcionesAsistente? opciones = null)
    {
        var proveedor = new ProveedorGuionado(ProveedorGuionado.Generacion(sql));

        var turno = await CarrilCon(proveedor, opcionesDelRedactor: opciones).ResponderAsync(
            actor, pregunta, null, TestContext.Current.CancellationToken);

        return (turno, proveedor);
    }

    private CarrilSql CarrilCon(
        ProveedorGuionado proveedor,
        RegistroDeCapturas? registro = null,
        OpcionesAsistente? opcionesDelRedactor = null)
    {
        var (basica, pii) = CadenasDeLectura();
        var opciones = Options.Create(new OpcionesAsistente());
        var contador = new ContadorDeLlamadasDelTurno(
            new OpcionesAsistente().MaximoDeLlamadasPorTurno);
        var conTecho = new ProveedorConTechoDeLlamadas(proveedor, contador);

        return BancoDelAsistente.ArmarCarrilSql(
            basica,
            pii,
            Apertura,
            new EjecutorDeConsulta(Apertura,ClasificadorDeSensibilidad(), opciones),
            conTecho,
            contador,
            opciones,
            log: registro is null ? NullLogger<CarrilSql>.Instance : registro.Logger<CarrilSql>(),
            opcionesDelRedactor: opcionesDelRedactor is null ? null : Options.Create(opcionesDelRedactor));
    }

}
