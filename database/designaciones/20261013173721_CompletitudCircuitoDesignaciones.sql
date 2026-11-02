-- No completa ni inventa cargo/horas de pedidos previos.
LOCK TABLE designaciones.pedidos IN SHARE ROW EXCLUSIVE MODE;
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM designaciones.pedidos
        WHERE novedad IN ('Alta', 'Cambio de cargo o dedicación')
          AND estado IN ('en_revision_coordinador','en_revision_secretaria','en_revision_decanato','devuelto','en_lote')
          AND (cargo_solicitado_id IS NULL OR dedicacion_solicitada_id IS NULL OR horas IS NULL OR horas <= 0)) THEN
        RAISE EXCEPTION 'Pedidos incompletos en circuito; ejecutar diagnóstico y saneamiento aprobado'
            USING ERRCODE = '23514';
    END IF;
END $$;
ALTER TABLE designaciones.pedidos ADD CONSTRAINT pedidos_completitud_circuito CHECK (
    novedad NOT IN ('Alta','Cambio de cargo o dedicación')
    OR estado NOT IN ('en_revision_coordinador','en_revision_secretaria','en_revision_decanato','devuelto','en_lote')
    OR (cargo_solicitado_id IS NOT NULL AND dedicacion_solicitada_id IS NOT NULL AND horas IS NOT NULL AND horas > 0)
);
