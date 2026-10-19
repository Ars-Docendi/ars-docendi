using ArsDocendi.Migraciones;

namespace Modules.Portal.Infrastructure;

public sealed class DiagnosticoPortal(PortalDbContext db) : DiagnosticoEfSql<PortalDbContext>(db, "Portal", """
    SELECT 'referencia', 'portal-persona', id, 'persona', persona_id FROM portal.perfiles
    UNION ALL
    SELECT 'referencia', 'portal-archivo', id, 'archivo', archivo_id FROM portal.cvs WHERE archivo_id IS NOT NULL
    UNION ALL
    SELECT 'referencia', 'portal-archivo', id, 'archivo', archivo_id FROM portal.proyecto_documentos WHERE archivo_id IS NOT NULL
    """);
