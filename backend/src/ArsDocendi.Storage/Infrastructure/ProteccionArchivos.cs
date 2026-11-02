using ArsDocendi.Shared.Aplicacion;
using ArsDocendi.Storage.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace ArsDocendi.Storage.Infrastructure;

/// <summary>Una conexión y transacción local PostgreSQL; nunca TransactionScope/MSDTC.</summary>
public sealed class ProteccionArchivos(AlmacenamientoDbContext db, IEnumerable<IParticipanteTransaccionArchivos> participantes) : IProteccionArchivos
{
    private HashSet<Guid>? idsActivos;

    public async Task<T> EjecutarAsync<T>(IReadOnlyCollection<Guid> archivoIds, Func<CancellationToken, Task<T>> operacion, CancellationToken ct)
    {
        var ids = archivoIds.Distinct().OrderBy(x => x).ToArray();
        if (idsActivos is not null)
        {
            if (ids.Any(id => !idsActivos.Contains(id)))
                throw new InvalidOperationException("La operación anidada no puede ampliar los IDs protegidos.");
            return await operacion(ct);
        }
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("La protección debe iniciar la transacción antes de las escrituras consumidoras.");
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        var enlistados = new List<IParticipanteTransaccionArchivos>();
        try
        {
            await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '10s'", ct);
            foreach (var id in ids)
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({id.ToString()}, 8361))", ct);
            idsActivos = ids.ToHashSet();
            foreach (var participante in participantes)
            {
                enlistados.Add(participante);
                await participante.EnlistarAsync(db.Database.GetDbConnection(), transaccion.GetDbTransaction(), ct);
            }
            var resultado = await operacion(ct);
            await transaccion.CommitAsync(ct);
            return resultado;
        }
        catch (PostgresException ex) when (ex.SqlState is "55P03" or "40P01" or "40001")
        {
            await transaccion.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw new ExcepcionAplicacion(TipoErrorAplicacion.Conflicto, "archivo-concurrency-conflict", "Otra operación está modificando el archivo.");
        }
        catch
        {
            await transaccion.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
        finally
        {
            idsActivos = null;
            foreach (var participante in enlistados.AsEnumerable().Reverse())
                await participante.DesenlistarAsync(CancellationToken.None);
        }
    }
}
