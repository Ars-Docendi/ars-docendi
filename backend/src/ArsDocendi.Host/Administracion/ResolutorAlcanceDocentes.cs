using ArsDocendi.Shared.Identity;

namespace ArsDocendi.Host.Administracion;

public sealed class ResolutorAlcanceDocentes(IConsultasIdentity consultasIdentity)
{
    public async Task<IReadOnlySet<Guid>> ObtenerMateriasDeJefaturaAsync(
        Guid usuarioId,
        CancellationToken ct) =>
        (await consultasIdentity.ObtenerMateriasDeRolAsync(
            usuarioId, "jefe_catedra", ct)).ToHashSet();
}
