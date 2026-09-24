using ArsDocendi.Shared.Auth;
using ArsDocendi.Shared.Identity;
using Modules.Tareas.Domain;

namespace Modules.Tareas.Application;

/// <summary>Persona resuelta desde identity: nombre, roles de sistema y el rol que se muestra.</summary>
public sealed record PersonaResuelta(
    Guid Id, string Nombre, string RolNombre, IReadOnlyList<string> RolesCodigo, bool Activa);

/// <summary>
/// Resuelve ids de usuario a nombre y rol leyendo identity (solo lectura). Tareas guarda
/// únicamente el id del usuario: el nombre y el rol se leen acá, así un cambio en identity
/// se refleja sin migrar datos de Tareas.
/// </summary>
public sealed class DirectorioPersonas(IConsultasIdentity identity, ICurrentUser usuarioActual)
{
    /// <summary>Todos los usuarios de identity (activos o no), para armar respuestas con nombres.</summary>
    public async Task<IReadOnlyDictionary<Guid, PersonaResuelta>> CargarAsync(CancellationToken ct)
    {
        var usuarios = await identity.ListarUsuariosAsync(ct);
        var roles = await identity.ObtenerRolesDeSistemaAsync(usuarios.Select(u => u.Id).ToArray(), ct);

        return usuarios.ToDictionary(
            u => u.Id,
            u => Resolver(u, roles.GetValueOrDefault(u.Id) ?? []));
    }

    /// <summary>Usuarios activos con al menos un rol de sistema: los candidatos a Responsable.</summary>
    public async Task<IReadOnlyList<PersonaResuelta>> ListarCandidatosAsync(CancellationToken ct) =>
        (await CargarAsync(ct)).Values
            .Where(p => p.Activa && p.RolesCodigo.Count > 0)
            .OrderBy(p => p.Nombre, StringComparer.CurrentCulture)
            .ToList();

    /// <summary>Actor de la sesión: roles de sistema vigentes que coinciden con los de la sesión activa.</summary>
    public async Task<ActorTareas> ResolverActorAsync(CancellationToken ct)
    {
        if (!usuarioActual.IsAuthenticated || !Guid.TryParse(usuarioActual.UserId, out var usuarioId))
        {
            throw new ArsDocendi.Shared.Aplicacion.ExcepcionAplicacion(
                ArsDocendi.Shared.Aplicacion.TipoErrorAplicacion.NoAutenticado,
                "no-autenticado",
                "La acción requiere un usuario autenticado.");
        }

        var todos = await identity.ObtenerRolesDeSistemaAsync([usuarioId], ct);
        var persistidos = todos.GetValueOrDefault(usuarioId) ?? [];
        var deSesion = usuarioActual.Roles.Count == 0
            ? persistidos
            : persistidos.Where(r => usuarioActual.Roles.Contains(r.Codigo)).ToList();

        var codigos = deSesion.Select(r => r.Codigo).ToHashSet();
        var principal = JerarquiaAsignacion.RolPrincipal(codigos);
        var nombre = deSesion.FirstOrDefault(r => r.Codigo == principal)?.Nombre ?? string.Empty;
        return new ActorTareas(usuarioId, codigos, nombre);
    }

    private static PersonaResuelta Resolver(Usuario usuario, IReadOnlyList<RolDeSistema> roles)
    {
        var codigos = roles.Select(r => r.Codigo).ToList();
        var principal = JerarquiaAsignacion.RolPrincipal(codigos);
        var nombreRol = roles.FirstOrDefault(r => r.Codigo == principal)?.Nombre ?? string.Empty;
        return new PersonaResuelta(usuario.Id, usuario.NombreParaMostrar, nombreRol, codigos, usuario.Activo);
    }
}
