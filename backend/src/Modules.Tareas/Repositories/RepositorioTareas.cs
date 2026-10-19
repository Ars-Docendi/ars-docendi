using Microsoft.EntityFrameworkCore;
using Modules.Tareas.Domain;
using Modules.Tareas.Infrastructure;

namespace Modules.Tareas.Repositories;

public sealed class RepositorioTareas(TareasDbContext db)
{
    // Catálogos ---------------------------------------------------------

    public async Task<CatalogosTareas> ObtenerCatalogosAsync(CancellationToken ct) =>
        new(
            await db.EstadosProyecto.AsNoTracking().OrderBy(e => e.Orden).ToListAsync(ct),
            await db.EstadosTarea.AsNoTracking().OrderBy(e => e.Orden).ToListAsync(ct),
            await db.Prioridades.AsNoTracking().OrderBy(e => e.Orden).ToListAsync(ct),
            await db.Tipos.AsNoTracking().OrderBy(e => e.Orden).ToListAsync(ct));

    // Tareas ------------------------------------------------------------

    /// <param name="soloResponsableId">Si se indica, solo las tareas asignadas a ese usuario.</param>
    public Task<List<Tarea>> ListarAsync(Guid? soloResponsableId, CancellationToken ct) =>
        db.Tareas.AsNoTracking()
            .Where(t => soloResponsableId == null || t.ResponsableId == soloResponsableId)
            .OrderBy(t => t.FechaInicio).ThenBy(t => t.Numero)
            .ToListAsync(ct);

    /// <summary>Tarea con comentarios e historial, rastreada para modificarla.</summary>
    public Task<Tarea?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.Tareas
            .Include(t => t.Comentarios)
            .Include(t => t.Historial)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    /// <param name="soloResponsableId">Si se indica, exige además que la tarea esté asignada a ese usuario.</param>
    public Task<bool> ExisteAsync(Guid id, Guid? soloResponsableId, CancellationToken ct) =>
        db.Tareas.AnyAsync(t => t.Id == id && (soloResponsableId == null || t.ResponsableId == soloResponsableId), ct);

    public void Agregar(Tarea tarea) => db.Tareas.Add(tarea);

    // Relaciones --------------------------------------------------------

    public Task<List<RelacionTarea>> ListarRelacionesDeAsync(Guid tareaId, CancellationToken ct) =>
        db.Relaciones.AsNoTracking()
            .Where(r => r.TareaId == tareaId || r.RelacionadaId == tareaId)
            .ToListAsync(ct);

    public Task<List<RelacionTarea>> ListarRelacionesDeAsync(IReadOnlyCollection<Guid> tareaIds, CancellationToken ct) =>
        db.Relaciones.AsNoTracking().Where(r => tareaIds.Contains(r.TareaId) || tareaIds.Contains(r.RelacionadaId)).ToListAsync(ct);

    public Task CrearRelacionIdempotenteAsync(Guid menorId, Guid mayorId, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO tareas.tarea_relaciones(tarea_id, relacionada_id)
            VALUES ({menorId}, {mayorId}) ON CONFLICT (tarea_id, relacionada_id) DO NOTHING
            """, ct);

    public Task EliminarRelacionIdempotenteAsync(Guid menorId, Guid mayorId, CancellationToken ct) =>
        db.Relaciones.Where(r => r.TareaId == menorId && r.RelacionadaId == mayorId).ExecuteDeleteAsync(ct);

    public async Task ProtegerUsuariosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var tx = db.Database.CurrentTransaction
            ?? throw new InvalidOperationException("La referencia exige una transacción local.");
        await ArsDocendi.Shared.Identity.ProteccionDestinosIdentity.ValidarAsync(
            db.Database.GetDbConnection(), Microsoft.EntityFrameworkCore.Storage.DbContextTransactionExtensions.GetDbTransaction(tx), ids, [], ct);
    }

    public void Agregar(RelacionTarea relacion) => db.Relaciones.Add(relacion);

    public void Eliminar(RelacionTarea relacion) => db.Relaciones.Remove(relacion);

    // Proyectos ---------------------------------------------------------

    public Task<List<Proyecto>> ListarProyectosAsync(CancellationToken ct) =>
        db.Proyectos.AsNoTracking().OrderByDescending(p => p.FechaFin).ThenBy(p => p.Numero).ToListAsync(ct);

    public Task<int> BloquearProyectoAsync(Guid id, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM tareas.proyectos WHERE id = {id} FOR UPDATE", ct);

    public Task<Proyecto?> ObtenerProyectoAsync(Guid id, CancellationToken ct) =>
        db.Proyectos.FirstOrDefaultAsync(p => p.Id == id, ct);

    public void Agregar(Proyecto proyecto) => db.Proyectos.Add(proyecto);

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> IniciarTransaccionAsync(CancellationToken ct) =>
        db.Database.BeginTransactionAsync(ct);

    public Task BloquearTareaAsync(Guid id, CancellationToken ct) => BloquearTareasAsync([id], ct);

    public async Task BloquearTareasAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var ordenados = ids.Distinct().Order().ToArray();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            WITH RECURSIVE ancestros AS (
                SELECT id, tarea_padre_id FROM tareas.tareas WHERE id = ANY({ordenados})
                UNION
                SELECT t.id, t.tarea_padre_id FROM tareas.tareas t JOIN ancestros a ON t.id = a.tarea_padre_id
            ) SELECT pg_advisory_xact_lock(hashtextextended(id::text, 52005))
              FROM ancestros WHERE tarea_padre_id IS NULL ORDER BY id
            """, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM tareas.tareas WHERE id = ANY({ordenados}) ORDER BY id FOR UPDATE", ct);
    }

    public Task<List<Tarea>> DescendientesAsync(Guid raizId, CancellationToken ct) =>
        db.Tareas.FromSqlInterpolated($"""
            WITH RECURSIVE descendientes AS (
                SELECT id FROM tareas.tareas WHERE tarea_padre_id = {raizId}
                UNION
                SELECT t.id FROM tareas.tareas t JOIN descendientes d ON t.tarea_padre_id = d.id
            ) SELECT t.*, t.xmin FROM tareas.tareas t JOIN descendientes d ON t.id = d.id
            """).ToListAsync(ct);

    public Task GuardarAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
