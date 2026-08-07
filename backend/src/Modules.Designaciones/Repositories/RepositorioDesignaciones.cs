using Microsoft.EntityFrameworkCore;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure;

namespace Modules.Designaciones.Repositories;

/// <summary>Persistencia del estado vigente de las designaciones.</summary>
internal interface IRepositorioDesignaciones
{
    /// <summary>Designaciones vigentes de una persona (las que no tienen fecha de cierre).</summary>
    Task<IReadOnlyList<Designacion>> ListarVigentesDePersonaAsync(Guid personaId, CancellationToken ct);

    /// <summary>
    /// Designación vigente de una persona sobre una materia, o <c>null</c>. Es única
    /// por la constraint <c>designaciones_sin_solapamiento</c>.
    /// </summary>
    Task<Designacion?> ObtenerVigenteAsync(Guid personaId, Guid materiaId, CancellationToken ct);

    /// <summary>Plantel vigente de una cátedra.</summary>
    Task<IReadOnlyList<Designacion>> ListarVigentesDeMateriaAsync(Guid materiaId, CancellationToken ct);

    void Agregar(Designacion designacion);
}

/// <inheritdoc cref="IRepositorioDesignaciones" />
internal sealed class RepositorioDesignaciones(DesignacionesDbContext db) : IRepositorioDesignaciones
{
    public async Task<IReadOnlyList<Designacion>> ListarVigentesDePersonaAsync(
        Guid personaId, CancellationToken ct) =>
        await db.Designaciones
                .Include(d => d.Cargo)
                .Where(d => d.PersonaId == personaId && d.VigenteHasta == null)
                .ToListAsync(ct);

    public Task<Designacion?> ObtenerVigenteAsync(Guid personaId, Guid materiaId, CancellationToken ct) =>
        db.Designaciones
          .Include(d => d.Cargo)
          .FirstOrDefaultAsync(
              d => d.PersonaId == personaId && d.MateriaId == materiaId && d.VigenteHasta == null, ct);

    public async Task<IReadOnlyList<Designacion>> ListarVigentesDeMateriaAsync(
        Guid materiaId, CancellationToken ct) =>
        await db.Designaciones
                .Include(d => d.Cargo)
                .Where(d => d.MateriaId == materiaId && d.VigenteHasta == null)
                .ToListAsync(ct);

    public void Agregar(Designacion designacion) => db.Designaciones.Add(designacion);
}
