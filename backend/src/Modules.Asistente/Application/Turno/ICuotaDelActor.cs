namespace Modules.Asistente.Application;

/// <summary>
/// Cupo diario de turnos por actor, persistente (asistente-presupuesto-persistente).
/// </summary>
public interface ICuotaDelActor
{
    /// <summary>Si al actor le queda cupo hoy.</summary>
    Task<bool> HayCupoAsync(Guid actor, CancellationToken ct);

    /// <summary>Anota que el turno gastó cupo.</summary>
    Task AnotarAsync(Guid actor, CancellationToken ct);

    /// <summary>
    /// Cuándo vuelve a haber cupo, o <c>null</c> si ya hay o si el cupo está desactivado (0).
    /// </summary>
    Task<DateTimeOffset?> CupoVuelveAAsync(Guid actor, CancellationToken ct);

    /// <summary>Cuántos turnos le quedan hoy al actor.</summary>
    Task<int> CupoRestanteAsync(Guid actor, CancellationToken ct);
}
