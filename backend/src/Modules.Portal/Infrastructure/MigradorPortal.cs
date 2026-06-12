using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Modules.Portal.Infrastructure;

/// <summary>
/// Aplica las migraciones del módulo Portal. Interno al módulo: el Host lo
/// resuelve solo a través de <see cref="IMigradorModulo"/>, sin conocer el
/// <see cref="PortalDbContext"/>.
/// </summary>
internal sealed class MigradorPortal(PortalDbContext db) : IMigradorModulo
{
    public Task MigrarAsync(CancellationToken ct) => db.Database.MigrateAsync(ct);
}
