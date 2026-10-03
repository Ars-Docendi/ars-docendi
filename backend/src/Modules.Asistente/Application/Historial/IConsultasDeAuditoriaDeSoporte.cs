namespace Modules.Asistente.Application;

/// <summary>
/// El lado de soporte del historial ajeno: listar y leer las conversaciones
/// de OTRO actor, con razón obligatoria y auditoría permanente ANTES de
/// devolver cualquier dato (asistente-acceso-de-soporte-al-historial,
/// design.md D10 de asistente-historial-conversaciones).
/// </summary>
/// <remarks>
/// A diferencia de <see cref="IRegistroDeHistorial"/> y del resto de los
/// registros del módulo, ACÁ UN FALLO DE ESCRITURA SÍ TIENE QUE ROMPER LA
/// LECTURA: la garantía es «se audita antes de leer», y una excepción tragada
/// convertiría eso en «se audita salvo que la auditoría falle», que es
/// exactamente el acceso sin auditar que este capability existe para impedir.
/// </remarks>
public interface IConsultasDeAuditoriaDeSoporte
{
    /// <summary>
    /// Lista las conversaciones de <paramref name="sujeto"/> (sin sus turnos).
    /// Audita como una LISTA, sin nombrar ninguna conversación en particular.
    /// </summary>
    Task<IReadOnlyList<ConversacionResumen>> ListarAsync(
        Guid lector, Guid sujeto, string razon, CancellationToken ct);

    /// <summary>
    /// Lee una conversación de <paramref name="sujeto"/>, con sus turnos.
    /// <c>null</c> si esa conversación no existe o no es de ese sujeto — los
    /// dos casos se tratan igual, para no filtrar cuál de los dos fue.
    /// Audita nombrando ESTA conversación en particular.
    /// </summary>
    Task<ConversacionDetalle?> LeerAsync(
        Guid lector, Guid sujeto, Guid hiloId, string razon, CancellationToken ct);
}
