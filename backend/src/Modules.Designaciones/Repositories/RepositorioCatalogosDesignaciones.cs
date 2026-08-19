using Microsoft.EntityFrameworkCore;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure;

namespace Modules.Designaciones.Repositories;

public interface IRepositorioCatalogosDesignaciones
{
    Task<IReadOnlyList<Periodo>> ListarPeriodosAsync(CancellationToken ct);
    Task<IReadOnlyList<Cargo>> ListarCargosActivosAsync(CancellationToken ct);
    Task<IReadOnlySet<Guid>> ListarPersonasConPedidoVivoAsync(Guid periodoId, CancellationToken ct);
    Task<IReadOnlyList<Designacion>> ListarDesignacionesVigentesAsync(CancellationToken ct);
}

internal sealed class RepositorioCatalogosDesignaciones(DesignacionesDbContext db)
    : IRepositorioCatalogosDesignaciones
{
    public async Task<IReadOnlyList<Periodo>> ListarPeriodosAsync(CancellationToken ct) =>
        await db.Periodos.AsNoTracking()
            .OrderByDescending(p => p.ImpactoDesde)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Cargo>> ListarCargosActivosAsync(CancellationToken ct) =>
        await db.Cargos.AsNoTracking()
            .Where(c => c.Activo)
            .OrderBy(c => c.Orden)
            .ToListAsync(ct);

    public async Task<IReadOnlySet<Guid>> ListarPersonasConPedidoVivoAsync(
        Guid periodoId,
        CancellationToken ct) =>
        (await db.Pedidos.AsNoTracking()
            .Where(p => p.PeriodoId == periodoId && !EstadosPedido.NoOcupanCupo.Contains(p.Estado))
            .Select(p => p.PersonaId)
            .Distinct()
            .ToListAsync(ct))
        .ToHashSet();

    public async Task<IReadOnlyList<Designacion>> ListarDesignacionesVigentesAsync(CancellationToken ct) =>
        await db.Designaciones.AsNoTracking()
            .Include(d => d.Cargo)
            .Where(d => d.VigenteHasta == null)
            .ToListAsync(ct);
}
