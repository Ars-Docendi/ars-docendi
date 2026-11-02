using ArsDocendi.Migraciones;

namespace Modules.Designaciones.Infrastructure;

/// <summary>Solo relaciones locales y categorías técnicas; Host compara destinos sin acceso cruzado.</summary>
public sealed class DiagnosticoDesignaciones(DesignacionesDbContext db)
    : DiagnosticoEfSql<DesignacionesDbContext>(db, "Designaciones", """
        SELECT 'referencia', 'designaciones-persona', id, 'persona', persona_id FROM designaciones.designaciones
        UNION ALL SELECT 'referencia', 'designaciones-materia', id, 'materia', materia_id FROM designaciones.designaciones
        UNION ALL SELECT 'referencia', 'designaciones-persona', id, 'persona', persona_id FROM designaciones.pedidos
        UNION ALL SELECT 'referencia', 'designaciones-materia', id, 'materia', materia_id FROM designaciones.pedidos
        UNION ALL SELECT 'referencia', 'designaciones-archivo', id, 'archivo', archivo_id FROM designaciones.pedido_adjuntos WHERE archivo_id IS NOT NULL
        UNION ALL SELECT 'hallazgo', 'designaciones-pedido-incompleto', id, NULL::text, NULL::uuid
        FROM designaciones.pedidos
        WHERE novedad IN ('Alta','Cambio de cargo o dedicación')
          AND estado IN ('en_revision_coordinador','en_revision_secretaria','en_revision_decanato','devuelto','en_lote')
          AND (cargo_solicitado_id IS NULL OR dedicacion_solicitada_id IS NULL OR horas IS NULL OR horas <= 0)
        """);
