using Microsoft.Extensions.Logging;
using Modules.Asistente.Infrastructure;

namespace ArsDocendi.IntegrationTests.Asistente;

/// <summary>
/// El aviso de umbral (50/80/100%), exactamente una vez por período (tarea
/// 9.7 de asistente-administracion-de-uso).
/// </summary>
public sealed class DetectorDeUmbralesTests
{
    [Fact]
    public void Avisa_una_sola_vez_al_cruzar_el_50_por_ciento()
    {
        var log = new LoggerDeEventos<DetectorDeUmbralesTests>();
        var detector = new DetectorDeUmbrales();

        detector.RegistrarSiCorresponde(log, "usuario", "actor-1", "2026-09-25", 60.0);
        detector.RegistrarSiCorresponde(log, "usuario", "actor-1", "2026-09-25", 65.0);
        detector.RegistrarSiCorresponde(log, "usuario", "actor-1", "2026-09-25", 70.0);

        Assert.Equal(1, log.Eventos.Count);
    }

    [Fact]
    public void Avisa_cada_umbral_nuevo_que_se_cruza()
    {
        var log = new LoggerDeEventos<DetectorDeUmbralesTests>();
        var detector = new DetectorDeUmbrales();

        detector.RegistrarSiCorresponde(log, "usuario", "actor-1", "2026-09-25", 55.0);
        detector.RegistrarSiCorresponde(log, "usuario", "actor-1", "2026-09-25", 85.0);
        detector.RegistrarSiCorresponde(log, "usuario", "actor-1", "2026-09-25", 100.0);

        // 50%, 80% y 100%: tres avisos distintos, uno por umbral.
        Assert.Equal(3, log.Eventos.Count);
    }

    [Fact]
    public void Un_salto_directo_a_100_avisa_los_tres_umbrales_de_una()
    {
        var log = new LoggerDeEventos<DetectorDeUmbralesTests>();
        var detector = new DetectorDeUmbrales();

        detector.RegistrarSiCorresponde(log, "usuario", "actor-1", "2026-09-25", 100.0);

        Assert.Equal(3, log.Eventos.Count);
    }

    [Fact]
    public void Actores_distintos_no_comparten_el_estado_de_aviso()
    {
        var log = new LoggerDeEventos<DetectorDeUmbralesTests>();
        var detector = new DetectorDeUmbrales();

        detector.RegistrarSiCorresponde(log, "usuario", "actor-1", "2026-09-25", 100.0);
        detector.RegistrarSiCorresponde(log, "usuario", "actor-2", "2026-09-25", 100.0);

        Assert.Equal(6, log.Eventos.Count);
    }

    [Fact]
    public void Un_periodo_nuevo_vuelve_a_avisar()
    {
        var log = new LoggerDeEventos<DetectorDeUmbralesTests>();
        var detector = new DetectorDeUmbrales();

        detector.RegistrarSiCorresponde(log, "usuario", "actor-1", "2026-09-25", 100.0);
        detector.RegistrarSiCorresponde(log, "usuario", "actor-1", "2026-09-26", 100.0);

        Assert.Equal(6, log.Eventos.Count);
    }

    [Fact]
    public void Por_debajo_del_50_por_ciento_no_avisa_nada()
    {
        var log = new LoggerDeEventos<DetectorDeUmbralesTests>();
        var detector = new DetectorDeUmbrales();

        detector.RegistrarSiCorresponde(log, "usuario", "actor-1", "2026-09-25", 49.9);

        Assert.Empty(log.Eventos);
    }

    private sealed class LoggerDeEventos<T> : ILogger<T>
    {
        private readonly List<string> _eventos = [];
        public IReadOnlyList<string> Eventos => _eventos;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            _eventos.Add(formatter(state, exception));
    }
}
