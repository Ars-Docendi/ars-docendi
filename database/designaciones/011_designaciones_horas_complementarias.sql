ALTER TABLE designaciones.designaciones
    ADD COLUMN horas_investigacion INTEGER CHECK (horas_investigacion >= 0),
    ADD COLUMN horas_externas INTEGER CHECK (horas_externas >= 0);

-- Sólo se recuperan valores cuyo pedido de origen sigue identificado.
-- Las cargas desconocidas y los snapshots históricos conservan su ausencia.
UPDATE designaciones.designaciones d
SET horas_investigacion = p.horas_investigacion,
    horas_externas = p.horas_externas
FROM designaciones.pedidos p
WHERE p.id = d.origen_pedido_id;
