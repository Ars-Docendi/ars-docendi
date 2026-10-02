using Microsoft.EntityFrameworkCore;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure;

namespace Modules.Designaciones.Repositories;

internal sealed record ConsultaLoteDesignaciones(
    Periodo Periodo,
    Periodo? PeriodoAnterior,
    IReadOnlyList<Pedido> PedidosDelPeriodo,
    IReadOnlyList<Designacion> DesignacionesVigentes);

/// <summary>Lee las fuentes del lote dentro de una misma transacción.</summary>
internal sealed class RepositorioLoteDesignaciones(DesignacionesDbContext db)
{
    public async Task<ConsultaLoteDesignaciones?> LeerAsync(Guid periodoId, CancellationToken ct)
    {
        var periodo = await db.Periodos.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == periodoId, ct);
        if (periodo is null) return null;

        var periodoAnterior = await db.Periodos.AsNoTracking()
            .Where(p => p.ImpactoDesde < periodo.ImpactoDesde)
            .OrderByDescending(p => p.ImpactoDesde)
            .ThenByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);

        var pedidos = await db.Pedidos
            .AsNoTracking()
            .Include(p => p.CargoSolicitado)
            .Include(p => p.DedicacionSolicitadaCatalogo)
            .Where(p => p.PeriodoId == periodoId)
            .OrderBy(p => p.Numero)
            .ToListAsync(ct);

        var designaciones = await db.Designaciones
            .AsNoTracking()
            .Include(d => d.Cargo)
            .Include(d => d.DedicacionCatalogo)
            .Where(d => d.VigenteHasta == null)
            .OrderBy(d => d.PersonaId)
            .ThenBy(d => d.MateriaId)
            .ToListAsync(ct);

        return new ConsultaLoteDesignaciones(periodo, periodoAnterior, pedidos, designaciones);
    }
}
