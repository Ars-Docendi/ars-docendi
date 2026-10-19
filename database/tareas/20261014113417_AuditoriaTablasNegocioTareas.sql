-- Identity instala antes el enganche preciso y su allowlist de snapshots seguros.
-- Cada tabla de negocio se registra una vez, usando la PK real completa.
SELECT audit.attach('tareas.proyectos');
SELECT audit.attach('tareas.tareas');
SELECT audit.attach('tareas.tarea_relaciones', ARRAY['tarea_id','relacionada_id']);
SELECT audit.attach('tareas.tarea_comentarios');
SELECT audit.attach('tareas.tarea_historial');
