using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Modules.Designaciones.Infrastructure;

/// <summary>
/// Aplica las migraciones del módulo Designaciones. Interno al módulo: el Host
/// lo resuelve solo a través de <see cref="IMigradorModulo"/>, sin conocer el
/// <see cref="DesignacionesDbContext"/>.
/// </summary>
internal sealed class MigradorDesignaciones(DesignacionesDbContext db) : IMigradorModulo
{
    public Task MigrarAsync(CancellationToken ct) => db.Database.MigrateAsync(ct);
}
