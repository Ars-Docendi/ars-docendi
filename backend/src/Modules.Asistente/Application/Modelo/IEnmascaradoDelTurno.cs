namespace Modules.Asistente.Application;

/// <summary>
/// Whether redaction of this turn runs without masking <c>sensible-valor</c>
/// columns, decided from the model adapter the turn resolved
/// (asistente-modelo-en-caliente, D7).
/// </summary>
/// <remarks>
/// It is a port and not a read of <see cref="OpcionesAsistente"/> because the
/// environment alone cannot decide it any more: with a runtime selection, an
/// environment that runs on own hardware could be sending the redaction prompt to
/// a cloud company. A cloud adapter always masks.
/// </remarks>
public interface IEnmascaradoDelTurno
{
    /// <summary>True only for own hardware, with the option on and no cassette directory.</summary>
    Task<bool> RedaccionSinEnmascararAsync(CancellationToken ct);
}
