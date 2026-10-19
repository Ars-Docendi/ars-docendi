using ArsDocendi.Shared.Aplicacion;
using Microsoft.EntityFrameworkCore;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure;
using Npgsql;

namespace Modules.Designaciones.Repositories;

/// <summary>Período lectivo con comisiones importadas y el estado de sus importaciones.</summary>
public sealed record PeriodoImportado(
    int AnioAcademico,
    string PeriodoLectivoId,
    string PeriodoLectivoNombre,
    ImportacionComisiones? UltimaExitosa,
    ImportacionComisiones? UltimoIntento);

public sealed class RepositorioComisiones(DesignacionesDbContext db)
{
    /// <summary>
    /// Registra una importación <c>en_curso</c>. Antes caduca la reserva de un proceso que
    /// murió sin cerrarla; el índice único parcial decide quién importa si hay carrera.
    /// </summary>
    public async Task<ImportacionComisiones> ReservarAsync(
        int anio, string periodoId, string periodoNombre, Guid? usuarioId, TimeSpan caducidad, CancellationToken ct)
    {
        var limite = DateTimeOffset.UtcNow - caducidad;
        await db.ImportacionesComisiones
            .Where(i => i.PeriodoLectivoGuaraniId == periodoId
                && i.Estado == EstadosImportacion.EnCurso
                && i.IniciadaEn < limite)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Estado, EstadosImportacion.Fallida)
                .SetProperty(i => i.Causa, CausasImportacion.Interrumpida)
                .SetProperty(i => i.FinalizadaEn, DateTimeOffset.UtcNow), ct);

        var importacion = new ImportacionComisiones
        {
            Id = Guid.NewGuid(),
            AnioAcademico = anio,
            PeriodoLectivoGuaraniId = periodoId,
            PeriodoLectivoNombre = periodoNombre,
            UsuarioId = usuarioId,
            IniciadaEn = DateTimeOffset.UtcNow,
            Estado = EstadosImportacion.EnCurso,
            CreadoEn = DateTimeOffset.UtcNow,
        };
        db.ImportacionesComisiones.Add(importacion);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException error) when (
            error.InnerException is PostgresException { ConstraintName: "sga_importaciones_una_en_curso" })
        {
            db.Entry(importacion).State = EntityState.Detached;
            throw new ExcepcionAplicacion(
                TipoErrorAplicacion.Conflicto,
                "comisiones-importacion-en-curso",
                "Ya hay una importación en curso para este período. Esperá a que termine.");
        }
        return importacion;
    }

    /// <summary>
    /// Reemplaza en una transacción las comisiones del período (las hijas caen por cascada)
    /// y cierra la importación como exitosa. Devuelve false si una comisión del lote ya
    /// pertenece a otro período: no se reemplaza nada.
    /// </summary>
    public async Task<bool> ReemplazarAsync(
        ImportacionComisiones importacion,
        IReadOnlyList<ComisionGuarani> comisiones,
        int docentesSinVincular,
        int carrerasSinVincular,
        CancellationToken ct)
    {
        var estrategia = db.Database.CreateExecutionStrategy();
        try
        {
            await estrategia.ExecuteAsync(async token =>
            {
                await using var transaccion = await db.Database.BeginTransactionAsync(token);
                await db.ComisionesGuarani
                    .Where(c => c.PeriodoLectivoGuaraniId == importacion.PeriodoLectivoGuaraniId)
                    .ExecuteDeleteAsync(token);
                db.ComisionesGuarani.AddRange(comisiones);
                importacion.Estado = EstadosImportacion.Exitosa;
                importacion.FinalizadaEn = DateTimeOffset.UtcNow;
                importacion.Cantidad = comisiones.Count;
                importacion.DocentesSinVincular = docentesSinVincular;
                importacion.CarrerasSinVincular = carrerasSinVincular;
                await db.SaveChangesAsync(token);
                await transaccion.CommitAsync(token);
            }, ct);
            return true;
        }
        catch (DbUpdateException error) when (
            error.InnerException is PostgresException { ConstraintName: "sga_comisiones_pkey" })
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task FallarAsync(Guid importacionId, string causa, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await db.ImportacionesComisiones
            .Where(i => i.Id == importacionId && i.Estado == EstadosImportacion.EnCurso)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Estado, EstadosImportacion.Fallida)
                .SetProperty(i => i.Causa, causa)
                .SetProperty(i => i.FinalizadaEn, DateTimeOffset.UtcNow), ct);
    }

    public async Task<IReadOnlyList<PeriodoImportado>> ListarPeriodosImportadosAsync(CancellationToken ct)
    {
        var periodos = await db.ComisionesGuarani.AsNoTracking()
            .GroupBy(c => new { c.AnioAcademico, c.PeriodoLectivoGuaraniId, c.PeriodoLectivoNombre })
            .Select(g => g.Key)
            .ToListAsync(ct);
        var ids = periodos.Select(p => p.PeriodoLectivoGuaraniId).ToArray();
        var importaciones = await db.ImportacionesComisiones.AsNoTracking()
            .Where(i => ids.Contains(i.PeriodoLectivoGuaraniId) && i.Estado != EstadosImportacion.EnCurso)
            .ToListAsync(ct);

        return periodos
            .Select(p =>
            {
                var delPeriodo = importaciones
                    .Where(i => i.PeriodoLectivoGuaraniId == p.PeriodoLectivoGuaraniId)
                    .OrderByDescending(i => i.IniciadaEn)
                    .ToArray();
                return new PeriodoImportado(
                    p.AnioAcademico,
                    p.PeriodoLectivoGuaraniId,
                    p.PeriodoLectivoNombre,
                    delPeriodo.FirstOrDefault(i => i.Estado == EstadosImportacion.Exitosa),
                    delPeriodo.FirstOrDefault());
            })
            .OrderByDescending(p => p.AnioAcademico)
            .ThenByDescending(p => p.PeriodoLectivoId.Length)
            .ThenByDescending(p => p.PeriodoLectivoId, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<IReadOnlyList<ComisionGuarani>> ListarComisionesAsync(string periodoId, CancellationToken ct) =>
        await db.ComisionesGuarani.AsNoTracking()
            .Where(c => c.PeriodoLectivoGuaraniId == periodoId)
            .Include(c => c.Carreras)
            .Include(c => c.Docentes)
            .Include(c => c.Horarios)
            .AsSplitQuery()
            .OrderBy(c => c.MateriaNombre)
            .ThenBy(c => c.Nombre)
            .ToListAsync(ct);
}
