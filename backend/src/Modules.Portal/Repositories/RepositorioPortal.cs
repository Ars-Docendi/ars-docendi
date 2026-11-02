using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using Modules.Portal.Domain;
using Modules.Portal.Infrastructure;

namespace Modules.Portal.Repositories;

public sealed class RepositorioPortal(PortalDbContext db)
{
    public async Task<Perfil?> ObtenerAsync(Guid personaId, CancellationToken ct)
    {
        // Las seis consultas observan el mismo snapshot; no incluye I/O de Storage.
        await using var snapshot = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var perfil = await Consulta(personaId).AsNoTracking().AsSplitQuery().FirstOrDefaultAsync(ct);
        await snapshot.CommitAsync(ct);
        return perfil;
    }

    public async Task<Perfil> ObtenerOCrearAsync(Guid personaId, CancellationToken ct, string? seccion = null)
    {
        if (!await db.Perfiles.AnyAsync(p => p.PersonaId == personaId, ct))
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await ArsDocendi.Shared.Identity.ProteccionDestinosIdentity.ValidarAsync(
                db.Database.GetDbConnection(), tx.GetDbTransaction(), [], [personaId], ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO portal.perfiles (persona_id) VALUES ({personaId}) ON CONFLICT (persona_id) DO NOTHING", ct);
            await tx.CommitAsync(ct);
        }
        IQueryable<Perfil> consulta = db.Perfiles;
        if (seccion == "contacto") consulta = consulta.Include(p => p.Contacto);
        if (seccion == "cv") consulta = consulta.Include(p => p.Cv);
        return await consulta.SingleAsync(p => p.PersonaId == personaId, ct);
    }

    public Task<Perfil?> ObtenerSeccionAsync(Guid personaId, string seccion, CancellationToken ct)
    {
        IQueryable<Perfil> consulta = seccion == "cv" ? db.Perfiles.Include(p => p.Cv) : db.Perfiles.Include(p => p.Contacto);
        return consulta.SingleOrDefaultAsync(p => p.PersonaId == personaId, ct);
    }

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
    public void EsperarVersion<T>(T entidad, uint? version) where T : class
    {
        var propiedad = db.Entry(entidad).Property<uint>("Version");
        if (version is null || version != propiedad.CurrentValue) throw Conflicto();
        propiedad.OriginalValue = version.Value;
        // Aun una intención sin cambios visibles debe consumir su versión.
        var campo = db.Entry(entidad).Properties.First(p => !p.Metadata.IsPrimaryKey() && p.Metadata.Name != "Version");
        campo.IsModified = true;
    }

    public static ArsDocendi.Shared.Aplicacion.ExcepcionAplicacion Conflicto() => new(
        ArsDocendi.Shared.Aplicacion.TipoErrorAplicacion.Conflicto, "concurrency-conflict",
        "La sección cambió. Recargue el perfil antes de confirmar nuevamente.");

    public async Task GuardarAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw Conflicto(); }
        catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: "23505", ConstraintName: "contactos_perfil_id_key" or "cvs_perfil_id_key" })
        { throw Conflicto(); }
    }

    public async Task ReemplazarTagsAsync(Perfil perfil, string tipo, IReadOnlyList<string> terminos, CancellationToken ct, long? version = null)
    {
        if (tipo is not ("habilidad" or "interes")) throw new ArgumentOutOfRangeException(nameof(tipo));
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var esperada = version ?? 0;
        var filas = tipo == "habilidad"
            ? await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE portal.perfiles SET version_habilidades=version_habilidades+1 WHERE id={perfil.Id} AND version_habilidades={esperada}", ct)
            : await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE portal.perfiles SET version_intereses=version_intereses+1 WHERE id={perfil.Id} AND version_intereses={esperada}", ct);
        if (filas != 1) throw Conflicto();
        await db.DocenteHabilidades.Where(x => x.PerfilId == perfil.Id && x.Tipo == tipo).ExecuteDeleteAsync(ct);
        foreach (var original in terminos.Select(x => x.Trim()).Where(x => x.Length > 0)
                     .GroupBy(Normalizar).OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.First()))
        {
            var normalizado = Normalizar(original);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO portal.habilidades (termino,termino_norm,sugerido) VALUES ({original},{normalizado},true) ON CONFLICT (termino_norm) DO NOTHING", ct);
            var id = await db.Habilidades.Where(x => x.TerminoNorm == normalizado).Select(x => x.Id).SingleAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO portal.docente_habilidades (perfil_id,habilidad_id,tipo) VALUES ({perfil.Id},{id},{tipo}) ON CONFLICT DO NOTHING", ct);
        }
        await tx.CommitAsync(ct);
    }

    private IQueryable<Perfil> Consulta(Guid personaId) => db.Perfiles
        .Include(x => x.Contacto).Include(x => x.Cv)
        .Include(x => x.Experiencias).Include(x => x.Educaciones)
        .Include(x => x.Certificaciones).Include(x => x.Proyectos).ThenInclude(x => x.Documento)
        .Include(x => x.Habilidades).ThenInclude(x => x.Habilidad)
        .Where(x => x.PersonaId == personaId);

    private static string Normalizar(string texto) => texto.Trim().ToUpperInvariant();
}
