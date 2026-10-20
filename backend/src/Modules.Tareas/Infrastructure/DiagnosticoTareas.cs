using ArsDocendi.Migraciones;

namespace Modules.Tareas.Infrastructure;

/// <summary>Solo lectura local; no duplica nombres, soluciones ni textos en el diagnóstico.</summary>
public sealed class DiagnosticoTareas(TareasDbContext db) : DiagnosticoEfSql<TareasDbContext>(db, "Tareas", """
    WITH RECURSIVE recorrido AS (
        SELECT id AS origen, id, tarea_padre_id, ARRAY[id] AS camino, false AS ciclo FROM tareas.tareas
        UNION ALL
        SELECT r.origen, t.id, t.tarea_padre_id, r.camino || t.id, t.id = ANY(r.camino)
        FROM recorrido r JOIN tareas.tareas t ON t.id = r.tarea_padre_id WHERE NOT r.ciclo
    )
    SELECT 'referencia', 'tareas-usuario', id, 'usuario', responsable_id FROM tareas.tareas
    UNION ALL SELECT 'referencia', 'tareas-usuario', id, 'usuario', creado_por_id FROM tareas.tareas
    UNION ALL SELECT 'referencia', 'tareas-usuario', id, 'usuario', responsable_id FROM tareas.proyectos
    UNION ALL SELECT 'referencia', 'tareas-usuario', id, 'usuario', autor_id FROM tareas.tarea_comentarios
    UNION ALL SELECT 'referencia', 'tareas-usuario', id, 'usuario', actor_id FROM tareas.tarea_historial
    UNION ALL SELECT DISTINCT 'hallazgo', 'tareas-ciclo', origen, NULL, NULL::uuid FROM recorrido WHERE ciclo
    UNION ALL SELECT 'hallazgo', 'tareas-proyecto-incoherente', t.id, NULL, NULL::uuid
        FROM tareas.tareas t JOIN tareas.tareas p ON p.id = t.tarea_padre_id
        WHERE t.proyecto_id IS DISTINCT FROM p.proyecto_id
    UNION ALL SELECT 'hallazgo', 'tareas-padre-huerfano', t.id, NULL, NULL::uuid FROM tareas.tareas t
        WHERE tarea_padre_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM tareas.tareas p WHERE p.id = t.tarea_padre_id)
    UNION ALL SELECT 'hallazgo', 'tareas-proyecto-huerfano', t.id, NULL, NULL::uuid FROM tareas.tareas t
        WHERE proyecto_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM tareas.proyectos p WHERE p.id = t.proyecto_id)
    UNION ALL SELECT 'hallazgo', 'tareas-resuelta-sin-solucion', t.id, NULL, NULL::uuid
        FROM tareas.tareas t JOIN tareas.estados_tarea e ON e.id = t.estado_id
        WHERE e.codigo = 'resuelta' AND nullif(btrim(t.solucion), '') IS NULL
    """);
