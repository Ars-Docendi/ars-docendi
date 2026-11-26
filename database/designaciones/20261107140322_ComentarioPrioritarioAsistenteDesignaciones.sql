-- 20261107140322_ComentarioPrioritarioAsistenteDesignaciones (Designaciones).
-- No editar después de publicar: crear otra migración incremental.
--
-- El comentario de `designaciones.pedidos.prioritario` alimenta el prefijo del asistente.
-- Con las solicitudes de aula en el prefijo, «solicitudes urgentes» dejó de traducirse a
-- esta columna: el modelo no encontraba «urgente» en ningún comentario y se abstenía. El
-- texto nuevo nombra los sinónimos con que el Departamento pregunta.
--
-- Idempotente: COMMENT ON reemplaza el comentario anterior.

COMMENT ON COLUMN designaciones.pedidos.prioritario IS
    'Si el pedido fue marcado para tratamiento prioritario. Sinónimos del dominio: urgente, prioritario, con prioridad. «Solicitudes urgentes» o «pedidos urgentes» son los pedidos con prioritario verdadero.';
