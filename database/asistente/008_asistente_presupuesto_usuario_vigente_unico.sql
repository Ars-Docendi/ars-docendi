-- A lo sumo un override VIGENTE por actor en asistente.presupuesto_usuario,
-- impuesto por el motor.
--
-- 006_asistente_administracion.sql lo dejaba a la aplicación con el argumento
-- de que cerrar la fila anterior y abrir la nueva van en la misma transacción.
-- Eso hace atómico UN cambio, no dos concurrentes: en READ COMMITTED, dos
-- ediciones simultáneas del mismo actor cierran la misma fila (o ninguna, si no
-- había) y abren una cada una. La cuota lee entonces una de las dos al azar.
-- El escritor ahora además serializa por actor (PresupuestosAdministrablesReal);
-- este índice es lo que lo vuelve falsable.
--
-- Convergente como el resto del DDL del asistente: se reaplica entero.

-- Antes del índice, cierra las vigentes duplicadas que ya existan y deja abierta
-- la más reciente de cada actor. Sin duplicados no toca ninguna fila.
UPDATE asistente.presupuesto_usuario AS anterior
   SET vigente_hasta = now()
 WHERE anterior.vigente_hasta IS NULL
   AND EXISTS (
       SELECT 1
         FROM asistente.presupuesto_usuario AS posterior
        WHERE posterior.actor_id = anterior.actor_id
          AND posterior.vigente_hasta IS NULL
          AND posterior.id > anterior.id);

CREATE UNIQUE INDEX IF NOT EXISTS ux_presupuesto_usuario_actor_vigente
    ON asistente.presupuesto_usuario (actor_id)
 WHERE vigente_hasta IS NULL;
