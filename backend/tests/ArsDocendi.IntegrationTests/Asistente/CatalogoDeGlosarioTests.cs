using System.Text.Json;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El cargador del glosario institucional, sin base de datos
/// (asistente-glosario-institucional, D3 y D4 regla 1).
/// </summary>
/// <remarks>
/// Cada caso roto vive ACÁ y nunca en <c>glosario.json</c>: un archivo real con una
/// entrada rota haría fallar el arranque con la opción prendida, y un test que la
/// necesita para probar el detector no prueba nada si alguien la «arregla».
/// </remarks>
public sealed class CatalogoDeGlosarioTests
{
    private const string Estado = "designaciones.pedidos.estado";

    // ------------------------------------------------------------ el archivo

    [Fact]
    public void El_recurso_embebido_se_encuentra_por_nombre()
    {
        Assert.Equal("Modules.Asistente.Recursos.glosario.json", CatalogoDeGlosario.RecursoCatalogo);
        Assert.Contains(
            CatalogoDeGlosario.RecursoCatalogo,
            typeof(CatalogoDeGlosario).Assembly.GetManifestResourceNames());
    }

    [Fact]
    public void El_archivo_real_carga_con_los_veinticuatro_terminos_decididos()
    {
        var catalogo = CatalogoDeGlosario.Cargar();

        Assert.Equal(24, catalogo.Terminos.Count);
        Assert.All(catalogo.Terminos, t => Assert.NotEmpty(t.Referencias));
    }

    [Fact]
    public void El_archivo_real_se_lee_una_sola_vez()
    {
        Assert.Same(CatalogoDeGlosario.Vigente, CatalogoDeGlosario.Vigente);
    }

    [Fact]
    public void Aprobado_es_en_lote_y_no_existe_un_estado_aprobado()
    {
        var referencia = Assert.Single(Termino("aprobado").Referencias);

        Assert.Equal(Estado, referencia.Columna);
        Assert.Equal(["en_lote"], referencia.Valores);
    }

    [Fact]
    public void Finalizado_son_tres_estados_guardados()
    {
        var referencia = Assert.Single(Termino("finalizado").Referencias);

        Assert.Equal(Estado, referencia.Columna);
        Assert.Equal(["en_lote", "rechazado", "cancelado"], referencia.Valores);
    }

    [Fact]
    public void La_carga_horaria_total_suma_las_tres_columnas_de_horas()
    {
        var total = Termino("carga horaria total");

        Assert.Equal(
            [
                "designaciones.designaciones.horas",
                "designaciones.designaciones.horas_investigacion",
                "designaciones.designaciones.horas_externas",
            ],
            total.Referencias.Select(r => r.Columna));
        Assert.NotNull(total.Sql);
        Assert.Contains("COALESCE", total.Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_termino_sin_columna_no_esta_en_el_archivo()
    {
        // Decisión D1 del product owner: sin dato, sin término. Ni como término ni
        // como sinónimo de otro.
        string[] sinDato =
            ["interino", "suplente", "carácter", "exclusiva", "semiexclusiva", "concurso", "licencia"];

        var nombres = CatalogoDeGlosario.Vigente.Terminos
            .SelectMany(t => t.Sinonimos.Append(t.Termino))
            .Select(Normalizar)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(sinDato, palabra => Assert.DoesNotContain(Normalizar(palabra), nombres));
    }

    [Fact]
    public void Ningun_termino_nombra_los_schemas_de_tareas_ni_de_aulas()
    {
        var esquemas = CatalogoDeGlosario.Vigente.Terminos
            .SelectMany(t => t.Referencias)
            .SelectMany(r => new[] { r.Esquema, r.VerificaContra?.Split('.')[0] })
            .Where(e => e is not null)
            .ToHashSet();

        Assert.DoesNotContain("tareas", esquemas);
        Assert.DoesNotContain("aulas", esquemas);
    }

    [Fact]
    public void Las_palabras_ambiguas_sueltas_no_son_un_termino()
    {
        // D8: «estado» y «tipo» solos chocan con las tareas, los proyectos y los
        // adjuntos. La única forma admitida es «tipo de pedido», que dice el sentido.
        var nombres = CatalogoDeGlosario.Vigente.Terminos
            .SelectMany(t => t.Sinonimos.Append(t.Termino))
            .Select(Normalizar)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("estado", nombres);
        Assert.DoesNotContain("tipo", nombres);
    }

    // ----------------------------------------------------- estructura (regla 1)

    [Fact]
    public void Un_archivo_bien_formado_carga()
    {
        var catalogo = CatalogoDeGlosario.Interpretar(Archivo(Entrada("en Decanato", Estado, "en_revision_decanato")));

        var termino = Assert.Single(catalogo.Terminos);
        Assert.Equal("en Decanato", termino.Termino);
        Assert.Equal("designaciones", termino.Referencias[0].Esquema);
        Assert.Equal("pedidos", termino.Referencias[0].Tabla);
        Assert.Equal("estado", termino.Referencias[0].NombreDeColumna);
    }

    [Fact]
    public void Una_entrada_sin_referencias_se_rechaza_nombrando_el_termino()
    {
        var json = Archivo("""{ "termino": "huérfano", "explicacion": "Sin columna.", "referencias": [] }""");

        var error = Assert.Throws<InvalidOperationException>(() => CatalogoDeGlosario.Interpretar(json));

        Assert.Contains("huérfano", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_termino_repetido_se_rechaza_sin_mirar_mayusculas_ni_acentos()
    {
        var json = Archivo(
            Entrada("en Cátedra", Estado, "devuelto"),
            Entrada("EN CATEDRA", Estado, "devuelto"));

        var error = Assert.Throws<InvalidOperationException>(() => CatalogoDeGlosario.Interpretar(json));

        Assert.Contains("CATEDRA", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "termino": "sin explicación", "referencias": [{ "columna": "designaciones.pedidos.estado" }] }""", "sin explicación")]
    [InlineData("""{ "termino": "dos líneas", "explicacion": "Una.\nDos.", "referencias": [{ "columna": "designaciones.pedidos.estado" }] }""", "dos líneas")]
    [InlineData("""{ "termino": "sin esquema", "explicacion": "x", "referencias": [{ "columna": "pedidos.estado" }] }""", "sin esquema")]
    [InlineData("""{ "termino": "con mayúsculas", "explicacion": "x", "referencias": [{ "columna": "Designaciones.Pedidos.Estado" }] }""", "con mayúsculas")]
    [InlineData("""{ "termino": "valor vacío", "explicacion": "x", "referencias": [{ "columna": "designaciones.pedidos.estado", "valores": [""] }] }""", "valor vacío")]
    [InlineData("""{ "termino": "fuente sin valores", "explicacion": "x", "referencias": [{ "columna": "designaciones.pedidos.propietario_actual", "verificaContra": "identity.roles.code" }] }""", "fuente sin valores")]
    [InlineData("""{ "termino": "fuente mal escrita", "explicacion": "x", "referencias": [{ "columna": "designaciones.pedidos.propietario_actual", "valores": ["a"], "verificaContra": "roles.code" }] }""", "fuente mal escrita")]
    public void Una_entrada_mal_formada_se_rechaza_nombrando_el_termino(string entrada, string termino)
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => CatalogoDeGlosario.Interpretar(Archivo(entrada)));

        Assert.Contains(termino, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_archivo_que_no_es_json_falla_con_un_mensaje_propio()
    {
        Assert.Throws<InvalidOperationException>(() => CatalogoDeGlosario.Interpretar("no es json"));
    }

    [Fact]
    public void Un_archivo_sin_terminos_se_rechaza()
    {
        Assert.Throws<InvalidOperationException>(() => CatalogoDeGlosario.Interpretar(Archivo()));
    }

    // ---------------------------------------------------- cierre de la pista SQL

    [Fact]
    public void Una_pista_cuyas_columnas_y_literales_son_del_termino_carga()
    {
        var catalogo = CatalogoDeGlosario.Interpretar(Archivo(
            Entrada("pendiente", Estado, "en_revision_decanato", sql: $"estado IN ('en_revision_decanato') AND estado IS NOT NULL")));

        Assert.NotNull(Assert.Single(catalogo.Terminos).Sql);
    }

    [Fact]
    public void Una_pista_con_una_columna_que_ninguna_referencia_nombra_se_rechaza()
    {
        var json = Archivo(Entrada("ajena", Estado, "devuelto", sql: "propietario_actual = 'devuelto'"));

        var error = Assert.Throws<InvalidOperationException>(() => CatalogoDeGlosario.Interpretar(json));

        Assert.Contains("ajena", error.Message, StringComparison.Ordinal);
        Assert.Contains("propietario_actual", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Una_pista_con_un_literal_que_ninguna_referencia_tiene_se_rechaza()
    {
        var json = Archivo(Entrada("colada", Estado, "devuelto", sql: "estado = 'aprobado'"));

        var error = Assert.Throws<InvalidOperationException>(() => CatalogoDeGlosario.Interpretar(json));

        Assert.Contains("colada", error.Message, StringComparison.Ordinal);
        Assert.Contains("aprobado", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Una_pista_con_una_funcion_fuera_de_la_lista_se_rechaza()
    {
        var json = Archivo(Entrada("peligrosa", Estado, "devuelto", sql: "pg_sleep(10)"));

        var error = Assert.Throws<InvalidOperationException>(() => CatalogoDeGlosario.Interpretar(json));

        Assert.Contains("pg_sleep", error.Message, StringComparison.Ordinal);
    }

    // ----------------------------------------------------------------- apoyo

    private static TerminoDeGlosario Termino(string nombre) =>
        CatalogoDeGlosario.Vigente.Terminos.Single(t => Normalizar(t.Termino) == Normalizar(nombre));

    private static string Normalizar(string texto) =>
        Modules.Asistente.Application.NormalizadorLexico.SinAcentos(texto).ToLowerInvariant();

    private static string Archivo(params string[] entradas) =>
        $$"""{ "version": 1, "descripcion": "de prueba", "terminos": [ {{string.Join(",", entradas)}} ] }""";

    private static string Entrada(string termino, string columna, string valor, string? sql = null) =>
        JsonSerializer.Serialize(new
        {
            termino,
            explicacion = "Explicación de prueba.",
            referencias = new[] { new { columna, valores = new[] { valor } } },
            sql,
        });
}
