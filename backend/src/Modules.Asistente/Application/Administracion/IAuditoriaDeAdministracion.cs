namespace Modules.Asistente.Application;

/// <summary>
/// El rastro append-only de administración del asistente (asistente-auditoria-de-administracion,
/// design.md D11 de asistente-administracion-de-uso).
/// </summary>
public interface IAuditoriaDeAdministracion
{
    /// <summary>
    /// Registra una acción de administración. Se llama ANTES de responder éxito al cliente
    /// (write-before-respond), nunca en fire-and-forget.
    /// </summary>
    Task RegistrarAsync(
        Guid actor, string accion, string? antes, string despues, CancellationToken ct);
}
