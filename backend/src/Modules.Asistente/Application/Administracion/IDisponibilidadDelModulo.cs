namespace Modules.Asistente.Application;

/// <summary>El modo mantenimiento, tal como lo ve cualquier consultante.</summary>
public sealed record EstadoDeMantenimiento(bool Activo, string? Razon);

/// <summary>
/// El kill switch del módulo (asistente-modo-mantenimiento, design.md D7 de
/// asistente-administracion-de-uso).
/// </summary>
public interface IDisponibilidadDelModulo
{
    /// <summary>Consulta el estado actual. Sin caché de proceso (tarea 6.7).</summary>
    Task<EstadoDeMantenimiento> ConsultarAsync(CancellationToken ct);

    /// <summary>
    /// Activa el modo mantenimiento. La razón es obligatoria; <paramref name="actor"/>
    /// queda en <c>modo_mantenimiento.actor_id</c> (quién lo activó por última vez).
    /// </summary>
    Task ActivarAsync(Guid actor, string razon, CancellationToken ct);

    /// <summary>Desactiva el modo mantenimiento.</summary>
    Task DesactivarAsync(Guid actor, CancellationToken ct);
}
