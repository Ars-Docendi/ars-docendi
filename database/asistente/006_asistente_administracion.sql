-- 006_asistente_administracion.sql
--
-- Administración de uso del asistente (asistente-administracion-de-uso):
-- presupuestos persistentes por rol y por usuario, el tope organizacional de
-- gasto mensual y su acumulador, la tabla de precios versionada, el modo
-- mantenimiento y la auditoría de administración. Ver design.md, Migration
-- Plan punto 2.
--
-- POR QUÉ SIETE TABLAS EN UNA SOLA MIGRACIÓN
-- Igual que 002_asistente_registros.sql creó sus dos registros juntos: nacen
-- juntas porque describen una única capacidad —control operativo sobre uso y
-- gasto— y separarlas en migraciones sucesivas no compra nada.
--
-- POR QUÉ NINGUNA LLEVA CLAVE FORÁNEA A identity, A PROPÓSITO
-- Mismo criterio que auditoria_acceso_historial (004) y registro_operativo
-- (002): estas tablas tienen que poder purgarse, editarse y conservarse con
-- independencia del padrón, y una baja o cambio en identity.roles/users no
-- puede quedar bloqueada por una fila de presupuesto o de auditoría.
--
-- IDEMPOTENTE COMO 002-005: `IF NOT EXISTS` en todo. Toda columna que se sume
-- después de que una tabla exista va TAMBIÉN como
-- `ALTER TABLE ... ADD COLUMN IF NOT EXISTS`, junto al CREATE.

-- ------------------------------------------------------------ presupuesto_rol
--
-- Cupo diario de turnos por defecto, por código de rol de sistema. Una fila
-- por rol; se sobreescribe en el lugar (a diferencia de tabla_de_precios, no
-- hace falta reconstruir el costo histórico de un turno a partir del cupo
-- vigente en ese momento — el cupo solo importa AHORA, para decidir si el
-- turno de hoy entra). El "antes"/"después" de cada edición vive en
-- asistente.auditoria_administracion, no en un historial de filas acá.
--
-- `cupo_diario_turnos = 0` DESACTIVA el cupo de ese rol — mismo convenio que
-- `CupoDeLlamadasPorActor` en OpcionesAsistente. Se siembra en cero para los
-- siete roles de sistema (design.md, Open Questions): un default adivinado y
-- restrictivo es peor que arrancar visiblemente "apagado" hasta que el
-- Departamento confirme los números reales.
CREATE TABLE IF NOT EXISTS asistente.presupuesto_rol (
    rol_code           text        PRIMARY KEY,
    cupo_diario_turnos integer     NOT NULL,
    actualizado_en     timestamptz NOT NULL
);

COMMENT ON TABLE asistente.presupuesto_rol IS
    'Cupo diario de turnos por defecto, por código de rol de sistema. 0 desactiva el cupo de ese rol. Sin retención/purga: baja cardinalidad y el valor solo importa vigente, no históricamente.';

INSERT INTO asistente.presupuesto_rol (rol_code, cupo_diario_turnos, actualizado_en)
VALUES
    ('docente', 0, now()),
    ('jefe_catedra', 0, now()),
    ('coordinador_carrera', 0, now()),
    ('secretaria', 0, now()),
    ('decanato', 0, now()),
    ('administrativo', 0, now()),
    ('sys_admin', 0, now())
ON CONFLICT (rol_code) DO NOTHING;

-- --------------------------------------------------------- presupuesto_usuario
--
-- Override de cupo diario para UN actor puntual, con precedencia sobre el
-- default de su rol (design.md tarea 3.4). Versionado igual que
-- tabla_de_precios (D6) y no editado en el lugar: cerrar `vigente_hasta` de la
-- fila anterior y abrir una nueva deja auditable cuándo empezó a valer cada
-- override, sin depender solo de auditoria_administracion para eso.
--
-- La fila VIGENTE de un actor es la de `vigente_hasta IS NULL`; a lo sumo una
-- por actor en un instante dado, garantizado por la aplicación (no por una
-- constraint: un `UNIQUE` parcial sobre `vigente_hasta IS NULL` exigiría que
-- el cierre de la anterior y la apertura de la nueva fueran atómicos, y ya lo
-- son porque los escribe la misma transacción).
CREATE TABLE IF NOT EXISTS asistente.presupuesto_usuario (
    id                 bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    actor_id           uuid        NOT NULL,
    cupo_diario_turnos integer     NOT NULL,
    vigente_desde      timestamptz NOT NULL,
    vigente_hasta      timestamptz NULL
);

COMMENT ON TABLE asistente.presupuesto_usuario IS
    'Override de cupo diario de turnos para un actor puntual, con precedencia sobre presupuesto_rol. Versionado: la fila vigente de un actor tiene vigente_hasta NULL. 0 desactiva el cupo para ese actor.';

CREATE INDEX IF NOT EXISTS ix_presupuesto_usuario_actor_vigente
    ON asistente.presupuesto_usuario (actor_id, vigente_hasta);

-- ------------------------------------------------------------- tope_organizacional
--
-- Tope de gasto mensual estimado, en USD, para la organización entera.
-- Versionado igual que presupuesto_usuario: la fila vigente es la de mayor
-- `vigente_desde` que ya empezó. `tope_mensual_usd = 0` desactiva el tope —
-- mismo convenio de cero-desactiva del resto del módulo. Se siembra en cero
-- (design.md, Open Questions) hasta que el Departamento confirme el número.
CREATE TABLE IF NOT EXISTS asistente.tope_organizacional (
    id              bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tope_mensual_usd numeric(14,2) NOT NULL,
    vigente_desde    timestamptz   NOT NULL
);

COMMENT ON TABLE asistente.tope_organizacional IS
    'Tope de gasto mensual estimado (USD) para toda la organización. 0 desactiva el tope. Versionado: la fila vigente es la de mayor vigente_desde ya alcanzado.';

CREATE INDEX IF NOT EXISTS ix_tope_organizacional_vigente_desde
    ON asistente.tope_organizacional (vigente_desde);

INSERT INTO asistente.tope_organizacional (tope_mensual_usd, vigente_desde)
SELECT 0, now()
WHERE NOT EXISTS (SELECT 1 FROM asistente.tope_organizacional);

-- ------------------------------------------------- consumo_organizacional_mensual
--
-- Acumulador incremental del gasto estimado del mes en curso (design.md D6):
-- se suma una vez por turno, al momento de cobrarlo, con el precio vigente en
-- ese momento — nunca se recalcula releyendo tabla_de_precios entero. Una
-- fila por (año, mes); el mes siguiente arranca en cero por el simple hecho
-- de no tener fila todavía (D4.4: sin acción de ningún admin).
CREATE TABLE IF NOT EXISTS asistente.consumo_organizacional_mensual (
    anio                        integer       NOT NULL,
    mes                         integer       NOT NULL,
    costo_estimado_acumulado    numeric(14,6) NOT NULL DEFAULT 0,
    PRIMARY KEY (anio, mes)
);

COMMENT ON TABLE asistente.consumo_organizacional_mensual IS
    'Acumulador incremental del gasto estimado del mes, en USD. Una fila por (anio, mes); un mes sin fila vale cero. Se incrementa una vez por turno al cobrarlo, con el precio vigente en ese momento.';

-- ------------------------------------------------------------- tabla_de_precios
--
-- Precio por token de entrada/salida/caché, versionado por rango de vigencia
-- (design.md D6). Publicar un precio nuevo cierra `vigente_hasta` de la fila
-- anterior y abre una nueva — la historia nunca se edita en el lugar, así que
-- un turno ya ocurrido siempre se costea con el precio que estaba vigente
-- cuando ocurrió, sin que un cambio de precio posterior reescriba esa
-- estimación (D1/A1).
--
-- `version` es informativa (para mostrar en el panel qué versión de precio
-- costeó cada fila), no la clave de vigencia: la clave de vigencia es el
-- rango `[vigente_desde, vigente_hasta)`.
CREATE TABLE IF NOT EXISTS asistente.tabla_de_precios (
    id                        bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    proveedor                 text          NOT NULL,
    modelo                    text          NOT NULL,
    precio_por_token_entrada  numeric(18,12) NOT NULL,
    precio_por_token_salida   numeric(18,12) NOT NULL,
    precio_por_token_cache    numeric(18,12) NOT NULL,
    version                   integer       NOT NULL,
    vigente_desde             timestamptz   NOT NULL,
    vigente_hasta             timestamptz   NULL
);

COMMENT ON TABLE asistente.tabla_de_precios IS
    'Precio por token de entrada/salida/caché, por proveedor y modelo, versionado por rango [vigente_desde, vigente_hasta). Nunca se edita en el lugar: un precio nuevo cierra la fila anterior y abre una nueva, para no reescribir el costeo de turnos ya ocurridos. Sin fila para un (proveedor, modelo): esas filas se reportan como "sin precio", nunca como costo cero.';

CREATE INDEX IF NOT EXISTS ix_tabla_de_precios_proveedor_modelo_vigencia
    ON asistente.tabla_de_precios (proveedor, modelo, vigente_desde);

-- No se siembra ninguna fila: un precio adivinado es peor que costear "sin
-- precio" hasta que el Departamento cargue los valores reales (mismo
-- criterio del cupo y el tope en cero).

-- --------------------------------------------------------------- modo_mantenimiento
--
-- Interruptor de mantenimiento, fila única (design.md D7). `id` fijo en 1 con
-- un CHECK es el patrón estándar de "tabla de una sola fila": impide un
-- segundo INSERT sin necesitar un trigger.
CREATE TABLE IF NOT EXISTS asistente.modo_mantenimiento (
    id             smallint    PRIMARY KEY DEFAULT 1,
    activo         boolean     NOT NULL,
    razon          text        NULL,
    actor_id       uuid        NULL,
    actualizado_en timestamptz NOT NULL,
    CONSTRAINT modo_mantenimiento_fila_unica CHECK (id = 1)
);

COMMENT ON TABLE asistente.modo_mantenimiento IS
    'Interruptor de mantenimiento del asistente, fila única. activo=false por default. razon/actor_id nulos cuando nunca se activó.';

INSERT INTO asistente.modo_mantenimiento (id, activo, razon, actor_id, actualizado_en)
VALUES (1, false, NULL, NULL, now())
ON CONFLICT (id) DO NOTHING;

-- ----------------------------------------------------------- auditoria_administracion
--
-- Append-only: cada edición de presupuesto (rol o usuario), del tope
-- organizacional, y cada toggle del modo mantenimiento (design.md D11, tareas
-- 6.4/8.1). Mismo criterio que auditoria_acceso_historial (005): "append-only,
-- no borrable" se garantiza no escribiendo el código que actualizaría o
-- borraría una fila, no con un trigger de base — hay un test que enumera las
-- rutas del módulo y falla si alguna ofrece esa forma contra esta tabla.
--
-- `antes`/`despues` son texto (JSON serializado por la aplicación) y no
-- columnas tipadas: la forma de "antes" y "después" difiere según la acción
-- (cupo de rol, override de usuario, tope organizacional, mantenimiento), y
-- una fila de auditoría no necesita esa forma para ser consultable — se
-- consulta por actor y por momento, no por campo del valor editado.
CREATE TABLE IF NOT EXISTS asistente.auditoria_administracion (
    id          bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    actor_id    uuid        NOT NULL,
    ocurrido_en timestamptz NOT NULL,
    accion      text        NOT NULL,
    antes       text        NULL,
    despues     text        NULL
);

COMMENT ON TABLE asistente.auditoria_administracion IS
    'Append-only: cada edición de presupuesto (rol/usuario), del tope organizacional, y cada toggle de mantenimiento. antes/despues son JSON serializado por la aplicación, de forma variable según accion. Retención propia (default 365 días) vía OpcionesAsistente.RetencionDeAuditoriaDeAdministracionDias, purgada por PurgaDeRegistros.';

CREATE INDEX IF NOT EXISTS ix_auditoria_administracion_ocurrido_en
    ON asistente.auditoria_administracion (ocurrido_en);

-- --------------------------------------------- el asistente no lee esta administración
--
-- Igual que 004/005: no hace falta ningún REVOKE nuevo. Las siete tablas
-- viven en el schema `asistente`, ya denegado por completo a los dos roles
-- de solo lectura desde 002_asistente_registros.sql (REVOKE ALL ON SCHEMA).
-- Hay tests (ManifiestoPrivilegiosTests/PrivilegiosLecturaTests) que verifican
-- que ninguna de las siete quede alcanzable sin cambiar el manifiesto.
