using ArsDocendi.Evaluacion.Nucleo.Dataset;
using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Evaluacion;

/// <summary>
/// Verifica el dataset del eje de capacidad.
/// </summary>
/// <remarks>
/// El dataset es el instrumento de medición. Un instrumento mal calibrado no da un
/// error: da un número, y ese número se usa para tomar decisiones. Por eso hay más
/// tests sobre el dataset que sobre casi cualquier otra cosa del evaluador.
/// </remarks>
public sealed class DatasetDeCapacidadTests
{
    private static readonly DatasetDeCapacidad Dataset = Cargar();

    [Fact]
    public void El_dataset_tiene_items()
    {
        Assert.NotEmpty(Dataset.Items);
    }

    [Fact]
    public void Cada_item_declara_una_categoria_de_la_lista_cerrada()
    {
        var invalidos = Dataset.Items
            .Where(item => !CategoriaDeItem.Todas.Contains(item.Categoria))
            .Select(item => $"{item.Id}: '{item.Categoria}'")
            .ToArray();

        Assert.Empty(invalidos);
    }

    [Fact]
    public void Cada_item_declara_un_actor_conocido()
    {
        var invalidos = Dataset.Items
            .Where(item => !ActorDeItem.Todos.Contains(item.Actor))
            .Select(item => $"{item.Id}: '{item.Actor}'")
            .ToArray();

        Assert.Empty(invalidos);
    }

    [Fact]
    public void Las_seis_categorias_estan_representadas()
    {
        // Estratificado de verdad: sin ítems de una categoría, el número esconde
        // que el asistente acierta lo fácil y falla lo que importa.
        var faltantes = CategoriaDeItem.Todas
            .Except(Dataset.Items.Select(item => item.Categoria), StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(faltantes);
    }

    [Fact]
    public void Los_identificadores_no_se_repiten()
    {
        // Son el lock del gate de regresión: repetidos, dos ítems distintos
        // compartirían su historial.
        var repetidos = Dataset.Items
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .Where(grupo => grupo.Count() > 1)
            .Select(grupo => grupo.Key)
            .ToArray();

        Assert.Empty(repetidos);
    }

    [Fact]
    public void Los_items_infactibles_no_traen_consulta_de_referencia()
    {
        var invalidos = Dataset.Items
            .Where(item => item.EsInfactible && item.SqlReferencia is not null)
            .Select(item => item.Id)
            .ToArray();

        Assert.Empty(invalidos);
    }

    [Fact]
    public void Los_items_factibles_traen_consulta_de_referencia()
    {
        var invalidos = Dataset.Items
            .Where(item => !item.EsInfactible && string.IsNullOrWhiteSpace(item.SqlReferencia))
            .Select(item => item.Id)
            .ToArray();

        Assert.Empty(invalidos);
    }

    [Fact]
    public void Toda_consulta_de_referencia_pasa_el_validador()
    {
        // Una referencia que el propio validador rechazaría nunca podría empatar
        // con una respuesta del asistente: el ítem sería imposible de acertar.
        var rechazadas = Dataset.Items
            .Where(item => item.SqlReferencia is not null)
            .Select(item => (item.Id, Veredicto: ValidadorDeSql.Validar(item.SqlReferencia!)))
            .Where(par => !par.Veredicto.EsValida)
            .Select(par => $"{par.Id} — {par.Veredicto.Motivo}")
            .ToArray();

        Assert.Empty(rechazadas);
    }

    [Fact]
    public void Ninguna_consulta_de_referencia_usa_el_reloj()
    {
        // Ya lo cubre el validador, pero se afirma aparte porque es el requisito
        // del ticket: un dataset con reloj mide qué día lo corriste.
        string[] reloj =
        [
            "now(", "current_date", "current_timestamp", "localtime",
            "localtimestamp", "statement_timestamp", "clock_timestamp",
            "transaction_timestamp",
        ];

        var infractoras = Dataset.Items
            .Where(item => item.SqlReferencia is not null)
            .Where(item => reloj.Any(funcion =>
                item.SqlReferencia!.Contains(funcion, StringComparison.OrdinalIgnoreCase)))
            .Select(item => item.Id)
            .ToArray();

        Assert.Empty(infractoras);
    }

    [Fact]
    public void Hay_items_de_alcance_acotado()
    {
        // La distinción entre «no hay» y «no podés verlo» solo se mide con actores
        // que no ven todo. Con un dataset puramente global, el caso central de la
        // política de abstención no se ejercita nunca.
        Assert.Contains(Dataset.Items, item => item.Actor != ActorDeItem.Global);
    }

    [Fact]
    public void Hay_al_menos_un_item_que_declara_el_orden()
    {
        // Si ninguno lo declarara, la rama de comparación ordenada del comparador
        // nunca se ejercitaría en una corrida real.
        Assert.Contains(Dataset.Items, item => item.OrdenImporta);
    }

    // ------------------------------------------------------------ disjunción

    [Fact]
    public void El_dataset_es_disjunto_del_catalogo_de_ejemplos()
    {
        // ESTA ES LA TAREA que el cambio del carril dejó abierta a propósito: no se
        // podía verificar contra un archivo que no existía.
        //
        // Si se solaparan, la métrica mediría cuán bien el sistema reproduce
        // ejemplos que ya vio. Y como el catálogo de capacidades deriva sus
        // sugerencias de esos ejemplos, el asistente estaría proponiendo las
        // preguntas con las que se lo evalúa.
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

    [Fact]
    public void El_test_de_disjuncion_no_es_vacio()
    {
        // Anti-vacuidad: si el normalizador devolviera siempre el conjunto vacío,
        // los dos tests de arriba pasarían sin comparar nada.
        var terminos = Normalizar("¿Qué docentes están designados en Bases de Datos?");

        Assert.NotEmpty(terminos);
        Assert.Contains("docente", terminos);
        Assert.Contains("designacion", terminos);
    }

    // ----------------------------------------------------------------- huella

    [Fact]
    public void La_huella_es_estable()
    {
        Assert.Equal(Cargar().Huella, Cargar().Huella);
    }

    [Fact]
    public void Un_dataset_distinto_tiene_otra_huella()
    {
        var otro = DatasetDeCapacidad.Interpretar(
            """
            {"items":[{"id":"x-1","pregunta":"¿Qué?","categoria":"consulta_simple",
                       "actor":"global","sql_referencia":"SELECT 1"}]}
            """);

        Assert.NotEqual(Dataset.Huella, otro.Huella);
    }

    [Fact]
    public void Los_conteos_por_categoria_suman_el_total()
    {
        Assert.Equal(Dataset.Items.Count, Dataset.ConteoPorCategoria().Values.Sum());
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

    // ------------------------------------------------------------------ apoyo

    /// <summary>Carga el dataset versionado del repositorio.</summary>
    public static DatasetDeCapacidad Cargar() =>
        DatasetDeCapacidad.Cargar(RutaDelDataset());

    /// <summary>Ruta del dataset versionado.</summary>
    public static string RutaDelDataset() =>
        Path.Combine(RaizRepositorio.Ruta(), "backend", "eval", "datasets", "capacidad.json");

    /// <summary>
    /// Normaliza una pregunta con el mismo criterio con que el selector de ejemplos
    /// decide qué se parece a qué.
    /// </summary>
    /// <remarks>
    /// Se usa el normalizador del módulo y no uno propio a propósito: si acá
    /// hubiera una copia, la disjunción se verificaría contra un criterio distinto
    /// del que el selector aplica en producción, y dos preguntas que el selector
    /// considera la misma pasarían el test.
    /// </remarks>
    private static IReadOnlySet<string> Normalizar(string pregunta) =>
        NormalizadorLexico.Terminos(pregunta);
}
