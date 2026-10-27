namespace Modules.Asistente.Application;

/// <summary>
/// Si un actor tiene hoy acceso operativo al asistente (asistente-acceso-granular), según <see
/// cref="ReglaDeAccesoEfectivo"/>.
/// </summary>
internal interface IAccesoAlAsistente
{
    Task<bool> TieneAccesoAsync(Guid actor, CancellationToken ct);
}
