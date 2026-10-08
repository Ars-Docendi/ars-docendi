using Microsoft.EntityFrameworkCore;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure;
using Npgsql;

namespace Modules.Designaciones.Repositories;

internal sealed class RepositorioPedidos(DesignacionesDbContext db)
{
    /// <summary>
    /// Nombre del índice único parcial que impone BR-designaciones-001. Debe coincidir
    /// con <c>database/designaciones/003_designaciones_pedidos.sql</c>: si se renombra
    /// allá y no acá, la violación deja de traducirse y se filtra como error 500.
    /// </summary>
    private const string IndiceUnPedidoPorDocentePeriodo = "pedidos_uno_por_docente_periodo";

    public Task<Pedido?> ObtenerPorIdAsync(Guid pedidoId, CancellationToken ct) =>
        db.Pedidos
          .Include(p => p.Adjuntos)
          .Include(p => p.Historial.OrderBy(h => h.CreadoEn).ThenBy(h => h.Id))
          .Include(p => p.CargoSolicitado)
          .Include(p => p.DedicacionSolicitadaCatalogo)
          .Include(p => p.Periodo)
          .AsSplitQuery()
          .FirstOrDefaultAsync(p => p.Id == pedidoId, ct);

    public Task<Pedido?> ObtenerLivianoAsync(Guid pedidoId, CancellationToken ct) =>
        db.Pedidos.FirstOrDefaultAsync(p => p.Id == pedidoId, ct);

    public Task<bool> ExisteVivoParaPersonaEnPeriodoAsync(
        Guid periodoId, Guid personaId, Guid? exceptoPedidoId, CancellationToken ct) =>
        db.Pedidos
          .AsNoTracking()
          .AnyAsync(p => p.PeriodoId == periodoId
                      && p.PersonaId == personaId
                      && p.Id != exceptoPedidoId
                      && !EstadosPedido.NoOcupanCupo.Contains(p.Estado), ct);

    public Task<Periodo?> ObtenerPeriodoActivoAsync(CancellationToken ct) =>
        db.Periodos.AsNoTracking().SingleOrDefaultAsync(p => p.Activo, ct);

    public Task<bool> ExisteDedicacionActivaAsync(Guid dedicacionId, CancellationToken ct) =>
        db.Dedicaciones.AsNoTracking().AnyAsync(d => d.Id == dedicacionId && d.Activo, ct);

    public Task<bool> ExisteCargoActivoAsync(Guid cargoId, CancellationToken ct) =>
        db.Cargos.AsNoTracking().AnyAsync(c => c.Id == cargoId && c.Activo, ct);

    public async Task<IReadOnlyList<Pedido>> ListarPorMateriasAsync(
        Guid periodoId, IReadOnlyCollection<Guid> materiaIds, CancellationToken ct) =>
        await db.Pedidos
                .AsNoTracking()
                .Include(p => p.Periodo)
                .Include(p => p.CargoSolicitado)
          .Include(p => p.DedicacionSolicitadaCatalogo)
                .Include(p => p.Adjuntos)
                .Include(p => p.Historial)
                .AsSplitQuery()
                .Where(p => p.PeriodoId == periodoId && materiaIds.Contains(p.MateriaId))
                .OrderByDescending(p => p.Prioritario)
                .ThenByDescending(p => p.CreadoEn)
                .ToListAsync(ct);

    public async Task<IReadOnlyList<Pedido>> ListarPorCarrerasAsync(
        Guid periodoId, IReadOnlyCollection<Guid> carreraIds, CancellationToken ct) =>
        await db.Pedidos
                .AsNoTracking()
                .Include(p => p.Periodo)
                .Include(p => p.CargoSolicitado)
          .Include(p => p.DedicacionSolicitadaCatalogo)
                .Include(p => p.Adjuntos)
                .Include(p => p.Historial)
                .AsSplitQuery()
                .Where(p => p.PeriodoId == periodoId && carreraIds.Contains(p.CarreraId))
                .OrderByDescending(p => p.Prioritario)
                .ThenByDescending(p => p.CreadoEn)
                .ToListAsync(ct);

    public async Task<IReadOnlyList<Pedido>> ListarDelPeriodoAsync(Guid periodoId, CancellationToken ct) =>
        await db.Pedidos
                .AsNoTracking()
                .Include(p => p.Periodo)
                .Include(p => p.CargoSolicitado)
          .Include(p => p.DedicacionSolicitadaCatalogo)
                .Include(p => p.Adjuntos)
                .Include(p => p.Historial)
                .AsSplitQuery()
                .Where(p => p.PeriodoId == periodoId)
                .OrderByDescending(p => p.Prioritario)
                .ThenByDescending(p => p.CreadoEn)
                .ToListAsync(ct);

    public async Task<string> SiguienteNumeroAsync(CancellationToken ct)
    {
        var numeros = await db.Database
            .SqlQuery<string>($"SELECT designaciones.siguiente_numero_pedido()")
            .ToListAsync(ct);

        return numeros[0];
    }

    public void Agregar(Pedido pedido) => db.Pedidos.Add(pedido);

    public void Eliminar(Pedido pedido) => db.Pedidos.Remove(pedido);

    public void ReemplazarAdjuntos(Pedido pedido, IReadOnlyList<PedidoAdjunto> adjuntos)
    {
        db.PedidoAdjuntos.RemoveRange(pedido.Adjuntos);
        pedido.Adjuntos.Clear();
        foreach (var adjunto in adjuntos)
        {
            pedido.Adjuntos.Add(adjunto);
            db.PedidoAdjuntos.Add(adjunto);
        }
    }

    public void EsperarVersion(Pedido pedido, uint version) =>
        db.Entry(pedido).Property(p => p.Version).OriginalValue = version;

    public void AgregarHistorial(PedidoHistorial historial) => db.PedidoHistorial.Add(historial);

    public async Task GuardarCambiosAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (EsViolacionDeUnPedidoPorDocente(ex))
        {
            // El mensaje NO menciona la cátedra ni el autor del pedido bloqueante:
            // puede pertenecer a una cátedra que el actor no tiene permitido ver.
            throw new ErrorPedidoDuplicado(
                "Ya existe un pedido en curso para ese docente en el período [BR-designaciones-001].");
        }
    }

    private static bool EsViolacionDeUnPedidoPorDocente(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg
        && pg.SqlState == PostgresErrorCodes.UniqueViolation
        && pg.ConstraintName == IndiceUnPedidoPorDocentePeriodo;
}
