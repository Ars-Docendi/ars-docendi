using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica la frontera de salida contra una base real: que la clasificación se resuelva por los
/// identificadores del motor y que ningún dato personal llegue al prompt de redacción.
/// </summary>
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

        // Se comprueba a través de una consulta real: pedir las cuatro columnas y ver que las
        // cuatro salen clasificadas.
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
    public async Task A_cloud_adapter_resolved_for_the_turn_masks_even_when_the_environment_runs_on_own_hardware()
    {
        await SembrarAsync();

        // The environment says own hardware with the option on; the turn resolved a
        // cloud company (a runtime selection). The turn's decision wins: masked.
        var (_, proveedor) = await PreguntarAsync(
            Secretaria,
            "¿Cuál es el documento de los docentes?",
            SqlDeDocumentos,
            SinEnmascarar(provider: "ollama"),
            new EnmascaradoFijo(sinEnmascarar: false));

        var redaccion = proveedor.Recibidas[^1].Mensaje;

        Assert.DoesNotContain(DocumentoSembrado, redaccion, StringComparison.Ordinal);
        Assert.Contains("«documento", redaccion, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_own_hardware_adapter_resolved_for_the_turn_unmasks_per_the_environment_option()
    {
        await SembrarAsync();

        // The reverse: a cloud environment, and the turn resolved own hardware.
        var (_, proveedor) = await PreguntarAsync(
            Secretaria,
            "¿Cuál es el documento de los docentes?",
            SqlDeDocumentos,
            SinEnmascarar(provider: "anthropic"),
            new EnmascaradoFijo(sinEnmascarar: true));

        Assert.Contains(DocumentoSembrado, proveedor.Recibidas[^1].Mensaje, StringComparison.Ordinal);
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
        // La política de la aplicación es la puerta; el acotamiento se aplica después, en el controller.
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
        Guid actor, string pregunta, string sql, OpcionesAsistente? opciones = null,
        IEnmascaradoDelTurno? enmascarado = null)
    {
        var proveedor = new ProveedorGuionado(ProveedorGuionado.Generacion(sql));

        var turno = await CarrilCon(proveedor, opcionesDelRedactor: opciones, enmascarado: enmascarado)
            .ResponderAsync(actor, pregunta, null, TestContext.Current.CancellationToken);

        return (turno, proveedor);
    }

    private CarrilSql CarrilCon(
        ProveedorGuionado proveedor,
        RegistroDeCapturas? registro = null,
        OpcionesAsistente? opcionesDelRedactor = null,
        IEnmascaradoDelTurno? enmascarado = null)
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
            opcionesDelRedactor: opcionesDelRedactor is null ? null : Options.Create(opcionesDelRedactor),
            enmascarado: enmascarado);
    }

    // ---- the real composition wires the resolver into CarrilSql (task 2.19, end to end)

    [Fact]
    public async Task Through_the_real_composition_a_cloud_selection_masks_and_own_hardware_unmasks()
    {
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;
        var cable = new CableDeRedaccion();

        static string A(string n) => $"{OpcionesAsistente.Seccion}:{n}";
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ArsDocendi"] = Cadena,
                [A(nameof(OpcionesAsistente.RolSoloLectura))] = RolSoloLectura,
                [A(nameof(OpcionesAsistente.PasswordSoloLectura))] = PostgresFixture.PasswordDeRol,
                [A(nameof(OpcionesAsistente.RolSoloLecturaPii))] = RolSoloLecturaPii,
                [A(nameof(OpcionesAsistente.PasswordSoloLecturaPii))] = PostgresFixture.PasswordDeRol,
                [A(nameof(OpcionesAsistente.Proveedor))] = "ollama",
                [A(nameof(OpcionesAsistente.Modelo))] = "qwen3.5:9b",
                [A(nameof(OpcionesAsistente.ContextoDelModelo))] = "16384",
                [A(nameof(OpcionesAsistente.RedaccionSinEnmascarar))] = "true",
                [A(nameof(OpcionesAsistente.ClaveMaestra))] = CifradoDeCredencialesTests.ClaveMaestra(7),
                [A(nameof(OpcionesAsistente.MaximoDeIntentosDeTransporte))] = "1",
                [$"{OpcionesInferencia.Seccion}:Url"] = "https://ia.example.net",
                [$"{OpcionesInferencia.Seccion}:Usuario"] = "preview",
                [$"{OpcionesInferencia.Seccion}:Contrasena"] = "clave",
            })
            .Build();

        var servicios = new ServiceCollection();
        servicios.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(new AmbienteDelHost());
        servicios.AddLogging();
        servicios.AddArsDocendiShared(configuracion);
        servicios.AddAsistenteModule(configuracion);
        servicios.AddHttpClient(ModuleExtensions.ClienteDelProveedor)
            .ConfigurePrimaryHttpMessageHandler(() => cable);
        await using var raiz = servicios.BuildServiceProvider();

        async Task<string> RedaccionAsync()
        {
            cable.Reiniciar();
            using var turno = raiz.CreateScope();
            var carril = turno.ServiceProvider.GetRequiredService<CarrilSql>();
            var resultado = await carril.ResponderAsync(
                Secretaria, "¿Cuál es el documento de los docentes?", null, ct);
            Assert.Equal(EstadoDelTurno.Respondida, resultado.Estado);
            return cable.UltimoPrompt();
        }

        async Task SeleccionarAsync(string empresa, string modelo, string? clave)
        {
            await using var alcance = raiz.CreateAsyncScope();
            if (clave is not null)
            {
                await alcance.ServiceProvider.GetRequiredService<RepositorioDeCredenciales>()
                    .GuardarAsync(empresa, clave, Guid.NewGuid(), ct);
            }

            Assert.True(await alcance.ServiceProvider.GetRequiredService<RepositorioDelModelo>()
                .AplicarSeleccionAsync(empresa, modelo, Guid.NewGuid(), ct));
        }

        // Environment: own hardware with the option on, no selection: unmasked.
        Assert.Contains(DocumentoSembrado, await RedaccionAsync(), StringComparison.Ordinal);

        // A stored cloud selection: the redaction prompt sent to the cloud is masked.
        await SeleccionarAsync("openai", "gpt-5", "sk-live-ABCDEFGH12345678WXYZ");
        var enLaNube = await RedaccionAsync();
        Assert.Equal("api.openai.com", cable.UltimoHost);
        Assert.DoesNotContain(DocumentoSembrado, enLaNube, StringComparison.Ordinal);
        Assert.Contains("«documento", enLaNube, StringComparison.Ordinal);

        // Back to own hardware: unmasked again.
        await SeleccionarAsync("ollama", "qwen3.5:9b", null);
        Assert.Contains(DocumentoSembrado, await RedaccionAsync(), StringComparison.Ordinal);
        Assert.Equal("ia.example.net", cable.UltimoHost);
    }

    /// <summary>Answers a generation (call 1) and a redaction (call 2) for both wire shapes.</summary>
    private sealed class CableDeRedaccion : HttpMessageHandler
    {
        private const string Sql = "SELECT apellido, documento FROM identity.personas ORDER BY legajo";
        private readonly List<(string Host, string Cuerpo)> _pedidos = [];

        public string UltimoHost => _pedidos[^1].Host;

        public void Reiniciar() => _pedidos.Clear();

        /// <summary>The text of every message of the last request, JSON-decoded.</summary>
        public string UltimoPrompt() => string.Join(
            "\n",
            System.Text.Json.JsonDocument.Parse(_pedidos[^1].Cuerpo).RootElement
                .GetProperty("messages").EnumerateArray()
                .Select(m => m.GetProperty("content").ToString()));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken ct)
        {
            _pedidos.Add((pedido.RequestUri!.Host, await pedido.Content!.ReadAsStringAsync(ct)));
            var texto = _pedidos.Count == 1 ? ProveedorGuionado.Generacion(Sql) : "Respuesta redactada.";
            var json = System.Text.Json.JsonSerializer.Serialize(texto);

            var cuerpo = pedido.RequestUri.Host == "ia.example.net"
                ? $$$"""{"model":"m","message":{"role":"assistant","content":{{{json}}} },"done":true,"done_reason":"stop","prompt_eval_count":10,"eval_count":5}"""
                : $$$"""{"id":"c","object":"chat.completion","created":1,"model":"m","choices":[{"index":0,"message":{"role":"assistant","content":{{{json}}} },"finish_reason":"stop"}],"usage":{"prompt_tokens":10,"completion_tokens":5,"total_tokens":15}}""";

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(cuerpo, System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class AmbienteDelHost : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "pruebas";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private sealed class EnmascaradoFijo(bool sinEnmascarar) : IEnmascaradoDelTurno
    {
        public Task<bool> RedaccionSinEnmascararAsync(CancellationToken ct) => Task.FromResult(sinEnmascarar);
    }

}
