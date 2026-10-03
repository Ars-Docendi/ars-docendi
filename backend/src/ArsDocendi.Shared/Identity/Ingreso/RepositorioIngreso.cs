using Microsoft.EntityFrameworkCore;

namespace ArsDocendi.Shared.Identity.Ingreso;

/// <summary>
/// Acceso a identity para el ingreso y la sesión. La única escritura es el registro
/// de un ingreso aceptado (vínculo de la cuenta y último ingreso): rechazar nunca escribe.
/// </summary>
public sealed class RepositorioIngreso(IdentityDbContext db)
{
    public Task<Usuario?> BuscarPorCuentaAsync(Guid tenantId, Guid objectId, CancellationToken ct) =>
        db.Usuarios.AsNoTracking()
            .SingleOrDefaultAsync(u => u.AzureTid == tenantId && u.AzureOid == objectId, ct);

    public Task<Usuario> ObtenerParaActualizarAsync(Guid usuarioId, CancellationToken ct) =>
        db.Usuarios.SingleAsync(u => u.Id == usuarioId, ct);

    public Task GuardarAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

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
