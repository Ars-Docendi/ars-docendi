using ArsDocendi.Evaluacion.Nucleo.Dataset;
using ArsDocendi.Evaluacion.Nucleo.Puntuacion;
using ArsDocendi.Evaluacion.Nucleo.Runner;
using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Evaluacion;

/// <summary>
/// Verifica el preflight y el runner, incluida la trampa que los motiva.
/// </summary>
/// <remarks>
/// <b>Sin crédito de API el eval no falla: miente.</b> El request devuelve una
/// abstención con error seteado y métricas en cero, los ítems no contestables
/// pasan espuriamente y el reporte muestra un número bajo que parece una regresión
/// del modelo. Los tests de acá son los que impiden que eso llegue a disco.
/// </remarks>
[Collection(ColeccionPostgres.Nombre)]
public sealed class RunnerYPreflightTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_eval")
{
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");

    private const string DatasetMinimo = """
        {"items":[
          {"id":"cap-x1","pregunta":"¿Cuántos pedidos hay registrados?","categoria":"agregacion",
           "actor":"global","sql_referencia":"SELECT count(*) AS total FROM designaciones.pedidos"},
          {"id":"cap-x2","pregunta":"¿Cuál es el sueldo de cada docente?","categoria":"no_contestable",
           "actor":"global","sql_referencia":null}]}
        """;

    private static readonly SelloDeIdentidad Sello = new("prefijo-x", "dataset-x", "fixture-x");

    // --------------------------------------------------------- el preflight

    [Fact]
    public async Task Un_proveedor_simulado_se_rechaza()
    {
        // El caso de olvidarse de configurar el proveedor real. Se corta ANTES de
        // gastar la llamada: la bandera ya lo dice.
        var veredicto = await Preflight.VerificarAsync(
            new ProveedorDeCortesia { Simulado = true }, TestContext.Current.CancellationToken);

        Assert.False(veredicto.Aprobado);
        Assert.Contains("simulado", veredicto.Motivo!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Un_proveedor_caido_se_rechaza()
    {
        var veredicto = await Preflight.VerificarAsync(
            new ProveedorDeCortesia { Falla = new HttpRequestException("sin ruta") },
            TestContext.Current.CancellationToken);

        Assert.False(veredicto.Aprobado);
        Assert.Contains("no respondió", veredicto.Motivo!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Una_respuesta_con_metricas_en_cero_se_rechaza()
    {
        // EL CASO QUE ENGAÑA: hay proveedor, contesta, y no cobra nada porque no
        // procesó nada. Entrada y salida en cero con latencias de milisegundos en
        // vez de segundos es la firma de una cuenta sin crédito.
        var veredicto = await Preflight.VerificarAsync(
            new ProveedorDeCortesia { TokensDeEntrada = 0, TokensDeSalida = 0 },
            TestContext.Current.CancellationToken);

        Assert.False(veredicto.Aprobado);
        Assert.Contains("métricas en cero", veredicto.Motivo!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Una_respuesta_con_salida_en_cero_se_rechaza()
    {
        var veredicto = await Preflight.VerificarAsync(
            new ProveedorDeCortesia { TokensDeEntrada = 120, TokensDeSalida = 0 },
            TestContext.Current.CancellationToken);

        Assert.False(veredicto.Aprobado);
    }

    [Fact]
    public async Task Un_proveedor_que_responde_de_verdad_aprueba()
    {
        var veredicto = await Preflight.VerificarAsync(
            new ProveedorDeCortesia(), TestContext.Current.CancellationToken);

        Assert.True(veredicto.Aprobado, veredicto.Motivo);
    }

    // ---------------------------------------- el runner no deja reporte falso

    [Fact]
    public async Task Con_el_preflight_fallido_no_hay_reporte()
    {
        await SembrarAsync();
        var proveedor = new ProveedorDeCortesia { TokensDeEntrada = 0, TokensDeSalida = 0 };

        var corrida = await CorrerAsync(proveedor, DatasetMinimo);

        // Un reporte escrito sobre una corrida inválida es peor que no tener
        // reporte: el que no existe se nota, el que miente no.
        Assert.NotEqual(0, corrida.Codigo);
        Assert.False(corrida.HayReporte);
        Assert.NotNull(corrida.Motivo);
    }

    [Fact]
    public async Task Con_el_preflight_fallido_no_se_evalua_ningun_item()
    {
        await SembrarAsync();
        var proveedor = new ProveedorDeCortesia { Falla = new HttpRequestException("sin ruta") };

        await CorrerAsync(proveedor, DatasetMinimo);

        // Ni siquiera se intenta: la corrida corta antes del primer ítem.
        Assert.Equal(1, proveedor.Llamadas);
    }

    [Fact]
    public async Task Con_el_preflight_fallido_un_reporte_anterior_queda_intacto()
    {
        await SembrarAsync();
        var directorio = Directory.CreateTempSubdirectory("reportes-eval").FullName;
        var archivo = Path.Combine(directorio, "capacidad.md");
        const string anterior = "# Reporte de una corrida válida anterior";

        try
        {
            await File.WriteAllTextAsync(
                archivo, anterior, TestContext.Current.CancellationToken);

            var corrida = await CorrerAsync(
                new ProveedorDeCortesia { Simulado = true }, DatasetMinimo);

            if (corrida.HayReporte)
            {
                await File.WriteAllTextAsync(
                    archivo, corrida.Reporte!.Renderizar(), TestContext.Current.CancellationToken);
            }

            Assert.Equal(
                anterior,
                await File.ReadAllTextAsync(archivo, TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(directorio, recursive: true);
        }
    }

    [Fact]
    public async Task El_preflight_no_se_cuenta_como_item()
    {
        await SembrarAsync();
        var proveedor = new ProveedorDeCortesia();

        var corrida = await CorrerAsync(proveedor, DatasetMinimo);

        Assert.True(corrida.HayReporte, corrida.Motivo);
        Assert.Equal(2, corrida.Reporte!.Total);
    }

    // ------------------------------------------------------------ el reporte

    [Fact]
    public async Task El_reporte_trae_los_tres_hashes()
    {
        await SembrarAsync();

        var corrida = await CorrerAsync(new ProveedorDeCortesia(), DatasetMinimo);
        var texto = corrida.Reporte!.Renderizar();

        Assert.Contains(Sello.Prefijo, texto, StringComparison.Ordinal);
        Assert.Contains(Sello.Dataset, texto, StringComparison.Ordinal);
        Assert.Contains(Sello.Fixture, texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_total_del_reporte_sale_del_dataset_cargado()
    {
        await SembrarAsync();
        var dataset = DatasetDeCapacidad.Interpretar(DatasetMinimo);

        var corrida = await CorrerAsync(new ProveedorDeCortesia(), DatasetMinimo);

        // Del archivo, no de prosa: un reporte cuyos números no se derivan de lo
        // que se ejecutó es un reporte que puede mentir sin que nada falle.
        Assert.Equal(dataset.Items.Count, corrida.Reporte!.Total);
    }

    [Fact]
    public async Task Los_conteos_por_categoria_suman_el_total()
    {
        await SembrarAsync();

        var corrida = await CorrerAsync(new ProveedorDeCortesia(), DatasetMinimo);

        Assert.Equal(
            corrida.Reporte!.Total,
            corrida.Reporte.ConteoPorCategoria.Values.Sum());
    }

    [Fact]
    public async Task El_reporte_dice_que_es_generado()
    {
        await SembrarAsync();

        var corrida = await CorrerAsync(new ProveedorDeCortesia(), DatasetMinimo);

        Assert.Contains("generado", corrida.Reporte!.Renderizar(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dos_corridas_con_proveedor_determinista_dan_el_mismo_reporte()
    {
        await SembrarAsync();

        var primera = await CorrerAsync(new ProveedorDeCortesia(), DatasetMinimo);
        var segunda = await CorrerAsync(new ProveedorDeCortesia(), DatasetMinimo);

        Assert.Equal(primera.Reporte!.Renderizar(), segunda.Reporte!.Renderizar());
    }

    // ------------------------------------------------- los desenlaces reales

    [Fact]
    public async Task Un_turno_degradado_cuenta_como_fallo_y_no_como_abstencion()
    {
        await SembrarAsync();

        // El proveedor aprueba el preflight y después se cae: es el caso en que la
        // cuota se agota a mitad de la corrida. Los ítems infactibles NO pueden
        // acreditarse por eso.
        var proveedor = new ProveedorDeCortesia { FallaDespuesDe = 1 };

        var corrida = await CorrerAsync(proveedor, DatasetMinimo);

        Assert.True(corrida.HayReporte, corrida.Motivo);
        Assert.Equal(2, corrida.Reporte!.Conteos[DesenlaceDeItem.Fallo]);
        Assert.Equal(0, corrida.Reporte.Conteos[DesenlaceDeItem.AbstencionCorrecta]);
    }

    [Fact]
    public async Task Una_traduccion_correcta_se_acredita()
    {
        await SembrarAsync();

        // El proveedor devuelve exactamente la consulta de referencia del primer
        // ítem, y se abstiene en el segundo: la corrida perfecta.
        var proveedor = new ProveedorDeCortesia
        {
            Generaciones =
            [
                "SELECT count(*) AS total FROM designaciones.pedidos",
                null,
            ],
        };

        var corrida = await CorrerAsync(proveedor, DatasetMinimo);

        Assert.Equal(1, corrida.Reporte!.Conteos[DesenlaceDeItem.TraduccionCorrecta]);
        Assert.Equal(1, corrida.Reporte.Conteos[DesenlaceDeItem.AbstencionCorrecta]);
        Assert.All(corrida.Reporte.Puntajes, puntaje => Assert.Equal(1m, puntaje.Normalizado));
    }

    [Fact]
    public async Task Una_traduccion_incorrecta_se_castiga()
    {
        await SembrarAsync();

        var proveedor = new ProveedorDeCortesia
        {
            Generaciones =
            [
                // Cuenta materias en vez de pedidos: consulta válida, respuesta falsa.
                "SELECT count(*) AS total FROM identity.materias",
                null,
            ],
        };

        var corrida = await CorrerAsync(proveedor, DatasetMinimo);

        Assert.Equal(1, corrida.Reporte!.Conteos[DesenlaceDeItem.TraduccionIncorrecta]);
        Assert.True(corrida.Reporte.Puntajes[0].Puntaje < 1m);
    }

    [Fact]
    public async Task Responder_algo_infactible_se_castiga()
    {
        await SembrarAsync();

        var proveedor = new ProveedorDeCortesia
        {
            Generaciones =
            [
                "SELECT count(*) AS total FROM designaciones.pedidos",
                // Se inventa una respuesta para la pregunta sobre sueldos.
                "SELECT count(*) AS total FROM identity.personas",
            ],
        };

        var corrida = await CorrerAsync(proveedor, DatasetMinimo);

        Assert.Equal(1, corrida.Reporte!.Conteos[DesenlaceDeItem.IntentoSobreLoInfactible]);
    }

    // ----------------------------------------------- el fixture contra la base

    [Fact]
    public async Task El_fixture_generado_aplica_sobre_una_base_migrada()
    {
        // Un fixture que no aplica es un dataset que no se puede correr, y el
        // generador puede romperse por cosas que ningún test de texto ve: una
        // clave foránea que no resuelve, un CHECK que rechaza un valor, un
        // identificador de cargo que no existe.
        var fixture = new ArsDocendi.Evaluacion.Nucleo.Fixture.GeneradorDeFixture().Generar();

        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(fixture, conexion) { CommandTimeout = 60 };

        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task El_fixture_es_idempotente()
    {
        var fixture = new ArsDocendi.Evaluacion.Nucleo.Fixture.GeneradorDeFixture().Generar();
        var ct = TestContext.Current.CancellationToken;

        await using var conexion = await AbrirConexionAsync();

        for (var vez = 0; vez < 2; vez++)
        {
            await using var comando = new NpgsqlCommand(fixture, conexion) { CommandTimeout = 60 };
            await comando.ExecuteNonQueryAsync(ct);
        }

        await using var contar = new NpgsqlCommand(
            "SELECT count(*) FROM identity.personas", conexion);

        // Aplicarlo dos veces no duplica nada: todos los INSERT llevan
        // ON CONFLICT DO NOTHING y los identificadores son deterministas.
        Assert.Equal(24L, await contar.ExecuteScalarAsync(ct));
    }

    [Fact]
    public async Task Las_colisiones_del_fixture_existen_en_la_base()
    {
        // La verificación de texto dice que el SQL las declara; ésta dice que la
        // base las tiene. Son cosas distintas: un ON CONFLICT mal puesto podría
        // descartar la segunda materia repetida sin que el texto cambie.
        var fixture = new ArsDocendi.Evaluacion.Nucleo.Fixture.GeneradorDeFixture().Generar();
        var ct = TestContext.Current.CancellationToken;

        await using var conexion = await AbrirConexionAsync();
        await using (var aplicar = new NpgsqlCommand(fixture, conexion) { CommandTimeout = 60 })
        {
            await aplicar.ExecuteNonQueryAsync(ct);
        }

        await using var materias = new NpgsqlCommand(
            """
            SELECT count(*) FROM identity.materias
             WHERE name = 'Análisis Matemático'
            """, conexion);

        await using var personas = new NpgsqlCommand(
            "SELECT count(*) FROM identity.personas WHERE apellido = 'Gómez'", conexion);

        Assert.Equal(3L, await materias.ExecuteScalarAsync(ct));
        Assert.Equal(3L, await personas.ExecuteScalarAsync(ct));
    }

    // ------------------------------------------------------------------ apoyo

    private async Task<ResultadoDeCorrida> CorrerAsync(ProveedorDeCortesia proveedor, string dataset)
    {
        var (basica, conDatosPersonales) = CadenasDeLectura();
        var opciones = Options.Create(new OpcionesAsistente());
        var contador = new ContadorDeLlamadasDelTurno(64);
        var conTecho = new ProveedorConTechoDeLlamadas(proveedor, contador);
        var ejecutor = new EjecutorDeConsulta(basica, conDatosPersonales, ClasificadorDeSensibilidad(), opciones);

        // Una fábrica y no una instancia: el techo de llamadas es POR TURNO, y un
        // carril compartido para todo el dataset lo convertiría en un techo de la
        // corrida entera —el tercer ítem lo agotaría y todos los siguientes
        // resolverían degradado—. El modo de falla no da error: da un número.
        CarrilSql Carril() => new(
            new GeneradorDeSql(
                new ProveedorDeEsquema(basica, conDatosPersonales),
                new SelectorDeEjemplos(),
                conTecho,
                new FechaDeReferenciaFija(new DateOnly(2026, 3, 2))),
            ejecutor,
            new ConsultorDeAlcance(basica),
            new RedactorDeRespuesta(conTecho),
            new SelectorDeEjemplos(),
            contador,
            NullLogger<CarrilSql>.Instance);

        var runner = new RunnerDeCapacidad(Carril, ejecutor, new ActoresDePrueba(), proveedor);

        return await runner.CorrerAsync(
            DatasetDeCapacidad.Interpretar(dataset), Sello, TestContext.Current.CancellationToken);
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

    /// <summary>Resuelve los actores del seed sintético.</summary>
    private sealed class ActoresDePrueba : IResolutorDeActores
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

    /// <summary>
    /// Proveedor que aprueba el preflight y devuelve las generaciones guionadas.
    /// </summary>
    /// <remarks>
    /// Se llama «de cortesía» y no «simulado» a propósito: el preflight rechaza
    /// cualquier proveedor que se declare simulado, así que un doble de test que lo
    /// hiciera no podría ejercitar el camino feliz.
    /// </remarks>
    private sealed class ProveedorDeCortesia : IProveedorDeModelo
    {
        private int _llamadas;

        public string Nombre => "cortesia";

        public bool EsSimulado => Simulado;

        /// <summary>Si se declara simulado, para probar que el preflight lo rechaza.</summary>
        public bool Simulado { get; init; }

        public int TokensDeEntrada { get; init; } = 120;

        public int TokensDeSalida { get; init; } = 40;

        /// <summary>Excepción que lanza siempre, si se le pone una.</summary>
        public Exception? Falla { get; init; }

        /// <summary>A partir de qué llamada empieza a fallar. Cero es «nunca».</summary>
        public int FallaDespuesDe { get; init; }

        /// <summary>
        /// Consultas a devolver, en orden. Un nulo significa abstenerse.
        /// </summary>
        public IReadOnlyList<string?> Generaciones { get; init; } = [];

        public int Llamadas => _llamadas;

        public Task<RespuestaDelModelo> CompletarAsync(
            SolicitudAlModelo solicitud, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(solicitud);
            _llamadas++;

            if (Falla is not null)
            {
                throw Falla;
            }

            if (FallaDespuesDe > 0 && _llamadas > FallaDespuesDe)
            {
                throw new HttpRequestException("la cuota se agotó a mitad de la corrida");
            }

            return Task.FromResult(new RespuestaDelModelo(
                Responder(solicitud), TokensDeEntrada, TokensDeSalida, EsSimulada: false));
        }

        /// <summary>
        /// Devuelve lo que corresponda según qué llamada sea.
        /// </summary>
        /// <remarks>
        /// Distingue generación de redacción por el PREFIJO de la solicitud y no
        /// contando llamadas. Contar sería frágil: el carril puede gastar un
        /// reintento de generación ante un resultado vacío, y el guion se
        /// desalinearía sin que el test lo dijera.
        /// </remarks>
        private string Responder(SolicitudAlModelo solicitud)
        {
            if (solicitud.PrefijoEstable == RedactorDeRespuesta.Instrucciones)
            {
                return "Respuesta redactada sobre las filas.";
            }

            if (!solicitud.PrefijoEstable.Contains(
                "ESQUEMA DISPONIBLE", StringComparison.Ordinal))
            {
                // El preflight: no lleva esquema ni instrucciones de redacción.
                return "listo";
            }

            if (_generaciones >= Generaciones.Count)
            {
                return ProveedorGuionado.NoContestable();
            }

            var guionada = Generaciones[_generaciones];
            _generaciones++;

            return guionada is null
                ? ProveedorGuionado.NoContestable()
                : ProveedorGuionado.Generacion(guionada);
        }

        private int _generaciones;
    }
}
