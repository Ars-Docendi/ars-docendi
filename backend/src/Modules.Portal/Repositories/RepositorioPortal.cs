using Microsoft.EntityFrameworkCore;
using Modules.Portal.Domain;
using Modules.Portal.Infrastructure;

namespace Modules.Portal.Repositories;

internal sealed class RepositorioPortal(PortalDbContext db) : IRepositorioPortal
{
    public Task<Perfil?> ObtenerAsync(Guid personaId, CancellationToken ct) => Consulta(personaId).FirstOrDefaultAsync(ct);

    public async Task<Perfil> ObtenerOCrearAsync(Guid personaId, CancellationToken ct) =>
        await ObtenerAsync(personaId, ct) ?? Crear(personaId);

    public async Task<T?> ObtenerItemAsync<T>(Guid id, Guid personaId, CancellationToken ct) where T : class
    {
        if (typeof(T) == typeof(DocenteHabilidad))
            return await db.DocenteHabilidades.FirstOrDefaultAsync(x => x.PerfilId == id, ct) as T;
        if (typeof(T) == typeof(Proyecto))
            return await db.Proyectos.Include(x => x.Documento)
                .Join(db.Perfiles, item => item.PerfilId, perfil => perfil.Id,
                    (item, perfil) => new { item, perfil })
                .Where(x => x.item.Id == id && x.perfil.PersonaId == personaId)
                .Select(x => x.item).FirstOrDefaultAsync(ct) as T;

        var entidad = db.Set<T>();
        return await entidad.Join(db.Perfiles, item => EF.Property<Guid>(item, "PerfilId"), perfil => perfil.Id,
            (item, perfil) => new { item, perfil })
            .Where(x => EF.Property<Guid>(x.item, "Id") == id && x.perfil.PersonaId == personaId)
            .Select(x => x.item).FirstOrDefaultAsync(ct);
    }

    public void Agregar<T>(T entidad) where T : class => db.Add(entidad);
    public void Eliminar<T>(T entidad) where T : class => db.Remove(entidad);
    public Task GuardarAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public async Task ReemplazarTagsAsync(Perfil perfil, string tipo, IReadOnlyList<string> terminos, CancellationToken ct)
    {
        var actuales = await db.DocenteHabilidades.Where(x => x.PerfilId == perfil.Id && x.Tipo == tipo).ToListAsync(ct);
        db.DocenteHabilidades.RemoveRange(actuales);
        foreach (var original in terminos.Select(x => x.Trim()).Where(x => x.Length > 0)
                     .GroupBy(Normalizar).Select(x => x.First()))
        {
            var habilidad = await db.Habilidades.FirstOrDefaultAsync(x => x.TerminoNorm == Normalizar(original), ct);
            if (habilidad is null)
            {
                habilidad = new Habilidad { Id = Guid.NewGuid(), Termino = original, TerminoNorm = Normalizar(original), Sugerido = true };
                db.Habilidades.Add(habilidad);
            }
            db.DocenteHabilidades.Add(new DocenteHabilidad { PerfilId = perfil.Id, HabilidadId = habilidad.Id, Tipo = tipo });
        }
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<Perfil> Consulta(Guid personaId) => db.Perfiles
        .Include(x => x.Contacto).Include(x => x.Cv)
        .Include(x => x.Experiencias).Include(x => x.Educaciones)
        .Include(x => x.Certificaciones).Include(x => x.Proyectos).ThenInclude(x => x.Documento)
        .Include(x => x.Habilidades).ThenInclude(x => x.Habilidad)
        .Where(x => x.PersonaId == personaId);

    private Perfil Crear(Guid personaId)
    {
        var perfil = new Perfil { Id = Guid.NewGuid(), PersonaId = personaId };
        db.Perfiles.Add(perfil);
        return perfil;
    }

    private static string Normalizar(string texto) => texto.Trim().ToUpperInvariant();
}
