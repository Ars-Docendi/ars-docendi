using ArsDocendi.Migraciones;

namespace Modules.Aulas.Infrastructure;

internal sealed class MigradorAulas(AulasDbContext db)
    : MigradorEfSql<AulasDbContext>(db, "aulas", AulasDbContext.Schema);
