namespace ArsDocendi.Shared.Identity.Ingreso;

/// <summary>
/// Resuelve en cada solicitud con qué asignación de rol y permisos opera una sesión.
/// Devuelve <c>null</c> si el usuario ya no está activo o no tiene asignaciones
/// vigentes, para que una desactivación o una revocación apliquen en la solicitud
/// siguiente.
/// </summary>
public sealed class ServicioSesion(RepositorioIngreso repositorio)
{
    /// <param name="asignacionId">
    /// La asignación elegida por la sesión. Si no se eligió o ya no es vigente, se opera
    /// con la primera vigente; con una sola, esa cuenta como elegida.
    /// </param>
    public async Task<IdentidadSesion?> ObtenerAsync(Guid usuarioId, Guid? asignacionId, CancellationToken ct)
    {
        var usuario = await repositorio.ObtenerConRolesVigentesAsync(usuarioId, ct);
        if (usuario is not { Activo: true }) return null;

        var vigentes = usuario.Roles
            .OrderBy(a => a.Rol!.Nombre, StringComparer.Ordinal)
            .ThenBy(a => NombreAmbito(a), StringComparer.Ordinal)
            .ToArray();
        if (vigentes.Length == 0) return null;

        var elegida = vigentes.FirstOrDefault(a => a.Id == asignacionId);
        var activa = elegida ?? vigentes[0];
        var permisos = activa.Rol!.Permisos
            .Where(rp => rp.Permiso is not null)
            .Select(rp => rp.Permiso!.Codigo)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToArray();
        return new IdentidadSesion(
            usuario.Id,
            usuario.NombreParaMostrar,
            usuario.Upn,
            Mapear(activa),
            elegida is not null || vigentes.Length == 1,
            permisos,
            vigentes.Select(Mapear).ToArray());
    }

    private static AsignacionSesion Mapear(UsuarioRol a) => new(
        a.Id,
        a.Rol!.Codigo,
        a.Rol.Nombre,
        a.Materia is not null ? "materia" : a.Carrera is not null ? "carrera" : "global",
        NombreAmbito(a));

    // El Docente tiene materia y carrera: se muestran las dos.
    private static string? NombreAmbito(UsuarioRol a) =>
        (a.Materia, a.Carrera) switch
        {
            ({ } materia, { } carrera) => $"{materia.Nombre} – {carrera.Nombre}",
            ({ } materia, null) => materia.Nombre,
            (null, { } carrera) => carrera.Nombre,
            _ => null,
        };
}
