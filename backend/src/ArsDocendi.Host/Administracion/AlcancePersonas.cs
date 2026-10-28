using System.Security.Claims;
using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Shared.Auth;

namespace ArsDocendi.Host.Administracion;

/// <summary>
/// Ámbito del actor sobre personas: <c>null</c> = todas (<c>personas.ver</c>); si sólo tiene
/// <c>personas.ver_ambito</c>, las materias donde es Jefe de Cátedra. Sin ninguno, 403.
/// </summary>
public sealed class AlcancePersonas(ResolutorAlcanceDocentes resolutor)
{
    public async Task<IReadOnlySet<Guid>?> MateriasVisiblesAsync(ClaimsPrincipal usuario, CancellationToken ct)
    {
        if (usuario.HasClaim(Permisos.Claim, Permisos.PersonasVer)) return null;
        if (!usuario.HasClaim(Permisos.Claim, Permisos.PersonasVerAmbito))
        {
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Prohibido, "forbidden", "No tenés permiso para ver personas.");
        }
        var usuarioId = Guid.Parse(usuario.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return await resolutor.ObtenerMateriasDeJefaturaAsync(usuarioId, ct);
    }
}
