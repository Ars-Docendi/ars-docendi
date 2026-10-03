using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica el catálogo cerrado de intenciones: su forma, su validación y la
/// disciplina de que ninguna entre sin caso de prueba.
/// </summary>
/// <remarks>
/// Todo en memoria: el catálogo es un archivo embebido y no toca la base. Lo que sí
/// necesita base es resolver los slots, y eso vive en
/// <see cref="ResolucionDeSlotsTests"/>.
/// </remarks>
public sealed class CatalogoDeIntencionesTests
{
    // ------------------------------------------------------ la forma del catálogo

    [Fact]
    public void El_catalogo_embebido_carga()
    {
        var catalogo = CatalogoDeIntenciones.Cargar();

        Assert.NotEmpty(catalogo.Intenciones);
    }

    [Fact]
    public void Cada_intencion_declara_nombre_terminos_slots_y_destino()
    {
        foreach (var intencion in CatalogoDeIntenciones.Cargar().Intenciones)
        {
            Assert.False(string.IsNullOrWhiteSpace(intencion.Nombre));
            Assert.NotEmpty(intencion.Terminos);
            Assert.NotEmpty(intencion.Slots);
            Assert.False(string.IsNullOrWhiteSpace(intencion.Destino));
        }
    }

    [Fact]
    public void Los_nombres_de_las_intenciones_no_se_repiten()
    {
        var nombres = CatalogoDeIntenciones.Cargar().Intenciones.Select(i => i.Nombre).ToList();

        Assert.Equal(nombres.Count, nombres.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Los_terminos_del_catalogo_ya_estan_normalizados()
    {
        // Se comparan contra la pregunta YA normalizada, así que un término
        // acentuado o en plural no coincidiría nunca con nada. Sin este test el
        // error no rompe: la intención simplemente no se reconoce jamás.
        foreach (var intencion in CatalogoDeIntenciones.Cargar().Intenciones)
        {
            foreach (var termino in intencion.Terminos)
            {
                Assert.Equal([termino], NormalizadorLexico.Terminos(termino));
            }
        }
    }

    // --------------------------------------------------------------- validación

    [Fact]
    public void Una_clase_de_slot_inexistente_no_carga()
    {
        var error = Assert.Throws<CatalogoDeIntencionesInvalido>(() => CatalogoDeIntenciones.Cargar(
            Json("""{"nombre":"x","terminos":["pedido"],"slots":[{"nombre":"s","clase":"Planeta"}],"destino":"d"}""")));

        // El mensaje enumera las clases válidas: quien edita el catálogo está
        // mirando un JSON, no los tipos de .NET.
        Assert.Contains(nameof(ClaseDeSlot.Persona), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_termino_sin_normalizar_no_carga()
    {
        var error = Assert.Throws<CatalogoDeIntencionesInvalido>(() => CatalogoDeIntenciones.Cargar(
            Json("""{"nombre":"acentuada","terminos":["revisión"],"slots":[{"nombre":"s","clase":"Estado"}],"destino":"d"}""")));

        Assert.Contains("acentuada", error.Message, StringComparison.Ordinal);
        Assert.Contains("revisión", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Una_intencion_sin_terminos_no_carga()
    {
        // Reconocería cualquier pregunta: el subconjunto vacío está en todos.
        var error = Assert.Throws<CatalogoDeIntencionesInvalido>(() => CatalogoDeIntenciones.Cargar(
            Json("""{"nombre":"vacia","terminos":[],"slots":[{"nombre":"s","clase":"Estado"}],"destino":"d"}""")));

        Assert.Contains("vacia", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Una_intencion_sin_slots_no_carga()
    {
        var error = Assert.Throws<CatalogoDeIntencionesInvalido>(() => CatalogoDeIntenciones.Cargar(
            Json("""{"nombre":"sinslots","terminos":["pedido"],"slots":[],"destino":"d"}""")));

        Assert.Contains("sinslots", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Una_intencion_sin_destino_no_carga()
    {
        var error = Assert.Throws<CatalogoDeIntencionesInvalido>(() => CatalogoDeIntenciones.Cargar(
            Json("""{"nombre":"sindestino","terminos":["pedido"],"slots":[{"nombre":"s","clase":"Estado"}],"destino":""}""")));

        Assert.Contains("sindestino", error.Message, StringComparison.Ordinal);
    }

    // --------------------------------------------------- términos excluidos

    [Fact]
    public void Un_termino_excluido_impide_el_reconocimiento()
    {
        // La forma que separa el listado del conteo. «¿qué pedidos de Alta hay?» y
        // «¿cuántos pedidos de Alta hay?» comparten todas sus palabras de contenido:
        // lo único que las distingue es la presencia de «cuántos».
        var catalogo = CatalogoDeIntenciones.Cargar(Json(
            """{"nombre":"listado","terminos":["pedido"],"excluye":["cuantos"],"slots":[{"nombre":"n","clase":"Novedad"}],"destino":"d"}"""));

        var intencion = catalogo.Intenciones.Single();

        Assert.True(intencion.Terminos.IsSubsetOf(NormalizadorLexico.Terminos("¿qué pedidos hay?")));
        Assert.False(intencion.Excluye.Overlaps(NormalizadorLexico.Terminos("¿qué pedidos hay?")));

        // La de conteo declara los mismos términos exigidos y además el excluido.
        Assert.True(intencion.Excluye.Overlaps(
            NormalizadorLexico.Terminos("¿cuántos pedidos hay?")));
    }

    [Fact]
    public void Un_termino_excluido_sin_normalizar_no_carga()
    {
        var error = Assert.Throws<CatalogoDeIntencionesInvalido>(() => CatalogoDeIntenciones.Cargar(
            Json("""{"nombre":"malexcluida","terminos":["pedido"],"excluye":["Cuántos"],"slots":[{"nombre":"n","clase":"Novedad"}],"destino":"d"}""")));

        Assert.Contains("malexcluida", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_termino_exigido_y_excluido_a_la_vez_no_carga()
    {
        // Así declarada no podría reconocerse nunca: el término tendría que estar y
        // no estar. Es un error de escritura, y el catálogo lo dice al cargar.
        var error = Assert.Throws<CatalogoDeIntencionesInvalido>(() => CatalogoDeIntenciones.Cargar(
            Json("""{"nombre":"contradictoria","terminos":["pedido"],"excluye":["pedido"],"slots":[{"nombre":"n","clase":"Novedad"}],"destino":"d"}""")));

        Assert.Contains("contradictoria", error.Message, StringComparison.Ordinal);
        Assert.Contains("pedido", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Una_intencion_sin_excluidos_carga_igual()
    {
        // El campo es opcional: la mayoría de las intenciones no lo necesita.
        var catalogo = CatalogoDeIntenciones.Cargar(Json(
            """{"nombre":"simple","terminos":["plantel"],"slots":[{"nombre":"m","clase":"Materia"}],"destino":"d"}"""));

        Assert.Empty(catalogo.Intenciones.Single().Excluye);
    }

    // ----------------------------------------- una intención nueva, un caso nuevo

    [Fact]
    public void Toda_intencion_del_catalogo_tiene_caso_de_prueba()
    {
        // La disciplina que sostiene al catálogo. Crece de a una intención, y cada
        // una amplía la superficie donde un reconocimiento incorrecto manda una
        // pregunta al carril que no la responde. El archivo hace barato el control:
        // el test lo itera y falla nombrando lo que falta.
        var declaradas = CatalogoDeIntenciones.Cargar().Intenciones.Select(i => i.Nombre);

        var sinCaso = declaradas.Except(ResolucionDeSlotsTests.IntencionesConCaso, StringComparer.Ordinal);

        Assert.True(!sinCaso.Any(),
            "Estas intenciones no tienen caso de prueba en ResolucionDeSlotsTests: "
            + string.Join(", ", sinCaso));
    }

    private static string Json(string intencion) => $$"""{"intenciones":[{{intencion}}]}""";
}
