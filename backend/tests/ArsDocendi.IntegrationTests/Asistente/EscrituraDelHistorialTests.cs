using ArsDocendi.IntegrationTests.Infraestructura;
using ArsDocendi.Shared.Persistencia;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Asistente;
using Modules.Asistente.Application;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El gancho de escritura del historial dentro de <c>CapaConversacional</c>,
/// de punta a punta contra la base real.
/// </summary>
/// <remarks>
/// Va contra la base real y no contra <c>HistorialEnMemoria</c> porque lo que
/// se prueba es justamente que el gancho persiste — cuatro estados sí, uno no
/// — y que un fallo de esa escritura no le llega al usuario.
/// </remarks>
public sealed class EscrituraDelHistorialTests(PostgresFixture postgres)
    : ClasePostgresAislada(postgres, "asistente_escritura_historial")
{
    private static readonly DateTimeOffset Ancla = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Secretaria = Guid.Parse("a0000000-0000-4000-8000-000000000004");

    [Fact]
    public async Task Un_turno_respondido_se_persiste_al_historial()
    {
        await SembrarAsync();
        var banco = Banco("hola");

        await banco.Capa().ResponderAsync(
            Secretaria, null, "hola", TestContext.Current.CancellationToken);

        Assert.Equal(
            nameof(EstadoDelTurno.Respondida),
            await EscalarAsync<string>("SELECT estado FROM asistente.turno_historico"));
    }

    [Fact]
    public async Task Un_turno_degradado_se_persiste_al_historial()
    {
        await SembrarAsync();

        var (basica, pii) = CadenasDeLectura();
        var banco = BancoDelAsistente.Armar(
            basica, pii, ClasificadorDeSensibilidad(), Apertura,
            new OpcionesAsistente { FallosParaAbrirElBreaker = 1 },
            reloj: new RelojFijo(Ancla),
            historial: HistorialReal());

        banco.Breaker.Fallo();

        await banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes hay?", TestContext.Current.CancellationToken);

        Assert.Equal(
            nameof(EstadoDelTurno.ServicioDegradado),
            await EscalarAsync<string>("SELECT estado FROM asistente.turno_historico"));
    }

    [Fact]
    public async Task Un_turno_caido_por_excepcion_NO_se_persiste_al_historial()
    {
        await SembrarAsync();

        var (basica, pii) = CadenasDeLectura();
        var banco = BancoDelAsistente.Armar(
            basica, pii, ClasificadorDeSensibilidad(), Apertura,
            new OpcionesAsistente(),
            reloj: new RelojFijo(Ancla),
            proveedor: new ProveedorGuionado { Falla = new InvalidOperationException("boom") },
            historial: HistorialReal());

        await Assert.ThrowsAsync<InvalidOperationException>(() => banco.Capa().ResponderAsync(
            Secretaria, null, "¿cuántos docentes hay?", TestContext.Current.CancellationToken));

        Assert.Equal(0L, await EscalarAsync<long>("SELECT count(*) FROM asistente.hilo_historico"));
        Assert.Equal(0L, await EscalarAsync<long>("SELECT count(*) FROM asistente.turno_historico"));
    }

    [Fact]
    public async Task Un_fallo_real_al_escribir_el_historial_no_le_llega_al_usuario()
    {
        // LA CADENA APUNTA A UNA BASE INALCANZABLE, a propósito: lo que se
        // prueba es el mismo camino de producción (RegistroDeHistorial real,
        // con su propio try/catch), no un doble que viole el contrato. Es el
        // mismo criterio con que RegistroDelTurno se traga NpgsqlException.
        await SembrarAsync();

        var (basica, pii) = CadenasDeLectura();
        var banco = BancoDelAsistente.Armar(
            basica, pii, ClasificadorDeSensibilidad(), Apertura,
            new OpcionesAsistente(),
            reloj: new RelojFijo(Ancla),
            historial: new RegistroDeHistorial(
                new CadenaDuena("Host=localhost;Port=1;Database=inalcanzable;Timeout=1"),
                NullLogger<RegistroDeHistorial>.Instance));

        var turno = await banco.Capa().ResponderAsync(
            Secretaria, null, "hola", TestContext.Current.CancellationToken);

        Assert.Equal(EstadoDelTurno.Respondida, turno.Estado);
    }

    // ------------------------------------------------------------------ apoyo

    private BancoDelAsistente Banco(params string[] guion)
    {
        var (basica, pii) = CadenasDeLectura();

        return BancoDelAsistente.Armar(
            basica, pii, ClasificadorDeSensibilidad(), Apertura,
            new OpcionesAsistente(),
            reloj: new RelojFijo(Ancla),
            historial: HistorialReal(),
            guion: guion);
    }

    private IRegistroDeHistorial HistorialReal() =>
        new RegistroDeHistorial(new CadenaDuena(Cadena), NullLogger<RegistroDeHistorial>.Instance);
}
