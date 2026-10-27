namespace Modules.Asistente.Application;

/// <summary>
/// Whether redaction of this turn runs without masking <c>sensible-valor</c> columns, decided from
/// the model adapter the turn resolved (asistente-modelo-en-caliente, D7).
/// </summary>
public interface IEnmascaradoDelTurno
{
    /// <summary>True only for own hardware, with the option on and no cassette directory.</summary>
    Task<bool> RedaccionSinEnmascararAsync(CancellationToken ct);
}
