using ArsDocendi.Migraciones;

namespace Modules.Portal.Infrastructure;

internal sealed class MigradorPortal(PortalDbContext db)
    : MigradorEfSql<PortalDbContext>(db, "portal", PortalDbContext.Schema);
