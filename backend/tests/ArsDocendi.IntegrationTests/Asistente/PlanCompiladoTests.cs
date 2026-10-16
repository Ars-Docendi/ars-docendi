using ArsDocendi.Evaluacion.Nucleo.Fixture;
using ArsDocendi.Evaluacion.Nucleo.Puntuacion;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El plan compilado contra una base real con el fixture de evaluación y su
/// suplemento compuesto (change <c>asistente-plan-compilado</c>).
/// </summary>
public sealed class PlanCompiladoTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "plan_compilado")
{
    private const string PreguntaDelInforme =
        "¿Qué porcentaje de titulares tiene más de 20 años de antigüedad desde su primera designación "
        + "y además dicta en dos o más carreras?";

    private const string PlanDelInforme =
        """
        {"expresable":true,"medida":"porcentaje","filtros":[{"campo":"cargo","operador":"=","valor":"titular"}],
         "condiciones":[{"campo":"antiguedad_designacion","operador":">","valor":"20"},
                        {"campo":"cantidad_carreras","operador":">=","valor":"2"}]}
        """;

    // ---------------------------------------------------- compilador y dataset

    [Fact]
    public async Task Cada_plan_de_referencia_compilado_devuelve_lo_mismo_que_su_consulta_de_referencia()
    {
        await AplicarFixtureAsync();
        var ct = TestContext.Current.CancellationToken;
        var ejecutor = Ejecutor();
        var resolutor = new ResolutorDeEntidadesDelPlan(ejecutor, new BuscadorDeMenciones(Apertura));
        var fallas = new List<string>();
        var comparados = 0;

        foreach (var item in PlanCompiladoPurosTests.ItemsDelDataset().Where(i => i.Plan is not null))
        {
            comparados++;
            var actor = Actor(item.Actor);
            var plan = ValidadorDePlan.Validar(
                item.Plan!, new TextoDeLaPregunta(item.Pregunta), PlanCompiladoPurosTests.Carreras).Plan!;

            var resolucion = await resolutor.ResolverAsync(actor, plan, ct);
            if (!resolucion.Resolvio)
            {
                fallas.Add($"{item.Id}: no resolvió «{resolucion.Fallida!.NombreOriginal}».");
                continue;
            }

            var consulta = CompiladorDePlan.Compilar(plan, resolucion.Entidades, GeneradorDeFixture.Ancla);
            var marcadores = consulta.Bindings.Keys.ToHashSet(StringComparer.Ordinal);
            var veredicto = ValidadorDeSql.Validar(consulta.Sql, marcadores, marcadores);
            if (!veredicto.EsValida)
            {
                fallas.Add($"{item.Id}: el validador rechazó la compilada: {veredicto.Motivo}");
                continue;
            }

            var compilada = await ejecutor.EjecutarAsync(consulta.Sql, actor, false, ct, consulta.Bindings);
            var referencia = await ejecutor.EjecutarAsync(item.SqlReferencia!, actor, false, ct);

            if (!ComparadorDeResultados.Coinciden(compilada, referencia, ordenImporta: false))
            {
                fallas.Add($"{item.Id}: compilada {Mostrar(compilada)} · referencia {Mostrar(referencia)}");
            }
        }

        // Sin esto, un dataset que dejara de interpretarse pasaría en verde sin comparar nada.
        Assert.True(comparados >= 25, $"Solo se compararon {comparados} planes.");
        Assert.True(fallas.Count == 0, string.Join(Environment.NewLine, fallas));
    }

    [Theory]
    [InlineData("cmp-001", "4")]
    [InlineData("cmp-004", "1|4")]
    [InlineData("cmp-005", "2|4")]
    [InlineData("cmp-010", "2")]
    [InlineData("cmp-013", "2")]
    [InlineData("cmp-026", "7")]
    [InlineData("cmp-027", "0")]
    [InlineData("cmp-034", "4|20")]
    public async Task El_suplemento_da_las_cardinalidades_que_el_dataset_declara(string id, string esperado)
    {
        // Depende del fixture y no del código: si las cardinalidades cambian, este
        // test dice qué ítem dejó de discriminar lo que discriminaba.
        await AplicarFixtureAsync();
        var item = PlanCompiladoPurosTests.ItemsDelDataset().Single(i => i.Id == id);

        var referencia = await Ejecutor().EjecutarAsync(
            item.SqlReferencia!, Actor(item.Actor), false, TestContext.Current.CancellationToken);

        Assert.Equal(esperado, string.Join('|', referencia.Filas[0].Select(v => Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture))));
    }

    // ------------------------------------------------------------------- carril

    [Fact]
    public async Task Con_tres_muestras_iguales_responde_por_plantilla_sin_redactar_con_el_modelo()
    {
        await AplicarFixtureAsync();
        var (carril, guion) = Carril(PlanDelInforme, PlanDelInforme, PlanDelInforme);

        var resultado = await ResponderAsync(carril, PreguntaDelInforme);

        Assert.NotNull(resultado);
        Assert.Equal(EstadoDelTurno.Respondida, resultado.Estado);
        Assert.Equal(CarrilDelPlan.CategoriaRespondida, resultado.Categoria);
        Assert.Equal(3, guion.Llamadas);
        Assert.Equal(3, resultado.LlamadasAlModelo);
        Assert.Contains("25 %", resultado.Respuesta);
        Assert.Contains("(1 de 4)", resultado.Respuesta);
        Assert.Equal(0m, guion.Recibidas[0].Temperatura);
        Assert.Equal(0.6m, guion.Recibidas[1].Temperatura);
        Assert.All(guion.Recibidas, s => Assert.Equal(CatalogoDelPlan.EsquemaJson, s.EsquemaDeSalidaJson));
    }

    [Fact]
    public async Task Con_muestras_distintas_aclara_con_una_opcion_por_lectura_y_no_ejecuta()
    {
        await AplicarFixtureAsync();
        // La otra lectura cambia el denominador: «de los titulares con más de 20 años» en
        // vez de «de los titulares». Las dos están ancladas en la pregunta.
        const string OtraLectura =
            """
            {"expresable":true,"medida":"porcentaje","filtros":[{"campo":"cargo","operador":"=","valor":"titular"},
             {"campo":"antiguedad_designacion","operador":">","valor":"20"}],
             "condiciones":[{"campo":"cantidad_carreras","operador":">=","valor":"2"}]}
            """;
        var (carril, _) = Carril(PlanDelInforme, OtraLectura, PlanDelInforme);

        var resultado = await ResponderAsync(carril, PreguntaDelInforme);

        Assert.NotNull(resultado);
        Assert.Equal(EstadoDelTurno.NecesitaAclaracion, resultado.Estado);
        Assert.Equal(2, resultado.Opciones!.Count);
        Assert.Empty(resultado.Filas);
    }

    [Fact]
    public async Task Una_pregunta_no_expresable_sigue_por_el_carril_SQL_con_una_sola_llamada()
    {
        await AplicarFixtureAsync();
        var (carril, guion) = Carril("""{"expresable":false,"medida":"conteo","filtros":[],"condiciones":[]}""");

        var resultado = await ResponderAsync(carril, "¿Cuántos docentes dictan en dos carreras distintas a la vez?");

        Assert.Null(resultado);
        Assert.Equal(1, guion.Llamadas);
    }

    [Fact]
    public async Task Una_primera_muestra_invalida_se_abstiene_sin_ejecutar_ni_seguir_por_SQL()
    {
        await AplicarFixtureAsync();
        var inventada =
            """{"expresable":true,"medida":"conteo","filtros":[{"campo":"cargo","operador":"=","valor":"titular"},{"campo":"cantidad_carreras","operador":">=","valor":"2"}],"condiciones":[]}""";
        var (carril, guion) = Carril(inventada);

        var resultado = await ResponderAsync(carril, "¿Cuántos titulares hay?");

        Assert.NotNull(resultado);
        Assert.Equal(EstadoDelTurno.NoContestable, resultado.Estado);
        Assert.Equal(CarrilDelPlan.CategoriaAbstencion, resultado.Categoria);
        Assert.Equal(1, guion.Llamadas);
    }

    [Fact]
    public async Task La_antiguedad_sin_calificar_se_aclara_sin_llamar_al_modelo()
    {
        await AplicarFixtureAsync();
        var (carril, guion) = Carril(PlanDelInforme);

        var resultado = await ResponderAsync(carril, "¿Cuántos titulares tienen más de 20 años de antigüedad?");

        Assert.NotNull(resultado);
        Assert.Equal(EstadoDelTurno.NecesitaAclaracion, resultado.Estado);
        Assert.Equal(2, resultado.Opciones!.Count);
        Assert.Equal(0, guion.Llamadas);
    }

    [Fact]
    public async Task Una_carrera_que_no_existe_se_dice_y_no_se_responde_que_no_hay()
    {
        await AplicarFixtureAsync();
        var plan =
            """{"expresable":true,"medida":"conteo","filtros":[{"campo":"cargo","operador":"=","valor":"titular"},{"campo":"carrera","operador":"=","valor":"Ingeniería Química"}],"condiciones":[]}""";
        var (carril, _) = Carril(plan, plan, plan);

        var resultado = await ResponderAsync(carril, "¿Cuántos titulares hay en la carrera de Ingeniería Química?");

        Assert.NotNull(resultado);
        Assert.Equal(EstadoDelTurno.NoContestable, resultado.Estado);
        Assert.Contains("No encontré la carrera", resultado.Respuesta);
    }

    [Fact]
    public async Task Una_materia_de_varias_carreras_se_aclara_y_cada_opcion_nombra_su_carrera()
    {
        await AplicarFixtureAsync();
        var plan = """{"expresable":true,"medida":"conteo","filtros":[{"campo":"materia","operador":"=","valor":"Análisis Matemático"}],"condiciones":[]}""";
        var (carril, _) = Carril(plan, plan, plan);

        var resultado = await ResponderAsync(carril, "¿Cuántos docentes dictan Análisis Matemático?");

        Assert.NotNull(resultado);
        Assert.Equal(EstadoDelTurno.NecesitaAclaracion, resultado.Estado);
        Assert.Equal(3, resultado.Opciones!.Count);

        foreach (var opcion in resultado.Opciones)
        {
            // La opción tiene que volver a entrar al plan, con la carrera, sin aclararse otra vez.
            var texto = new TextoDeLaPregunta(opcion.PreguntaResuelta);
            Assert.Equal(DecisionDeLaPuerta.Candidata, PuertaDelPlan.Evaluar(texto));
            Assert.Contains("en la carrera", opcion.PreguntaResuelta);
        }
    }

    [Fact]
    public async Task Fuera_del_catalogo_no_gasta_ninguna_llamada()
    {
        await AplicarFixtureAsync();
        var (carril, guion) = Carril(PlanDelInforme);

        var resultado = await ResponderAsync(carril, "¿Cuántos pedidos prioritarios hay en Ingeniería Industrial?");

        Assert.Null(resultado);
        Assert.Equal(0, guion.Llamadas);
    }

    [Fact]
    public async Task Un_actor_acotado_ve_su_ambito_y_la_respuesta_lo_dice()
    {
        await AplicarFixtureAsync();
        var plan = """{"expresable":true,"medida":"conteo","filtros":[{"campo":"cargo","operador":"=","valor":"titular"}],"condiciones":[]}""";
        var (carril, _) = Carril(plan, plan, plan);

        var resultado = await ResponderAsync(carril, "¿Cuántos titulares hay?", actor: "carrera");

        Assert.NotNull(resultado);
        Assert.Equal(2L, Convert.ToInt64(resultado.Filas[0][0], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Contains("dentro de lo que podés ver", resultado.Respuesta);
    }

    [Fact]
    public async Task El_carril_SQL_no_consulta_el_plan_con_la_opcion_apagada()
    {
        await AplicarFixtureAsync();
        var (plan, guionDelPlan) = Carril(PlanDelInforme, PlanDelInforme, PlanDelInforme);
        var (carrilSql, guionSql) = CarrilSql(plan, planCompilado: false, ProveedorGuionado.NoContestable());

        await carrilSql.ResponderAsync(Actor("global"), PreguntaDelInforme, null, TestContext.Current.CancellationToken);

        Assert.Equal(0, guionDelPlan.Llamadas);
        Assert.Equal(1, guionSql.Llamadas);
    }

    [Fact]
    public async Task El_carril_SQL_responde_con_el_plan_con_la_opcion_encendida()
    {
        await AplicarFixtureAsync();
        var (plan, guionDelPlan) = Carril(PlanDelInforme, PlanDelInforme, PlanDelInforme);
        var (carrilSql, guionSql) = CarrilSql(plan, planCompilado: true, ProveedorGuionado.NoContestable());

        var resultado = await carrilSql.ResponderAsync(
            Actor("global"), PreguntaDelInforme, null, TestContext.Current.CancellationToken);

        Assert.Equal(CarrilDelPlan.CategoriaRespondida, resultado.Categoria);
        Assert.Equal(3, guionDelPlan.Llamadas);
        Assert.Equal(0, guionSql.Llamadas);
    }

    // -------------------------------------------------------------------- apoyo

    private async Task AplicarFixtureAsync()
    {
        await using var conexion = new NpgsqlConnection(Cadena);
        await conexion.OpenAsync(TestContext.Current.CancellationToken);
        await using var comando = new NpgsqlCommand(
            new GeneradorDeFixture(conSuplementoCompuesto: true).Generar(), conexion)
        { CommandTimeout = 60 };

        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private EjecutorDeConsulta Ejecutor() =>
        new(Apertura, ClasificadorDeSensibilidad(), Options.Create(new OpcionesAsistente()));

    private static Guid Actor(string actor) => Guid.Parse(GeneradorDeFixture.IdDeUsuario(actor switch
    {
        "global" => 0,
        "carrera" => 1,
        "materia" => 2,
        _ => 3,
    }));

    private (CarrilDelPlan Carril, ProveedorGuionado Guion) Carril(params string[] muestras)
    {
        var guion = new ProveedorGuionado(muestras);
        var contador = new ContadorDeLlamadasDelTurno(4);
        var ejecutor = Ejecutor();

        var carril = new CarrilDelPlan(
            new GeneradorDePlan(new ProveedorConTechoDeLlamadas(guion, contador)),
            new ResolutorDeEntidadesDelPlan(ejecutor, new BuscadorDeMenciones(Apertura)),
            ejecutor,
            new FechaDeReferenciaFija(GeneradorDeFixture.Ancla),
            contador,
            Options.Create(new OpcionesAsistente()),
            NullLogger<CarrilDelPlan>.Instance);

        return (carril, guion);
    }

    private (CarrilSql Carril, ProveedorGuionado Guion) CarrilSql(
        CarrilDelPlan plan, bool planCompilado, params string[] respuestas)
    {
        var guion = new ProveedorGuionado(respuestas);
        var (basica, pii) = CadenasDeLectura();
        var opciones = Options.Create(new OpcionesAsistente { PlanCompilado = planCompilado });

        var carril = BancoDelAsistente.ArmarCarrilSql(
            basica, pii, Apertura, Ejecutor(), guion, new ContadorDeLlamadasDelTurno(4),
            Options.Create(new OpcionesAsistente()), opcionesDelRedactor: opciones, plan: plan);

        return (carril, guion);
    }

    private async Task<ResultadoDelTurno?> ResponderAsync(CarrilDelPlan carril, string pregunta, string actor = "global")
    {
        var ct = TestContext.Current.CancellationToken;
        var perfil = await new ConsultorDeAlcance(Apertura).ObtenerAsync(Actor(actor), ct);
        return await carril.ResponderAsync(Actor(actor), pregunta, perfil, ct);
    }

    private static string Mostrar(ResultadoDeConsulta resultado) =>
        $"[{string.Join("; ", resultado.Filas.Select(f => string.Join(",", f)))}]";
}
