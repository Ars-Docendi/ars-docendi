using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Asistente;
using Modules.Asistente.Application;
using Npgsql;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Verifica qué sigue funcionando cuando no hay modelo, y qué cuesta un turno.
/// </summary>
/// <remarks>
/// Es la épica entera dicha en una frase: <b>el modo degradado no hay que
/// inventarlo, hay que exponerlo</b>. Cinco de los ocho pasos del pipeline no
/// necesitan proveedor, y lo que estos tests prueban es que siguen corriendo.
///
/// El proveedor va guionado y el reloj es falso, así que todo lo que acá se mide
/// —cuántas llamadas salieron, cuántas se cobraron, qué estado quedó— es
/// determinista.
/// </remarks>
[Collection(ColeccionPostgres.Nombre)]
public sealed class DegradacionDelTurnoTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_degradacion")
{
    private static readonly DateTimeOffset Ancla = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private static readonly Guid Coordinador = Guid.Parse("a0000000-0000-4000-8000-000000000003");
    private const string Industrial = "c0000000-0000-4000-8000-000000000202";

    private const string ContarDocentes =
        "SELECT count(*) AS cantidad FROM designaciones.designaciones";

    // ------------------------------------------------------------ la cuota

    [Fact]
    public async Task Un_turno_con_reescritor_cobra_tres_llamadas()
    {
        // La unidad de la cuota son LLAMADAS AL MODELO, no requests ni turnos.
        // Contar requests subestimaría el consumo por un factor de tres, que es
        // exactamente la diferencia que este test mide.
        await SembrarAsync();
        var banco = Banco(
            new OpcionesAsistente { CupoDeLlamadasPorActor = 100 },
            ProveedorGuionado.Generacion(ContarDocentes),
            "Hay 4 docentes.",
            "¿cuántos docentes están designados en Sistemas?",
            ProveedorGuionado.Generacion(ContarDocentes),
            "Hay 2 docentes.");

        var ct = TestContext.Current.CancellationToken;

        var primero = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes están designados?", ct);

        Assert.Equal(2, primero.LlamadasAlModelo);

        var segundo = await banco.Capa().ResponderAsync(
            Secretaria, primero.Hilo, "¿y en Sistemas?", ct);

        // Reescritor, generación y redacción.
        Assert.Equal(3, segundo.LlamadasAlModelo);
        Assert.Equal(5, banco.Proveedor.Llamadas);
    }

    [Fact]
    public async Task Un_saludo_no_consume_cupo()
    {
        await SembrarAsync();
        var banco = Banco(new OpcionesAsistente { CupoDeLlamadasPorActor = 1 });
        var ct = TestContext.Current.CancellationToken;

        await banco.Capa().ResponderAsync(Secretaria, null, "hola", ct);
        await banco.Capa().ResponderAsync(Secretaria, null, "gracias", ct);

        Assert.True(banco.Cuota.HayCupo(Secretaria));
    }

    [Fact]
    public async Task Con_el_cupo_agotado_el_proveedor_no_recibe_ninguna_llamada()
    {
        // No una que falle: NINGUNA. Se cuenta contra el proveedor y no contra el
        // estado devuelto, porque el estado se puede acertar sin haber evitado el
        // gasto, que es lo único que la cuota existe para evitar.
        await SembrarAsync();
        var banco = Banco(
            new OpcionesAsistente { CupoDeLlamadasPorActor = 2 },
            ProveedorGuionado.Generacion(ContarDocentes),
            "Hay 4 docentes.");

        var ct = TestContext.Current.CancellationToken;

        await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes están designados?", ct);

        var gastadas = banco.Proveedor.Llamadas;
        Assert.Equal(2, gastadas);

        var segundo = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos pedidos hay?", ct);

        Assert.Equal(EstadoDelTurno.ServicioDegradado, segundo.Estado);
        Assert.Equal(gastadas, banco.Proveedor.Llamadas);
    }

    [Fact]
    public async Task El_mensaje_de_cuota_agotada_dice_cuando_vuelve_el_cupo()
    {
        await SembrarAsync();
        var banco = Banco(
            new OpcionesAsistente { CupoDeLlamadasPorActor = 2, VentanaDeCuotaMinutos = 60 },
            ProveedorGuionado.Generacion(ContarDocentes),
            "Hay 4 docentes.");

        var ct = TestContext.Current.CancellationToken;

        await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes están designados?", ct);

        var segundo = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos pedidos hay?", ct);

        // No comparte texto con el proveedor caído: acá el sistema SABE cuándo
        // vuelve el cupo, y callárselo manda a reintentar a ciegas.
        Assert.Contains("límite", segundo.Respuesta, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(PoliticaDeAbstencion.TextoServicioDegradado, segundo.Respuesta);
    }

    [Fact]
    public async Task Dos_actores_no_comparten_cupo()
    {
        await SembrarAsync();
        var banco = Banco(
            new OpcionesAsistente { CupoDeLlamadasPorActor = 2 },
            [.. GuionDeTurnos(2)]);

        var ct = TestContext.Current.CancellationToken;

        await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes están designados?", ct);

        var delOtro = await banco.Capa().ResponderAsync(
            Coordinador, null, "¿cuántos docentes están designados?", ct);

        Assert.Equal(EstadoDelTurno.Respondida, delOtro.Estado);
    }

    [Fact]
    public async Task Un_cupo_en_cero_nunca_bloquea()
    {
        await SembrarAsync();
        var banco = Banco(
            new OpcionesAsistente { CupoDeLlamadasPorActor = 0 },
            [.. GuionDeTurnos(5)]);

        var ct = TestContext.Current.CancellationToken;

        for (var i = 0; i < 5; i++)
        {
            var turno = await banco.Capa().ResponderAsync(
                Secretaria, null, "¿cuántos docentes están designados?", ct);

            Assert.Equal(EstadoDelTurno.Respondida, turno.Estado);
        }
    }

    [Fact]
    public async Task Un_turno_que_se_cae_a_la_mitad_paga_lo_que_gasto()
    {
        // Si el cargo fuera solo al terminar bien, fallar sería una forma de
        // consultar gratis: bastaría con hacer explotar el turno después de la
        // llamada cara. Por eso se cobra en `finally`.
        await SembrarAsync();
        var banco = BancoCon(
            new OpcionesAsistente { CupoDeLlamadasPorActor = 2 },
            null,
            new ProveedorGuionado(ProveedorGuionado.Generacion(ContarDocentes))
            {
                // La generación pasa; la redacción revienta con algo que el carril
                // no atrapa, así que el turno entero se cae.
                Antes = SegundaExplota(),
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            banco.Capa().ResponderAsync(
                Secretaria, null, "¿cuántos docentes están designados?",
                TestContext.Current.CancellationToken));

        Assert.Equal(2, banco.Proveedor.Llamadas);
        Assert.False(banco.Cuota.HayCupo(Secretaria));
    }

    // ----------------------------------------------------- los topes del turno

    [Fact]
    public async Task Varias_etapas_lentas_agotan_el_presupuesto_sin_que_ninguna_exceda_el_suyo()
    {
        // ÉSTE ES EL MODO DE FALLA QUE RNF-09 NOMBRA. Cada llamada tarda 16 s con
        // un timeout de 20, así que ninguna se pasa; entre dos suman 32 s contra un
        // presupuesto de 30. Sin una cota global, el turno duraría lo que sumen las
        // etapas y cada una habría respetado su límite.
        await SembrarAsync();
        var reloj = new RelojFijo(Ancla);

        var banco = BancoCon(
            new OpcionesAsistente
            {
                PresupuestoDelTurnoSegundos = 30,
                TimeoutDeLlamadaSegundos = 20,
                CupoDeLlamadasPorActor = 0,
            },
            reloj,
            new ProveedorGuionado(
                ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.")
            {
                Antes = () => reloj.Avanzar(TimeSpan.FromSeconds(16)),
            });

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes están designados?",
            TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.ServicioDegradado, turno.Estado);
    }

    [Fact]
    public async Task La_cancelacion_del_usuario_no_se_registra_como_degradacion()
    {
        // Sin esta distinción, cada persona que cierra la pestaña quedaría contada
        // como una caída del servicio y la métrica de disponibilidad mentiría.
        await SembrarAsync();
        var banco = Banco(new OpcionesAsistente { CupoDeLlamadasPorActor = 0 });

        var fuente = new CancellationTokenSource();
        await fuente.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            banco.Capa().ResponderAsync(
                Secretaria, null, "¿cuántos docentes están designados?", fuente.Token));
    }

    [Fact]
    public async Task Un_presupuesto_en_cero_deja_el_turno_sin_cota()
    {
        await SembrarAsync();
        var banco = Banco(
            new OpcionesAsistente { PresupuestoDelTurnoSegundos = 0, CupoDeLlamadasPorActor = 0 },
            ProveedorGuionado.Generacion(ContarDocentes),
            "Hay 4 docentes.");

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes están designados?",
            TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.Respondida, turno.Estado);
    }

    [Fact]
    public async Task El_resultado_reporta_el_consumo_real_de_llamadas()
    {
        await SembrarAsync();
        var banco = Banco(
            new OpcionesAsistente { CupoDeLlamadasPorActor = 0 },
            ProveedorGuionado.Generacion(ContarDocentes),
            "Hay 4 docentes.");

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes están designados?",
            TestContext.Current.CancellationToken);

        Assert.Equal(2, turno.LlamadasAlModelo);
        Assert.Equal(banco.Proveedor.Llamadas, turno.LlamadasAlModelo);
    }

    // --------------------------------------- lo que sigue vivo sin proveedor

    [Fact]
    public async Task Con_el_breaker_abierto_un_saludo_sigue_costando_cero()
    {
        await SembrarAsync();
        var banco = BancoSinProveedor();

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "hola", TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.Respondida, turno.Estado);
        Assert.Equal(0, banco.Proveedor.Llamadas);
    }

    [Fact]
    public async Task Con_el_breaker_abierto_una_pregunta_ambigua_devuelve_su_menu()
    {
        // El menú sale de una consulta a la base, no del modelo. Si la falta de
        // proveedor cortara el turno, se perdería el único estado que le dice al
        // usuario cómo destrabar su propia pregunta.
        await SembrarAsync();
        await AgregarColisionesAsync();
        var banco = BancoSinProveedor();

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿Quiénes dan Bases de Datos?",
            TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.NecesitaAclaracion, turno.Estado);
        Assert.NotNull(turno.Opciones);
        Assert.True(turno.Opciones!.Count > 1);
        Assert.Equal(0, banco.Proveedor.Llamadas);
    }

    [Fact]
    public async Task Con_el_breaker_abierto_la_respuesta_a_una_aclaracion_se_reconoce()
    {
        await SembrarAsync();
        await AgregarColisionesAsync();
        var banco = BancoSinProveedor();
        var ct = TestContext.Current.CancellationToken;

        var menu = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿Quiénes dan Bases de Datos?", ct);

        Assert.Equal(EstadoDelTurno.NecesitaAclaracion, menu.Estado);

        var elegida = await banco.Capa().ResponderAsync(Secretaria, menu.Hilo, "1", ct);

        // Se reconoció: el turno YA NO pide aclaración y el hilo la cerró. Que
        // termine degradado es lo correcto —el paso que sigue necesita generar una
        // consulta—, pero la elección se consumió sin modelo.
        Assert.Equal(EstadoDelTurno.ServicioDegradado, elegida.Estado);
        Assert.Null(banco.Hilos.Resolver(menu.Hilo, Secretaria).AclaracionPendiente);
        Assert.Equal(0, banco.Proveedor.Llamadas);
    }

    [Fact]
    public async Task Con_el_breaker_abierto_una_pregunta_de_datos_resuelve_degradada()
    {
        await SembrarAsync();
        var banco = BancoSinProveedor();

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes están designados?",
            TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.ServicioDegradado, turno.Estado);
        Assert.Equal(PoliticaDeAbstencion.TextoServicioDegradado, turno.Respuesta);
        Assert.Equal(0, banco.Proveedor.Llamadas);
    }

    [Fact]
    public async Task Con_el_breaker_abierto_no_se_consume_cupo_del_actor()
    {
        // Sale del orden: el veredicto se resuelve ANTES del pipeline, así que sin
        // modelo no hay llamadas, y sin llamadas no hay nada que cobrar. Un proveedor
        // caído no le gasta el cupo a nadie.
        await SembrarAsync();
        var banco = BancoSinProveedor(cupo: 4);

        for (var i = 0; i < 10; i++)
        {
            await banco.Capa().ResponderAsync(
                Secretaria, null, "¿cuántos docentes están designados?",
                TestContext.Current.CancellationToken);
        }

        Assert.True(banco.Cuota.HayCupo(Secretaria));
    }

    [Fact]
    public async Task Sin_cupo_un_saludo_sigue_resolviendo()
    {
        await SembrarAsync();
        var banco = Banco(new OpcionesAsistente { CupoDeLlamadasPorActor = 1 });

        banco.Cuota.Anotar(Secretaria, 5);

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "hola", TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.Respondida, turno.Estado);
        Assert.Equal(0, banco.Proveedor.Llamadas);
    }

    [Fact]
    public async Task El_estado_degradado_se_distingue_del_no_contestable()
    {
        // «No contestable» significa que la pregunta no se puede responder nunca;
        // degradado significa que ahora no. Colapsarlos le diría al usuario que su
        // pregunta está mal cuando lo que pasa es que el proveedor se cayó.
        await SembrarAsync();

        var caido = BancoSinProveedor();
        var degradado = await caido.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes están designados?",
            TestContext.Current.CancellationToken);

        var sano = Banco(
            new OpcionesAsistente { CupoDeLlamadasPorActor = 0 },
            ProveedorGuionado.NoContestable());

        var noContestable = await sano.Capa().ResponderAsync(
            Secretaria, null, "¿cuál es la temperatura de la sala?",
            TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.ServicioDegradado, degradado.Estado);
        Assert.Equal(EstadoDelTurno.NoContestable, noContestable.Estado);
        Assert.NotEqual(degradado.Respuesta, noContestable.Respuesta);
    }

    // ------------------------------------------------------------------ apoyo

    private BancoDelAsistente Banco(
        OpcionesAsistente configuracion,
        params string[] guion) =>
        BancoCon(configuracion, null, null, guion);

    private BancoDelAsistente BancoCon(
        OpcionesAsistente configuracion,
        RelojFijo? reloj,
        ProveedorGuionado? proveedor,
        params string[] guion)
    {
        var (basica, pii) = CadenasDeLectura();

        return BancoDelAsistente.Armar(
            basica,
            pii,
            ClasificadorDeSensibilidad(),
            configuracion,
            reloj: reloj ?? new RelojFijo(Ancla),
            proveedor: proveedor,
            registro: null,
            envolver: null,
            guion: guion);
    }

    /// <summary>Un banco con el corte al proveedor ya abierto.</summary>
    private BancoDelAsistente BancoSinProveedor(int cupo = 0)
    {
        var banco = Banco(new OpcionesAsistente
        {
            FallosParaAbrirElBreaker = 1,
            CupoDeLlamadasPorActor = cupo,
        });

        banco.Breaker.Fallo();

        return banco;
    }

    /// <summary>
    /// El guion de <paramref name="turnos"/> turnos completos del carril SQL.
    /// </summary>
    /// <remarks>
    /// Hace falta porque el proveedor guionado repite su última respuesta cuando se
    /// le acaba el guion: un turno de más leería «Hay 4 docentes» como si fuera una
    /// generación y el turno terminaría no contestable por una razón que no es la
    /// que el test mide.
    /// </remarks>
    private static IEnumerable<string> GuionDeTurnos(int turnos) =>
        Enumerable.Range(0, turnos).SelectMany(_ => new[]
        {
            ProveedorGuionado.Generacion(ContarDocentes),
            "Hay 4 docentes.",
        });

    /// <summary>Un gancho que deja pasar la primera llamada y hace explotar la segunda.</summary>
    private static Action SegundaExplota()
    {
        var vistas = 0;

        return () =>
        {
            if (++vistas >= 2)
            {
                throw new InvalidOperationException("se cayó el turno a la mitad");
            }
        };
    }

    private async Task AgregarColisionesAsync()
    {
        await using var conexion = await AbrirConexionAsync();
        await using var comando = new NpgsqlCommand(
            $"""
            INSERT INTO identity.materias (id, code, name, carrera_id, is_active)
            VALUES ('70000000-0000-4000-8000-0000000009f1', '04910', 'Bases de Datos',
                    '{Industrial}', true);
            """,
            conexion);

        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
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
