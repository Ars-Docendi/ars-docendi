using ArsDocendi.Migraciones;

namespace Modules.Designaciones.Infrastructure;

internal sealed class MigradorDesignaciones(DesignacionesDbContext db)
    : MigradorEfSql<DesignacionesDbContext>(db, "designaciones", DesignacionesDbContext.Schema);
