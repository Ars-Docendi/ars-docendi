using ArsDocendi.IntegrationTests.Infraestructura;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica el carril SQL de punta a punta, contra una base real.
/// </summary>
/// <remarks>
/// El proveedor va guionado: devuelve la consulta que se quiere ejercitar. Así se
/// prueba todo el carril —generación, validación, ejecución acotada, guard de
/// vacío, reintento y redacción— salvo la calidad de la traducción, que es lo que
/// mide la épica de evaluación y lo único que necesita una clave real.
///
/// El gate de la épica es el primer test: una pregunta de cobertura de cátedra
/// responde correctamente y acotada al alcance del actor.
/// </remarks>
[Collection(ColeccionPostgres.Nombre)]
public sealed class CarrilSqlTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_carril")
{
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private static readonly Guid Coordinador = Guid.Parse("a0000000-0000-4000-8000-000000000003");
    private static readonly Guid Docente = Guid.Parse("a0000000-0000-4000-8000-000000000001");

    private const string ContarPedidos = "SELECT count(*) AS cantidad FROM designaciones.pedidos";

    private const string CoberturaDeCatedra = """
        SELECT p.apellido, p.nombre, c.nombre AS cargo, d.horas
          FROM designaciones.designaciones d
          JOIN identity.personas p ON p.id = d.persona_id
          JOIN identity.materias m ON m.id = d.materia_id
          JOIN designaciones.cargos c ON c.id = d.cargo_id
         WHERE d.vigente_hasta IS NULL
         ORDER BY c.orden, p.apellido
        """;

    // ------------------------------------------------------- gate de la épica

    [Fact]
    public async Task Una_pregunta_de_cobertura_de_catedra_responde_acotada_al_actor()
    {
        await SembrarAsync();

        var (global, _) = await PreguntarAsync(
            Secretaria, "¿Qué docentes están designados?", CoberturaDeCatedra);
        var (deCarrera, _) = await PreguntarAsync(
            Coordinador, "¿Qué docentes están designados?", CoberturaDeCatedra);

        Assert.Equal(EstadoDelTurno.Respondida, global.Estado);
        Assert.Equal(EstadoDelTurno.Respondida, deCarrera.Estado);

        // La misma consulta, dos alcances. El filtrado no lo hace la consulta: lo
        // hace el motor, con las policies y el actor fijado.
        Assert.NotEmpty(global.Filas);
        Assert.True(
            deCarrera.Filas.Count < global.Filas.Count,
            $"El coordinador vio {deCarrera.Filas.Count} y la secretaría {global.Filas.Count}.");
    }

    [Fact]
    public async Task La_respuesta_trae_las_columnas_y_el_razonamiento()
    {
        await SembrarAsync();

        var (turno, _) = await PreguntarAsync(
            Secretaria, "¿Cuántos pedidos hay?", ContarPedidos, "Conté los pedidos del sistema.");

        Assert.Equal(["cantidad"], turno.Columnas);
        Assert.Equal("Conté los pedidos del sistema.", turno.Razonamiento);
    }

    // ------------------------------------------------------------ abstención

    [Fact]
    public async Task Una_pregunta_no_contestable_corta_el_turno()
    {
        await SembrarAsync();
        var proveedor = new ProveedorGuionado(ProveedorGuionado.NoContestable());

        var turno = await CarrilCon(proveedor).ResponderAsync(
            Secretaria, "¿Cuánto gana cada docente?", null, TestContext.Current.CancellationToken);

        // Una sola llamada: no hay segunda porque no hay nada que redactar.
        Assert.Equal(EstadoDelTurno.NoContestable, turno.Estado);
        Assert.Equal(1, proveedor.Llamadas);
        Assert.Empty(turno.Filas);
    }

    [Fact]
    public async Task El_razonamiento_sobrevive_a_la_abstencion()
    {
        await SembrarAsync();
        var proveedor = new ProveedorGuionado(
            ProveedorGuionado.NoContestable("Eso no está entre los datos que puedo consultar."));

        var turno = await CarrilCon(proveedor).ResponderAsync(
            Secretaria, "¿Cuánto gana cada docente?", null, TestContext.Current.CancellationToken);

        Assert.Equal("Eso no está entre los datos que puedo consultar.", turno.Razonamiento);
    }

    [Fact]
    public async Task Una_consulta_rechazada_por_el_validador_no_se_reintenta_a_ciegas()
    {
        await SembrarAsync();
        var proveedor = new ProveedorGuionado(ProveedorGuionado.Generacion(
            """SELECT "set_config"('app.asistente_user_id','x',true)"""));

        var turno = await CarrilCon(proveedor).ResponderAsync(
            Secretaria, "¿Cuántos pedidos hay?", null, TestContext.Current.CancellationToken);

        // Volver a generar sobre el mismo prompt gasta una llamada para obtener,
        // con alta probabilidad, lo mismo.
        Assert.Equal(EstadoDelTurno.NoContestable, turno.Estado);
        Assert.Equal(1, proveedor.Llamadas);
    }

    [Fact]
    public async Task El_ataque_del_prototipo_no_obtiene_alcance_global()
    {
        await SembrarAsync();
        var proveedor = new ProveedorGuionado(ProveedorGuionado.Generacion(
            $"""
            SELECT count(*) AS cantidad
              FROM designaciones.pedidos
              CROSS JOIN LATERAL (
                  SELECT "set_config"('app.asistente_user_id','{Secretaria}',true)
              ) AS colado
            """));

        var turno = await CarrilCon(proveedor).ResponderAsync(
            Coordinador, "¿Cuántos pedidos hay?", null, TestContext.Current.CancellationToken);

        // El coordinador ve siete; la secretaría ocho. El ataque intentaba
        // fijarse el actor de la secretaría desde adentro de la consulta.
        Assert.Equal(EstadoDelTurno.NoContestable, turno.Estado);
        Assert.Empty(turno.Filas);
    }

    [Fact]
    public async Task El_texto_del_rechazo_no_nombra_la_consulta()
    {
        await SembrarAsync();
        var proveedor = new ProveedorGuionado(ProveedorGuionado.Generacion(
            """SELECT "set_config"('app.asistente_user_id','x',true)"""));

        var turno = await CarrilCon(proveedor).ResponderAsync(
            Secretaria, "¿Cuántos pedidos hay?", null, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("set_config", turno.Respuesta, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT", turno.Respuesta, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------- vacío y reintento

    [Fact]
    public async Task Con_actor_acotado_un_vacio_no_gasta_el_reintento()
    {
        await SembrarAsync();
        var proveedor = new ProveedorGuionado(ProveedorGuionado.Generacion(
            "SELECT numero FROM designaciones.pedidos WHERE numero = 'no-existe'"));

        var turno = await CarrilCon(proveedor).ResponderAsync(
            Coordinador, "¿Existe el trámite no-existe?", null, TestContext.Current.CancellationToken);

        // Una sola llamada de generación y ninguna de redacción: el vacío se
        // resuelve sin modelo.
        Assert.Equal(1, proveedor.Llamadas);
        Assert.Empty(turno.Filas);
    }

    [Fact]
    public async Task Con_actor_global_un_vacio_si_gasta_el_reintento()
    {
        await SembrarAsync();
        var proveedor = new ProveedorGuionado(ProveedorGuionado.Generacion(
            "SELECT numero FROM designaciones.pedidos WHERE numero = 'no-existe'"));

        await CarrilCon(proveedor).ResponderAsync(
            Secretaria, "¿Existe el trámite no-existe?", null, TestContext.Current.CancellationToken);

        // Para un actor global, cero filas sí significa cero filas: vale la pena
        // volver a intentar la traducción.
        Assert.Equal(2, proveedor.Llamadas);
    }

    [Fact]
    public async Task Con_actor_acotado_el_vacio_no_se_narra_como_inexistencia()
    {
        await SembrarAsync();
        var proveedor = new ProveedorGuionado(ProveedorGuionado.Generacion(
            "SELECT numero FROM designaciones.pedidos WHERE numero = 'no-existe'"));

        var turno = await CarrilCon(proveedor).ResponderAsync(
            Docente, "¿Qué pedidos hay?", null, TestContext.Current.CancellationToken);

        // El docente no tiene designaciones.ver: RLS le devuelve cero filas. La
        // respuesta tiene que encuadrar eso en su alcance, no afirmar que no hay.
        Assert.Contains("alcance", turno.Respuesta, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task El_reintento_se_queda_con_la_segunda_consulta_si_esa_trae_datos()
    {
        await SembrarAsync();
        var proveedor = new ProveedorGuionado(
            ProveedorGuionado.Generacion(
                "SELECT numero FROM designaciones.pedidos WHERE numero = 'no-existe'"),
            ProveedorGuionado.Generacion(ContarPedidos));

        var turno = await CarrilCon(proveedor).ResponderAsync(
            Secretaria, "¿Cuántos pedidos hay?", null, TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.Respondida, turno.Estado);
        Assert.Equal(8L, turno.Filas[0][0]);
    }

    // --------------------------------------------------------------- truncado

    [Fact]
    public async Task Un_resultado_truncado_llega_marcado_y_sin_conteo()
    {
        await SembrarAsync();
        var proveedor = new ProveedorGuionado(ProveedorGuionado.Generacion(
            "SELECT numero FROM designaciones.pedidos ORDER BY numero"));

        var turno = await CarrilCon(proveedor, tope: 3).ResponderAsync(
            Secretaria, "¿Qué pedidos hay?", null, TestContext.Current.CancellationToken);

        Assert.True(turno.Truncado);
        Assert.Equal(3, turno.Filas.Count);

        // El prompt de redacción llevó la prohibición de afirmar conteos.
        var redaccion = proveedor.Recibidas[^1].Mensaje;
        Assert.Contains("recortó", redaccion, StringComparison.OrdinalIgnoreCase);
    }

    // --------------------------------------------------- pregunta interpretada

    [Fact]
    public async Task La_pregunta_interpretada_se_devuelve_solo_cuando_difiere()
    {
        await SembrarAsync();
        var ct = TestContext.Current.CancellationToken;

        var igual = await CarrilCon(Guion()).ResponderAsync(
            Secretaria, "¿Cuántos pedidos hay?", "¿Cuántos pedidos hay?", ct);
        var distinta = await CarrilCon(Guion()).ResponderAsync(
            Secretaria, "¿y cuántos hay?", "¿Cuántos pedidos hay en el período activo?", ct);

        Assert.Null(igual.PreguntaInterpretada);
        Assert.Equal("¿Cuántos pedidos hay en el período activo?", distinta.PreguntaInterpretada);
    }

    [Fact]
    public async Task Sin_pregunta_interpretada_se_usa_el_mensaje()
    {
        await SembrarAsync();
        var proveedor = Guion();

        var turno = await CarrilCon(proveedor).ResponderAsync(
            Secretaria, "¿Cuántos pedidos hay?", null, TestContext.Current.CancellationToken);

        Assert.Null(turno.PreguntaInterpretada);
        Assert.Contains("¿Cuántos pedidos hay?", proveedor.Recibidas[0].Mensaje, StringComparison.Ordinal);
    }

    // ------------------------------------------------------ servicio degradado

    [Fact]
    public async Task Un_proveedor_caido_resuelve_servicio_degradado()
    {
        await SembrarAsync();
        var proveedor = new ProveedorGuionado { Falla = new HttpRequestException("sin ruta") };

        var turno = await CarrilCon(proveedor).ResponderAsync(
            Secretaria, "¿Cuántos pedidos hay?", null, TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.ServicioDegradado, turno.Estado);
        Assert.DoesNotContain("sin ruta", turno.Respuesta, StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_techo_de_llamadas_agotado_resuelve_servicio_degradado()
    {
        await SembrarAsync();
        var contador = new ContadorDeLlamadasDelTurno(1);
        contador.Reservar();

        var turno = await CarrilCon(Guion(), contador: contador).ResponderAsync(
            Secretaria, "¿Cuántos pedidos hay?", null, TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.ServicioDegradado, turno.Estado);
    }

    [Fact]
    public async Task Un_actor_que_no_existe_no_se_disfraza_de_respuesta_vacia()
    {
        await SembrarAsync();

        // El oid del directorio externo. Sin la validación explícita, el turno
        // habría contestado «no encontré nada» sobre una base llena.
        await Assert.ThrowsAsync<ActorNoResuelto>(() =>
            CarrilCon(Guion()).ResponderAsync(
                Guid.Parse("a9000000-0000-4000-8000-000000000004"),
                "¿Cuántos pedidos hay?",
                null,
                TestContext.Current.CancellationToken));
    }

    // ---------------------------------------------------------------- el costo

    [Fact]
    public async Task Un_turno_completo_consume_exactamente_dos_llamadas()
    {
        await SembrarAsync();
        var proveedor = Guion();

        var turno = await CarrilCon(proveedor).ResponderAsync(
            Secretaria, "¿Cuántos pedidos hay?", null, TestContext.Current.CancellationToken);

        // Generación y redacción. Ni una más: todo lo del medio es determinista.
        Assert.Equal(2, proveedor.Llamadas);
        Assert.Equal(2, turno.LlamadasAlModelo);
    }

    [Fact]
    public async Task El_turno_no_supera_el_techo_de_llamadas_por_defecto()
    {
        await SembrarAsync();

        // El peor caso del carril: generación, reintento y redacción. El techo por
        // omisión es cuatro.
        var proveedor = new ProveedorGuionado(
            ProveedorGuionado.Generacion(
                "SELECT numero FROM designaciones.pedidos WHERE numero = 'no-existe'"),
            ProveedorGuionado.Generacion(ContarPedidos));

        var turno = await CarrilCon(proveedor).ResponderAsync(
            Secretaria, "¿Cuántos pedidos hay?", null, TestContext.Current.CancellationToken);

        Assert.NotEqual(EstadoDelTurno.ServicioDegradado, turno.Estado);
        Assert.True(
            turno.LlamadasAlModelo <= new OpcionesAsistente().MaximoDeLlamadasPorTurno,
            $"El turno consumió {turno.LlamadasAlModelo} llamadas.");
    }

    [Fact]
    public async Task La_redaccion_usa_temperatura_baja_pero_no_cero()
    {
        await SembrarAsync();
        var proveedor = Guion();

        await CarrilCon(proveedor).ResponderAsync(
            Secretaria, "¿Cuántos pedidos hay?", null, TestContext.Current.CancellationToken);

        var redaccion = proveedor.Recibidas[^1];
        Assert.True(redaccion.Temperatura > 0m && redaccion.Temperatura < 1m);
        Assert.NotEqual(proveedor.Recibidas[0].PrefijoEstable, redaccion.PrefijoEstable);
    }

    // ---------------------------------------------------- catálogo de ejemplos

    [Fact]
    public async Task Toda_consulta_del_catalogo_ejecuta_contra_el_esquema_vigente()
    {
        await SembrarAsync();
        var (basica, conDatosPersonales) = CadenasDeLectura();
        var ejecutor = new EjecutorDeConsulta(
            basica, conDatosPersonales, ClasificadorDeSensibilidad(),
            Options.Create(new OpcionesAsistente()));
        var ct = TestContext.Current.CancellationToken;

        // Un ejemplo con SQL rota le estaría enseñando al modelo a escribir
        // consultas rotas, y como el catálogo va en el prompt de todos los turnos
        // parecidos, el error se multiplicaría en silencio.
        var fallidos = new List<string>();

        foreach (var ejemplo in new SelectorDeEjemplos().Catalogo)
        {
            try
            {
                await ejecutor.EjecutarAsync(ejemplo.Sql, Secretaria, false, ct);
            }
            catch (PostgresException excepcion)
            {
                fallidos.Add($"{ejemplo.Pregunta} — {excepcion.SqlState}: {excepcion.MessageText}");
            }
        }

        Assert.Empty(fallidos);
    }

    // ------------------------------------------------------------------ apoyo

    private static ProveedorGuionado Guion(string? sql = null) =>
        new(ProveedorGuionado.Generacion(sql ?? ContarPedidos));

    private async Task<(ResultadoDelTurno Turno, ProveedorGuionado Proveedor)> PreguntarAsync(
        Guid actor, string pregunta, string sql, string razonamiento = "Interpreté la pregunta.")
    {
        var proveedor = new ProveedorGuionado(ProveedorGuionado.Generacion(sql, razonamiento));

        var turno = await CarrilCon(proveedor).ResponderAsync(
            actor, pregunta, null, TestContext.Current.CancellationToken);

        return (turno, proveedor);
    }

    private CarrilSql CarrilCon(
        ProveedorGuionado proveedor,
        int tope = 200,
        ContadorDeLlamadasDelTurno? contador = null)
    {
        var (basica, conDatosPersonales) = CadenasDeLectura();
        var opciones = Options.Create(new OpcionesAsistente { TopeDeFilas = tope });

        // El proveedor va envuelto en el decorador del techo, igual que en la
        // composición del Host: probar el carril con el proveedor pelado dejaría
        // sin ejercitar la cota de costo.
        var contadorDelTurno = contador ?? new ContadorDeLlamadasDelTurno(
            new OpcionesAsistente().MaximoDeLlamadasPorTurno);
        var conTecho = new ProveedorConTechoDeLlamadas(proveedor, contadorDelTurno);

        var generador = new GeneradorDeSql(
            new ProveedorDeEsquema(basica, conDatosPersonales),
            new SelectorDeEjemplos(),
            conTecho,
            new FechaDeReferenciaFija(new DateOnly(2026, 8, 24)),
            Options.Create(new OpcionesAsistente()));

        return new CarrilSql(
            generador,
            new EjecutorDeConsulta(basica, conDatosPersonales, ClasificadorDeSensibilidad(), opciones),
            new ConsultorDeAlcance(basica),
            new RedactorDeRespuesta(conTecho, Options.Create(new OpcionesAsistente())),
            new SelectorDeEjemplos(),
            contadorDelTurno,
            NullLogger<CarrilSql>.Instance);
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
