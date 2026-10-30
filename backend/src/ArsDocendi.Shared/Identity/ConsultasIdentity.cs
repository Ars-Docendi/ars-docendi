using ArsDocendi.Shared.Auth;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArsDocendi.Shared.Identity;

/// <summary>
/// Implementación de <see cref="IConsultasIdentity"/> sobre <see cref="IdentityDbContext"/>.
/// Todas las consultas son <c>AsNoTracking</c>: se usan para autorizar, nunca para mutar.
/// <para>
/// El filtro de asignaciones vigentes (<c>deleted_at IS NULL</c>) lo aplica el
/// query filter global de <see cref="UsuarioRol"/>, no cada consulta.
/// </para>
/// <para>
/// Cuando se consulta por el propio actor de la sesión, sólo cuenta la asignación con
/// la que opera: las demás asignaciones de la persona no aportan rol, ámbito ni
/// permisos. Las consultas sobre terceros consideran todas sus asignaciones.
/// </para>
/// </summary>
internal sealed class ConsultasIdentity(IdentityDbContext db, ICurrentUser? actor = null) : IConsultasIdentity
{
    private IQueryable<UsuarioRol> AsignacionesDe(Guid usuarioId)
    {
        var consulta = db.UsuarioRoles.AsNoTracking().Where(ur => ur.UsuarioId == usuarioId);
        return actor?.AsignacionActivaId is { } activa && actor.UserId == usuarioId.ToString()
            ? consulta.Where(ur => ur.Id == activa)
            : consulta;
    }

    public Task<Usuario?> ObtenerUsuarioAsync(Guid usuarioId, CancellationToken ct) =>
        db.Usuarios.AsNoTracking().Include(u => u.Persona).SingleOrDefaultAsync(u => u.Id == usuarioId, ct);

    public async Task<IReadOnlyList<Usuario>> ObtenerUsuariosAsync(IReadOnlyCollection<Guid> usuarioIds, CancellationToken ct) =>
        await db.Usuarios.AsNoTracking().Include(u => u.Persona).Where(u => usuarioIds.Contains(u.Id)).ToListAsync(ct);

    public async Task<IReadOnlyList<Persona>> ObtenerPersonasAsync(IReadOnlyCollection<Guid> personaIds, CancellationToken ct) =>
        await db.Personas.AsNoTracking().Where(p => personaIds.Contains(p.Id)).ToListAsync(ct);

    public async Task<IReadOnlyList<Usuario>> BuscarCandidatosAsync(FiltroCandidatosIdentity filtro, CancellationToken ct)
    {
        if (filtro.Limite is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(filtro), "El límite debe estar entre 1 y 50.");
        var todosLosRoles = filtro.CodigosRolesAdmitidos is null;
        var admitidos = filtro.CodigosRolesAdmitidos?.ToArray() ?? [];
        var excluidos = filtro.CodigosRolesExcluidos?.ToArray() ?? [];
        var incluidos = filtro.UsuarioIdsIncluidos?.ToArray() ?? [];
        var busqueda = filtro.Busqueda?.Trim() ?? string.Empty;
        // Todos los filtros quedan en SQL. strpos trata %/_ como caracteres, no comodines.
        return await db.Usuarios.FromSqlRaw("""
            SELECT u.*, u.xmin FROM identity.users u
            LEFT JOIN identity.personas p ON p.id = u.persona_id
            WHERE u.is_active
              AND EXISTS (SELECT 1 FROM identity.user_roles a JOIN identity.roles r ON r.id=a.role_id
                  WHERE a.user_id=u.id AND a.deleted_at IS NULL AND r.is_active AND r.es_sistema)
              AND (u.id = ANY(@incluidos) OR (
                  (@todosLosRoles OR EXISTS (
                      SELECT 1 FROM identity.user_roles a JOIN identity.roles r ON r.id=a.role_id
                      WHERE a.user_id=u.id AND a.deleted_at IS NULL AND r.is_active AND r.es_sistema
                        AND r.code = ANY(@admitidos)))
                  AND NOT EXISTS (
                      SELECT 1 FROM identity.user_roles a JOIN identity.roles r ON r.id=a.role_id
                      WHERE a.user_id=u.id AND a.deleted_at IS NULL AND r.is_active AND r.es_sistema
                        AND r.code = ANY(@excluidos))))
              AND NOT EXISTS (
                  SELECT 1 FROM regexp_split_to_table(identity.normalizar_busqueda(@busqueda), '\\s+') palabra
                  WHERE palabra <> '' AND strpos(identity.normalizar_busqueda(concat_ws(' ',
                      u.display_name,p.nombre,p.apellido,u.upn,p.legajo,p.documento)), palabra) = 0)
            """,
            new NpgsqlParameter("incluidos", incluidos),
            new NpgsqlParameter("todosLosRoles", todosLosRoles),
            new NpgsqlParameter("admitidos", admitidos),
            new NpgsqlParameter("excluidos", excluidos),
            new NpgsqlParameter("busqueda", busqueda))
            .AsNoTracking().Include(u => u.Persona)
            .OrderBy(u => u.NombreParaMostrar).ThenBy(u => u.Id).Take(filtro.Limite).ToListAsync(ct);
    }

    public Task<Persona?> ObtenerPersonaAsync(Guid personaId, CancellationToken ct) =>
        db.Personas.AsNoTracking().FirstOrDefaultAsync(p => p.Id == personaId, ct);

    public Task<bool> TieneRolEnMateriaAsync(Guid usuarioId, string codigoRol, Guid materiaId, CancellationToken ct) =>
        AsignacionesDe(usuarioId)
          .AnyAsync(ur => ur.Usuario!.Activo
                       && ur.MateriaId == materiaId
                       && ur.Rol!.Codigo == codigoRol
                       && ur.Rol.EsSistema
                       && ur.Rol.Activo, ct);

    public Task<bool> TieneRolEnCarreraAsync(Guid usuarioId, string codigoRol, Guid carreraId, CancellationToken ct) =>
        AsignacionesDe(usuarioId)
          .AnyAsync(ur => ur.Usuario!.Activo
                       && ur.CarreraId == carreraId
                       && ur.Rol!.Codigo == codigoRol
                       && ur.Rol.EsSistema
                       && ur.Rol.Activo, ct);

    public Task<bool> TieneRolGlobalAsync(Guid usuarioId, string codigoRol, CancellationToken ct) =>
        AsignacionesDe(usuarioId)
          .AnyAsync(ur => ur.Usuario!.Activo
                       && ur.MateriaId == null
                       && ur.CarreraId == null
                       && ur.Rol!.Codigo == codigoRol
                       && ur.Rol.EsSistema
                       && ur.Rol.Activo, ct);

    public async Task<IReadOnlyList<string>> ObtenerCodigosDeRolesDeSistemaAsync(Guid usuarioId, CancellationToken ct) =>
        await AsignacionesDe(usuarioId)
                .Where(ur => ur.Usuario!.Activo
                          && ur.Rol!.EsSistema
                          && ur.Rol.Activo)
                .Select(ur => ur.Rol!.Codigo)
                .Distinct()
                .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<RolDeSistema>>> ObtenerRolesDeSistemaAsync(
        IReadOnlyCollection<Guid> usuarioIds, CancellationToken ct)
    {
        var filas = await db.UsuarioRoles
            .AsNoTracking()
            .Where(ur => usuarioIds.Contains(ur.UsuarioId)
                      && ur.Usuario!.Activo
                      && ur.Rol!.EsSistema
                      && ur.Rol.Activo)
            .Select(ur => new { ur.UsuarioId, ur.Rol!.Codigo, ur.Rol.Nombre })
            .Distinct()
            .ToListAsync(ct);

        return filas
            .GroupBy(f => f.UsuarioId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<RolDeSistema>)g.Select(f => new RolDeSistema(f.Codigo, f.Nombre)).ToList());
    }

    // Los permisos SÍ consideran los roles creados por el operador: agrupar permisos
    // es exactamente para lo que sirven. Lo que no habilitan es el circuito de
    // aprobación, y eso lo resuelve ObtenerCodigosDeRolesDeSistemaAsync.
    public async Task<IReadOnlyList<string>> ObtenerCodigosDePermisosAsync(Guid usuarioId, CancellationToken ct) =>
        await AsignacionesDe(usuarioId)
                .Where(ur => ur.Usuario!.Activo && ur.Rol!.Activo)
                .SelectMany(ur => ur.Rol!.Permisos)
                .Select(rp => rp.Permiso!.Codigo)
                .Distinct()
                .ToListAsync(ct);

    // Materia canónica: la usan el Jefe de Cátedra (en todos sus planes) y el Docente
    // (junto a la carrera que devuelve ObtenerCarrerasDeRolAsync).
    public async Task<IReadOnlyList<Guid>> ObtenerMateriasDeRolAsync(
        Guid usuarioId, string codigoRol, CancellationToken ct) =>
        await AsignacionesDe(usuarioId)
                .Where(ur => ur.Usuario!.Activo
                          && ur.MateriaId != null
                          && ur.Rol!.Codigo == codigoRol
                          && ur.Rol.EsSistema
                          && ur.Rol.Activo)
                .Select(ur => ur.MateriaId!.Value)
                .Distinct()
                .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> ObtenerCarrerasDeRolAsync(
        Guid usuarioId, string codigoRol, CancellationToken ct) =>
        await AsignacionesDe(usuarioId)
                .Where(ur => ur.Usuario!.Activo
                          && ur.CarreraId != null
                          && ur.Rol!.Codigo == codigoRol
                          && ur.Rol.EsSistema
                          && ur.Rol.Activo)
                .Select(ur => ur.CarreraId!.Value)
                .Distinct()
                .ToListAsync(ct);


    public async Task<IReadOnlyList<Materia>> ListarMateriasAsync(CancellationToken ct) =>
        await db.Materias.AsNoTracking()
            .OrderBy(m => m.Nombre)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<MateriaPlan>> ListarMateriasPlanAsync(CancellationToken ct) =>
        await db.MateriasPlan.AsNoTracking()
            .Include(mp => mp.Materia)
            .Include(mp => mp.Plan).ThenInclude(p => p!.Carrera)
            .OrderBy(mp => mp.Materia!.Nombre)
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
