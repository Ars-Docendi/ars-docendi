using ArsDocendi.Migraciones;

namespace ArsDocendi.Storage.Infrastructure;

internal sealed class MigradorAlmacenamiento(AlmacenamientoDbContext db)
    : MigradorEfSql<AlmacenamientoDbContext>(db, "storage", AlmacenamientoDbContext.Schema);
