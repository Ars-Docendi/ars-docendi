using Modules.Portal.Domain;

namespace Modules.Portal.Repositories;

public interface IRepositorioPortal
{
    Task<Perfil?> ObtenerAsync(Guid personaId, CancellationToken ct);
    Task<Perfil> ObtenerOCrearAsync(Guid personaId, CancellationToken ct);
    Task<T?> ObtenerItemAsync<T>(Guid id, Guid personaId, CancellationToken ct) where T : class;
    void Agregar<T>(T entidad) where T : class;
    void Eliminar<T>(T entidad) where T : class;
    Task GuardarAsync(CancellationToken ct);
    Task ReemplazarTagsAsync(Perfil perfil, string tipo, IReadOnlyList<string> terminos, CancellationToken ct);
}
