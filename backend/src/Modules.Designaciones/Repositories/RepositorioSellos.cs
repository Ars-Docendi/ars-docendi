using Microsoft.EntityFrameworkCore;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure;

namespace Modules.Designaciones.Repositories;

/// <summary>Todo lo que la verificación necesita de un período, leído junto.</summary>
public sealed record CadenaPeriodo(
    IReadOnlyList<PedidoSello> Sellos,
    IReadOnlyList<PedidoSello> SellosDePedidos,
    IReadOnlyList<PedidoHistorial> Eventos,
    IReadOnlyList<Pedido> Pedidos,
    IReadOnlyList<PeriodoAncla> Anclas);

public sealed class RepositorioSellos(DesignacionesDbContext db)
{
    /// <summary>Mismo lock que el trigger <c>encadenar_sello</c>; exige transacción abierta.</summary>
    public Task BloquearCadenaAsync(Guid periodoId, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT designaciones.bloquear_cadena_periodo({periodoId})", ct);

    /// <summary>Cabeza vigente, contando sellos agregados en esta unidad de trabajo y todavía sin guardar.</summary>
    public async Task<PedidoSello?> ObtenerCabezaAsync(Guid periodoId, CancellationToken ct) =>
        db.PedidoSellos.Local.Where(s => s.PeriodoId == periodoId).MaxBy(s => s.Secuencia)
        ?? await db.PedidoSellos.AsNoTracking()
            .Where(s => s.PeriodoId == periodoId)
            .OrderByDescending(s => s.Secuencia)
            .FirstOrDefaultAsync(ct);

    public Task<PeriodoAncla?> ObtenerUltimaAnclaAsync(Guid periodoId, CancellationToken ct) =>
        db.PeriodoAnclas.AsNoTracking()
            .Where(a => a.PeriodoId == periodoId)
            .OrderByDescending(a => a.Secuencia)
            .FirstOrDefaultAsync(ct);

    public void Agregar(PedidoSello sello) => db.PedidoSellos.Add(sello);
    public void AgregarAncla(PeriodoAncla ancla) => db.PeriodoAnclas.Add(ancla);
    public Task GuardarAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public async Task<IReadOnlyList<Guid>> ListarPeriodosAsync(CancellationToken ct) =>
        await db.Periodos.AsNoTracking().OrderBy(p => p.Id).Select(p => p.Id).ToListAsync(ct);

    public Task<bool> ExistePeriodoAsync(Guid periodoId, CancellationToken ct) =>
        db.Periodos.AsNoTracking().AnyAsync(p => p.Id == periodoId, ct);

    /// <summary>
    /// Sellos del período, pedidos que hoy pertenecen a él o que tienen sellos en él, sus
    /// eventos y todos sus sellos (un pedido editado puede haber cambiado de período).
    /// </summary>
    public async Task<CadenaPeriodo> LeerCadenaAsync(Guid periodoId, CancellationToken ct)
    {
        var sellos = await db.PedidoSellos.AsNoTracking()
            .Where(s => s.PeriodoId == periodoId)
            .OrderBy(s => s.Secuencia)
            .ToListAsync(ct);
        var pedidoIds = sellos.Select(s => s.PedidoId).Distinct().ToList();
        var pedidos = await db.Pedidos.AsNoTracking()
            .Include(p => p.Adjuntos)
            .Where(p => p.PeriodoId == periodoId || pedidoIds.Contains(p.Id))
            .AsSplitQuery()
            .ToListAsync(ct);
        var todosLosPedidos = pedidos.Select(p => p.Id).ToList();
        var historialIds = sellos.Select(s => s.HistorialId).ToList();
        var eventos = await db.PedidoHistorial.AsNoTracking()
            .Where(h => todosLosPedidos.Contains(h.PedidoId) || historialIds.Contains(h.Id))
            .OrderBy(h => h.CreadoEn).ThenBy(h => h.Id)
            .ToListAsync(ct);
        var sellosDePedidos = await db.PedidoSellos.AsNoTracking()
            .Where(s => todosLosPedidos.Contains(s.PedidoId))
            .ToListAsync(ct);
        var anclas = await db.PeriodoAnclas.AsNoTracking()
            .Where(a => a.PeriodoId == periodoId)
            .OrderBy(a => a.Secuencia)
            .ToListAsync(ct);
        return new(sellos, sellosDePedidos, eventos, pedidos, anclas);
    }

    /// <summary>Pedidos fuera de borrador del período, con su historial, para el sellado retroactivo.</summary>
    public async Task<IReadOnlyList<Pedido>> ListarPedidosEnviadosAsync(Guid periodoId, CancellationToken ct) =>
        await db.Pedidos.AsNoTracking()
            .Include(p => p.Adjuntos)
            .Include(p => p.Historial)
            .Where(p => p.PeriodoId == periodoId && p.Estado != EstadosPedido.Borrador)
            .AsSplitQuery()
            .ToListAsync(ct);

    public async Task<IReadOnlySet<Guid>> ListarEventosSelladosAsync(IReadOnlyList<Guid> eventoIds, CancellationToken ct) =>
        (await db.PedidoSellos.AsNoTracking()
            .Where(s => eventoIds.Contains(s.HistorialId))
            .Select(s => s.HistorialId)
            .ToListAsync(ct)).ToHashSet();
}
