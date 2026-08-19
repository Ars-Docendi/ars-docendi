using Microsoft.EntityFrameworkCore;
using ArsDocendi.Shared.Aplicacion;
using Npgsql;

namespace ArsDocendi.Shared.Identity.Administracion;

public interface IRepositorioUsuarios
{
    Task<IReadOnlyList<Usuario>> ListarAsync(CancellationToken ct);
    Task<Usuario?> ObtenerAsync(Guid id, CancellationToken ct);
    Task<bool> ExisteUpnAsync(string upn, Guid? exceptoId, CancellationToken ct);
    Task<bool> ExisteDocumentoAsync(string documento, Guid? exceptoPersonaId, CancellationToken ct);
    Task<IReadOnlyList<Rol>> ObtenerRolesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<IReadOnlyList<Carrera>> ObtenerCarrerasAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<IReadOnlyList<Materia>> ObtenerMateriasAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<CatalogosUsuariosDto> ObtenerCatalogosAsync(CancellationToken ct);
    void Agregar(Persona persona, Usuario usuario);
    void EsperarVersion(Usuario usuario, uint version);
    Task GuardarAsync(CancellationToken ct);
}

internal sealed class RepositorioUsuarios(IdentityDbContext db) : IRepositorioUsuarios
{
    public async Task<IReadOnlyList<Usuario>> ListarAsync(CancellationToken ct) =>
        await ConsultaCompleta()
            .OrderBy(u => u.Persona!.Apellido)
            .ThenBy(u => u.Persona!.Nombre)
            .ToListAsync(ct);

    public Task<Usuario?> ObtenerAsync(Guid id, CancellationToken ct) =>
        ConsultaCompleta(false).SingleOrDefaultAsync(u => u.Id == id, ct);

    public Task<bool> ExisteUpnAsync(string upn, Guid? exceptoId, CancellationToken ct) =>
        db.Usuarios.AsNoTracking().AnyAsync(u => u.Upn == upn && u.Id != exceptoId, ct);

    public Task<bool> ExisteDocumentoAsync(
        string documento,
        Guid? exceptoPersonaId,
        CancellationToken ct) =>
        db.Personas.AsNoTracking().AnyAsync(
            p => p.Documento == documento && p.Id != exceptoPersonaId, ct);

    public async Task<IReadOnlyList<Rol>> ObtenerRolesAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct) =>
        // Estas entidades se vinculan a nuevas asignaciones. Mantenerlas bajo
        // tracking evita que EF interprete los roles existentes como inserts.
        await db.Roles.Where(r => ids.Contains(r.Id) && r.Activo).ToListAsync(ct);

    public async Task<IReadOnlyList<Materia>> ObtenerMateriasAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct) =>
        await db.Materias.AsNoTracking().Where(m => ids.Contains(m.Id) && m.Activo).ToListAsync(ct);

    public async Task<IReadOnlyList<Carrera>> ObtenerCarrerasAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct) =>
        await db.Carreras.AsNoTracking().Where(c => ids.Contains(c.Id) && c.Activo).ToListAsync(ct);

    public async Task<CatalogosUsuariosDto> ObtenerCatalogosAsync(CancellationToken ct)
    {
        var roles = await db.Roles.AsNoTracking().Where(r => r.Activo)
            .OrderBy(r => r.Nombre)
            .Select(r => new RolCatalogoDto(r.Id, r.Codigo, r.Nombre, r.Ambito, r.EsSistema))
            .ToListAsync(ct);
        var carreras = await db.Carreras.AsNoTracking().Where(c => c.Activo)
            .OrderBy(c => c.Nombre)
            .Select(c => new OpcionCatalogoDto(c.Id, c.Codigo, c.Nombre))
            .ToListAsync(ct);
        var materias = await db.Materias.AsNoTracking().Where(m => m.Activo)
            .OrderBy(m => m.Nombre)
            .Select(m => new OpcionCatalogoDto(m.Id, m.Codigo, m.Nombre))
            .ToListAsync(ct);
        return new CatalogosUsuariosDto(roles, carreras, materias);
    }

    public void Agregar(Persona persona, Usuario usuario)
    {
        db.Personas.Add(persona);
        db.Usuarios.Add(usuario);
    }

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
                "users_upn_key" => new ExcepcionAplicacion(
                    TipoErrorAplicacion.Conflicto,
                    "identity-upn-conflict",
                    "Ya existe otro usuario con esa UPN."),
                "personas_documento_key" => new ExcepcionAplicacion(
                    TipoErrorAplicacion.Conflicto,
                    "identity-document-conflict",
                    "Ya existe otra persona con ese documento."),
                "personas_legajo_key" => new ExcepcionAplicacion(
                    TipoErrorAplicacion.Conflicto,
                    "identity-file-number-conflict",
                    "Ya existe otra persona con ese legajo."),
                _ => null,
            };
            if (traducido is not null) throw traducido;
            throw;
        }
    }

    private IQueryable<Usuario> ConsultaCompleta(bool sinTracking = true)
    {
        var consulta = db.Usuarios
            .Include(u => u.Persona)
            .Include(u => u.Roles).ThenInclude(ur => ur.Rol)
            .AsSplitQuery();
        return sinTracking ? consulta.AsNoTracking() : consulta;
    }
}
