using Microsoft.EntityFrameworkCore;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure;

namespace Modules.Designaciones.Repositories;

internal sealed class RepositorioIdempotencia(DesignacionesDbContext db)
{
    public async Task BloquearAsync(Guid actorId, string ruta, Guid clave, CancellationToken ct)
    {
        var alcance = $"{actorId:N}:{ruta}:{clave:N}";
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({alcance}, 0));", ct);
        await db.ComandosIdempotentes
            .Where(c => c.ActorId == actorId
                && c.Ruta == ruta
                && c.Clave == clave
                && c.CreadoEn < DateTimeOffset.UtcNow.AddHours(-24))
            .ExecuteDeleteAsync(ct);
    }

    public Task<ComandoIdempotente?> ObtenerVigenteAsync(
        Guid actorId,
        string ruta,
        Guid clave,
        CancellationToken ct) =>
        db.ComandosIdempotentes.AsNoTracking().SingleOrDefaultAsync(c =>
            c.ActorId == actorId
            && c.Ruta == ruta
            && c.Clave == clave
            && c.CreadoEn >= DateTimeOffset.UtcNow.AddHours(-24), ct);

    /// <summary>Borra las claves vencidas de todos los actores; lo llama la limpieza periódica.</summary>
    public Task<int> LimpiarVencidosAsync(CancellationToken ct) =>
        db.ComandosIdempotentes
            .Where(c => c.CreadoEn < DateTimeOffset.UtcNow.AddHours(-24))
            .ExecuteDeleteAsync(ct);

    public void Agregar(ComandoIdempotente comando) => db.ComandosIdempotentes.Add(comando);
    public Task GuardarAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
