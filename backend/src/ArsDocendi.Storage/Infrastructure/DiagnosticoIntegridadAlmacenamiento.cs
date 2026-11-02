using ArsDocendi.Migraciones;

namespace ArsDocendi.Storage.Infrastructure;

/// <summary>Destinos técnicos locales leídos bajo snapshot de solo lectura.</summary>
public sealed class DiagnosticoIntegridadAlmacenamiento(AlmacenamientoDbContext db)
    : DiagnosticoEfSql<AlmacenamientoDbContext>(db, "Storage", """
        SELECT 'destino', 'archivo', id, NULL::text, NULL::uuid FROM storage.archivos
        """);
