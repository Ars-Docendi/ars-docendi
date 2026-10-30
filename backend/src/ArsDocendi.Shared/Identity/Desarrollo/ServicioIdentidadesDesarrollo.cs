using Microsoft.EntityFrameworkCore;

namespace ArsDocendi.Shared.Identity.Desarrollo;

/// <summary>
/// Identidades sembradas para ingresar en desarrollo. Sólo eligen quién ingresa: la
/// asignación de rol se elige después, igual que en el ingreso con Microsoft.
/// </summary>
public sealed class ServicioIdentidadesDesarrollo(IdentityDbContext db)
{
    public async Task<IReadOnlyList<IdentidadDesarrolloDto>> ListarAsync(CancellationToken ct)
    {
        var usuarios = await Elegibles()
            .Include(u => u.Roles).ThenInclude(a => a.Rol)
            .OrderBy(u => u.NombreParaMostrar)
            .ToListAsync(ct);
        return usuarios
            .Select(u => new IdentidadDesarrolloDto(
                u.Id,
                u.NombreParaMostrar,
                u.Upn,
                u.Roles
                    .Where(a => a.EliminadoEn is null && a.Rol is { Activo: true })
                    .Select(a => a.Rol!.Nombre)
                    .Distinct()
                    .Order()
                    .ToArray()))
            .ToArray();
    }

    public Task<bool> EsElegibleAsync(Guid usuarioId, CancellationToken ct) =>
        Elegibles().AnyAsync(u => u.Id == usuarioId, ct);

    private IQueryable<Usuario> Elegibles() =>
        db.Usuarios.AsNoTracking()
            .Where(u => u.Activo && db.IdentidadesSembradas.Any(s => s.UsuarioId == u.Id));
}
