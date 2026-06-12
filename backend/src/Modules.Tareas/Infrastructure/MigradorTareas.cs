using ArsDocendi.Shared.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Modules.Tareas.Infrastructure;

/// <summary>
/// Aplica las migraciones del módulo Tareas. Interno al módulo: el Host lo
/// resuelve solo a través de <see cref="IMigradorModulo"/>, sin conocer el
/// <see cref="TareasDbContext"/>.
/// </summary>
internal sealed class MigradorTareas(TareasDbContext db) : IMigradorModulo
{
    public Task MigrarAsync(CancellationToken ct) => db.Database.MigrateAsync(ct);
}
