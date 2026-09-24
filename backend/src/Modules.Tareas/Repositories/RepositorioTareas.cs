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

    public Task<List<RelacionTarea>> ListarRelacionesAsync(CancellationToken ct) =>
        db.Relaciones.AsNoTracking().ToListAsync(ct);

    public Task<List<RelacionTarea>> ListarRelacionesDeAsync(Guid tareaId, CancellationToken ct) =>
        db.Relaciones.AsNoTracking()
            .Where(r => r.TareaId == tareaId || r.RelacionadaId == tareaId)
            .ToListAsync(ct);

    public Task<RelacionTarea?> ObtenerRelacionAsync(Guid menorId, Guid mayorId, CancellationToken ct) =>
        db.Relaciones.FirstOrDefaultAsync(r => r.TareaId == menorId && r.RelacionadaId == mayorId, ct);

    public void Agregar(RelacionTarea relacion) => db.Relaciones.Add(relacion);

    public void Eliminar(RelacionTarea relacion) => db.Relaciones.Remove(relacion);

    // Proyectos ---------------------------------------------------------

    public Task<List<Proyecto>> ListarProyectosAsync(CancellationToken ct) =>
        db.Proyectos.AsNoTracking().OrderByDescending(p => p.FechaFin).ThenBy(p => p.Numero).ToListAsync(ct);

    public Task<Proyecto?> ObtenerProyectoAsync(Guid id, CancellationToken ct) =>
        db.Proyectos.FirstOrDefaultAsync(p => p.Id == id, ct);

    public void Agregar(Proyecto proyecto) => db.Proyectos.Add(proyecto);

    public Task GuardarAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
