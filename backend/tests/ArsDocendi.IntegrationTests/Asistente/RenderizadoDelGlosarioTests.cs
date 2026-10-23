using ArsDocendi.IntegrationTests.Infraestructura;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El bloque «GLOSARIO INSTITUCIONAL» del prefijo: qué dice, dónde va y que no
/// cambia nada cuando la opción está apagada (asistente-glosario-institucional, D6).
/// </summary>
public sealed class RenderizadoDelGlosarioTests
{
    private static readonly ColumnaLegible[] Columnas =
    [
        new("designaciones", "pedidos", "id", "uuid", true, "Pedidos.", "Identificador del pedido."),
        new("designaciones", "pedidos", "estado", "text", true, "Pedidos.", "Etapa del circuito."),
    ];

    private static readonly ReferenciaLegible[] Referencias = [];

    private static readonly VocabularioDeUnaColumna[] Vocabularios =
        [new("designaciones", "cargos", "codigo", ["titular", "adjunto"])];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ------------------------------------------------------------- el bloque

    [Fact]
    public void El_bloque_es_identico_entre_dos_llamadas_y_entre_dos_cargas()
    {
        var uno = RenderizadorDeGlosario.Renderizar(CatalogoDeGlosario.Vigente.Terminos);
        var otro = RenderizadorDeGlosario.Renderizar(CatalogoDeGlosario.Vigente.Terminos);
        var recargado = RenderizadorDeGlosario.Renderizar(CatalogoDeGlosario.Cargar().Terminos);

        Assert.Equal(uno, otro);
        Assert.Equal(uno, recargado);
    }

    [Fact]
    public void El_bloque_sale_de_los_campos_y_no_de_los_bytes_del_archivo()
    {
        // Un archivo con otro formato —saltos de línea de Windows, sin sangría— tiene
        // que dar el MISMO prefijo: si no, reformatear el JSON invalidaría la caché.
        var json = """
            { "version": 1, "terminos": [ { "termino": "en Decanato", "sinonimos": [],
              "explicacion": "pedido en revisión del decanato",
              "referencias": [ { "columna": "designaciones.pedidos.estado", "valores": ["en_revision_decanato"] } ] } ] }
            """;
        var compacto = JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(json));
        var crlf = json.Replace("\n", "\r\n", StringComparison.Ordinal);

        Assert.Equal(
            RenderizadorDeGlosario.Renderizar(CatalogoDeGlosario.Interpretar(json).Terminos),
            RenderizadorDeGlosario.Renderizar(CatalogoDeGlosario.Interpretar(compacto).Terminos));
        Assert.Equal(
            RenderizadorDeGlosario.Renderizar(CatalogoDeGlosario.Interpretar(json).Terminos),
            RenderizadorDeGlosario.Renderizar(CatalogoDeGlosario.Interpretar(crlf).Terminos));
    }

    [Fact]
    public void Hay_una_linea_por_termino_y_en_el_orden_del_archivo()
    {
        var terminos = CatalogoDeGlosario.Vigente.Terminos;

        var lineas = RenderizadorDeGlosario.Renderizar(terminos)
            .Split('\n')
            .Where(l => l.StartsWith("- «", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(terminos.Count, lineas.Length);
        for (var i = 0; i < terminos.Count; i++)
        {
            Assert.StartsWith($"- «{terminos[i].Termino}»", lineas[i], StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Cada_linea_nombra_sus_columnas_y_sus_valores_guardados()
    {
        var bloque = RenderizadorDeGlosario.Renderizar(CatalogoDeGlosario.Vigente.Terminos);

        Assert.StartsWith("\nGLOSARIO INSTITUCIONAL\n\n", bloque, StringComparison.Ordinal);
        Assert.Contains("designaciones.pedidos.estado = 'en_revision_decanato'", bloque, StringComparison.Ordinal);
        Assert.Contains(
            "designaciones.pedidos.estado IN ('en_lote', 'rechazado', 'cancelado')", bloque, StringComparison.Ordinal);
        // La conjunción de «en Cátedra»: las dos igualdades en la misma línea.
        Assert.Contains(
            "designaciones.pedidos.estado = 'devuelto' AND designaciones.pedidos.propietario_actual = 'jefe_catedra'",
            bloque, StringComparison.Ordinal);
        Assert.Contains("designaciones.pedidos.novedad = 'Cambio de cargo o dedicación'", bloque, StringComparison.Ordinal);
        // Lo que no es una conjunción de igualdades lleva su pista.
        Assert.Contains(
            "SQL: COALESCE(horas,0) + COALESCE(horas_investigacion,0) + COALESCE(horas_externas,0)",
            bloque, StringComparison.Ordinal);
    }

    [Fact]
    public void Ninguna_linea_prescribe_ILIKE_para_una_columna_codificada()
    {
        // D7: los valores del glosario son códigos y cadenas exactas de catálogo, y la
        // regla 8 del prompt (`unaccent … ILIKE`) es para texto libre.
        var bloque = RenderizadorDeGlosario.Renderizar(CatalogoDeGlosario.Vigente.Terminos);

        Assert.DoesNotContain("ILIKE", bloque, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("valor exacto", bloque, StringComparison.Ordinal);
    }

    // ------------------------------------------------------- dónde se inserta

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void El_bloque_va_despues_del_esquema_y_el_resto_queda_como_estaba(bool compacto)
    {
        var bloque = RenderizadorDeGlosario.Renderizar(CatalogoDeGlosario.Vigente.Terminos);

        var sin = RenderizadorDeEsquema.Renderizar(Columnas, Referencias, Vocabularios, compacto);
        var con = RenderizadorDeEsquema.Renderizar(Columnas, Referencias, Vocabularios, compacto, bloque);

        // Lo que ya existía no se mueve ni se toca: el bloque sólo se agrega al final.
        Assert.Equal(sin + bloque, con);

        var orden = new[]
            {
                InstruccionesDeGeneracion.Instrucciones[..30],
                "VALORES POSIBLES",
                compacto ? "ESQUEMA DISPONIBLE" : "ESQUEMA",
                "## designaciones.pedidos",
                "GLOSARIO INSTITUCIONAL",
            }
            .Select(marca => con.IndexOf(marca, StringComparison.Ordinal))
            .ToArray();

        Assert.All(orden, posicion => Assert.True(posicion >= 0));
        Assert.Equal(orden.OrderBy(p => p), orden);
    }

    [Fact]
    public void Sin_bloque_el_esquema_es_byte_a_byte_el_de_siempre()
    {
        foreach (var compacto in new[] { false, true })
        {
            Assert.Equal(
                RenderizadorDeEsquema.Renderizar(Columnas, Referencias, Vocabularios, compacto),
                RenderizadorDeEsquema.Renderizar(Columnas, Referencias, Vocabularios, compacto, bloqueDeGlosario: null));
        }
    }

    [Fact]
    public async Task Los_ejemplos_van_despues_del_glosario_y_son_el_mismo_texto_con_o_sin_el()
    {
        var bloque = RenderizadorDeGlosario.Renderizar(CatalogoDeGlosario.Vigente.Terminos);
        var sinGlosario = RenderizadorDeEsquema.Renderizar(Columnas, Referencias, Vocabularios, compacto: true);
        var conGlosario = RenderizadorDeEsquema.Renderizar(Columnas, Referencias, Vocabularios, compacto: true, bloque);

        var ejemplos = GeneradorDeSql.ArmarBloqueDeEjemplos(new SelectorDeEjemplos().Catalogo);

        var prefijoSin = await PrefijoEstableAsync(sinGlosario);
        var prefijoCon = await PrefijoEstableAsync(conGlosario);

        // Con la opción apagada el prefijo termina en el bloque de ejemplos de siempre…
        Assert.Equal(sinGlosario + ejemplos, prefijoSin);
        // …y con ella prendida el bloque de ejemplos es el MISMO texto, empezando
        // justo después de la última línea del glosario.
        Assert.Equal(conGlosario + ejemplos, prefijoCon);
        Assert.True(
            prefijoCon.IndexOf("EJEMPLOS VERIFICADOS", StringComparison.Ordinal)
            > prefijoCon.LastIndexOf("\n- «", StringComparison.Ordinal));
    }

    private static async Task<string> PrefijoEstableAsync(string esquema)
    {
        var modelo = new ProveedorGuionado(ProveedorGuionado.Generacion("SELECT 1"));
        var generador = new GeneradorDeSql(
            new EsquemaFijo(esquema),
            new SelectorDeEjemplos(),
            modelo,
            new FechaDeReferenciaFija(new DateOnly(2026, 10, 3)),
            Options.Create(new OpcionesAsistente { EjemplosEnElPrefijo = true }),
            NullLogger<GeneradorDeSql>.Instance);

        await generador.GenerarAsync("¿cuántos pedidos hay?", false, Ct);

        return modelo.Recibidas[0].PrefijoEstable;
    }

    private sealed class EsquemaFijo(string prefijo) : IProveedorDeEsquema
    {
        public Task<EsquemaParaPrompt> ObtenerAsync(bool conDatosPersonales, CancellationToken ct) =>
            Task.FromResult(new EsquemaParaPrompt(prefijo, ProveedorDeEsquema.Huella(prefijo)));
    }
}
