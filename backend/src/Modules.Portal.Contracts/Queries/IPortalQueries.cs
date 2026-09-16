namespace Modules.Portal.Contracts.Queries;

using Dtos;

public interface IPortalQueries
{
    Task<PerfilDocenteDto?> ObtenerPerfilAsync(Guid personaId, CancellationToken ct);
}
