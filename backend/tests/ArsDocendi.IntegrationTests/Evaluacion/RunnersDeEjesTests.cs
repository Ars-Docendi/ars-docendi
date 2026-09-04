using ArsDocendi.Evaluacion.Nucleo.Dataset;
using ArsDocendi.Evaluacion.Nucleo.Puntuacion;
using ArsDocendi.Evaluacion.Nucleo.Runner;
using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Evaluacion;

/// <summary>
/// Verifica los runners del eje social y del eje de diálogo contra una base real.
/// </summary>
/// <remarks>
/// El proveedor va guionado, así que lo que se prueba es el <b>criterio de
/// puntuación</b> de cada eje: qué cuenta como acierto, qué como fallo, y cuándo la
/// corrida entera se descarta. La calidad de las respuestas es otra cosa y necesita
/// un proveedor real.
/// </remarks>
[Collection(ColeccionPostgres.Nombre)]
public sealed class RunnersDeEjesTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "eval_ejes")
{
    private static readonly SelloDeIdentidad Sello = new("pre", "dat", "fix");
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");

    private const string ContarDocentes =
        "SELECT count(*) AS cantidad FROM designaciones.designaciones";

    // ----------------------------------------------------------- eje social

    [Fact]
    public async Task Un_saludo_a_costo_cero_aprueba_y_un_negativo_que_llega_al_modelo_tambien()
    {
        await SembrarAsync();
        var banco = Banco(out var medidor, GuionDeTurnos(4));

        var resultado = await Social(banco, medidor).CorrerAsync(
            DatasetSocial.Interpretar("""
                {"items": [
                  {"id": "s1", "clase": "social", "actor": "global", "pregunta": "hola"},
                  {"id": "n1", "clase": "negativo", "actor": "global",
                   "pregunta": "¿cuántos docentes están designados?"}
                ]}
                """),
            Sello,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, resultado.Codigo);
        Assert.NotNull(resultado.Reporte);
        Assert.All(resultado.Reporte!.Resultados, r =>
            Assert.Equal(DesenlaceDeItem.TraduccionCorrecta, r.Desenlace));
    }

    [Fact]
    public async Task Un_negativo_que_el_enrutador_captura_falla()
    {
        // ES EL MODO DE FALLA QUE ESTE EJE EXISTE PARA DETECTAR: comerse una pregunta
        // legítima. Un enrutador que captura de más da un eje social perfecto y un
        // asistente inútil.
        await SembrarAsync();
        var banco = Banco(out var medidor, GuionDeTurnos(2));

        var resultado = await Social(banco, medidor).CorrerAsync(
            DatasetSocial.Interpretar("""
                {"items": [
                  {"id": "n1", "clase": "negativo", "actor": "global", "pregunta": "gracias"},
                  {"id": "n2", "clase": "negativo", "actor": "global",
                   "pregunta": "¿cuántos docentes están designados?"}
                ]}
                """),
            Sello,
            TestContext.Current.CancellationToken);

        var capturado = Assert.Single(resultado.Reporte!.Resultados, r => r.Id == "n1");

        Assert.Equal(DesenlaceDeItem.IntentoSobreLoInfactible, capturado.Desenlace);
        Assert.Contains("capturó", capturado.Detalle, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Un_no_contestable_sin_sugerencias_falla()
    {
        // Abstenerse no alcanza: un «no puedo» sin salida deja al usuario sin nada
        // que hacer, y el rechazo cooperativo existe justamente para eso.
        await SembrarAsync();
        var banco = Banco(out var medidor,
            [ProveedorGuionado.NoContestable(), .. GuionDeTurnos(2)]);

        var resultado = await Social(banco, medidor).CorrerAsync(
            DatasetSocial.Interpretar("""
                {"items": [
                  {"id": "nc1", "clase": "no_contestable", "actor": "global",
                   "pregunta": "¿cuál es el sueldo de cada profesor?"},
                  {"id": "n1", "clase": "negativo", "actor": "global",
                   "pregunta": "¿cuántos docentes están designados?"}
                ]}
                """),
            Sello,
            TestContext.Current.CancellationToken);

        var abstenido = Assert.Single(resultado.Reporte!.Resultados, r => r.Id == "nc1");

        // El carril SÍ sugiere hoy, así que este ítem aprueba. Lo que el test fija es
        // que el criterio EXIGE las sugerencias: sin ellas, el desenlace sería otro.
        Assert.Equal(DesenlaceDeItem.AbstencionCorrecta, abstenido.Desenlace);
        Assert.Contains("sugirió", abstenido.Detalle, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Con_todos_los_turnos_a_cero_el_runner_aborta_y_no_deja_reporte()
    {
        // LA TRAMPA, CUBIERTA. Un proveedor caído consume cero tokens en todos los
        // ítems y la corrida daría verde perfecto. Con el corte abierto, ningún turno
        // llega al modelo — que es exactamente la firma de no tener proveedor.
        await SembrarAsync();
        var banco = Banco(
            out var medidor,
            GuionDeTurnos(2),
            new OpcionesAsistente { FallosParaAbrirElBreaker = 1, CupoDeLlamadasPorActor = 0 });

        banco.Breaker.Fallo();

        var resultado = await Social(banco, medidor).CorrerAsync(
            DatasetSocial.Interpretar("""
                {"items": [
                  {"id": "s1", "clase": "social", "actor": "global", "pregunta": "hola"},
                  {"id": "n1", "clase": "negativo", "actor": "global",
                   "pregunta": "¿cuántos docentes están designados?"}
                ]}
                """),
            Sello,
            TestContext.Current.CancellationToken);

        Assert.Equal(RunnerSocial.CodigoDeCorridaSinProveedor, resultado.Codigo);
        Assert.False(resultado.HayReporte);
        Assert.Contains("no hay proveedor", resultado.Motivo!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task El_techo_de_llamadas_es_por_item_y_no_por_corrida()
    {
        // DEFECTO ENCONTRADO AL ESCRIBIR ESTOS TESTS, y fijado acá para que no
        // vuelva. `ContadorDeLlamadasDelTurno` es POR TURNO: en producción vive con
        // el alcance del request. Los runners sostenían UNA instancia del pipeline
        // para todo el dataset, así que ese techo —cuatro llamadas— funcionaba como
        // techo de la corrida entera: el tercer ítem ya lo había agotado, resolvía
        // degradado, y el eje reportaba fallo casi total.
        //
        // El modo de falla es especialmente malo porque NO da error: da un número.
        await SembrarAsync();

        // Techo de dos: con una sola instancia compartida, el segundo ítem ya no
        // tendría cupo.
        var banco = Banco(
            out var medidor,
            GuionDeTurnos(4),
            new OpcionesAsistente { CupoDeLlamadasPorActor = 0, MaximoDeLlamadasPorTurno = 2 });

        var resultado = await Social(banco, medidor).CorrerAsync(
            DatasetSocial.Interpretar("""
                {"items": [
                  {"id": "n1", "clase": "negativo", "actor": "global",
                   "pregunta": "¿cuántos docentes están designados?"},
                  {"id": "n2", "clase": "negativo", "actor": "global",
                   "pregunta": "¿cuántos docentes están designados?"},
                  {"id": "n3", "clase": "negativo", "actor": "global",
                   "pregunta": "¿cuántos docentes están designados?"}
                ]}
                """),
            Sello,
            TestContext.Current.CancellationToken);

        Assert.All(resultado.Reporte!.Resultados, r =>
            Assert.Equal(DesenlaceDeItem.TraduccionCorrecta, r.Desenlace));
    }

    // ---------------------------------------------------------- eje de diálogo

    [Fact]
    public async Task Los_turnos_de_un_dialogo_comparten_hilo()
    {
        await SembrarAsync();
        var banco = Banco(out _, GuionDeSeguimiento());

        var resultado = await Dialogo(banco).CorrerAsync(
            DatasetDeDialogo.Interpretar($$"""
                {"dialogos": [
                  {"id": "d1", "actor": "global", "es_pivote_duro": true, "turnos": [
                    {"pregunta": "¿cuántos docentes están designados?",
                     "sql_referencia": {{Json(ContarDocentes)}}},
                    {"pregunta": "¿y en Sistemas?",
                     "sql_referencia": {{Json(ContarDocentes)}},
                     "terminos_prohibidos": ["zzz-imposible"]}
                  ]}
                ]}
                """),
            Sello,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, resultado.Codigo);
        Assert.Equal(2, resultado.Reporte!.Resultados.Count);

        // El segundo turno pagó el reescritor, que es la prueba de que hubo hilo: sin
        // historial vigente, el reescritor no se llama. Uno del preflight, dos del
        // primer turno y tres del segundo.
        Assert.Equal(6, banco.Proveedor.Llamadas);
    }

    [Fact]
    public async Task Un_termino_arrastrado_hace_fallar_el_turno_aunque_el_resultado_sea_correcto()
    {
        // EL CHEQUEO NEGATIVO VA PRIMERO, y por eso. Un turno que arrastra puede
        // devolver el resultado correcto de casualidad —o porque el filtro arrastrado
        // no cambiaba nada en este fixture— y contarlo como acierto escondería el
        // defecto que el eje existe para ver.
        await SembrarAsync();
        var banco = Banco(out _, GuionDeSeguimiento());

        var resultado = await Dialogo(banco).CorrerAsync(
            DatasetDeDialogo.Interpretar($$"""
                {"dialogos": [
                  {"id": "d1", "actor": "global", "es_pivote_duro": true, "turnos": [
                    {"pregunta": "¿cuántos docentes están designados?",
                     "sql_referencia": {{Json(ContarDocentes)}}},
                    {"pregunta": "¿y en Sistemas?",
                     "sql_referencia": {{Json(ContarDocentes)}},
                     "terminos_prohibidos": ["Sistemas"]}
                  ]}
                ]}
                """),
            Sello,
            TestContext.Current.CancellationToken);

        var segundo = Assert.Single(resultado.Reporte!.Resultados, r => r.Id == "d1#2");

        Assert.Equal(DesenlaceDeItem.TraduccionIncorrecta, segundo.Desenlace);
        Assert.Contains("Arrastró", segundo.Detalle, StringComparison.Ordinal);

        // Y el primero conserva lo suyo.
        Assert.Equal(
            DesenlaceDeItem.TraduccionCorrecta,
            Assert.Single(resultado.Reporte.Resultados, r => r.Id == "d1#1").Desenlace);
    }

    [Fact]
    public async Task Un_turno_caido_no_invalida_los_anteriores_y_corta_el_dialogo()
    {
        await SembrarAsync();
        // Techo de dos llamadas: el primer turno entra justo —generación y
        // redacción— y el segundo no, porque además paga el reescritor.
        var banco = Banco(
            out _,
            GuionDeSeguimiento(),
            new OpcionesAsistente { CupoDeLlamadasPorActor = 0, MaximoDeLlamadasPorTurno = 2 });

        var resultado = await Dialogo(banco).CorrerAsync(
            DatasetDeDialogo.Interpretar($$"""
                {"dialogos": [
                  {"id": "d1", "actor": "global", "es_pivote_duro": true, "turnos": [
                    {"pregunta": "¿cuántos docentes están designados?",
                     "sql_referencia": {{Json(ContarDocentes)}}},
                    {"pregunta": "¿y en Sistemas?",
                     "sql_referencia": {{Json(ContarDocentes)}},
                     "terminos_prohibidos": ["zzz-imposible"]},
                    {"pregunta": "¿y en Industrial?",
                     "sql_referencia": {{Json(ContarDocentes)}},
                     "terminos_prohibidos": ["zzz-imposible"]}
                  ]}
                ]}
                """),
            Sello,
            TestContext.Current.CancellationToken);

        // El segundo turno agota el techo del turno y resuelve degradado; el diálogo
        // se corta ahí y el tercero no se evalúa. Lo que el test fija es que el
        // primero conserva su desenlace: un turno caído no invalida los anteriores.
        Assert.Equal(2, resultado.Reporte!.Resultados.Count);
        Assert.Equal(
            DesenlaceDeItem.TraduccionCorrecta,
            resultado.Reporte.Resultados[0].Desenlace);
        Assert.Equal(DesenlaceDeItem.Fallo, resultado.Reporte.Resultados[1].Desenlace);
    }

    // ------------------------------------------------------------------ apoyo

    private static string Json(string valor) => System.Text.Json.JsonSerializer.Serialize(valor);

    /// <summary>
    /// Antepone la respuesta que consume el preflight.
    /// </summary>
    /// <remarks>
    /// El preflight pide una completación trivial ANTES de evaluar nada, y esa
    /// llamada sale del mismo guion. Sin esta línea, el primer turno de cada diálogo
    /// recibe la respuesta pensada para el segundo y termina no contestable por una
    /// razón que no es la que el test mide — que es exactamente lo que pasó al
    /// escribirlos.
    /// </remarks>
    private static string[] ConPreflight(params string[] guion) => ["ping del preflight", .. guion];

    private static string[] GuionDeTurnos(int turnos) =>
        ConPreflight([.. Enumerable.Range(0, turnos).SelectMany(_ => new[]
        {
            ProveedorGuionado.Generacion(ContarDocentes),
            "Hay 4 docentes designados.",
        })]);

    /// <summary>Guion de un seguimiento: el segundo turno paga además el reescritor.</summary>
    private static string[] GuionDeSeguimiento() =>
        ConPreflight(
            ProveedorGuionado.Generacion(ContarDocentes),
            "Hay 4 docentes designados.",
            "¿cuántos docentes están designados en Sistemas?",
            ProveedorGuionado.Generacion(ContarDocentes),
            "Hay 2 docentes designados.");

    private BancoDelAsistente Banco(
        out MedidorDeConsumo medidor,
        string[] guion,
        OpcionesAsistente? configuracion = null)
    {
        var (basica, pii) = CadenasDeLectura();
        MedidorDeConsumo? capturado = null;

        var banco = BancoDelAsistente.Armar(
            basica,
            pii,
            ClasificadorDeSensibilidad(),
            configuracion ?? new OpcionesAsistente { CupoDeLlamadasPorActor = 0 },
            // No simulado: el preflight rechaza a los proveedores simulados, y lo que
            // estos tests miden es el criterio de puntuación de cada eje. El preflight
            // tiene sus propios tests.
            proveedor: new ProveedorGuionado(guion) { EsSimulado = false },
            envolver: interno => capturado = new MedidorDeConsumo(interno));

        medidor = capturado
            ?? throw new InvalidOperationException("El banco no llamó al envoltorio.");

        return banco;
    }

    private static RunnerSocial Social(BancoDelAsistente banco, MedidorDeConsumo medidor) =>
        new(banco.Capa, new ActoresDelSeed(), medidor);

    private RunnerDeDialogo Dialogo(BancoDelAsistente banco)
    {
        var (basica, pii) = CadenasDeLectura();

        return new RunnerDeDialogo(
            banco.Capa,
            new EjecutorDeConsulta(
                basica, pii, ClasificadorDeSensibilidad(),
                Microsoft.Extensions.Options.Options.Create(new OpcionesAsistente())),
            new ActoresDelSeed(),
            banco.Proveedor);
    }

    private sealed class ActoresDelSeed : IResolutorDeActores
    {
        public Guid Resolver(string actor) => actor switch
        {
            ActorDeItem.Global => Secretaria,
            ActorDeItem.Carrera => Guid.Parse("a0000000-0000-4000-8000-000000000003"),
            ActorDeItem.Materia => Guid.Parse("a0000000-0000-4000-8000-000000000002"),
            ActorDeItem.SinPermiso => Guid.Parse("a0000000-0000-4000-8000-000000000001"),
            _ => throw new ArgumentOutOfRangeException(nameof(actor), actor, null),
        };
    }

    private async Task SembrarAsync()
    {
        var sql = await File.ReadAllTextAsync(
            Path.Combine(RaizRepositorio.Ruta(), "infra", "scripts", "seed-data", "sintetico.sql"),
            TestContext.Current.CancellationToken);

        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(sql, conexion) { CommandTimeout = 60 };
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
