namespace ArsDocendi.Shared.Identity.Ingreso;

/// <summary>
/// Resuelve en cada solicitud con qué rol y permisos opera una sesión. Devuelve
/// <c>null</c> si el usuario ya no está activo o no tiene roles vigentes, para que
/// una desactivación o una revocación apliquen en la solicitud siguiente.
/// </summary>
public sealed class ServicioSesion(RepositorioIngreso repositorio)
{
    public async Task<IdentidadSesion?> ObtenerAsync(Guid usuarioId, CancellationToken ct)
    {
        var usuario = await repositorio.ObtenerConRolesVigentesAsync(usuarioId, ct);
        if (usuario is not { Activo: true }) return null;

        // Varios roles todavía no se eligen: se toma uno fijo, el primero por nombre.
        var roles = usuario.Roles
            .Select(a => a.Rol!)
            .DistinctBy(r => r.Id)
            .OrderBy(r => r.Nombre, StringComparer.Ordinal)
            .ToArray();
        if (roles.Length == 0) return null;

        var rol = roles[0];
        var permisos = rol.Permisos
            .Where(rp => rp.Permiso is not null)
            .Select(rp => rp.Permiso!.Codigo)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToArray();
        return new IdentidadSesion(
            usuario.Id,
            usuario.NombreParaMostrar,
            usuario.Upn,
            rol.Codigo,
            rol.Nombre,
            permisos,
            roles.Length);
    }
}
