using ArsDocendi.Migraciones;

namespace Modules.Tareas.Infrastructure;

internal sealed class MigradorTareas(TareasDbContext db)
    : MigradorEfSql<TareasDbContext>(db, "tareas", TareasDbContext.Schema);
