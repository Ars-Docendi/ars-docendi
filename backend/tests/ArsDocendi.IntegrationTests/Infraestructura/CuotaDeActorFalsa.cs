using Modules.Asistente.Application;

namespace ArsDocendi.IntegrationTests.Infraestructura;

/// <summary>
/// Fake en memoria de <see cref="ICuotaDelActor"/>, con la semántica nueva
/// (turnos por día calendario UTC, design.md D2 de
/// asistente-administracion-de-uso).
/// </summary>
/// <remarks>
/// Existe por dos motivos:
/// <list type="bullet">
/// <item>Tarea 3.1: fijar el contrato de la interfaz contra un fake simple,
/// sin pagar Postgres — lo que prueba <c>CuotaPersistente</c> (contra una base
/// real) es otra cosa.</item>
/// <item><see cref="BancoDelAsistente"/>: la mayoría de los tests de
/// <c>CapaConversacional</c> no necesitan una base real para ejercitar la
/// cuota, y forzarlos a <c>CuotaPersistente</c> los volvería tests de
/// integración por una pieza que no están probando.</item>
/// </list>
///
/// A DIFERENCIA DE <c>CuotaPersistente</c>, que deriva el consumo contando
/// <c>registro_operativo</c> y por eso no necesita que <c>AnotarAsync</c>
/// escriba nada, este fake NO TIENE ningún registro que contar: es su propia
/// fuente de verdad, así que <c>AnotarAsync</c> incrementa de verdad un
/// contador en memoria. El sitio de llamada (<c>CapaConversacional</c>, en su
/// <c>finally</c>) ya decide cuándo corresponde llamarlo —solo si el turno
/// invocó al modelo, design.md D4—, así que este fake no necesita saberlo.
/// </remarks>
internal sealed class CuotaDeActorFalsa(int cupoDiario, TimeProvider reloj) : ICuotaDelActor
{
    private readonly Dictionary<Guid, List<DateTimeOffset>> _turnosPorActor = [];

    public Task<bool> HayCupoAsync(Guid actor, CancellationToken ct) =>
        Task.FromResult(cupoDiario <= 0 || TurnosDeHoy(actor) < cupoDiario);

    public Task AnotarAsync(Guid actor, CancellationToken ct)
    {
        if (!_turnosPorActor.TryGetValue(actor, out var turnos))
        {
            turnos = [];
            _turnosPorActor[actor] = turnos;
        }

        turnos.Add(reloj.GetUtcNow());
        return Task.CompletedTask;
    }

    public Task<DateTimeOffset?> CupoVuelveAAsync(Guid actor, CancellationToken ct)
    {
        if (cupoDiario <= 0 || TurnosDeHoy(actor) < cupoDiario)
        {
            return Task.FromResult((DateTimeOffset?)null);
        }

        return Task.FromResult((DateTimeOffset?)InicioDelDiaUtc(reloj.GetUtcNow()).AddDays(1));
    }

    public Task<int> CupoRestanteAsync(Guid actor, CancellationToken ct) =>
        Task.FromResult(cupoDiario <= 0 ? int.MaxValue : Math.Max(0, cupoDiario - TurnosDeHoy(actor)));

    private int TurnosDeHoy(Guid actor)
    {
        if (!_turnosPorActor.TryGetValue(actor, out var turnos))
        {
            return 0;
        }

        var inicio = InicioDelDiaUtc(reloj.GetUtcNow());
        return turnos.Count(cuando => cuando >= inicio);
    }

    private static DateTimeOffset InicioDelDiaUtc(DateTimeOffset momento) =>
        new(DateOnly.FromDateTime(momento.UtcDateTime).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
