using ArsDocendi.Evaluacion.Nucleo.Dataset;
using ArsDocendi.Evaluacion.Nucleo.Fixture;
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
        //
        // Los marcadores declarados del ítem viajan como "declarados" y
        // "requeridos" — mismo criterio que `CarrilSql`: una referencia de D11
        // que la consulta de referencia no use sería un ítem mal escrito.
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

    [Fact]
    public void Cada_motivo_de_rechazo_esta_representado_en_el_dataset()
    {
        var representados = Dataset.Items
            .SelectMany(item => item.MotivosAceptables ?? [])
            .ToHashSet();

        foreach (var motivo in Enum.GetValues<MotivoDeRechazo>())
        {
            Assert.Contains(MotivosDeRechazo.ValorDeCable(motivo), representados);
        }
    }

    [Fact]
    public void La_palabra_python_espera_muy_general()
    {
        var item = Dataset.Items.Single(i => i.Pregunta == "python");

        Assert.Equal(["muy_general"], item.MotivosAceptables);
    }

    // ------------------------------------------------------ menciones (D10/D11)

    [Fact]
    public void La_mencion_de_materia_nombra_una_de_las_compartidas_del_fixture()
    {
        // Homónimo real: "Algoritmos y Estructuras de Datos" es una de las tres
        // entradas de `MateriasCompartidas`, así que hay más de una fila con ese
        // nombre — exactamente el caso que D10 dice que un id exacto por fila
        // desambigua.
        var nombresCompartidos = GeneradorDeFixture.MateriasCompartidas.Select(m => m.Nombre).ToHashSet();

        var referencias = Dataset.Items
            .SelectMany(item => item.Referencias ?? [])
            .Where(r => r.Tipo == "materia")
            .ToArray();

        Assert.NotEmpty(referencias);
        Assert.All(referencias, r => Assert.Contains(r.Nombre, nombresCompartidos));
    }

    [Fact]
    public void La_mencion_de_docente_nombra_un_apellido_compartido_del_fixture()
    {
        var apellidosCompartidos = GeneradorDeFixture.ApellidosCompartidos.Select(a => a.Apellido).ToHashSet();

        var referencias = Dataset.Items
            .SelectMany(item => item.Referencias ?? [])
            .Where(r => r.Tipo == "docente")
            .ToArray();

        Assert.NotEmpty(referencias);
        Assert.All(
            referencias,
            r => Assert.Contains(apellidosCompartidos, apellido => r.Nombre.Contains(apellido, StringComparison.Ordinal)));
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
