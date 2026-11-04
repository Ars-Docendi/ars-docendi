using Microsoft.EntityFrameworkCore;
using Modules.Designaciones.Domain;

namespace Modules.Designaciones.Repositories;

internal sealed partial class RepositorioPedidos
{
    /// <summary>
    /// Cuatro eventos como máximo por fila: primero, creación, primer envío y último.
    /// Conserva fechas/actor de grillas actuales sin leer comentarios ni historia completa.
    /// Cada subconsulta selecciona un evento real con desempate por ID, no MAX independientes.
    /// </summary>
    public async Task<IReadOnlyList<PedidoHistorial>> ObtenerResumenesAsync(
        IReadOnlyCollection<Guid> pedidoIds, CancellationToken ct)
    {
        if (pedidoIds.Count == 0) return [];
        return await db.PedidoHistorial.AsNoTracking()
            .Where(h => pedidoIds.Contains(h.PedidoId)
                && (h.Id == db.PedidoHistorial.Where(x => x.PedidoId == h.PedidoId)
                    .OrderBy(x => x.CreadoEn).ThenBy(x => x.Id).Select(x => (Guid?)x.Id).FirstOrDefault()
                || h.Id == db.PedidoHistorial.Where(x => x.PedidoId == h.PedidoId && x.Accion == AccionesHistorial.Crear)
                    .OrderBy(x => x.CreadoEn).ThenBy(x => x.Id).Select(x => (Guid?)x.Id).FirstOrDefault()
                || h.Id == db.PedidoHistorial.Where(x => x.PedidoId == h.PedidoId && x.Accion == AccionesHistorial.Enviar)
                    .OrderBy(x => x.CreadoEn).ThenBy(x => x.Id).Select(x => (Guid?)x.Id).FirstOrDefault()
                || h.Id == db.PedidoHistorial.Where(x => x.PedidoId == h.PedidoId)
                    .OrderByDescending(x => x.CreadoEn).ThenByDescending(x => x.Id)
                    .Select(x => (Guid?)x.Id).FirstOrDefault()))
            .Select(h => new PedidoHistorial
            {
                Id = h.Id, PedidoId = h.PedidoId, Accion = h.Accion, RolId = h.RolId,
                ActorId = h.ActorId, Etapa = h.Etapa, CreadoEn = h.CreadoEn,
            }).ToListAsync(ct);
    }

    /// <summary>Comentario de la última devolución de cada pedido, con desempate por ID.</summary>
    public async Task<IReadOnlyDictionary<Guid, string?>> ObtenerUltimosMotivosDevolucionAsync(
        IReadOnlyCollection<Guid> pedidoIds, CancellationToken ct)
    {
        if (pedidoIds.Count == 0) return new Dictionary<Guid, string?>();
        return await db.PedidoHistorial.AsNoTracking()
            .Where(h => pedidoIds.Contains(h.PedidoId)
                && h.Id == db.PedidoHistorial
                    .Where(x => x.PedidoId == h.PedidoId && x.Accion == AccionesHistorial.Devolver)
                    .OrderByDescending(x => x.CreadoEn).ThenByDescending(x => x.Id)
                    .Select(x => (Guid?)x.Id).FirstOrDefault())
            .ToDictionaryAsync(h => h.PedidoId, h => h.Comentario, ct);
    }
}
