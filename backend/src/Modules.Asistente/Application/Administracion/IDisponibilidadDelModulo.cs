namespace Modules.Asistente.Application;

/// <summary>El modo mantenimiento, tal como lo ve cualquier consultante.</summary>
public sealed record EstadoDeMantenimiento(bool Activo, string? Razon);

/// <summary>
/// El kill switch del módulo (asistente-modo-mantenimiento, design.md D7 de
/// asistente-administracion-de-uso).
/// </summary>
/// <remarks>
/// Puerto chico a propósito: hoy lo implementa Postgres
/// (<c>DisponibilidadDelModuloReal</c>), y nada Azure se referencia en ningún
/// lado. Que sea un puerto —y no un booleano leído directo de
/// <c>OpcionesAsistente</c>— es lo que deja reemplazar el default por una
/// implementación respaldada en Azure App Configuration más adelante como una
/// clase nueva y un cambio de composición, nunca un cambio en quien lo llama —
/// el mismo patrón que ya usa el módulo para el proveedor del modelo.
///
/// Sólo lectura y escritura del ESTADO. La escritura del rastro de auditoría
/// (quién, cuándo, por qué) es responsabilidad de quien llama —el controller—
/// y no de este puerto: mover el kill switch a otro backend no debería
/// arrastrar la auditoría con él.
/// </remarks>
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
