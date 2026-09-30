namespace Modules.Asistente.Application;

/// <summary>
/// Si un actor tiene hoy acceso operativo al asistente
/// (asistente-acceso-granular), según <see cref="ReglaDeAccesoEfectivo"/>.
/// </summary>
/// <remarks>
/// Lo consulta <c>DisponibilidadDelModeloReal</c> ANTES que el cupo: un actor
/// sin acceso no tiene cupo que mirar.
/// </remarks>
internal interface IAccesoAlAsistente
{
    Task<bool> TieneAccesoAsync(Guid actor, CancellationToken ct);
}
