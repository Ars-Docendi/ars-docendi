using Microsoft.EntityFrameworkCore;
using Modules.Designaciones.Domain;
using Modules.Designaciones.Infrastructure;

namespace Modules.Designaciones.Repositories;

internal interface IRepositorioIdempotencia
{
    Task BloquearAsync(Guid actorId, string ruta, Guid clave, CancellationToken ct);
    Task<ComandoIdempotente?> ObtenerVigenteAsync(
        Guid actorId, string ruta, Guid clave, CancellationToken ct);
    void Agregar(ComandoIdempotente comando);
    Task GuardarAsync(CancellationToken ct);
}

internal sealed class RepositorioIdempotencia(DesignacionesDbContext db) : IRepositorioIdempotencia
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

    public void Agregar(ComandoIdempotente comando) => db.ComandosIdempotentes.Add(comando);
    public Task GuardarAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
