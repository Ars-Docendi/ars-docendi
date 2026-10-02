-- Numeración de trámite.
--
-- `pedidos.numero` es el identificador legible que el usuario cita ("2026-0042").
-- Se genera desde una secuencia y no contando filas: contar es una race condition
-- —dos altas simultáneas leen el mismo total— y el índice UNIQUE de numero haría
-- fallar una de las dos sin motivo aparente.
--
-- La secuencia es global y monotónica: no reinicia cada año. El año del prefijo
-- viene de la fecha de creación, así que los números son únicos y ordenables, pero
-- la parte numérica NO arranca de 0001 en enero. Si el cliente pide numeración que
-- reinicie por año, es una secuencia por año o un contador en tabla — cambio acotado.

CREATE SEQUENCE designaciones.pedidos_numero_seq AS BIGINT START WITH 1;

-- Devuelve el próximo número de trámite con formato AAAA-NNNN.
-- VOLATILE (default) e independiente de la transacción: nextval no se revierte en
-- un ROLLBACK, así que un pedido fallido "consume" un número. Es lo correcto para
-- una numeración de trámite — se prefiere un salto a un número reutilizado.
CREATE OR REPLACE FUNCTION designaciones.siguiente_numero_pedido()
RETURNS TEXT
LANGUAGE sql
AS $$
    SELECT to_char(now(), 'YYYY') || '-' ||
           lpad(nextval('designaciones.pedidos_numero_seq')::TEXT, 4, '0');
$$;
