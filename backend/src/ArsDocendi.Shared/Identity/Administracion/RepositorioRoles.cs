using ArsDocendi.Shared.Aplicacion;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArsDocendi.Shared.Identity.Administracion;

public interface IRepositorioRoles
{
    Task<IReadOnlyList<Rol>> ListarAsync(CancellationToken ct);
    Task<Rol?> ObtenerAsync(Guid id, bool tracking, CancellationToken ct);
    Task<IReadOnlyList<Permiso>> ListarPermisosAsync(CancellationToken ct);
    Task<IReadOnlyList<Permiso>> ObtenerPermisosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<bool> ExisteCodigoAsync(string codigo, Guid? exceptoId, CancellationToken ct);
    Task<bool> ExisteNombreAsync(string nombre, Guid? exceptoId, CancellationToken ct);
    void Agregar(Rol rol);
    void ReemplazarPermisos(Rol rol, IReadOnlyCollection<Guid> permisoIds);
    void EsperarVersion(Rol rol, uint version);
    void MarcarCambioDeMembresia(Rol rol);
    Task GuardarAsync(CancellationToken ct);
}

internal sealed class RepositorioRoles(IdentityDbContext db) : IRepositorioRoles
{
    public async Task<IReadOnlyList<Rol>> ListarAsync(CancellationToken ct) =>
        await ConsultaCompleta(false)
            .OrderBy(r => r.Nombre)
            .ToListAsync(ct);

    public Task<Rol?> ObtenerAsync(Guid id, bool tracking, CancellationToken ct) =>
        ConsultaCompleta(tracking).SingleOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<Permiso>> ListarPermisosAsync(CancellationToken ct) =>
        await db.Permisos.AsNoTracking().OrderBy(p => p.Nombre).ToListAsync(ct);

    public async Task<IReadOnlyList<Permiso>> ObtenerPermisosAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct) =>
        await db.Permisos.AsNoTracking().Where(p => ids.Contains(p.Id)).ToListAsync(ct);

    public Task<bool> ExisteNombreAsync(string nombre, Guid? exceptoId, CancellationToken ct) =>
        db.Roles.AsNoTracking().AnyAsync(r => r.Activo
            && r.Nombre.Trim().ToLower() == nombre.Trim().ToLower()
            && r.Id != exceptoId, ct);

    public Task<bool> ExisteCodigoAsync(string codigo, Guid? exceptoId, CancellationToken ct) =>
        db.Roles.AsNoTracking().AnyAsync(r => r.Codigo == codigo && r.Id != exceptoId, ct);

    public void Agregar(Rol rol) => db.Roles.Add(rol);

    public void ReemplazarPermisos(Rol rol, IReadOnlyCollection<Guid> permisoIds)
    {
        var deseados = permisoIds.ToHashSet();
        var existentes = rol.Permisos.Select(rp => rp.PermisoId).ToHashSet();
        db.RolPermisos.RemoveRange(rol.Permisos.Where(rp => !deseados.Contains(rp.PermisoId)));
        var ahora = DateTimeOffset.UtcNow;
        foreach (var permisoId in deseados.Except(existentes))
        {
            rol.Permisos.Add(new RolPermiso
            {
                RolId = rol.Id,
                PermisoId = permisoId,
                CreadoEn = ahora,
            });
        }
    }

    public void EsperarVersion(Rol rol, uint version) =>
        db.Entry(rol).Property(r => r.Version).OriginalValue = version;

    public void MarcarCambioDeMembresia(Rol rol) =>
        db.Entry(rol).Property(r => r.Nombre).IsModified = true;

    public async Task GuardarAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException pg)
        {
            if (pg.SqlState == PostgresErrorCodes.UniqueViolation && pg.ConstraintName == "roles_code_key")
            {
                throw new ExcepcionAplicacion(
                    TipoErrorAplicacion.Conflicto,
                    "identity-role-code-conflict",
                    "Ya existe un rol con ese código.");
            }
            if (pg.SqlState == PostgresErrorCodes.UniqueViolation && pg.ConstraintName == "roles_nombre_activo_normalizado_key")
            {
                throw new ExcepcionAplicacion(TipoErrorAplicacion.Conflicto,
                    "identity-role-name-conflict", "Ya existe un rol activo con ese nombre.");
            }
            if (pg.SqlState == PostgresErrorCodes.CheckViolation && pg.ConstraintName == "roles_scope_asignaciones_vivas")
            {
                throw new ExcepcionAplicacion(TipoErrorAplicacion.Conflicto,
                    "identity-role-scope-conflict", "No se puede cambiar el ámbito con asignaciones vigentes.");
            }
            throw;
        }
    }

    private IQueryable<Rol> ConsultaCompleta(bool tracking) =>
        (tracking ? db.Roles : db.Roles.AsNoTracking())
            .Include(r => r.Permisos)
            .ThenInclude(rp => rp.Permiso)
            .AsSplitQuery();
}
