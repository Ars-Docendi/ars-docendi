-- SQL real capturado mediante DbCommandInterceptor en PostgreSQL 18 Testcontainers.
-- Origen: designaciones-tdd-verificado.trx, LecturasSelectivasDesignacionesTests.
-- Los parámetros son UUIDs de fixture; no se registran sus valores ni PII.
-- Es evidencia de comandos parametrizados de EF, no un script de migración.

-- Relectura administrativa: 1 fila de la persona de la fixture.
SELECT d.id, d.cargo_id, d.carrera_id, d.created_at, d.dedicacion, d.dedicacion_id, d.horas, d.horas_externas, d.horas_investigacion, d.materia_id, d.origen_pedido_id, d.persona_id, d.xmin, d.vigente_desde, d.vigente_hasta, c.id, c.abreviatura, c.activo, c.codigo, c.created_at, c.nombre, c.orden, d0.id, d0.activo, d0.codigo, d0.created_at, d0.nombre, d0.orden
FROM designaciones.designaciones AS d
INNER JOIN designaciones.cargos AS c ON d.cargo_id = c.id
LEFT JOIN designaciones.dedicaciones AS d0 ON d.dedicacion_id = d0.id
WHERE d.persona_id = @personaId AND d.vigente_hasta IS NULL
ORDER BY d.materia_id;

-- Resumen: 8 filas visibles, 22 eventos antes y después de agregar 2.000 eventos.
-- Una consulta por lote. No selecciona comentario ni documentos.
SELECT p.id AS "Id", p.pedido_id AS "PedidoId", p.accion AS "Accion", p.rol_id AS "RolId", p.actor_id AS "ActorId", p.etapa AS "Etapa", p.created_at AS "CreadoEn"
FROM designaciones.pedido_historial AS p
WHERE p.pedido_id = ANY (@pedidoIds) AND (p.id = (
    SELECT p0.id
    FROM designaciones.pedido_historial AS p0
    WHERE p0.pedido_id = p.pedido_id
    ORDER BY p0.created_at, p0.id
    LIMIT 1) OR p.id = (
    SELECT p1.id
    FROM designaciones.pedido_historial AS p1
    WHERE p1.pedido_id = p.pedido_id AND p1.accion = 'crear'
    ORDER BY p1.created_at, p1.id
    LIMIT 1) OR p.id = (
    SELECT p2.id
    FROM designaciones.pedido_historial AS p2
    WHERE p2.pedido_id = p.pedido_id AND p2.accion = 'enviar'
    ORDER BY p2.created_at, p2.id
    LIMIT 1) OR p.id = (
    SELECT p3.id
    FROM designaciones.pedido_historial AS p3
    WHERE p3.pedido_id = p.pedido_id
    ORDER BY p3.created_at DESC, p3.id DESC
    LIMIT 1));
