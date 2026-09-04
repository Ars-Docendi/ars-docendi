using Microsoft.EntityFrameworkCore;

namespace ArsDocendi.Shared.Identity.Desarrollo;

public sealed class ServicioIdentidadesDesarrollo(IdentityDbContext db)
{
    public async Task<IReadOnlyList<IdentidadDesarrolloDto>> ListarAsync(CancellationToken ct)
    {
        var usuarios = await ConsultaElegibles()
            .OrderBy(u => u.NombreParaMostrar)
            .ToListAsync(ct);
        return usuarios.Select(Mapear).ToArray();
    }

    public async Task<IdentidadAutenticadaDesarrollo?> ValidarAsync(
        Guid usuarioId,
        string rolCodigo,
        CancellationToken ct)
    {
        var usuario = await ConsultaElegibles()
            .SingleOrDefaultAsync(u => u.Id == usuarioId, ct);
        if (usuario is null) return null;

        var asignaciones = usuario.Roles
            .Where(a => a.EliminadoEn is null
                && a.Rol is { Activo: true }
                && a.Rol.Codigo == rolCodigo)
            .ToArray();
        if (asignaciones.Length == 0) return null;

        var permisos = asignaciones
            .SelectMany(a => a.Rol!.Permisos)
            .Where(rp => rp.Permiso is not null)
            .Select(rp => rp.Permiso!.Codigo)
            .Distinct()
            .Order()
            .ToArray();
        return new IdentidadAutenticadaDesarrollo(
            usuario.Id,
            usuario.NombreParaMostrar,
            usuario.Upn,
            rolCodigo,
            permisos);
    }

    private IQueryable<Usuario> ConsultaElegibles() =>
        db.Usuarios.AsNoTracking()
            .Where(u => u.Activo && db.IdentidadesSembradas.Any(s => s.UsuarioId == u.Id))
            .Include(u => u.Roles).ThenInclude(a => a.Rol).ThenInclude(r => r!.Permisos)
                .ThenInclude(rp => rp.Permiso)
            .Include(u => u.Roles).ThenInclude(a => a.Materia)
            .Include(u => u.Roles).ThenInclude(a => a.Carrera);

    private static IdentidadDesarrolloDto Mapear(Usuario usuario)
    {
        var roles = usuario.Roles
            .Where(a => a.EliminadoEn is null && a.Rol is { Activo: true })
            .GroupBy(a => new { a.Rol!.Codigo, a.Rol.Nombre })
            .Select(g => new RolDesarrolloDto(
                g.Key.Codigo,
                g.Key.Nombre,
                g.Where(a => a.Materia is not null)
                    .Select(a => new AmbitoDesarrolloDto(
                        a.Materia!.Id, a.Materia.Codigo, a.Materia.Nombre))
                    .DistinctBy(m => m.Id).OrderBy(m => m.Nombre).ToArray(),
                g.Where(a => a.Carrera is not null)
                    .Select(a => new AmbitoDesarrolloDto(
                        a.Carrera!.Id, a.Carrera.Codigo, a.Carrera.Nombre))
                    .DistinctBy(c => c.Id).OrderBy(c => c.Nombre).ToArray()))
            .OrderBy(r => r.Nombre)
            .ToArray();
        return new IdentidadDesarrolloDto(
            usuario.Id, usuario.NombreParaMostrar, usuario.Upn, roles);
    }
}
