namespace Modules.Asistente.Application;

/// <summary>
/// El lado de soporte del historial ajeno: listar y leer las conversaciones de OTRO actor, con
/// razón obligatoria y auditoría permanente ANTES de devolver cualquier dato
/// (asistente-acceso-de-soporte-al-historial, design.md D10 de asistente-historial-conversaciones).
/// </summary>
public interface IConsultasDeAuditoriaDeSoporte
{
    /// <summary>Lista las conversaciones de <paramref name="sujeto"/> (sin sus turnos).</summary>
    Task<IReadOnlyList<ConversacionResumen>> ListarAsync(
        Guid lector, Guid sujeto, string razon, CancellationToken ct);

    /// <summary>
    /// Lee una conversación de <paramref name="sujeto"/>, con sus turnos. <c>null</c> si esa
    /// conversación no existe o no es de ese sujeto — los dos casos se tratan igual, para no
    /// filtrar cuál de los dos fue.
    /// </summary>
    Task<ConversacionDetalle?> LeerAsync(
        Guid lector, Guid sujeto, Guid hiloId, string razon, CancellationToken ct);
}
