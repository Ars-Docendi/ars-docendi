using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Modules.Aulas.Infrastructure;

/// <summary>
/// Aplica las migraciones del módulo Aulas. Interno al módulo: el Host lo
/// resuelve solo a través de <see cref="IMigradorModulo"/>, sin conocer el
/// <see cref="AulasDbContext"/>.
/// </summary>
internal sealed class MigradorAulas(AulasDbContext db) : IMigradorModulo
{
    public Task MigrarAsync(CancellationToken ct) => db.Database.MigrateAsync(ct);
}
