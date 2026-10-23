using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Las optimizaciones para un modelo local que no necesitan una base
/// (asistente-optimizaciones-modelo-local): esquema compacto, ejemplos en el
/// prefijo, reintento y reparación, plantillas y caché de consultas.
/// </summary>
public sealed class OptimizacionesModeloLocalPurasTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 3);

    // ------------------------------------------------------- D2, esquema compacto

    private static readonly ColumnaLegible[] Columnas =
    [
        new("designaciones", "designaciones", "id", "uuid", true, "Designaciones docentes.", "Identificador de la designación."),
        new("designaciones", "designaciones", "persona_id", "uuid", true, "Designaciones docentes.", "Persona designada."),
        new("designaciones", "designaciones", "vigente_hasta", "date", false, "Designaciones docentes.",
            "Último día de vigencia. NULO significa vigencia ABIERTA."),
        new("designaciones", "designaciones", "creada", "timestamp with time zone", true, "Designaciones docentes.", null),
        new("identity", "personas", "id", "uuid", true, "Personas.", "Identificador de la persona."),
    ];

    private static readonly ReferenciaLegible[] Referencias =
    [
        new("designaciones", "designaciones", "persona_id", "identity", "personas", "id"),
    ];

    [Fact]
    public void Sin_la_opcion_el_esquema_es_byte_a_byte_el_de_siempre()
    {
        // D1: el prefijo de Claude está sellado en los cassettes.
        Assert.Equal(
            RenderizadorDeEsquema.Renderizar(Columnas, Referencias, []),
            RenderizadorDeEsquema.Renderizar(Columnas, Referencias, [], compacto: false));
    }

    [Fact]
    public void El_esquema_compacto_lleva_la_clave_foranea_en_la_linea_de_su_columna()
    {
        var compacto = RenderizadorDeEsquema.Renderizar(Columnas, Referencias, [], compacto: true);

        Assert.Contains("- persona_id uuid → identity.personas.id: Persona designada.", compacto, StringComparison.Ordinal);
        Assert.DoesNotContain("Cómo se relacionan", compacto, StringComparison.Ordinal);
    }

    [Fact]
    public void El_esquema_compacto_conserva_las_reglas_y_abrevia_la_forma()
    {
        var compacto = RenderizadorDeEsquema.Renderizar(Columnas, Referencias, [], compacto: true);
        var completo = RenderizadorDeEsquema.Renderizar(Columnas, Referencias, []);

        // La regla del dominio viaja entera; la nulabilidad es un `?`.
        Assert.Contains(
            "- vigente_hasta date?: Último día de vigencia. NULO significa vigencia ABIERTA.",
            compacto, StringComparison.Ordinal);
        Assert.Contains("- creada timestamptz\n", compacto, StringComparison.Ordinal);
        // El comentario de un `id` que sólo dice «Identificador de …» se omite.
        Assert.Contains("- id uuid\n", compacto, StringComparison.Ordinal);
        Assert.Contains("Designaciones docentes.", compacto, StringComparison.Ordinal);
        Assert.True(compacto.Length < completo.Length);
    }

    // ---------------------------------------------- D3, ejemplos en el prefijo

    [Fact]
    public async Task Con_los_ejemplos_en_el_prefijo_van_todos_alli_y_ninguno_en_el_mensaje()
    {
        var proveedor = new ProveedorGuionado(ProveedorGuionado.Generacion("SELECT 1"));
        var selector = new SelectorDeEjemplos();

        await Generador(proveedor, new OpcionesAsistente { EjemplosEnElPrefijo = true })
            .GenerarAsync("¿cuántos pedidos hay?", false, Ct);

        var solicitud = proveedor.Recibidas[0];
        Assert.All(selector.Catalogo, ejemplo =>
            Assert.Contains(ejemplo.Pregunta, solicitud.PrefijoEstable, StringComparison.Ordinal));
        Assert.DoesNotContain("Ejemplos de preguntas", solicitud.Mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Con_los_ejemplos_en_el_prefijo_el_prefijo_es_el_mismo_para_cualquier_pregunta()
    {
        var proveedor = new ProveedorGuionado(ProveedorGuionado.Generacion("SELECT 1"));
        var generador = Generador(proveedor, new OpcionesAsistente { EjemplosEnElPrefijo = true });

        await generador.GenerarAsync("¿cuántos pedidos hay?", false, Ct);
        await generador.GenerarAsync("¿qué materias dicta Pérez?", false, Ct);

        Assert.Equal(proveedor.Recibidas[0].PrefijoEstable, proveedor.Recibidas[1].PrefijoEstable);
    }

    // ------------------------------------------- D4, intento anterior y saneado

    [Fact]
    public async Task El_intento_anterior_viaja_al_final_del_mensaje()
    {
        var proveedor = new ProveedorGuionado(ProveedorGuionado.Generacion("SELECT 1"));

        await Generador(proveedor).GenerarAsync(
            "¿cuántos pedidos hay?", false, Ct,
            intentoAnterior: ("SELECT count(*) FROM x", "no devolvió filas"));

        var mensaje = proveedor.Recibidas[0].Mensaje;
        Assert.EndsWith(
            "devolvé `es_contestable` en false.\n", mensaje, StringComparison.Ordinal);
        Assert.Contains("SQL: SELECT count(*) FROM x\nQué pasó: no devolvió filas", mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_literal_que_no_esta_en_la_consulta_no_viaja_al_modelo()
    {
        // El cast que falla sobre el valor de una FILA lo cita entero: ese valor no
        // pasó por el enmascarador.
        var problema = ErrorDelMotorSaneado.Problema(
            "SELECT numero::int FROM designaciones.pedidos",
            "22P02",
            "invalid input syntax for type integer: \"P-2026-0042\"");

        Assert.DoesNotContain("P-2026-0042", problema, StringComparison.Ordinal);
        Assert.Contains("\"…\"", problema, StringComparison.Ordinal);
        Assert.Contains("22P02", problema, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_literal_que_esta_en_la_consulta_si_viaja()
    {
        var problema = ErrorDelMotorSaneado.Problema(
            "SELECT foo FROM designaciones.pedidos",
            "42703",
            "column \"foo\" does not exist");

        Assert.Contains("\"foo\"", problema, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("42501", false)]
    [InlineData("57014", false)]
    [InlineData("42703", true)]
    [InlineData("22P02", true)]
    public void Privilegio_y_timeout_no_se_reparan(string estado, bool reparable) =>
        Assert.Equal(reparable, ErrorDelMotorSaneado.EsReparable(estado));

    // ------------------------------------------------------------ D5, plantillas

    [Fact]
    public void Un_valor_unico_se_dice_sin_modelo()
    {
        var resultado = new ResultadoDeConsulta(["cantidad"], [[4L]], Truncado: false);

        Assert.Equal("El resultado es 4.", PlantillaDeRedaccion.Intentar(resultado, alcanzaTodo: true, []));
    }

    [Fact]
    public void Una_lista_corta_se_dice_sin_modelo()
    {
        var resultado = new ResultadoDeConsulta(
            ["nombre"], [["Ana"], ["Bruno"], ["Carla"]], Truncado: false);

        Assert.Equal(
            "Encontré 3 resultados: Ana, Bruno y Carla.",
            PlantillaDeRedaccion.Intentar(resultado, alcanzaTodo: true, []));
    }

    [Fact]
    public void Lo_que_necesita_matices_sigue_yendo_al_modelo()
    {
        var unValor = new ResultadoDeConsulta(["cantidad"], [[4L]], Truncado: false);

        // Alcance parcial, recorte, dato autodeclarado, dos columnas, seis filas.
        Assert.Null(PlantillaDeRedaccion.Intentar(unValor, alcanzaTodo: false, []));
        Assert.Null(PlantillaDeRedaccion.Intentar(unValor with { Truncado = true }, true, []));
        Assert.Null(PlantillaDeRedaccion.Intentar(unValor, true, [new CoberturaDeUnDato("portal.habilidades", 3, 20)]));
        Assert.Null(PlantillaDeRedaccion.Intentar(
            new ResultadoDeConsulta(["a", "b"], [[1L, 2L]], false), true, []));
        Assert.Null(PlantillaDeRedaccion.Intentar(
            new ResultadoDeConsulta(["n"], [.. Enumerable.Range(0, 6).Select(i => (IReadOnlyList<object?>)[(object?)i])], false),
            true, []));
    }

    [Fact]
    public async Task Con_plantillas_el_redactor_no_llama_al_modelo_para_un_valor_unico()
    {
        var proveedor = new ProveedorGuionado("No debería llamarse.");
        var redactor = new RedactorDeRespuesta(
            proveedor, Options.Create(new OpcionesAsistente { RedaccionConPlantillas = true }));

        var texto = await redactor.RedactarAsync(
            "¿cuántos pedidos hay?", new ResultadoDeConsulta(["cantidad"], [[12L]], false), true, [], Ct);

        Assert.Equal("El resultado es 12.", texto);
        Assert.Equal(0, proveedor.Llamadas);
    }

    [Fact]
    public async Task Sin_la_opcion_el_redactor_sigue_llamando_al_modelo()
    {
        var proveedor = new ProveedorGuionado("Hay 12 pedidos.");
        var redactor = new RedactorDeRespuesta(proveedor, Options.Create(new OpcionesAsistente()));

        await redactor.RedactarAsync(
            "¿cuántos pedidos hay?", new ResultadoDeConsulta(["cantidad"], [[12L]], false), true, [], Ct);

        Assert.Equal(1, proveedor.Llamadas);
    }

    // ----------------------------------------------------------- D6, caché

    [Fact]
    public void La_cache_normaliza_la_pregunta_pero_distingue_rol_y_dia()
    {
        var cache = new CacheDeConsultasGeneradas(TimeProvider.System);
        var generacion = new GeneracionDeSql(true, "SELECT 1", "Cuento.", "agregacion");

        cache.Guardar("¿Cuántos pedidos hay?", false, Hoy, generacion, TimeSpan.FromMinutes(5));

        Assert.Same(generacion, cache.Buscar("  cuántos   PEDIDOS hay ", false, Hoy));
        Assert.Null(cache.Buscar("¿Cuántos pedidos hay?", conDatosPersonales: true, Hoy));
        Assert.Null(cache.Buscar("¿Cuántos pedidos hay?", false, Hoy.AddDays(1)));
    }

    [Fact]
    public void La_cache_vence()
    {
        var reloj = new RelojFijo(new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.Zero));
        var cache = new CacheDeConsultasGeneradas(reloj);

        cache.Guardar("¿cuántos?", false, Hoy, new GeneracionDeSql(true, "SELECT 1", ".", "agregacion"), TimeSpan.FromMinutes(5));
        reloj.Avanzar(TimeSpan.FromMinutes(6));

        Assert.Null(cache.Buscar("¿cuántos?", false, Hoy));
    }

    [Fact]
    public void La_cache_tiene_tope()
    {
        var cache = new CacheDeConsultasGeneradas(TimeProvider.System);
        var generacion = new GeneracionDeSql(true, "SELECT 1", ".", "agregacion");

        for (var i = 0; i <= CacheDeConsultasGeneradas.Tope; i++)
        {
            cache.Guardar($"pregunta {i}", false, Hoy, generacion, TimeSpan.FromMinutes(5));
        }

        Assert.Equal(CacheDeConsultasGeneradas.Tope, cache.Cantidad);
        // La más vieja es la que se fue.
        Assert.Null(cache.Buscar("pregunta 0", false, Hoy));
    }

    // -------------------------------------- D7, reescritura en la generación

    [Fact]
    public async Task Con_preguntas_anteriores_el_mensaje_las_trae_y_pide_la_pregunta_resuelta()
    {
        var proveedor = new ProveedorGuionado(ProveedorGuionado.Generacion("SELECT 1"));

        await Generador(proveedor).GenerarAsync(
            "¿y en Sistemas?", false, Ct, preguntasAnteriores: ["¿cuántos docentes están designados?"]);

        var mensaje = proveedor.Recibidas[0].Mensaje;
        Assert.Contains("- ¿cuántos docentes están designados?", mensaje, StringComparison.Ordinal);
        Assert.Contains("`pregunta_interpretada`", mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public async Task La_pregunta_resuelta_por_la_generacion_se_interpreta()
    {
        var proveedor = new ProveedorGuionado(
            """{"pregunta_interpretada":"¿cuántos docentes están designados en Sistemas?","es_contestable":true,"sql":"SELECT 1","razonamiento":"Cuento.","categoria":"agregacion"}""");

        var generacion = await Generador(proveedor).GenerarAsync(
            "¿y en Sistemas?", false, Ct, preguntasAnteriores: ["¿cuántos docentes están designados?"]);

        Assert.Equal("¿cuántos docentes están designados en Sistemas?", generacion.PreguntaInterpretada);
    }

    // ------------------------ asistente-razonamiento-en-segunda-generacion

    [Fact]
    public async Task Con_el_razonamiento_la_segunda_generacion_pide_esfuerzo_alto_y_la_primera_no()
    {
        var proveedor = new ProveedorGuionado(ProveedorGuionado.Generacion("SELECT 1"));
        var generador = Generador(proveedor, new OpcionesAsistente { RazonamientoEnSegundaGeneracion = true });

        await generador.GenerarAsync("¿cuántos pedidos hay?", false, Ct);
        await generador.GenerarAsync("¿cuántos pedidos hay?", false, Ct, esSegundaGeneracion: true);

        Assert.Equal(EsfuerzoDelModelo.Medio, proveedor.Recibidas[0].Esfuerzo);
        Assert.Equal(EsfuerzoDelModelo.Alto, proveedor.Recibidas[1].Esfuerzo);
    }

    [Fact]
    public async Task Sin_el_razonamiento_ninguna_generacion_cambia_de_esfuerzo()
    {
        var proveedor = new ProveedorGuionado(ProveedorGuionado.Generacion("SELECT 1"));
        var generador = Generador(proveedor);

        await generador.GenerarAsync("¿cuántos pedidos hay?", false, Ct);
        await generador.GenerarAsync("¿cuántos pedidos hay?", false, Ct, esSegundaGeneracion: true);

        Assert.All(proveedor.Recibidas, solicitud => Assert.Equal(EsfuerzoDelModelo.Medio, solicitud.Esfuerzo));
    }

    [Theory]
    [InlineData(true, 2000, 600, 2000)]
    [InlineData(true, 0, 600, 600)]
    [InlineData(false, 2000, 600, 600)]
    public async Task El_techo_de_la_segunda_generacion_rige_solo_con_la_opcion_prendida_y_mayor_que_cero(
        bool razonamiento, int techoDeLaSegunda, int techoDeLaGeneracion, int esperadoEnLaSegunda)
    {
        var proveedor = new ProveedorGuionado(ProveedorGuionado.Generacion("SELECT 1"));
        var generador = Generador(proveedor, new OpcionesAsistente
        {
            RazonamientoEnSegundaGeneracion = razonamiento,
            MaximoDeTokensDeSegundaGeneracion = techoDeLaSegunda,
            MaximoDeTokensDeGeneracion = techoDeLaGeneracion,
        });

        await generador.GenerarAsync("¿cuántos pedidos hay?", false, Ct);
        await generador.GenerarAsync("¿cuántos pedidos hay?", false, Ct, esSegundaGeneracion: true);

        Assert.Equal(techoDeLaGeneracion, proveedor.Recibidas[0].MaximoDeTokens);
        Assert.Equal(esperadoEnLaSegunda, proveedor.Recibidas[1].MaximoDeTokens);
    }

    [Fact]
    public async Task Con_la_opcion_apagada_la_solicitud_de_la_segunda_generacion_es_identica_a_la_de_siempre()
    {
        // D4: la clave de los cassettes incluye el esfuerzo, así que un cambio acá
        // los invalidaría. Aun con un techo sobrante y con el proveedor anthropic,
        // la solicitud es la misma que sin el parámetro.
        var proveedor = new ProveedorGuionado(ProveedorGuionado.Generacion("SELECT 1"));
        var generador = Generador(proveedor, new OpcionesAsistente
        {
            Proveedor = "anthropic",
            MaximoDeTokensDeSegundaGeneracion = 2000,
        });

        await generador.GenerarAsync(
            "¿cuántos pedidos hay?", false, Ct, intentoAnterior: ("SELECT 1", "no devolvió filas"));
        await generador.GenerarAsync(
            "¿cuántos pedidos hay?", false, Ct, intentoAnterior: ("SELECT 1", "no devolvió filas"),
            esSegundaGeneracion: true);

        Assert.Equal(proveedor.Recibidas[0], proveedor.Recibidas[1]);
    }

    // ------------------------------------------------------------------ apoyo

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static GeneradorDeSql Generador(ProveedorGuionado proveedor, OpcionesAsistente? opciones = null) =>
        new(new EsquemaFijo(),
            new SelectorDeEjemplos(),
            proveedor,
            new FechaDeReferenciaFija(Hoy),
            Options.Create(opciones ?? new OpcionesAsistente()),
            NullLogger<GeneradorDeSql>.Instance);

    private sealed class EsquemaFijo : IProveedorDeEsquema
    {
        public Task<EsquemaParaPrompt> ObtenerAsync(bool conDatosPersonales, CancellationToken ct) =>
            Task.FromResult(new EsquemaParaPrompt("Esquema de prueba.", "huella"));
    }
}
