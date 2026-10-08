using ArsDocendi.Shared.Identity;

namespace ArsDocendi.IntegrationTests.Infraestructura;

/// <summary>
/// Fake en memoria de <see cref="IConsultasIdentity"/>, para
/// <see cref="BancoDelAsistente"/>: sólo lo que <c>CapaConversacional</c>
/// necesita (permisos y roles por actor), configurable por test. Todo lo
/// demás lanza <see cref="NotImplementedException"/> — nada de lo que se
/// ejercita a este nivel lo llama.
/// </summary>
internal sealed class ConsultasIdentityFalsa : IConsultasIdentity
{
    private readonly Dictionary<Guid, string[]> _permisosPorActor = [];
    private readonly Dictionary<Guid, string[]> _rolesPorActor = [];

    public void FijarPermisos(Guid actor, params string[] permisos) => _permisosPorActor[actor] = permisos;

    public void FijarRoles(Guid actor, params string[] roles) => _rolesPorActor[actor] = roles;

    public Task<IReadOnlyList<string>> ObtenerCodigosDePermisosAsync(Guid usuarioId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>(
            _permisosPorActor.TryGetValue(usuarioId, out var permisos) ? permisos : []);

    public Task<IReadOnlyList<string>> ObtenerCodigosDeRolesDeSistemaAsync(Guid usuarioId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>(
            _rolesPorActor.TryGetValue(usuarioId, out var roles) ? roles : []);

    public Task<Persona?> ObtenerPersonaAsync(Guid personaId, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<bool> TieneRolEnMateriaAsync(Guid usuarioId, string codigoRol, Guid materiaId, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<bool> TieneRolEnCarreraAsync(Guid usuarioId, string codigoRol, Guid carreraId, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<bool> TieneRolGlobalAsync(Guid usuarioId, string codigoRol, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<Guid>> ObtenerMateriasDeRolAsync(
        Guid usuarioId, string codigoRol, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<Guid>> ObtenerCarrerasDeRolAsync(
        Guid usuarioId, string codigoRol, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<Guid?> ObtenerCarreraDeMateriaAsync(Guid materiaId, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<Materia>> ListarMateriasAsync(CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<Persona>> ListarPersonasAsync(CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<Usuario>> ListarUsuariosAsync(CancellationToken ct) =>
        throw new NotImplementedException();
}
