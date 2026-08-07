using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using Modules.Designaciones.Domain;

namespace Modules.Designaciones.Services;

/// <summary>
/// Arma el <see cref="ActorContexto"/> del usuario autenticado leyendo identity.
/// <para>
/// Resolver el ámbito UNA vez, acá, es lo que permite que la máquina de estados sea
/// una función pura: los guards necesitan saber qué roles y qué materias/carreras
/// tiene el actor, pero no tienen por qué consultarlo.
/// </para>
/// <para>
/// Sólo LEE identity, nunca escribe: es la disciplina que fija el corolario del
/// invariante #4.
/// </para>
/// </summary>
internal sealed class ResolutorActor(ICurrentUser usuarioActual, IConsultasIdentity identity)
{
    public async Task<ActorContexto> ResolverAsync(CancellationToken ct)
    {
        if (!usuarioActual.IsAuthenticated || !Guid.TryParse(usuarioActual.UserId, out var usuarioId))
        {
            throw new ErrorDominioPedido("La acción requiere un usuario autenticado.");
        }

        // Sólo roles de sistema: un rol creado por el operador agrupa permisos pero no
        // participa del circuito de aprobación, y devolverlo acá invitaría a que la
        // máquina de estados lo tratara como si lo hiciera.
        var roles = await identity.ObtenerCodigosDeRolesDeSistemaAsync(usuarioId, ct);

        var materias = roles.Contains(RolesCircuito.JefeCatedra)
            ? await identity.ObtenerMateriasDeRolAsync(usuarioId, RolesCircuito.JefeCatedra, ct)
            : [];

        var carreras = roles.Contains(RolesCircuito.CoordinadorCarrera)
            ? await identity.ObtenerCarrerasDeRolAsync(usuarioId, RolesCircuito.CoordinadorCarrera, ct)
            : [];

        return new ActorContexto(
            usuarioId,
            roles.ToHashSet(),
            materias.ToHashSet(),
            carreras.ToHashSet());
    }
}
