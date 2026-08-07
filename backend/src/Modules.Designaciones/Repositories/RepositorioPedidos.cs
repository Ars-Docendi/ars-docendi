using Microsoft.EntityFrameworkCore;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure;
using Npgsql;

namespace Modules.Designaciones.Repositories;

/// <inheritdoc cref="IRepositorioPedidos" />
internal sealed class RepositorioPedidos(DesignacionesDbContext db) : IRepositorioPedidos
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
          .Include(p => p.Historial.OrderBy(h => h.CreadoEn))
          .Include(p => p.CargoSolicitado)
          .FirstOrDefaultAsync(p => p.Id == pedidoId, ct);

    public Task<Pedido?> ObtenerLivianoAsync(Guid pedidoId, CancellationToken ct) =>
        db.Pedidos.FirstOrDefaultAsync(p => p.Id == pedidoId, ct);

    public Task<bool> ExisteVivoParaPersonaEnPeriodoAsync(
        Guid periodoId, Guid personaId, CancellationToken ct) =>
        db.Pedidos
          .AsNoTracking()
          .AnyAsync(p => p.PeriodoId == periodoId
                      && p.PersonaId == personaId
                      && !EstadosPedido.NoOcupanCupo.Contains(p.Estado), ct);

    public async Task<IReadOnlyList<Pedido>> ListarPorMateriasAsync(
        Guid periodoId, IReadOnlyCollection<Guid> materiaIds, CancellationToken ct) =>
        await db.Pedidos
                .AsNoTracking()
                .Where(p => p.PeriodoId == periodoId && materiaIds.Contains(p.MateriaId))
                .OrderByDescending(p => p.Prioritario)
                .ThenByDescending(p => p.CreadoEn)
                .ToListAsync(ct);

    // La carrera del pedido se deriva de identity.materias. Se resuelve con una
    // subconsulta en vez de desnormalizar carrera_id en pedidos: data-model.md sólo
    // tolera created_at como denormalización.
    public async Task<IReadOnlyList<Pedido>> ListarPorCarrerasAsync(
        Guid periodoId, IReadOnlyCollection<Guid> carreraIds, CancellationToken ct)
    {
        var materiasDeLasCarreras = db.Database
            .SqlQuery<Guid>($"SELECT id FROM identity.materias WHERE carrera_id = ANY({carreraIds.ToArray()})");

        var materiaIds = await materiasDeLasCarreras.ToListAsync(ct);

        return await ListarPorMateriasAsync(periodoId, materiaIds, ct);
    }

    public async Task<IReadOnlyList<Pedido>> ListarDelPeriodoAsync(Guid periodoId, CancellationToken ct) =>
        await db.Pedidos
                .AsNoTracking()
                .Where(p => p.PeriodoId == periodoId)
                .OrderByDescending(p => p.Prioritario)
                .ThenByDescending(p => p.CreadoEn)
                .ToListAsync(ct);

    public async Task<Guid> ObtenerCarreraDelPedidoAsync(Guid pedidoId, CancellationToken ct)
    {
        var carreras = await db.Database
            .SqlQuery<Guid>($"""
                SELECT m.carrera_id
                  FROM designaciones.pedidos p
                  JOIN identity.materias m ON m.id = p.materia_id
                 WHERE p.id = {pedidoId}
                """)
            .ToListAsync(ct);

        return carreras.Count == 1
            ? carreras[0]
            : throw new ErrorDominioPedido($"No se pudo resolver la carrera del pedido {pedidoId}.");
    }

    public async Task<string> SiguienteNumeroAsync(CancellationToken ct)
    {
        var numeros = await db.Database
            .SqlQuery<string>($"SELECT designaciones.siguiente_numero_pedido()")
            .ToListAsync(ct);

        return numeros[0];
    }

    public void Agregar(Pedido pedido) => db.Pedidos.Add(pedido);

    public void Eliminar(Pedido pedido) => db.Pedidos.Remove(pedido);

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
