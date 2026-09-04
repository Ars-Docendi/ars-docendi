using Microsoft.EntityFrameworkCore;

namespace ArsDocendi.Shared.Identity;

/// <summary>
/// Implementación de <see cref="IConsultasIdentity"/> sobre <see cref="IdentityDbContext"/>.
/// Todas las consultas son <c>AsNoTracking</c>: se usan para autorizar, nunca para mutar.
/// <para>
/// El filtro de asignaciones vigentes (<c>deleted_at IS NULL</c>) lo aplica el
/// query filter global de <see cref="UsuarioRol"/>, no cada consulta.
/// </para>
/// </summary>
internal sealed class ConsultasIdentity(IdentityDbContext db) : IConsultasIdentity
{
    public Task<Persona?> ObtenerPersonaAsync(Guid personaId, CancellationToken ct) =>
        db.Personas.AsNoTracking().FirstOrDefaultAsync(p => p.Id == personaId, ct);

    public Task<bool> TieneRolEnMateriaAsync(Guid usuarioId, string codigoRol, Guid materiaId, CancellationToken ct) =>
        db.UsuarioRoles
          .AsNoTracking()
          .AnyAsync(ur => ur.UsuarioId == usuarioId
                       && ur.Usuario!.Activo
                       && ur.MateriaId == materiaId
                       && ur.Rol!.Codigo == codigoRol
                       && ur.Rol.EsSistema
                       && ur.Rol.Activo, ct);

    public Task<bool> TieneRolEnCarreraAsync(Guid usuarioId, string codigoRol, Guid carreraId, CancellationToken ct) =>
        db.UsuarioRoles
          .AsNoTracking()
          .AnyAsync(ur => ur.UsuarioId == usuarioId
                       && ur.Usuario!.Activo
                       && ur.CarreraId == carreraId
                       && ur.Rol!.Codigo == codigoRol
                       && ur.Rol.EsSistema
                       && ur.Rol.Activo, ct);

    public Task<bool> TieneRolGlobalAsync(Guid usuarioId, string codigoRol, CancellationToken ct) =>
        db.UsuarioRoles
          .AsNoTracking()
          .AnyAsync(ur => ur.UsuarioId == usuarioId
                       && ur.Usuario!.Activo
                       && ur.MateriaId == null
                       && ur.CarreraId == null
                       && ur.Rol!.Codigo == codigoRol
                       && ur.Rol.EsSistema
                       && ur.Rol.Activo, ct);

    public async Task<IReadOnlyList<string>> ObtenerCodigosDeRolesDeSistemaAsync(Guid usuarioId, CancellationToken ct) =>
        await db.UsuarioRoles
                .AsNoTracking()
                .Where(ur => ur.UsuarioId == usuarioId
                          && ur.Usuario!.Activo
                          && ur.Rol!.EsSistema
                          && ur.Rol.Activo)
                .Select(ur => ur.Rol!.Codigo)
                .Distinct()
                .ToListAsync(ct);

    // Los permisos SÍ consideran los roles creados por el operador: agrupar permisos
    // es exactamente para lo que sirven. Lo que no habilitan es el circuito de
    // aprobación, y eso lo resuelve ObtenerCodigosDeRolesDeSistemaAsync.
    public async Task<IReadOnlyList<string>> ObtenerCodigosDePermisosAsync(Guid usuarioId, CancellationToken ct) =>
        await db.UsuarioRoles
                .AsNoTracking()
                .Where(ur => ur.UsuarioId == usuarioId && ur.Usuario!.Activo && ur.Rol!.Activo)
                .SelectMany(ur => ur.Rol!.Permisos)
                .Select(rp => rp.Permiso!.Codigo)
                .Distinct()
                .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> ObtenerMateriasDeRolAsync(
        Guid usuarioId, string codigoRol, CancellationToken ct) =>
        await db.UsuarioRoles
                .AsNoTracking()
                .Where(ur => ur.UsuarioId == usuarioId
                          && ur.Usuario!.Activo
                          && ur.MateriaId != null
                          && ur.Rol!.Codigo == codigoRol
                          && ur.Rol.EsSistema
                          && ur.Rol.Activo)
                .Select(ur => ur.MateriaId!.Value)
                .Distinct()
                .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> ObtenerCarrerasDeRolAsync(
        Guid usuarioId, string codigoRol, CancellationToken ct) =>
        await db.UsuarioRoles
                .AsNoTracking()
                .Where(ur => ur.UsuarioId == usuarioId
                          && ur.Usuario!.Activo
                          && ur.CarreraId != null
                          && ur.Rol!.Codigo == codigoRol
                          && ur.Rol.EsSistema
                          && ur.Rol.Activo)
                .Select(ur => ur.CarreraId!.Value)
                .Distinct()
                .ToListAsync(ct);

    public async Task<Guid?> ObtenerCarreraDeMateriaAsync(Guid materiaId, CancellationToken ct) =>
        await db.Materias
                .AsNoTracking()
                .Where(m => m.Id == materiaId)
                .Select(m => (Guid?)m.CarreraId)
                .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Materia>> ListarMateriasActivasAsync(CancellationToken ct) =>
        await db.Materias.AsNoTracking()
            .Include(m => m.Carrera)
            .Where(m => m.Activo)
            .OrderBy(m => m.Nombre)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Persona>> ListarPersonasAsync(CancellationToken ct) =>
        await db.Personas.AsNoTracking()
            .OrderBy(p => p.Apellido)
            .ThenBy(p => p.Nombre)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Usuario>> ListarUsuariosAsync(CancellationToken ct) =>
        await db.Usuarios.AsNoTracking()
            .Include(u => u.Persona)
            .OrderBy(u => u.NombreParaMostrar)
            .ToListAsync(ct);
}
