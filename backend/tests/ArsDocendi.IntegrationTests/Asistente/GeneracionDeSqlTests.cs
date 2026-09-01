using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.Extensions.Options;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica la primera llamada al modelo: la generación de SQL (RF-11, RF-18).
/// </summary>
/// <remarks>
/// Sin base: el proveedor de esquema se sustituye por uno de guion. Lo que se
/// prueba acá es cómo se arma la solicitud y cómo se interpreta la respuesta, y
/// para eso una conexión real no aporta nada.
/// </remarks>
public sealed class GeneracionDeSqlTests
{
    private const string PrefijoDeGuion = "PREFIJO ESTABLE DE PRUEBA";
    private static readonly DateOnly Hoy = new(2026, 8, 24);

    // ------------------------------------------------------------- solicitud

    [Fact]
    public async Task La_llamada_usa_temperatura_cero()
    {
        var proveedor = new ProveedorGuionado(
            ProveedorGuionado.Generacion("SELECT 1 FROM identity.carreras"));

        await Componer(proveedor).GenerarAsync(
            "¿Qué carreras hay?", conDatosPersonales: false, TestContext.Current.CancellationToken);

        Assert.Equal(0.0m, proveedor.Recibidas[0].Temperatura);
    }

    [Fact]
    public async Task La_llamada_usa_el_prefijo_del_proveedor_de_esquema_sin_modificarlo()
    {
        var proveedor = new ProveedorGuionado(
            ProveedorGuionado.Generacion("SELECT 1 FROM identity.carreras"));

        await Componer(proveedor).GenerarAsync(
            "¿Qué carreras hay?", conDatosPersonales: false, TestContext.Current.CancellationToken);

        Assert.Equal(PrefijoDeGuion, proveedor.Recibidas[0].PrefijoEstable);
    }

    [Fact]
    public async Task El_prefijo_no_cambia_entre_preguntas_distintas()
    {
        var proveedor = new ProveedorGuionado(
            ProveedorGuionado.Generacion("SELECT 1 FROM identity.carreras"));
        var generador = Componer(proveedor);
        var ct = TestContext.Current.CancellationToken;

        await generador.GenerarAsync("¿Qué carreras hay?", conDatosPersonales: false, ct);
        await generador.GenerarAsync("¿Qué docentes hay en Bases de Datos?", conDatosPersonales: false, ct);

        // Byte a byte: el caché del proveedor es POR PREFIJO, y una diferencia de
        // un carácter hace que el turno siguiente pague escritura en lugar de
        // lectura sobre el bloque más grande del prompt.
        Assert.Equal(proveedor.Recibidas[0].PrefijoEstable, proveedor.Recibidas[1].PrefijoEstable);
    }

    [Fact]
    public async Task La_fecha_viaja_en_el_mensaje_y_no_en_el_prefijo()
    {
        var proveedor = new ProveedorGuionado(
            ProveedorGuionado.Generacion("SELECT 1 FROM identity.carreras"));

        await Componer(proveedor).GenerarAsync(
            "¿Qué pedidos hay ahora?", conDatosPersonales: false, TestContext.Current.CancellationToken);

        var solicitud = proveedor.Recibidas[0];
        Assert.Contains("2026-08-24", solicitud.Mensaje, StringComparison.Ordinal);
        Assert.DoesNotContain("2026-08-24", solicitud.PrefijoEstable, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Con_la_misma_fecha_la_solicitud_es_reproducible()
    {
        var primera = new ProveedorGuionado(
            ProveedorGuionado.Generacion("SELECT 1 FROM identity.carreras"));
        var segunda = new ProveedorGuionado(
            ProveedorGuionado.Generacion("SELECT 1 FROM identity.carreras"));
        var ct = TestContext.Current.CancellationToken;

        await Componer(primera).GenerarAsync("¿Qué pedidos hay?", conDatosPersonales: false, ct);
        await Componer(segunda).GenerarAsync("¿Qué pedidos hay?", conDatosPersonales: false, ct);

        Assert.Equal(primera.Recibidas[0].Mensaje, segunda.Recibidas[0].Mensaje);
        Assert.Equal(primera.Recibidas[0].PrefijoEstable, segunda.Recibidas[0].PrefijoEstable);
    }

    [Fact]
    public async Task Una_fecha_distinta_cambia_el_mensaje_pero_no_el_prefijo()
    {
        var deAgosto = new ProveedorGuionado(
            ProveedorGuionado.Generacion("SELECT 1 FROM identity.carreras"));
        var deMarzo = new ProveedorGuionado(
            ProveedorGuionado.Generacion("SELECT 1 FROM identity.carreras"));
        var ct = TestContext.Current.CancellationToken;

        await Componer(deAgosto, new DateOnly(2026, 8, 24))
            .GenerarAsync("¿Qué pedidos hay?", conDatosPersonales: false, ct);
        await Componer(deMarzo, new DateOnly(2026, 3, 1))
            .GenerarAsync("¿Qué pedidos hay?", conDatosPersonales: false, ct);

        Assert.NotEqual(deAgosto.Recibidas[0].Mensaje, deMarzo.Recibidas[0].Mensaje);
        Assert.Equal(deAgosto.Recibidas[0].PrefijoEstable, deMarzo.Recibidas[0].PrefijoEstable);
    }

    [Fact]
    public async Task El_prefijo_del_rol_con_datos_personales_es_el_suyo()
    {
        var proveedor = new ProveedorGuionado(
            ProveedorGuionado.Generacion("SELECT 1 FROM identity.carreras"));

        await Componer(proveedor).GenerarAsync(
            "¿Qué teléfono tiene Pérez?", conDatosPersonales: true, TestContext.Current.CancellationToken);

        Assert.Equal($"{PrefijoDeGuion} CON DATOS PERSONALES", proveedor.Recibidas[0].PrefijoEstable);
    }

    // --------------------------------------------------------- interpretación

    [Fact]
    public void Interpreta_una_respuesta_limpia()
    {
        var generacion = GeneradorDeSql.Interpretar(
            ProveedorGuionado.Generacion("SELECT 1 FROM identity.carreras", "Conté las carreras."));

        Assert.True(generacion.EsContestable);
        Assert.Equal("SELECT 1 FROM identity.carreras", generacion.Sql);
        Assert.Equal("Conté las carreras.", generacion.Razonamiento);
    }

    [Fact]
    public void Interpreta_una_respuesta_envuelta_en_delimitadores_de_codigo()
    {
        var texto =
            "```json\n"
            + ProveedorGuionado.Generacion("SELECT 1 FROM identity.carreras")
            + "\n```";

        var generacion = GeneradorDeSql.Interpretar(texto);

        Assert.True(generacion.EsContestable);
        Assert.Equal("SELECT 1 FROM identity.carreras", generacion.Sql);
    }

    [Fact]
    public void Interpreta_una_respuesta_con_prosa_alrededor()
    {
        var texto =
            "Claro, acá va:\n"
            + ProveedorGuionado.Generacion("SELECT 1 FROM identity.carreras")
            + "\nEspero que sirva.";

        Assert.True(GeneradorDeSql.Interpretar(texto).EsContestable);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("No entendí la pregunta.")]
    [InlineData("{ esto no es json }")]
    [InlineData("{\"es_contestable\": \"quizás\"}")]
    public void Una_respuesta_ininteligible_resuelve_no_contestable(string texto)
    {
        // No se intenta extraer algo que parezca SQL del texto: eso convertiría un
        // fallo de formato en la ejecución de una consulta que nadie declaró como
        // tal, que es justo el caso que el validador existe para evitar.
        var generacion = GeneradorDeSql.Interpretar(texto);

        Assert.False(generacion.EsContestable);
        Assert.Null(generacion.Sql);
        Assert.NotEmpty(generacion.Razonamiento);
    }

    [Fact]
    public void Contestable_sin_consulta_se_trata_como_abstencion()
    {
        // El modelo se contradijo. Seguir con una consulta vacía sería peor que
        // abstenerse.
        var generacion = GeneradorDeSql.Interpretar(
            """{"es_contestable": true, "sql": null, "razonamiento": "Se puede.", "categoria": "x"}""");

        Assert.False(generacion.EsContestable);
    }

    [Fact]
    public void El_razonamiento_sobrevive_a_la_abstencion()
    {
        var generacion = GeneradorDeSql.Interpretar(
            ProveedorGuionado.NoContestable("Eso no está en lo que puedo consultar."));

        Assert.False(generacion.EsContestable);
        Assert.Equal("Eso no está en lo que puedo consultar.", generacion.Razonamiento);
    }

    [Fact]
    public void Una_respuesta_sin_razonamiento_igual_trae_uno()
    {
        // El razonamiento se expone al usuario como transparencia media: dejarlo
        // vacío mostraría un campo hueco en la interfaz.
        var generacion = GeneradorDeSql.Interpretar(
            """{"es_contestable": false, "sql": null, "razonamiento": "", "categoria": "x"}""");

        Assert.NotEmpty(generacion.Razonamiento);
    }

    [Fact]
    public void El_razonamiento_de_una_respuesta_ininteligible_no_habla_de_esquema()
    {
        // Lo lee el usuario final: no puede mencionar formato, JSON, tablas ni SQL.
        var razonamiento = GeneradorDeSql.Interpretar("cualquier cosa").Razonamiento;

        Assert.DoesNotContain("json", razonamiento, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sql", razonamiento, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tabla", razonamiento, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------ apoyo

    private static GeneradorDeSql Componer(ProveedorGuionado proveedor, DateOnly? fecha = null) =>
        new(new EsquemaDeGuion(),
            new SelectorDeEjemplos(),
            proveedor,
            new FechaDeReferenciaFija(fecha ?? Hoy),
            Options.Create(new OpcionesAsistente()));

    /// <summary>Proveedor de esquema fijo, para no necesitar una base.</summary>
    private sealed class EsquemaDeGuion : IProveedorDeEsquema
    {
        public Task<EsquemaParaPrompt> ObtenerAsync(bool conDatosPersonales, CancellationToken ct)
        {
            var prefijo = conDatosPersonales
                ? $"{PrefijoDeGuion} CON DATOS PERSONALES"
                : PrefijoDeGuion;

            return Task.FromResult(new EsquemaParaPrompt(prefijo, $"huella-{conDatosPersonales}"));
        }
    }
}
