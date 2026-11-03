using ArsDocendi.Evaluacion.Nucleo.Dataset;
using ArsDocendi.Evaluacion.Nucleo.Fixture;
using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Evaluacion;

/// <summary>Verifica el dataset del eje de capacidad.</summary>
public sealed class DatasetDeCapacidadTests
{
    private static readonly DatasetDeCapacidad Dataset = Cargar();

    [Fact]
    public void Toda_consulta_de_referencia_pasa_el_validador()
    {
        // Una referencia que el propio validador rechazaría nunca podría empatar con una respuesta
        // del asistente: el ítem sería imposible de acertar.
        var rechazadas = Dataset.Items
            .Where(item => item.SqlReferencia is not null)
            .Select(item =>
            {
                var marcadores = item.Referencias?.Select(r => r.Marcador).ToHashSet(StringComparer.Ordinal);
                return (item.Id, Veredicto: ValidadorDeSql.Validar(item.SqlReferencia!, marcadores, marcadores));
            })
            .Where(par => !par.Veredicto.EsValida)
            .Select(par => $"{par.Id} — {par.Veredicto.Motivo}")
            .ToArray();

        Assert.Empty(rechazadas);
    }

    // ------------------------------------------------------------ disjunción

    [Fact]
    public void El_dataset_es_disjunto_del_catalogo_de_ejemplos()
    {
        // ESTA ES LA TAREA que el cambio del carril dejó abierta a propósito: no se podía verificar
        // contra un archivo que no existía.
        var delCatalogo = new SelectorDeEjemplos().Catalogo
            .ToDictionary(
                ejemplo => Normalizar(ejemplo.Pregunta),
                ejemplo => ejemplo.Pregunta);

        var colisiones = Dataset.Items
            .Where(item => delCatalogo.ContainsKey(Normalizar(item.Pregunta)))
            .Select(item => $"{item.Id}: «{item.Pregunta}» ≡ «{delCatalogo[Normalizar(item.Pregunta)]}»")
            .ToArray();

        Assert.Empty(colisiones);
    }

    [Fact]
    public void Ninguna_pregunta_del_dataset_es_un_subconjunto_de_una_del_catalogo()
    {
        // Más estricto que la igualdad, y a propósito. Dos preguntas donde una es
        // una versión más específica de la otra son la misma pregunta para lo que
        // acá importa: el modelo ya vio cómo se resuelve.
        var delCatalogo = new SelectorDeEjemplos().Catalogo
            .Select(ejemplo => (ejemplo.Pregunta, Terminos: Normalizar(ejemplo.Pregunta)))
            .ToArray();

        var sospechosas = Dataset.Items
            .Select(item => (item.Id, item.Pregunta, Terminos: Normalizar(item.Pregunta)))
            .SelectMany(item => delCatalogo
                .Where(ejemplo => ejemplo.Terminos.Count > 0
                                  && item.Terminos.Count > 0
                                  && (item.Terminos.IsSubsetOf(ejemplo.Terminos)
                                      || ejemplo.Terminos.IsSubsetOf(item.Terminos)))
                .Select(ejemplo => $"{item.Id}: «{item.Pregunta}» ⊂ «{ejemplo.Pregunta}»"))
            .ToArray();

        Assert.Empty(sospechosas);
    }

    // ------------------------------------------------------------ validación

    [Fact]
    public void Un_dataset_vacio_se_rechaza()
    {
        Assert.Throws<InvalidOperationException>(
            () => DatasetDeCapacidad.Interpretar("""{"items":[]}"""));
    }

    [Fact]
    public void Un_dataset_con_identificadores_repetidos_se_rechaza()
    {
        var excepcion = Assert.Throws<InvalidOperationException>(
            () => DatasetDeCapacidad.Interpretar(
                """
                {"items":[
                  {"id":"x-1","pregunta":"a","categoria":"consulta_simple","actor":"global","sql_referencia":"SELECT 1"},
                  {"id":"x-1","pregunta":"b","categoria":"consulta_simple","actor":"global","sql_referencia":"SELECT 2"}]}
                """));

        Assert.Contains("x-1", excepcion.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("consulta_simpel", "global", "categoría")]
    [InlineData("consulta_simple", "rector", "actor")]
    public void Un_item_fuera_de_las_listas_cerradas_se_rechaza(string categoria, string actor, string campo)
    {
        var excepcion = Assert.Throws<InvalidOperationException>(
            () => DatasetDeCapacidad.Interpretar(
                $$"""
                {"items":[{"id":"x-1","pregunta":"a","categoria":"{{categoria}}","actor":"{{actor}}","sql_referencia":"SELECT 1"}]}
                """));

        Assert.Contains("x-1", excepcion.Message, StringComparison.Ordinal);
        Assert.Contains(campo, excepcion.Message, StringComparison.Ordinal);
    }

    // ---------------------- motivos_aceptables (asistente-rechazos-dinamicos, D10)

    [Fact]
    public void Un_item_no_contestable_puede_declarar_motivos_aceptables()
    {
        var dataset = DatasetDeCapacidad.Interpretar(
            """
            {"items":[{"id":"x-1","pregunta":"a","categoria":"no_contestable","actor":"global",
                       "motivos_aceptables":["muy_general","no_cubierto"]}]}
            """);

        Assert.Equal(
            ["muy_general", "no_cubierto"], dataset.Items[0].MotivosAceptables);
    }

    [Fact]
    public void Un_item_no_contestable_sin_motivos_aceptables_sigue_cargando()
    {
        // Opcional en la unidad A (tarea 8.1): el dataset de hoy todavía no lo
        // declara en ningún ítem, y tiene que seguir cargando sin tocarlo.
        var dataset = DatasetDeCapacidad.Interpretar(
            """{"items":[{"id":"x-1","pregunta":"a","categoria":"no_contestable","actor":"global"}]}""");

        Assert.Null(dataset.Items[0].MotivosAceptables);
    }

    [Fact]
    public void Un_item_no_contestable_con_motivos_aceptables_vacio_se_rechaza()
    {
        var excepcion = Assert.Throws<InvalidOperationException>(() => DatasetDeCapacidad.Interpretar(
            """
            {"items":[{"id":"x-1","pregunta":"a","categoria":"no_contestable","actor":"global",
                       "motivos_aceptables":[]}]}
            """));

        Assert.Contains("x-1", excepcion.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_item_no_contestable_con_un_motivo_fuera_del_conjunto_se_rechaza()
    {
        var excepcion = Assert.Throws<InvalidOperationException>(() => DatasetDeCapacidad.Interpretar(
            """
            {"items":[{"id":"x-1","pregunta":"a","categoria":"no_contestable","actor":"global",
                       "motivos_aceptables":["clima"]}]}
            """));

        Assert.Contains("clima", excepcion.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_item_factible_no_puede_declarar_motivos_aceptables()
    {
        var excepcion = Assert.Throws<InvalidOperationException>(() => DatasetDeCapacidad.Interpretar(
            """
            {"items":[{"id":"x-1","pregunta":"a","categoria":"consulta_simple","actor":"global",
                       "sql_referencia":"SELECT 1","motivos_aceptables":["no_cubierto"]}]}
            """));

        Assert.Contains("x-1", excepcion.Message, StringComparison.Ordinal);
    }

    // ------------------------------------------------------ menciones (D10/D11)

    // ------------------------------------------------------------------ apoyo

    /// <summary>Carga el dataset versionado del repositorio.</summary>
    public static DatasetDeCapacidad Cargar() =>
        DatasetDeCapacidad.Cargar(RutaDelDataset());

    /// <summary>Ruta del dataset versionado.</summary>
    public static string RutaDelDataset() =>
        Path.Combine(RaizRepositorio.Ruta(), "backend", "eval", "datasets", "capacidad.json");

    /// <summary>
    /// Normaliza una pregunta con el mismo criterio con que el selector de ejemplos decide qué se parece a qué.
    /// </summary>
    private static IReadOnlySet<string> Normalizar(string pregunta) =>
        NormalizadorLexico.Terminos(pregunta);
}
