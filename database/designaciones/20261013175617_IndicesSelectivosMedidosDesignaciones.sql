-- Índices justificados por consultas y planes reproducibles, no por barrido nominal de FKs.
-- Evidencia: scripts/tests/fixtures/rendimiento-candidatos/resultado.json y
-- openspec/changes/corregir-integridad-y-rendimiento-post-auditoria/evidence/indices-candidatos-implementacion.md.
-- No editar después de publicar: crear otra migración incremental.
CREATE INDEX designaciones_materia_idx ON designaciones.designaciones (materia_id);
CREATE INDEX pedido_historial_actor_idx ON designaciones.pedido_historial (actor_id);
CREATE INDEX pedido_historial_rol_idx ON designaciones.pedido_historial (rol_id);
CREATE INDEX idempotencia_pedido_idx ON designaciones.idempotencia_comandos (pedido_id);
