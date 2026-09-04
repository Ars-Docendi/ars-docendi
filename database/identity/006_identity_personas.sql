-- identity.personas
-- Entidad canónica de un ser humano dentro del sistema. Existe con o sin cuenta
-- de Azure AD: un pedido de designación de novedad "Alta" refiere a un docente
-- que nunca se autenticó y que todavía no tiene legajo asignado.
--
-- Por eso `legajo` es NULL-able (BR-designaciones-018 exime al Alta de tenerlo;
-- Baja y Cambio lo exigen, y esa validación vive en el backend, no acá).
-- `documento` es la clave natural: es lo único que siempre está presente.
--
-- PII: esta tabla concentra los datos personales del sistema (documento, CUIL,
-- teléfono, fecha de nacimiento). Ver la sección de consideraciones PII en
-- docs/architecture/data-model.md.

CREATE TABLE identity.personas (
    id                UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    documento         TEXT         NOT NULL UNIQUE,
    cuil              TEXT         NULL,
    legajo            TEXT         NULL UNIQUE,
    nombre            TEXT         NOT NULL,
    apellido          TEXT         NOT NULL,
    fecha_nacimiento  DATE         NULL,
    telefono          TEXT         NULL,
    created_at        TIMESTAMPTZ  NOT NULL DEFAULT now()
);

-- Búsqueda por apellido/nombre en las pantallas de administración.
CREATE INDEX personas_apellido_nombre_idx
    ON identity.personas (apellido, nombre);

-- La cuenta apunta a la persona, no al revés: una persona puede no tener cuenta,
-- y sólo se entera de cuál es al primer login (donde se resuelve por documento/upn).
ALTER TABLE identity.users
    ADD COLUMN persona_id UUID NULL REFERENCES identity.personas(id) ON DELETE RESTRICT;

-- Una persona tiene a lo sumo una cuenta. Parcial para no colisionar entre las
-- filas que todavía no fueron vinculadas.
CREATE UNIQUE INDEX users_persona_unica
    ON identity.users (persona_id)
    WHERE persona_id IS NOT NULL;

SELECT audit.attach('identity.personas');
