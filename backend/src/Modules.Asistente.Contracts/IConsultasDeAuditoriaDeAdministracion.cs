namespace Modules.Asistente.Contracts;

/// <summary>
/// Un campo cambiado por un evento de administración del asistente, ya
/// normalizado por el módulo que escribió el JSON original
/// (design.md D1 de sistema-seccion-unificada).
/// </summary>
public sealed record CampoDeAdministracion(string Campo, string? ValorAnterior, string? ValorNuevo);

/// <summary>
/// Un evento de <c>asistente.auditoria_administracion</c>, ya normalizado para
/// que el Host lo mapee sin conocer el formato JSON interno del módulo.
/// </summary>
public sealed record EventoDeAdministracion(
    long Id,
    Guid ActorId,
    DateTimeOffset OcurridoEn,
    string Tipo,
    string? Clave,
    Guid? UsuarioAfectado,
    IReadOnlyList<CampoDeAdministracion> Campos);

/// <summary>
/// Un lote de eventos de administración, con la señal de si el cupo de lectura
/// se alcanzó (design.md D1: cap de 2000 filas).
/// </summary>
public sealed record LoteDeAuditoriaDeAdministracion(
    IReadOnlyList<EventoDeAdministracion> Eventos, bool Truncado);

/// <summary>
/// Lectura pública del rastro de administración del asistente, la única forma
/// en que el Host puede ver <c>asistente.auditoria_administracion</c> sin
/// referenciar el módulo (design.md D1 de sistema-seccion-unificada, ARS-157).
/// </summary>
public interface IConsultasDeAuditoriaDeAdministracion
{
    /// <summary>Lista los eventos ordenados <c>ocurrido_en DESC, id DESC</c>.</summary>
    /// <param name="desde">Cota inferior inclusiva, o sin cota si es <c>null</c>.</param>
    /// <param name="hasta">Cota superior inclusiva, o sin cota si es <c>null</c>.</param>
    Task<LoteDeAuditoriaDeAdministracion> ListarAsync(
        DateTimeOffset? desde, DateTimeOffset? hasta, CancellationToken ct);
}
