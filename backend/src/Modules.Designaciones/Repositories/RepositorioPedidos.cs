using Microsoft.EntityFrameworkCore;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure;
using Npgsql;

namespace Modules.Designaciones.Repositories;

internal sealed partial class RepositorioPedidos(DesignacionesDbContext db)
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
                .Where(p => p.PeriodoId == periodoId)
                .OrderByDescending(p => p.Prioritario)
                .ThenByDescending(p => p.CreadoEn)
                .ToListAsync(ct);

    // La carrera viaja en el propio pedido (materia_id + carrera_id elegidos juntos
    // al cargarlo), así que no hace falta cruzar a identity.
    //
    // El `= ANY` con un arreglo parametrizado, y no un IN armado a mano: los números
    // llegan de afuera del módulo —de lo que el asistente vio en un resultado— y
    // concatenarlos sería interpolar entrada ajena en SQL.
    public async Task<IReadOnlyList<PedidoUbicado>> UbicarPorNumerosAsync(
        IReadOnlyCollection<string> numeros, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(numeros);

        if (numeros.Count == 0)
        {
            return [];
        }

        var buscados = numeros.ToArray();

        return await db.Database
            .SqlQuery<PedidoUbicado>($"""
                SELECT p.id          AS "Id",
                       p.numero      AS "Numero",
                       p.materia_id  AS "MateriaId",
                       p.carrera_id  AS "CarreraId"
                  FROM designaciones.pedidos p
                 WHERE p.numero = ANY({buscados})
                """)
            .ToListAsync(ct);
    }

    // La ficha del trámite: una proyección y no el agregado. No hace falta el
    // historial entero ni los adjuntos, sólo la fecha del último movimiento, que se
    // calcula en la base. Sin colecciones cargadas, y sin seguimiento de cambios.
    public Task<DatosDeFicha?> ObtenerFichaAsync(Guid pedidoId, CancellationToken ct) =>
        db.Pedidos
          .AsNoTracking()
          .Where(p => p.Id == pedidoId)
          .Select(p => new DatosDeFicha
          {
              Id = p.Id,
              Numero = p.Numero,
              Novedad = p.Novedad,
              Estado = p.Estado,
              MateriaId = p.MateriaId,
              CarreraId = p.CarreraId,
              Periodo = p.Periodo == null ? string.Empty : p.Periodo.Nombre,
              Cargo = p.CargoSolicitado == null ? null : p.CargoSolicitado.Nombre,
              Dedicacion = p.DedicacionSolicitadaCatalogo != null
                  ? p.DedicacionSolicitadaCatalogo.Nombre
                  : p.DedicacionSolicitada,
              UltimoMovimiento = p.Historial.Max(h => (DateTimeOffset?)h.CreadoEn),
              CreadoEn = p.CreadoEn,
          })
          .FirstOrDefaultAsync(ct);

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

/// <summary>
/// Un pedido reducido a lo que hace falta para decidir si el actor lo alcanza.
/// </summary>
/// <remarks>
/// Clase con propiedades asignables y no <c>record</c>: la materializa EF Core
/// desde SQL crudo sobre un tipo no mapeado, y ese camino exige constructor sin
/// parámetros y propiedades escribibles.
/// </remarks>
internal sealed class PedidoUbicado
{
    public Guid Id { get; set; }

    public string Numero { get; set; } = string.Empty;

    public Guid MateriaId { get; set; }

    public Guid CarreraId { get; set; }
}

/// <summary>
/// Lo que la ficha de un trámite necesita leer, antes de resolver la materia y
/// filtrar por ámbito.
/// </summary>
internal sealed class DatosDeFicha
{
    public Guid Id { get; set; }

    public string Numero { get; set; } = string.Empty;

    public string Novedad { get; set; } = string.Empty;

    public string Estado { get; set; } = string.Empty;

    public Guid MateriaId { get; set; }

    public Guid CarreraId { get; set; }

    public string Periodo { get; set; } = string.Empty;

    public string? Cargo { get; set; }

    public string? Dedicacion { get; set; }

    /// <summary>Fecha del último evento del historial; nula si todavía no tiene ninguno.</summary>
    public DateTimeOffset? UltimoMovimiento { get; set; }

    public DateTimeOffset CreadoEn { get; set; }
}
