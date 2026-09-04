using ArsDocendi.Evaluacion.Nucleo.Dataset;
using ArsDocendi.Evaluacion.Nucleo.Puntuacion;
using ArsDocendi.Evaluacion.Nucleo.Runner;
using ArsDocendi.IntegrationTests.Infraestructura;

namespace ArsDocendi.IntegrationTests.Evaluacion;

/// <summary>
/// Verifica los datasets de los tres ejes nuevos y el gate de regresión.
/// </summary>
/// <remarks>
/// Todo acá es carga de archivos y aritmética sobre resultados ya obtenidos: no hace
/// falta base ni proveedor. Ése es justamente el motivo por el que estas piezas viven
/// en el núcleo, que sí está en la solución — son donde un error hace que el número
/// mienta.
/// </remarks>
public sealed class EjesDeEvaluacionTests
{
    private static readonly SelloDeIdentidad Sello = new("pre", "dat", "fix");

    private const string CapacidadMinima = """
        {"items": [
          {"id": "cap-001", "pregunta": "¿Qué carreras hay?", "categoria": "consulta_simple",
           "actor": "global", "sql_referencia": "SELECT name FROM identity.carreras", "orden_importa": false}
        ]}
        """;

    // --------------------------------------------------------- eje de robustez

    [Fact]
    public void La_consulta_de_un_item_de_robustez_es_la_de_su_origen()
    {
        // EL INVARIANTE ENTERO DEL EJE. Sin él, un fallo sería ambiguo: ¿no entendió
        // el fraseo, o no supo escribir la consulta? Y no está vigilado por un test
        // que compare copias: NO HAY DOS COPIAS. La consulta se deriva.
        var capacidad = DatasetDeCapacidad.Interpretar(CapacidadMinima);

        var robustez = DatasetDeRobustez.Interpretar("""
            {"items": [
              {"id": "rob-001", "origen": "cap-001", "clase": "sin_tildes", "pregunta": "Que carreras hay?"}
            ]}
            """, capacidad);

        var item = Assert.Single(robustez.Items);

        Assert.Equal(capacidad.Items[0].SqlReferencia, item.Item.SqlReferencia);
        Assert.Same(capacidad.Items[0].SqlReferencia, item.Item.SqlReferencia);
    }

    [Fact]
    public void Un_item_de_robustez_hereda_actor_categoria_y_orden()
    {
        var capacidad = DatasetDeCapacidad.Interpretar(CapacidadMinima);

        var item = Assert.Single(DatasetDeRobustez.Interpretar("""
            {"items": [
              {"id": "rob-001", "origen": "cap-001", "clase": "tipeo", "pregunta": "¿Qué carrreras hay?"}
            ]}
            """, capacidad).Items);

        var origen = capacidad.Items[0];

        Assert.Equal(origen.Actor, item.Item.Actor);
        Assert.Equal(origen.Categoria, item.Item.Categoria);
        Assert.Equal(origen.OrdenImporta, item.Item.OrdenImporta);

        // Lo único que cambia es la pregunta, y el identificador propio para que el
        // gate lo distinga del original.
        Assert.NotEqual(origen.Pregunta, item.Item.Pregunta);
        Assert.Equal("rob-001", item.Item.Id);
    }

    [Fact]
    public void Un_item_de_robustez_que_declara_su_propia_consulta_se_rechaza()
    {
        // El campo no existe en el contrato. Que alguien lo escriba significa que
        // está por mantener dos copias de la misma consulta, así que se lo frena acá
        // y no cuando ya divergieron.
        var capacidad = DatasetDeCapacidad.Interpretar(CapacidadMinima);

        var falla = Assert.Throws<InvalidOperationException>(() => DatasetDeRobustez.Interpretar("""
            {"items": [
              {"id": "rob-001", "origen": "cap-001", "clase": "tipeo", "pregunta": "otra",
               "sql_referencia": "SELECT 1"}
            ]}
            """, capacidad));

        Assert.Contains("hereda", falla.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Un_origen_inexistente_se_rechaza_nombrandolo()
    {
        var capacidad = DatasetDeCapacidad.Interpretar(CapacidadMinima);

        var falla = Assert.Throws<InvalidOperationException>(() => DatasetDeRobustez.Interpretar("""
            {"items": [
              {"id": "rob-001", "origen": "cap-999", "clase": "tipeo", "pregunta": "otra"}
            ]}
            """, capacidad));

        Assert.Contains("cap-999", falla.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Una_clase_de_perturbacion_desconocida_se_rechaza()
    {
        var capacidad = DatasetDeCapacidad.Interpretar(CapacidadMinima);

        var falla = Assert.Throws<InvalidOperationException>(() => DatasetDeRobustez.Interpretar("""
            {"items": [
              {"id": "rob-001", "origen": "cap-001", "clase": "en_ingles", "pregunta": "otra"}
            ]}
            """, capacidad));

        Assert.Contains("en_ingles", falla.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Una_perturbacion_identica_al_origen_se_rechaza()
    {
        // No mide robustez: mide dos veces lo mismo, y le sube el peso a ese caso en
        // el promedio.
        var capacidad = DatasetDeCapacidad.Interpretar(CapacidadMinima);

        Assert.Throws<InvalidOperationException>(() => DatasetDeRobustez.Interpretar("""
            {"items": [
              {"id": "rob-001", "origen": "cap-001", "clase": "tipeo", "pregunta": "¿Qué carreras hay?"}
            ]}
            """, capacidad));
    }

    [Fact]
    public void El_dataset_de_robustez_del_repositorio_carga_y_cubre_las_cinco_clases()
    {
        var capacidad = DatasetDeCapacidad.Cargar(Ruta("capacidad.json"));
        var robustez = DatasetDeRobustez.Cargar(Ruta("robustez.json"), capacidad);

        var porClase = robustez.ConteoPorClase();

        Assert.Equal(ClaseDePerturbacion.Todas.Count, porClase.Count);
        Assert.All(ClaseDePerturbacion.Todas, clase =>
            Assert.True(porClase.GetValueOrDefault(clase) >= 2,
                $"La clase '{clase}' tiene menos de dos ítems: no alcanza para ver una tendencia."));
    }

    // ---------------------------------------------------------- eje de diálogo

    [Fact]
    public void El_chequeo_de_arrastre_encuentra_el_termino_en_la_pregunta_interpretada()
    {
        // ES LO QUE HACE QUE EL EJE MIDA ALGO. Un diálogo puede dar 100% mientras el
        // sistema arrastra el filtro anterior: si el turno es autocontenido, el
        // arrastre no cambia el resultado y no se ve en ningún otro lado.
        var turno = new TurnoDeDialogo("¿Cuáles son los cargos?", null, ["Bases de Datos"], false);

        Assert.Equal(
            "Bases de Datos",
            RunnerDeDialogo.TerminoArrastrado(
                turno, "¿Cuáles son los cargos de Bases de Datos?"));
    }

    [Fact]
    public void El_chequeo_de_arrastre_ignora_acentos_y_mayusculas()
    {
        // «Análisis» y «analisis» son el mismo arrastre. Comparar literal dejaría
        // pasar el caso más común, que es el modelo reescribiendo sin tildes.
        var turno = new TurnoDeDialogo("...", null, ["Análisis Matemático"], false);

        Assert.NotNull(RunnerDeDialogo.TerminoArrastrado(
            turno, "cargos de ANALISIS MATEMATICO"));
    }

    [Fact]
    public void Sin_arrastre_el_chequeo_no_encuentra_nada()
    {
        var turno = new TurnoDeDialogo("...", null, ["Bases de Datos"], false);

        Assert.Null(RunnerDeDialogo.TerminoArrastrado(turno, "¿Cuáles son los cargos docentes?"));
    }

    [Fact]
    public void Sin_pregunta_interpretada_no_hay_nada_que_arrastrar()
    {
        // El turno era autocontenido y no se reescribió: no hubo reescritor, así que
        // no pudo arrastrar nada.
        var turno = new TurnoDeDialogo("...", null, ["Bases de Datos"], false);

        Assert.Null(RunnerDeDialogo.TerminoArrastrado(turno, null));
    }

    [Fact]
    public void Un_dialogo_de_un_solo_turno_se_rechaza()
    {
        // Es un ítem de capacidad con otro nombre: no ejercita nada de la capa
        // conversacional.
        Assert.Throws<InvalidOperationException>(() => DatasetDeDialogo.Interpretar("""
            {"dialogos": [
              {"id": "d1", "actor": "global", "es_pivote_duro": true,
               "turnos": [{"pregunta": "hola"}]}
            ]}
            """));
    }

    [Fact]
    public void Un_dataset_de_dialogo_sin_pivote_duro_se_rechaza()
    {
        var falla = Assert.Throws<InvalidOperationException>(() => DatasetDeDialogo.Interpretar("""
            {"dialogos": [
              {"id": "d1", "actor": "global", "es_pivote_duro": false,
               "turnos": [{"pregunta": "una"}, {"pregunta": "otra"}]}
            ]}
            """));

        Assert.Contains("pivote duro", falla.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Un_pivote_duro_sin_terminos_prohibidos_se_rechaza()
    {
        // Un pivote sin términos prohibidos no comprueba el pivote: comprueba que el
        // segundo turno se responde, que es otra cosa.
        var falla = Assert.Throws<InvalidOperationException>(() => DatasetDeDialogo.Interpretar("""
            {"dialogos": [
              {"id": "d1", "actor": "global", "es_pivote_duro": true,
               "turnos": [{"pregunta": "una"}, {"pregunta": "otra"}]}
            ]}
            """));

        Assert.Contains("términos prohibidos", falla.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void El_dataset_de_dialogo_del_repositorio_tiene_un_pivote_duro_sin_anafora()
    {
        var dialogo = DatasetDeDialogo.Cargar(Ruta("dialogo.json"));
        var pivotes = dialogo.Dialogos.Where(d => d.EsPivoteDuro).ToList();

        Assert.NotEmpty(pivotes);

        foreach (var pivote in pivotes)
        {
            var segundo = pivote.Turnos[1];

            Assert.NotEmpty(segundo.TerminosProhibidos);

            // Sin anáfora: si el segundo turno dijera «¿y en ésa?», el detector de
            // cambio de tema lo trataría como seguimiento —correctamente— y el
            // diálogo no probaría un pivote.
            Assert.All(
                new[] { " ese", " esa", " eso", " ahí", " y en ", "ésa", "ése" },
                marcador => Assert.DoesNotContain(
                    marcador, segundo.Pregunta, StringComparison.OrdinalIgnoreCase));
        }
    }

    // ----------------------------------------------------------- eje social

    [Fact]
    public void Un_dataset_social_sin_negativos_se_rechaza()
    {
        // Sin negativos el eje solo puede subir: un enrutador que captura todo daría
        // perfecto. Y son los únicos ítems que consumen tokens, que es lo que la
        // guarda de proveedor caído necesita para funcionar.
        var falla = Assert.Throws<InvalidOperationException>(() => DatasetSocial.Interpretar("""
            {"items": [{"id": "s1", "clase": "social", "actor": "global", "pregunta": "hola"}]}
            """));

        Assert.Contains("negativos", falla.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void El_dataset_social_del_repositorio_tiene_las_tres_clases()
    {
        var social = DatasetSocial.Cargar(Ruta("social.json"));
        var porClase = social.ConteoPorClase();

        Assert.All(ClaseSocial.Todas, clase =>
            Assert.True(porClase.GetValueOrDefault(clase) > 0, $"Falta la clase '{clase}'."));
    }

    [Fact]
    public void Los_negativos_del_eje_social_son_preguntas_del_eje_de_capacidad_o_de_robustez()
    {
        // Los negativos tienen que ser preguntas LEGÍTIMAS. Inventar preguntas raras
        // para que el enrutador no las capture sería medir contra un enemigo de
        // paja: el modo de falla real es comerse una pregunta normal.
        var capacidad = DatasetDeCapacidad.Cargar(Ruta("capacidad.json"));
        var robustez = DatasetDeRobustez.Cargar(Ruta("robustez.json"), capacidad);
        var social = DatasetSocial.Cargar(Ruta("social.json"));

        var legitimas = capacidad.Items.Select(i => i.Pregunta)
            .Concat(robustez.Items.Select(i => i.Pregunta))
            .ToHashSet(StringComparer.Ordinal);

        var negativos = social.Items.Where(i => i.Clase == ClaseSocial.Negativo).ToList();

        // Algunos negativos llevan una apertura cortés adelante —«hola, ¿cuántos…»—,
        // que es justo el caso que el enrutador social podría comerse. Se acepta un
        // negativo si CONTIENE una pregunta legítima.
        //
        // La comparación ignora mayúsculas y signos: bajar la inicial después de una
        // apertura cortés es español normal, no otra pregunta.
        var normalizadas = legitimas.Select(Comparable).ToList();

        Assert.All(negativos, negativo =>
            Assert.True(
                normalizadas.Any(legitima => Comparable(negativo.Pregunta).Contains(
                    legitima, StringComparison.Ordinal)),
                $"El negativo '{negativo.Id}' no sale de ningún otro eje: «{negativo.Pregunta}»."));
    }

    [Fact]
    public void La_guarda_de_proveedor_caido_dispara_solo_con_todo_en_cero()
    {
        // LA TRAMPA DE ESTE EJE. El assert es «consumió cero tokens», y un proveedor
        // caído consume cero en TODOS los ítems: la corrida entera daría verde
        // perfecto.
        Assert.True(RunnerSocial.TodoACero(0));
        Assert.False(RunnerSocial.TodoACero(1));
    }

    // ------------------------------------------------------ gate de regresión

    [Fact]
    public void Un_item_roto_hace_fallar_el_gate_aunque_el_promedio_suba()
    {
        // EL MOTIVO ARITMÉTICO DEL LOCK POR ÍTEM: tres que se rompen y tres que se
        // arreglan dan delta cero y pasan cualquier umbral, mientras el asistente
        // cambió de comportamiento.
        var linea = Linea(
            ("i1", DesenlaceDeItem.TraduccionCorrecta),
            ("i2", DesenlaceDeItem.TraduccionIncorrecta),
            ("i3", DesenlaceDeItem.TraduccionIncorrecta));

        // La corrida tiene MÁS aciertos que la línea de base —dos contra uno— y aun
        // así falla, porque i1 se rompió.
        var reporte = Reporte(
            ("i1", DesenlaceDeItem.TraduccionIncorrecta),
            ("i2", DesenlaceDeItem.TraduccionCorrecta),
            ("i3", DesenlaceDeItem.TraduccionCorrecta));

        var veredicto = GateDeRegresion.Comparar(reporte, linea);

        Assert.False(veredicto.Pasa);
        Assert.Contains(veredicto.Regresiones, r => r.Contains("i1", StringComparison.Ordinal));
        Assert.Equal(2, veredicto.Mejoras.Count);
    }

    [Fact]
    public void Una_corrida_identica_pasa()
    {
        var linea = Linea(("i1", DesenlaceDeItem.TraduccionCorrecta));
        var reporte = Reporte(("i1", DesenlaceDeItem.TraduccionCorrecta));

        Assert.True(GateDeRegresion.Comparar(reporte, linea).Pasa);
    }

    [Fact]
    public void Un_item_nuevo_no_hace_fallar_el_gate_y_se_informa()
    {
        var linea = Linea(("i1", DesenlaceDeItem.TraduccionCorrecta));
        var reporte = Reporte(
            ("i1", DesenlaceDeItem.TraduccionCorrecta),
            ("i2", DesenlaceDeItem.TraduccionIncorrecta));

        var veredicto = GateDeRegresion.Comparar(reporte, linea);

        Assert.True(veredicto.Pasa);
        Assert.Equal(["i2"], veredicto.Nuevos);
    }

    [Fact]
    public void Pasar_de_traducir_bien_a_abstenerse_ES_una_regresion()
    {
        // La abstención sobre algo contestable no resta puntos —es una falta de
        // capacidad, no una mentira—, pero tampoco es pasar. Un gate que la contara
        // como neutral dejaría que el asistente se volviera mudo sin avisar.
        var linea = Linea(("i1", DesenlaceDeItem.TraduccionCorrecta));
        var reporte = Reporte(("i1", DesenlaceDeItem.AbstencionSobreloFactible));

        Assert.False(GateDeRegresion.Comparar(reporte, linea).Pasa);
    }

    [Theory]
    [InlineData("otro", "dat", "fix", "prefijo")]
    [InlineData("pre", "otro", "fix", "dataset")]
    [InlineData("pre", "dat", "otro", "fixture")]
    public void Con_cualquier_hash_distinto_el_gate_no_compara(
        string prefijo, string dataset, string fixture, string esperado)
    {
        // Los tres hashes identifican CONTRA QUÉ se midió: con cualquiera distinto,
        // comparar ítem a ítem sería comparar dos cosas que no son la misma.
        var linea = new LineaDeBase(
            "capacidad", Sello,
            new Dictionary<string, DesenlaceDeItem>(StringComparer.Ordinal)
            {
                ["i1"] = DesenlaceDeItem.TraduccionCorrecta,
            });

        var reporte = new Reporte(
            "capacidad",
            new SelloDeIdentidad(prefijo, dataset, fixture),
            [new ResultadoDeItem("i1", "consulta_simple", DesenlaceDeItem.TraduccionCorrecta, "")],
            new Dictionary<string, int>(StringComparer.Ordinal));

        var veredicto = GateDeRegresion.Comparar(reporte, linea);

        Assert.False(veredicto.Pasa);
        Assert.NotNull(veredicto.ExigeRegenerar);
        Assert.Contains(esperado, veredicto.ExigeRegenerar!, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(veredicto.Regresiones);
    }

    [Fact]
    public void La_linea_de_base_va_y_vuelve_de_su_texto()
    {
        var original = LineaDeBase.De(Reporte(
            ("i1", DesenlaceDeItem.TraduccionCorrecta),
            ("i2", DesenlaceDeItem.AbstencionCorrecta)));

        var vuelta = LineaDeBase.Interpretar(original.Serializar());

        Assert.Equal(original.Sello, vuelta.Sello);
        Assert.Equal(original.Items, vuelta.Items);
    }

    [Fact]
    public void Una_linea_de_base_inexistente_devuelve_nulo_y_no_explota()
    {
        // La primera corrida de un eje no tiene con qué comparar, y eso no es un
        // error: es el estado inicial.
        Assert.Null(LineaDeBase.Cargar(Path.Combine(Path.GetTempPath(), "no-existe-jamas.json")));
    }

    [Fact]
    public void No_hay_ninguna_linea_de_base_versionada_todavia()
    {
        // Generar una exige una corrida REAL, y una corrida real exige un proveedor
        // que todavía no está elegido (TD-008). Un archivo generado con el proveedor
        // simulado registraría el comportamiento del simulador, no el del asistente,
        // y el gate empezaría a defender el número equivocado.
        var directorio = Path.Combine(
            RaizRepositorio.Ruta(), "backend", "eval", "lineas-de-base");

        Assert.True(Directory.Exists(directorio));
        Assert.Empty(Directory.GetFiles(directorio, "*.json"));
    }

    // ------------------------------------------------------------------ apoyo

    private static string Ruta(string archivo) =>
        Path.Combine(RaizRepositorio.Ruta(), "backend", "eval", "datasets", archivo);

    /// <summary>Baja la pregunta a lo que la hace la misma pregunta.</summary>
    private static string Comparable(string pregunta) =>
        pregunta.Replace("¿", string.Empty, StringComparison.Ordinal)
            .Replace("?", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToLowerInvariant();

    private static LineaDeBase Linea(params (string Id, DesenlaceDeItem Desenlace)[] items) =>
        new("capacidad", Sello,
            items.ToDictionary(i => i.Id, i => i.Desenlace, StringComparer.Ordinal));

    private static Reporte Reporte(params (string Id, DesenlaceDeItem Desenlace)[] items) =>
        new("capacidad", Sello,
            [.. items.Select(i => new ResultadoDeItem(i.Id, "consulta_simple", i.Desenlace, ""))],
            new Dictionary<string, int>(StringComparer.Ordinal));
}
