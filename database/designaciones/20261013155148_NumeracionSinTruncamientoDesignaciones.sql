-- Padding mínimo sin truncar. nextval se evalúa una sola vez; se conservan
-- la secuencia, sus gaps y todos los números ya asignados.
CREATE OR REPLACE FUNCTION designaciones.siguiente_numero_pedido() RETURNS text
    LANGUAGE sql
    AS $$
    SELECT to_char(now(), 'YYYY') || '-' || lpad(numero, greatest(4, length(numero)), '0')
    FROM (SELECT nextval('designaciones.pedidos_numero_seq')::text AS numero) AS correlativo;
$$;
