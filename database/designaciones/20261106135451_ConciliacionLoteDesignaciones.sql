-- 20261106135451_ConciliacionLoteDesignaciones (Designaciones).
-- No editar después de publicar: crear otra migración incremental.
--
-- Conciliación del lote con la rectificación (change conciliacion-designaciones):
-- * en_lote deja de ser terminal: queda pendiente de conciliar.
-- * aprobado: sincronizado con las designaciones vigentes (terminal).
-- * no_aprobado: rechazado en la conciliación; editable con designaciones.editar.
-- * Eventos de historial sincronizar y no_aprobar.
-- * materializado_en: cuándo el pedido se escribió sobre las designaciones vigentes.
--   Los en_lote existentes ya se materializaron al aceptar Decanato: se marcan con
--   el momento de esta migración para que la sincronización no los vuelva a escribir.
--
-- El índice pedidos_uno_por_docente_periodo no cambia: aprobado y no_aprobado
-- siguen ocupando el cupo de BR-designaciones-001.

ALTER TABLE designaciones.pedidos DROP CONSTRAINT pedidos_estado_valido;
ALTER TABLE designaciones.pedidos ADD CONSTRAINT pedidos_estado_valido CHECK (estado = ANY (ARRAY[
    'borrador', 'en_revision_coordinador', 'en_revision_secretaria', 'en_revision_decanato',
    'devuelto', 'en_lote', 'aprobado', 'no_aprobado', 'rechazado', 'cancelado']));

ALTER TABLE designaciones.pedido_historial DROP CONSTRAINT pedido_historial_accion_valida;
ALTER TABLE designaciones.pedido_historial ADD CONSTRAINT pedido_historial_accion_valida CHECK (accion = ANY (ARRAY[
    'crear', 'enviar', 'aceptar', 'rechazar', 'devolver', 'reenviar', 'editar', 'cancelar',
    'priorizar', 'despriorizar', 'sincronizar', 'no_aprobar']));

ALTER TABLE designaciones.pedido_historial DROP CONSTRAINT pedido_historial_etapa_valida;
ALTER TABLE designaciones.pedido_historial ADD CONSTRAINT pedido_historial_etapa_valida CHECK (etapa = ANY (ARRAY[
    'borrador', 'en_revision_coordinador', 'en_revision_secretaria', 'en_revision_decanato',
    'devuelto', 'en_lote', 'aprobado', 'no_aprobado', 'rechazado', 'cancelado']));

-- Un Alta o Cambio sigue completo mientras está en lote, rechazado en la
-- conciliación o sincronizado: la conciliación y el lote leen esos valores.
ALTER TABLE designaciones.pedidos DROP CONSTRAINT pedidos_completitud_circuito;
ALTER TABLE designaciones.pedidos ADD CONSTRAINT pedidos_completitud_circuito CHECK (
    novedad NOT IN ('Alta', 'Cambio de cargo o dedicación')
    OR estado NOT IN ('en_revision_coordinador', 'en_revision_secretaria', 'en_revision_decanato',
                      'devuelto', 'en_lote', 'aprobado', 'no_aprobado')
    OR (cargo_solicitado_id IS NOT NULL AND dedicacion_solicitada_id IS NOT NULL
        AND horas IS NOT NULL AND horas > 0)
);

ALTER TABLE designaciones.pedidos ADD COLUMN materializado_en timestamp with time zone;

UPDATE designaciones.pedidos SET materializado_en = now() WHERE estado = 'en_lote';

-- Sólo un pedido sincronizado o heredado del circuito anterior puede estar materializado.
ALTER TABLE designaciones.pedidos ADD CONSTRAINT pedidos_materializado_coherente CHECK (
    materializado_en IS NULL OR estado IN ('en_lote', 'aprobado')
);

COMMENT ON COLUMN designaciones.pedidos.materializado_en IS
    'Momento en que el pedido se escribió sobre las designaciones vigentes. Nulo mientras no se sincronizó. Los pedidos en lote anteriores a la conciliación lo tienen completo porque se materializaron al aceptar Decanato.';

COMMENT ON COLUMN designaciones.pedidos.estado IS
    'Etapa del circuito de aprobación. Valores admitidos: borrador (todavía no se envió), en_revision_coordinador, en_revision_secretaria, en_revision_decanato, devuelto (volvió al origen con observaciones), en_lote (aceptado por Decanato, exportado para rectificar y pendiente de conciliar), aprobado (la rectificación lo aprobó y se sincronizó con las designaciones vigentes; terminal), no_aprobado (la rectificación no lo aprobó; se puede corregir y volver a en_lote), rechazado (rechazado en el circuito; terminal), cancelado (terminal). Un pedido "pendiente" es cualquiera en alguna de las tres etapas en_revision_*.';

COMMENT ON COLUMN designaciones.pedido_historial.accion IS
    'Qué se hizo. Valores admitidos: crear, enviar, aceptar, rechazar, devolver, reenviar, editar, cancelar, priorizar, despriorizar, sincronizar (aprobado en la conciliación y escrito sobre las designaciones), no_aprobar (rechazado en la conciliación).';
