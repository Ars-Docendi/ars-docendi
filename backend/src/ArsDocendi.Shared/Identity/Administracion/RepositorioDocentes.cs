using ArsDocendi.Shared.Aplicacion;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArsDocendi.Shared.Identity.Administracion;

public interface IRepositorioDocentes
{
    Task<IReadOnlyList<Persona>> ListarPersonasAsync(CancellationToken ct);
    Task<Persona?> ObtenerPersonaAsync(Guid id, bool tracking, CancellationToken ct);
    /// <summary>Roles activos (de sistema o creados por el operador), con tracking para asignarlos.</summary>
    Task<IReadOnlyList<Rol>> ObtenerRolesActivosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<IReadOnlyList<Rol>> ListarRolesActivosAsync(CancellationToken ct);
    Task<IReadOnlyList<Carrera>> ObtenerCarrerasAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<IReadOnlyList<Carrera>> ListarCarrerasAsync(CancellationToken ct);
    Task<Persona?> BuscarPorDocumentoAsync(string documento, CancellationToken ct);
    Task<IReadOnlyList<Materia>> ObtenerMateriasAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<IReadOnlyList<Materia>> ListarMateriasAsync(CancellationToken ct);
    Task<IReadOnlyList<MateriaPlan>> ListarMateriasPlanAsync(CancellationToken ct);
    Task<bool> ExisteUpnAsync(string upn, Guid? exceptoUsuarioId, CancellationToken ct);
    Task<bool> ExisteDocumentoAsync(string documento, Guid? exceptoPersonaId, CancellationToken ct);
    void AgregarPersona(Persona persona);
    void Agregar(Persona? personaNueva, Usuario usuario);
    void AgregarAsignacion(UsuarioRol asignacion);
    void EsperarVersion(Usuario usuario, uint version);
    Task GuardarAsync(CancellationToken ct);
}

internal sealed class RepositorioDocentes(IdentityDbContext db) : IRepositorioDocentes
{
    public async Task<IReadOnlyList<Persona>> ListarPersonasAsync(CancellationToken ct) =>
        await db.Personas
            .AsNoTracking()
            .Include(p => p.Usuario)!
            .ThenInclude(u => u!.Roles)
            .ThenInclude(ur => ur.Rol)
            .AsSplitQuery()
            .OrderBy(p => p.Apellido)
            .ThenBy(p => p.Nombre)
            .ToListAsync(ct);

    public Task<Persona?> ObtenerPersonaAsync(Guid id, bool tracking, CancellationToken ct)
    {
        IQueryable<Persona> consulta = db.Personas
            .Include(p => p.Usuario)!
            .ThenInclude(u => u!.Roles)
            .ThenInclude(ur => ur.Rol)
            .AsSplitQuery();
        if (!tracking) consulta = consulta.AsNoTracking();
        return consulta.SingleOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<IReadOnlyList<Rol>> ObtenerRolesActivosAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct) =>
        await db.Roles.Where(r => ids.Contains(r.Id) && r.Activo).ToListAsync(ct);

    public async Task<IReadOnlyList<Rol>> ListarRolesActivosAsync(CancellationToken ct) =>
        await db.Roles.AsNoTracking().Where(r => r.Activo).OrderBy(r => r.Nombre).ToListAsync(ct);

    public async Task<IReadOnlyList<Carrera>> ObtenerCarrerasAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct) =>
        await db.Carreras.AsNoTracking().Where(c => ids.Contains(c.Id) && c.Activo).ToListAsync(ct);

    public async Task<IReadOnlyList<Carrera>> ListarCarrerasAsync(CancellationToken ct) =>
        await db.Carreras.AsNoTracking().Where(c => c.Activo).OrderBy(c => c.Nombre).ToListAsync(ct);

    public Task<Persona?> BuscarPorDocumentoAsync(string documento, CancellationToken ct) =>
        db.Personas.AsNoTracking().Include(p => p.Usuario)
            .SingleOrDefaultAsync(p => p.Documento == documento, ct);

    public async Task<IReadOnlyList<Materia>> ObtenerMateriasAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct) =>
        await db.Materias.AsNoTracking()
            .Where(m => ids.Contains(m.Id) && m.Activo)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Materia>> ListarMateriasAsync(CancellationToken ct) =>
        await db.Materias.AsNoTracking()
            .Where(m => m.Activo)
            .OrderBy(m => m.Nombre)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<MateriaPlan>> ListarMateriasPlanAsync(CancellationToken ct) =>
        await db.MateriasPlan.AsNoTracking()
            .Include(mp => mp.Materia)
            .Include(mp => mp.Plan).ThenInclude(p => p!.Carrera)
            .Where(mp => mp.Activo && mp.Materia!.Activo)
            .OrderBy(mp => mp.Materia!.Nombre)
            .ToListAsync(ct);

    public Task<bool> ExisteUpnAsync(string upn, Guid? exceptoUsuarioId, CancellationToken ct) =>
        db.Usuarios.AsNoTracking().AnyAsync(
            u => u.Upn.Trim().ToLower() == upn.Trim().ToLower() && u.Id != exceptoUsuarioId, ct);

    public Task<bool> ExisteDocumentoAsync(
        string documento,
        Guid? exceptoPersonaId,
        CancellationToken ct) =>
        db.Personas.AsNoTracking().AnyAsync(p =>
            p.Documento == documento && p.Id != exceptoPersonaId, ct);

    public void AgregarPersona(Persona persona) => db.Personas.Add(persona);

    public void Agregar(Persona? personaNueva, Usuario usuario)
    {
        if (personaNueva is not null) db.Personas.Add(personaNueva);
        db.Usuarios.Add(usuario);
    }

    public void AgregarAsignacion(UsuarioRol asignacion) => db.UsuarioRoles.Add(asignacion);

    public void EsperarVersion(Usuario usuario, uint version) =>
        db.Entry(usuario).Property(u => u.Version).OriginalValue = version;

    public async Task GuardarAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException pg)
        {
            var traducido = pg.ConstraintName switch
            {
                "users_upn_key" => Conflicto("identity-upn-conflict", "Ya existe otro usuario con esa UPN."),
                "personas_documento_key" => Conflicto("identity-document-conflict", "Ya existe otra persona con ese documento."),
                "personas_legajo_key" => Conflicto("identity-file-number-conflict", "Ya existe otra persona con ese legajo."),
                "user_roles_unique_assignment" => new ExcepcionAplicacion(
                    TipoErrorAplicacion.ReglaDeNegocio,
                    "identity-role-scope-conflict",
                    "No se puede repetir la misma asignación de rol y ámbito."),
                _ => null,
            };
            if (traducido is not null) throw traducido;
            throw;
        }
    }

    private static ExcepcionAplicacion Conflicto(string codigo, string mensaje) => new(
        TipoErrorAplicacion.Conflicto, codigo, mensaje);
}
