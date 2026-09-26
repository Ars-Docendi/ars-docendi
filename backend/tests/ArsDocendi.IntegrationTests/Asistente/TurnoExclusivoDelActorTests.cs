using ArsDocendi.IntegrationTests.Infraestructura;
using Modules.Asistente;
using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// Un turno en curso por actor rechaza al segundo (asistente-turno-exclusivo-del-actor,
/// design.md D5 de asistente-administracion-de-uso).
/// </summary>
/// <remarks>
/// Contra el fake de <see cref="ICandadoDelTurno"/> (<c>CandadoDelTurnoFalso</c>),
/// que reproduce la exclusión mutua sin pagar Postgres — el mecanismo real
/// está en <see cref="CandadoDelTurnoTests"/>. Lo que importa acá es que
/// <c>CapaConversacional</c> lo consulta primero y lo libera siempre.
/// </remarks>
public sealed class TurnoExclusivoDelActorTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_turno_exclusivo")
{
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");
    private static readonly Guid Coordinador = Guid.Parse("a0000000-0000-4000-8000-000000000003");
    private const string ContarDocentes = "SELECT count(*) AS cantidad FROM designaciones.designaciones";

    [Fact]
    public async Task Un_turno_ya_tomado_rechaza_al_segundo_del_mismo_actor()
    {
        await SembrarAsync();
        var banco = Banco();
        var ct = TestContext.Current.CancellationToken;

        // Simula el primer turno "en curso" tomando el candado a mano, sin
        // liberarlo, en lugar de correr un ResponderAsync real que se
        // resolvería antes de poder verificar el rechazo.
        var enCurso = await banco.CandadoDelTurno.IntentarAsync(Secretaria, ct);
        Assert.NotNull(enCurso);

        var rechazado = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes están designados?", ct);

        Assert.Equal(EstadoDelTurno.ServicioDegradado, rechazado.Estado);
        Assert.Equal(PoliticaDeAbstencion.TextoTurnoConcurrente, rechazado.Respuesta);
        Assert.Equal(0, banco.Proveedor.Llamadas);

        await enCurso!.DisposeAsync();

        var libre = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes están designados?", ct);

        Assert.Equal(EstadoDelTurno.Respondida, libre.Estado);
    }

    [Fact]
    public async Task Dos_actores_distintos_nunca_se_bloquean_entre_si()
    {
        await SembrarAsync();
        var banco = Banco();
        var ct = TestContext.Current.CancellationToken;

        var deOtro = await banco.CandadoDelTurno.IntentarAsync(Coordinador, ct);
        Assert.NotNull(deOtro);

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes están designados?", ct);

        Assert.Equal(EstadoDelTurno.Respondida, turno.Estado);

        await deOtro!.DisposeAsync();
    }

    // --------------------------------------------- se libera en los 4 casos

    [Fact]
    public async Task Se_libera_tras_un_turno_exitoso()
    {
        await SembrarAsync();
        var banco = Banco();
        var ct = TestContext.Current.CancellationToken;

        await banco.Capa().ResponderAsync(Secretaria, null, "hola", ct);

        var reintento = await banco.CandadoDelTurno.IntentarAsync(Secretaria, ct);
        Assert.NotNull(reintento);
        await reintento!.DisposeAsync();
    }

    [Fact]
    public async Task Se_libera_tras_un_turno_degradado()
    {
        await SembrarAsync();
        var banco = Banco(topeOrganizacional: 1);
        var ct = TestContext.Current.CancellationToken;

        // Agota el tope falso a mano, sin depender de si un turno concreto lo
        // hubiera agotado: lo único que este test mide es que el candado se
        // libera cuando el turno termina ServicioDegradado.
        await banco.PresupuestoOrganizacional.AcumularAsync(
            "guionado/x", DateTimeOffset.UtcNow, 1, 1, null, ct);

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes están designados?", ct);

        Assert.Equal(EstadoDelTurno.ServicioDegradado, turno.Estado);
        Assert.Equal(PoliticaDeAbstencion.TextoTopeOrganizacionalAgotado, turno.Respuesta);

        var reintento = await banco.CandadoDelTurno.IntentarAsync(Secretaria, ct);
        Assert.NotNull(reintento);
        await reintento!.DisposeAsync();
    }

    [Fact]
    public async Task Se_libera_tras_una_excepcion_no_prevista()
    {
        await SembrarAsync();
        var (basica, pii) = CadenasDeLectura();
        var banco = BancoDelAsistente.Armar(
            basica, pii, ClasificadorDeSensibilidad(), Apertura,
            new OpcionesAsistente(),
            proveedor: new ProveedorGuionado
            {
                Falla = new InvalidOperationException("el proveedor explotó"),
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            banco.Capa().ResponderAsync(
                Secretaria, null, "¿cuántos docentes hay?", TestContext.Current.CancellationToken));

        var reintento = await banco.CandadoDelTurno.IntentarAsync(
            Secretaria, TestContext.Current.CancellationToken);
        Assert.NotNull(reintento);
        await reintento!.DisposeAsync();
    }

    [Fact]
    public async Task Se_libera_tras_agotar_el_presupuesto_del_turno()
    {
        await SembrarAsync();
        var reloj = new RelojFijo(new DateTimeOffset(2026, 8, 25, 10, 0, 0, TimeSpan.Zero));
        var (basica, pii) = CadenasDeLectura();

        var banco = BancoDelAsistente.Armar(
            basica, pii, ClasificadorDeSensibilidad(), Apertura,
            new OpcionesAsistente { PresupuestoDelTurnoSegundos = 5, TimeoutDeLlamadaSegundos = 20 },
            reloj: reloj,
            proveedor: new ProveedorGuionado(
                ProveedorGuionado.Generacion(ContarDocentes), "Hay 4 docentes.")
            {
                Antes = () => reloj.Avanzar(TimeSpan.FromSeconds(6)),
            });

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes están designados?",
            TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.ServicioDegradado, turno.Estado);

        var reintento = await banco.CandadoDelTurno.IntentarAsync(
            Secretaria, TestContext.Current.CancellationToken);
        Assert.NotNull(reintento);
        await reintento!.DisposeAsync();
    }

    [Fact]
    public async Task Se_libera_tras_una_cancelacion_del_lado_del_cliente()
    {
        await SembrarAsync();
        var banco = Banco();

        var fuente = new CancellationTokenSource();
        await fuente.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            banco.Capa().ResponderAsync(
                Secretaria, null, "¿cuántos docentes están designados?", fuente.Token));

        var reintento = await banco.CandadoDelTurno.IntentarAsync(
            Secretaria, TestContext.Current.CancellationToken);
        Assert.NotNull(reintento);
        await reintento!.DisposeAsync();
    }

    // ------------------------------------------------------------------ apoyo

    private BancoDelAsistente Banco(decimal topeOrganizacional = 0)
    {
        var (basica, pii) = CadenasDeLectura();

        return BancoDelAsistente.Armar(
            basica, pii, ClasificadorDeSensibilidad(), Apertura,
            new OpcionesAsistente(),
            topeOrganizacional: topeOrganizacional,
            guion: [.. GuionDeTurnos(5)]);
    }

    /// <summary>El guion de <paramref name="turnos"/> turnos completos del carril SQL.</summary>
    private static IEnumerable<string> GuionDeTurnos(int turnos) =>
        Enumerable.Range(0, turnos).SelectMany(_ => new[]
        {
            ProveedorGuionado.Generacion(ContarDocentes),
            "Hay 4 docentes.",
        });

}
