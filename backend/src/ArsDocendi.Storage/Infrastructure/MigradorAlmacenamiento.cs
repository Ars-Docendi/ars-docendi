using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace ArsDocendi.Storage.Infrastructure;

internal sealed class MigradorAlmacenamiento(AlmacenamientoDbContext db) : IMigradorModulo
{
    public Task MigrarAsync(CancellationToken ct) => db.Database.MigrateAsync(ct);
}
