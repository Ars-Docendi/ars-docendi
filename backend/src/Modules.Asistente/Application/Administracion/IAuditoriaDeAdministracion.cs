namespace Modules.Asistente.Application;

/// <summary>
/// El rastro append-only de administración del asistente
/// (asistente-auditoria-de-administracion, design.md D11 de
/// asistente-administracion-de-uso).
/// </summary>
/// <remarks>
/// Sin ningún método de actualizar ni borrar, a propósito — "append-only, no
/// borrable" se garantiza no escribiendo ese código, no con un trigger de
/// base (mismo criterio que <c>auditoria_acceso_historial</c>, design.md D10
/// de asistente-historial-conversaciones). Hay un test que enumera las rutas
/// del módulo y falla si alguna ofrece esa forma contra esta tabla.
/// </remarks>
public interface IAuditoriaDeAdministracion
{
    /// <summary>
    /// Registra una acción de administración. Se llama ANTES de responder
    /// éxito al cliente (write-before-respond), nunca en fire-and-forget.
    /// </summary>
    /// <param name="accion">
    /// Un código corto y estable (p. ej. <c>"mantenimiento.activar"</c>), no
    /// texto libre — es lo que el panel de uso agrupa y filtra.
    /// </param>
    /// <param name="antes">El valor previo, serializado. Nulo si no aplica.</param>
    /// <param name="despues">El valor nuevo, serializado.</param>
    Task RegistrarAsync(
        Guid actor, string accion, string? antes, string despues, CancellationToken ct);
}
