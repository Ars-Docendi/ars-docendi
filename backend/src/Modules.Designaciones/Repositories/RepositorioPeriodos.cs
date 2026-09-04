using ArsDocendi.Shared.Aplicacion;
using Microsoft.EntityFrameworkCore;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure;
using Npgsql;

namespace Modules.Designaciones.Repositories;

public interface IRepositorioPeriodos
{
    Task<IReadOnlyList<Periodo>> ListarAsync(CancellationToken ct);
    Task<Periodo?> ObtenerAsync(Guid id, bool tracking, CancellationToken ct);
    Task<bool> ExisteOtroActivoAsync(Guid? exceptoId, CancellationToken ct);
    void Agregar(Periodo periodo);
    void Eliminar(Periodo periodo);
    void EsperarVersion(Periodo periodo, uint version);
    Task GuardarAsync(CancellationToken ct);
}

internal sealed class RepositorioPeriodos(DesignacionesDbContext db) : IRepositorioPeriodos
{
    public async Task<IReadOnlyList<Periodo>> ListarAsync(CancellationToken ct) =>
        await db.Periodos.AsNoTracking()
            .OrderByDescending(p => p.ImpactoDesde)
            .ToListAsync(ct);

    public Task<Periodo?> ObtenerAsync(Guid id, bool tracking, CancellationToken ct) =>
        (tracking ? db.Periodos : db.Periodos.AsNoTracking())
            .SingleOrDefaultAsync(p => p.Id == id, ct);

    public Task<bool> ExisteOtroActivoAsync(Guid? exceptoId, CancellationToken ct) =>
        db.Periodos.AsNoTracking().AnyAsync(p => p.Activo && p.Id != exceptoId, ct);

    public void Agregar(Periodo periodo) => db.Periodos.Add(periodo);
    public void Eliminar(Periodo periodo) => db.Periodos.Remove(periodo);
    public void EsperarVersion(Periodo periodo, uint version) =>
        db.Entry(periodo).Property(p => p.Version).OriginalValue = version;

    public async Task GuardarAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException pg)
        {
            var traducido = pg.ConstraintName switch
            {
                "periodos_unico_activo" => new ExcepcionAplicacion(
                    TipoErrorAplicacion.Conflicto,
                    "periodo-active-conflict",
                    "Ya existe otro período activo."),
                "pedidos_periodo_id_fkey" => new ExcepcionAplicacion(
                    TipoErrorAplicacion.Conflicto,
                    "periodo-in-use",
                    "El período tiene pedidos asociados y no se puede eliminar."),
                _ => null,
            };
            if (traducido is not null) throw traducido;
            throw;
        }
    }
}
