using ArsDocendi.Migraciones;

namespace Modules.Notificaciones.Infrastructure;

internal sealed class MigradorNotificaciones(NotificacionesDbContext db)
    : MigradorEfSql<NotificacionesDbContext>(db, "notificaciones", NotificacionesDbContext.Schema);
