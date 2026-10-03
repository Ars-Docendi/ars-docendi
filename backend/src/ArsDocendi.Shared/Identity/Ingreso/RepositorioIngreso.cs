using Microsoft.EntityFrameworkCore;

namespace ArsDocendi.Shared.Identity.Ingreso;

/// <summary>
/// Lecturas de identity para el ingreso y la sesión. Sólo consultas en la fase 1:
/// rechazar un ingreso nunca escribe.
/// </summary>
public sealed class RepositorioIngreso(IdentityDbContext db)
{
    public Task<Usuario?> BuscarPorUpnAsync(string upnNormalizado, CancellationToken ct) =>
        db.Usuarios.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Upn.ToLower() == upnNormalizado, ct);

    /// <summary>El usuario con sus asignaciones vigentes (no revocadas, de roles activos).</summary>
    public Task<Usuario?> ObtenerConRolesVigentesAsync(Guid usuarioId, CancellationToken ct) =>
        db.Usuarios.AsNoTracking()
            .Include(u => u.Roles.Where(a => a.EliminadoEn == null && a.Rol!.Activo))
                .ThenInclude(a => a.Rol)
                .ThenInclude(r => r!.Permisos)
                .ThenInclude(rp => rp.Permiso)
            .AsSplitQuery()
            .SingleOrDefaultAsync(u => u.Id == usuarioId, ct);
}
