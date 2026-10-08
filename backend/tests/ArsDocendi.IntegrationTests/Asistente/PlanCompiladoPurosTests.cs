using System.Text.Json;
using System.Text.Json.Nodes;
using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Las piezas deterministas del plan compilado, sin base ni modelo (change
/// <c>asistente-plan-compilado</c>).
/// </summary>
public sealed class PlanCompiladoPurosTests
{
    // ------------------------------------------------------------- esquema y plan

    [Fact]
    public void El_esquema_de_salida_es_JSON_y_enumera_el_catalogo()
    {
        var esquema = JsonNode.Parse(CatalogoDelPlan.EsquemaJson)!;
        var campos = esquema["properties"]!["filtros"]!["items"]!["properties"]!["campo"]!["enum"]!
            .AsArray().Select(n => n!.GetValue<string>()).ToHashSet();

        Assert.Equal(CatalogoDelPlan.Campos.Keys.ToHashSet(), campos);
        Assert.Equal(false, esquema["additionalProperties"]!.GetValue<bool>());
    }

    [Fact]
    public void Interpreta_un_plan_envuelto_en_texto_y_con_numeros_sin_comillas()
    {
        var plan = PlanDeConsulta.Interpretar(
            "Acá va: {\"expresable\":true,\"medida\":\"conteo\",\"filtros\":"
            + "[{\"campo\":\"cantidad_carreras\",\"operador\":\">=\",\"valor\":2}],\"condiciones\":[]} listo");

        Assert.NotNull(plan);
        Assert.Equal("2", plan.Filtros.Single().Valor);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no sé")]
    [InlineData("{\"expresable\":true}")]
    [InlineData("{\"expresable\":true,\"medida\":")]
    public void Una_salida_que_no_es_un_plan_es_nula(string salida) =>
        Assert.Null(PlanDeConsulta.Interpretar(salida));

    [Fact]
    public void Dos_muestras_con_las_condiciones_en_otro_orden_coinciden()
    {
        const string Pregunta = "¿Cuántos titulares dictan en al menos dos carreras?";
        var texto = new TextoDeLaPregunta(Pregunta);

        var una = Validar(texto, "conteo", [("cargo", "=", "titular"), ("cantidad_carreras", ">=", "2")]);
        var otra = Validar(texto, "conteo", [("cantidad_carreras", ">=", "2"), ("cargo", "=", "Titulares")]);

        Assert.Equal(una.Canonico(), otra.Canonico());
    }

    [Fact]
    public void Un_conteo_que_reparte_sus_condiciones_coincide_con_el_que_las_junta()
    {
        // Medición en la RTX 3070 (2026-10-08): Qwen3-8B reparte así las condiciones de
        // conteos y listados, y el validador las rechazaba por la forma.
        var texto = new TextoDeLaPregunta("¿Cuántos titulares dictan en al menos dos carreras?");

        var juntas = Validar(texto, "conteo", [("cargo", "=", "titular"), ("cantidad_carreras", ">=", "2")]);
        var repartidas = ValidadorDePlan.Validar(
            Plan("conteo", [("cargo", "=", "titular")], [("cantidad_carreras", ">=", "2")]), texto);

        Assert.True(repartidas.EsValido, repartidas.Motivo);
        Assert.Empty(repartidas.Plan!.Condiciones);
        Assert.Equal(juntas.Canonico(), repartidas.Plan.Canonico());
    }

    [Fact]
    public void Un_porcentaje_sin_condiciones_es_invalido()
    {
        var texto = new TextoDeLaPregunta("¿Qué porcentaje de los docentes dicta en más de una carrera?");

        var veredicto = ValidadorDePlan.Validar(
            Plan("porcentaje", [("cantidad_carreras", ">", "1")]), texto);

        Assert.False(veredicto.EsValido);
    }

    [Fact]
    public void Un_operador_distinto_es_otra_lectura()
    {
        var texto = new TextoDeLaPregunta("¿Cuántos titulares dictan en dos carreras?");

        var exactamente = Validar(texto, "conteo", [("cargo", "=", "titular"), ("cantidad_carreras", "=", "2")]);
        var alMenos = Validar(texto, "conteo", [("cargo", "=", "titular"), ("cantidad_carreras", ">=", "2")]);

        Assert.NotEqual(exactamente.Canonico(), alMenos.Canonico());
    }

    // -------------------------------------------------------------------- puerta

    [Theory]
    [InlineData("¿Cuántos pedidos rechazados hay?", "NoAplica")]
    [InlineData("¿Cuántas horas suman los titulares?", "NoAplica")]
    [InlineData("¿Cuántos titulares había en 2024?", "NoAplica")]
    [InlineData("¿Cuántos ayudantes hay?", "NoAplica")]
    [InlineData("¿Cuántos titulares hay por carrera?", "NoAplica")]
    [InlineData("¿Qué materias tiene Informática?", "NoAplica")]
    [InlineData("¿Cuántos ayudantes de primera hay?", "Candidata")]
    [InlineData("¿Cuántos titulares dictan en al menos dos carreras?", "Candidata")]
    [InlineData("¿Cuántos titulares tienen más de 20 años de antigüedad?", "AclararAntiguedad")]
    [InlineData("¿Cuántos titulares tienen más de 20 años de antigüedad desde su primera designación?", "Candidata")]
    [InlineData("¿Cuántos docentes tienen antigüedad según la experiencia declarada en el portal?", "Candidata")]
    public void La_puerta_decide_sin_modelo(string pregunta, string esperada) =>
        Assert.Equal(
            Enum.Parse<DecisionDeLaPuerta>(esperada), PuertaDelPlan.Evaluar(new TextoDeLaPregunta(pregunta)));

    [Fact]
    public void Cada_opcion_de_antiguedad_pasa_la_puerta()
    {
        var opciones = PuertaDelPlan.OpcionesDeAntiguedad("¿Cuántos titulares tienen más de 20 años de antigüedad?");

        Assert.Equal(2, opciones.Count);
        Assert.All(opciones, opcion => Assert.Equal(
            DecisionDeLaPuerta.Candidata, PuertaDelPlan.Evaluar(new TextoDeLaPregunta(opcion.PreguntaResuelta))));
    }

    // ------------------------------------------------------------ anclaje y cierre

    [Theory]
    [InlineData("conteo", "cantidad_carreras", ">=", "2", "condición inventada")]
    [InlineData("conteo", "cargo", "=", "adjunto", "cargo que la pregunta no nombra")]
    [InlineData("listado", "cargo", "=", "titular", "medida que la pregunta no pide")]
    public void Rechaza_la_muestra_que_no_corresponde_a_la_pregunta(
        string medida, string campo, string operador, string valor, string porque)
    {
        var texto = new TextoDeLaPregunta("¿Cuántos titulares hay?");
        var filtros = new List<(string, string, string)> { (campo, operador, valor) };
        if (campo != "cargo")
        {
            filtros.Insert(0, ("cargo", "=", "titular"));
        }

        var veredicto = ValidadorDePlan.Validar(Plan(medida, filtros), texto);

        Assert.False(veredicto.EsValido, porque);
    }

    [Fact]
    public void Rechaza_la_muestra_que_pierde_una_carrera_nombrada()
    {
        var texto = new TextoDeLaPregunta("¿Cuántos titulares de Ingeniería Industrial hay?");

        var veredicto = ValidadorDePlan.Validar(
            Plan("conteo", [("cargo", "=", "titular")]), texto, ["Ingeniería Industrial", "Ingeniería en Informática"]);

        Assert.False(veredicto.EsValido);
        Assert.Contains("Ingeniería Industrial", veredicto.Motivo);
    }

    [Theory]
    [InlineData("¿Cuántos titulares dictan en más de 2 carreras?", ">", true)]
    [InlineData("¿Cuántos titulares dictan en más de 2 carreras?", ">=", false)]
    [InlineData("¿Cuántos titulares dictan en 2 carreras o más?", ">=", true)]
    [InlineData("¿Cuántos titulares dictan en como máximo 2 carreras?", "<=", true)]
    [InlineData("¿Cuántos titulares dictan en 2 carreras?", "=", true)]
    [InlineData("¿Cuántos titulares dictan en 2 carreras?", ">=", true)]
    [InlineData("¿Cuántos titulares dictan en 2 carreras?", "<", false)]
    public void El_operador_tiene_que_coincidir_con_como_compara_la_pregunta(
        string pregunta, string operador, bool valido)
    {
        var veredicto = ValidadorDePlan.Validar(
            Plan("conteo", [("cargo", "=", "titular"), ("cantidad_carreras", operador, "2")]),
            new TextoDeLaPregunta(pregunta));

        Assert.Equal(valido, veredicto.EsValido);
    }

    [Fact]
    public void Una_negacion_del_plan_exige_una_negacion_en_la_pregunta()
    {
        var sinNegar = ValidadorDePlan.Validar(
            Plan("conteo", [("cargo", "!=", "titular")]), new TextoDeLaPregunta("¿Cuántos titulares hay?"));
        var negada = ValidadorDePlan.Validar(
            Plan("conteo", [("cargo", "!=", "titular")]), new TextoDeLaPregunta("¿Cuántos docentes no son titulares?"));

        Assert.False(sinNegar.EsValido);
        Assert.True(negada.EsValido, negada.Motivo);
    }

    [Fact]
    public void La_antiguedad_tiene_que_ser_la_que_la_pregunta_califica()
    {
        var texto = new TextoDeLaPregunta(
            "¿Cuántos titulares tienen más de 20 años de antigüedad desde su primera designación?");

        var declarada = ValidadorDePlan.Validar(
            Plan("conteo", [("cargo", "=", "titular"), ("antiguedad_declarada", ">", "20")]), texto);
        var deDesignacion = ValidadorDePlan.Validar(
            Plan("conteo", [("cargo", "=", "titular"), ("antiguedad_designacion", ">", "20")]), texto);

        Assert.False(declarada.EsValido);
        Assert.True(deDesignacion.EsValido, deDesignacion.Motivo);
    }

    // ------------------------------------------------------------ dataset entero

    [Fact]
    public void Cada_plan_de_referencia_pasa_la_puerta_y_el_validador_con_su_propia_pregunta()
    {
        var fallas = new List<string>();

        foreach (var item in ItemsDelDataset())
        {
            var texto = new TextoDeLaPregunta(item.Pregunta);
            var puerta = PuertaDelPlan.Evaluar(texto);

            if (item.Plan is null)
            {
                // La antigüedad se aclara en la puerta; una materia compartida, recién
                // al resolver las entidades, así que esa pregunta sí es candidata.
                var esperada = item.Categoria switch
                {
                    "ambigua" when texto.Contiene("antiguedad") => DecisionDeLaPuerta.AclararAntiguedad,
                    "ambigua" or "no_contestable" => DecisionDeLaPuerta.Candidata,
                    _ => DecisionDeLaPuerta.NoAplica,
                };

                if (puerta != esperada)
                {
                    fallas.Add($"{item.Id}: la puerta dijo {puerta} y se esperaba {esperada}.");
                }

                continue;
            }

            if (puerta != DecisionDeLaPuerta.Candidata)
            {
                fallas.Add($"{item.Id}: la puerta dijo {puerta}.");
                continue;
            }

            var veredicto = ValidadorDePlan.Validar(item.Plan, texto, Carreras);
            if (!veredicto.EsValido)
            {
                fallas.Add($"{item.Id}: {veredicto.Motivo}");
            }
        }

        Assert.True(fallas.Count == 0, string.Join(Environment.NewLine, fallas));
    }

    [Fact]
    public void La_pregunta_explicita_de_cada_plan_vuelve_a_dar_el_mismo_plan()
    {
        // Es la pregunta que se ofrece en una aclaración: si el propio plan la
        // rechazara, el usuario quedaría en un círculo.
        var fallas = new List<string>();

        foreach (var item in ItemsDelDataset().Where(i => i.Plan is not null))
        {
            var original = ValidadorDePlan.Validar(item.Plan!, new TextoDeLaPregunta(item.Pregunta), Carreras).Plan!;
            var explicita = RedaccionDelPlan.PreguntaExplicita(original);
            var texto = new TextoDeLaPregunta(explicita);

            var veredicto = PuertaDelPlan.Evaluar(texto) == DecisionDeLaPuerta.Candidata
                ? ValidadorDePlan.Validar(item.Plan!, texto, Carreras)
                : VeredictoDelPlan.Invalido("la puerta la rechaza");

            if (!veredicto.EsValido || veredicto.Plan!.Canonico() != original.Canonico())
            {
                fallas.Add($"{item.Id} → «{explicita}»: {veredicto.Motivo}");
            }
        }

        Assert.True(fallas.Count == 0, string.Join(Environment.NewLine, fallas));
    }

    [Fact]
    public void Los_ejemplos_del_prompt_no_estan_en_el_dataset()
    {
        var preguntas = ItemsDelDataset().Select(i => i.Pregunta).ToList();

        Assert.All(preguntas, pregunta => Assert.DoesNotContain(pregunta, GeneradorDePlan.Prefijo));
    }

    // ------------------------------------------------------------------ redacción

    [Fact]
    public void El_porcentaje_muestra_numerador_y_denominador()
    {
        var texto = new TextoDeLaPregunta("¿Qué porcentaje de los titulares dicta en más de una carrera?");
        var plan = ValidadorDePlan.Validar(
            Plan("porcentaje", [("cargo", "=", "titular")], [("cantidad_carreras", ">", "1")]), texto).Plan!;

        var respuesta = RedaccionDelPlan.Responder(
            plan, new ResultadoDeConsulta(["cumplen", "total"], [[1L, 4L]], false), new Dictionary<int, EntidadResuelta>(), true);

        Assert.Contains("25 %", respuesta);
        Assert.Contains("(1 de 4)", respuesta);
    }

    [Fact]
    public void Un_denominador_cero_no_calcula_porcentaje()
    {
        var texto = new TextoDeLaPregunta("¿Qué porcentaje de los titulares dicta en más de una carrera?");
        var plan = ValidadorDePlan.Validar(
            Plan("porcentaje", [("cargo", "=", "titular")], [("cantidad_carreras", ">", "1")]), texto).Plan!;

        var respuesta = RedaccionDelPlan.Responder(
            plan, new ResultadoDeConsulta(["cumplen", "total"], [[0L, 0L]], false), new Dictionary<int, EntidadResuelta>(), false);

        Assert.Contains("no se puede calcular", respuesta);
        Assert.Contains("dentro de lo que podés ver", respuesta);
    }

    // --------------------------------------------------------------------- apoyo

    internal static readonly IReadOnlyList<string> Carreras =
        ["Ingeniería en Informática", "Ingeniería Industrial", "Ingeniería Electrónica"];

    internal sealed record ItemCompuesto(
        string Id, string Pregunta, string Categoria, string Actor, string? SqlReferencia, PlanDeConsulta? Plan);

    internal static IReadOnlyList<ItemCompuesto> ItemsDelDataset()
    {
        var ruta = Path.Combine(RaizRepositorio.Ruta(), "backend", "eval", "datasets", "compuestas.json");
        using var documento = JsonDocument.Parse(File.ReadAllText(ruta));

        return [.. documento.RootElement.GetProperty("items").EnumerateArray().Select(item =>
            new ItemCompuesto(
                item.GetProperty("id").GetString()!,
                item.GetProperty("pregunta").GetString()!,
                item.GetProperty("categoria").GetString()!,
                item.GetProperty("actor").GetString()!,
                item.GetProperty("sql_referencia").GetString(),
                item.GetProperty("plan_referencia").ValueKind == JsonValueKind.Null
                    ? null
                    : PlanDeConsulta.Interpretar(item.GetProperty("plan_referencia").GetRawText())))];
    }

    private static PlanValidado Validar(
        TextoDeLaPregunta texto, string medida, IReadOnlyList<(string, string, string)> filtros)
    {
        var veredicto = ValidadorDePlan.Validar(Plan(medida, filtros), texto);
        Assert.True(veredicto.EsValido, veredicto.Motivo);
        return veredicto.Plan!;
    }

    private static PlanDeConsulta Plan(
        string medida,
        IReadOnlyList<(string Campo, string Operador, string Valor)> filtros,
        IReadOnlyList<(string Campo, string Operador, string Valor)>? condiciones = null) =>
        new(true, medida,
            [.. filtros.Select(f => new CondicionDelPlan(f.Campo, f.Operador, f.Valor))],
            [.. (condiciones ?? []).Select(c => new CondicionDelPlan(c.Campo, c.Operador, c.Valor))]);
}
