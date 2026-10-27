namespace Modules.Asistente.Application;

/// <summary>Un turno tal como lo necesita el historial persistido.</summary>
public sealed record TurnoParaHistorial(
    Guid Actor,
    string Pregunta,
    string? SqlResuelto,
    EstadoDelTurno Estado,
    DateTimeOffset OcurrioEn,
    IReadOnlyDictionary<string, (TipoDeMencion Tipo, Guid Id)>? Referencias = null);

/// <summary>
/// Persiste un turno al historial propio del actor (asistente-historial-conversaciones).
/// </summary>
public interface IRegistroDeHistorial
{
    /// <summary>Registra un turno. No propaga errores.</summary>
    Task RegistrarTurnoAsync(
        HiloConversacional conversacion, TurnoParaHistorial turno, CancellationToken ct);

    /// <summary>
    /// Reemplaza, en una sola transacción, la última fila de <c>asistente.turno_historico</c> de
    /// esta conversación por <paramref name="turno"/> — para «Editar y reenviar»
    /// (asistente-edicion-de-la-ultima-pregunta, design.md D9 de asistente-rediseno-v3).
    /// </summary>
    Task ReemplazarUltimoTurnoAsync(
        HiloConversacional conversacion, TurnoParaHistorial turno, CancellationToken ct);
}
