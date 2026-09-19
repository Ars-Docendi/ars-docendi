-- Permiso de admisión al asistente conversacional.
--
-- POR QUÉ UN PERMISO PERSISTIDO Y NO UNA LISTA DE ROLES EN CÓDIGO
-- La matriz rol -> permiso se edita desde /membresia-roles sin desplegar, y
-- identity.roles no es un catálogo cerrado: Secretaría puede crear roles nuevos.
-- Una lista de roles embebida en el backend falla ABIERTA con cualquier rol que
-- no conozca, y falla en silencio.
--
-- POR QUÉ NO SE REUSÓ `designaciones.ver`
-- Se descartó una política compuesta sobre ese permiso: con eso, quitarle el
-- asistente a alguien significaría también quitarle ver designaciones. Dos
-- decisiones distintas necesitan dos interruptores distintos.
--
-- SIEMBRA EXPLÍCITA PARA LOS SIETE ROLES DE SISTEMA
-- `sys_admin` NO hereda permisos nuevos. Su matriz se sembró en la migración 008
-- con `ARRAY(SELECT code FROM identity.permisos)`, evaluado en el momento en que
-- esa migración corrió: un permiso agregado después no le llega. La existencia de
-- la migración 010 es la prueba de que el repositorio ya tropezó con esto.
--
-- Idempotente: los dos INSERT llevan ON CONFLICT DO NOTHING.
--
-- EL ID ES ...024 Y NO ...021, Y CONVIENE DECIR POR QUÉ. Las dos ramas partieron
-- del mismo `007_identity_permisos.sql`, que llegaba hasta ...020, y cada una tomó
-- ...021 como el siguiente libre: acá para `asistente.consultar`, y en
-- `database-schema` para `designaciones.revisar`, que quedó en la migración
-- fundacional 007. Al integrar, la clave primaria de `identity.permisos` rechazó
-- el segundo INSERT y 555 tests se cayeron con 23505.
--
-- Cede este permiso porque el otro vive en 007, que es donde se declara el
-- catálogo base. Asignar UUIDs a mano en ramas paralelas produce exactamente esto;
-- el siguiente que agregue un permiso conviene que mire el máximo en TODAS las
-- migraciones de identity, no sólo en la suya.

INSERT INTO identity.permisos (id, code, nombre, descripcion) VALUES
    ('b2000000-0000-4000-8000-000000000024', 'asistente.consultar', 'Consultar el asistente', 'Hacer preguntas en lenguaje natural al asistente conversacional. El asistente responde solo con datos que el usuario ya puede ver.')
ON CONFLICT (code) DO NOTHING;

-- Guarda: si mañana aparece un rol de sistema que esta migración no contempla,
-- rompe acá en vez de dejarlo sin decisión tomada. Es el mismo modo de falla que
-- la trampa de sys_admin, y el que hace que "los siete" signifique algo.
DO $asistente_permiso$
DECLARE
    sin_decision TEXT[];
BEGIN
    SELECT array_agg(code ORDER BY code) INTO sin_decision
      FROM identity.roles
     WHERE es_sistema
       AND code <> ALL (ARRAY[
           'docente',
           'jefe_catedra',
           'coordinador_carrera',
           'secretaria',
           'decanato',
           'administrativo',
           'sys_admin']);

    IF sin_decision IS NOT NULL THEN
        RAISE EXCEPTION
            'Hay roles de sistema sin decisión sobre asistente.consultar: %. Agregalos a esta migración, concediendo o denegando explícitamente.',
            sin_decision;
    END IF;
END
$asistente_permiso$;

-- Los seis roles no `docente`. La exclusión de `docente` es provisional y se
-- revierte desde /membresia-roles, sin migración, cuando exista el portal docente.
INSERT INTO identity.rol_permisos (rol_id, permiso_id)
SELECT r.id, p.id
  FROM identity.roles r
 CROSS JOIN identity.permisos p
 WHERE p.code = 'asistente.consultar'
   AND r.code IN (
       'jefe_catedra',
       'coordinador_carrera',
       'secretaria',
       'decanato',
       'administrativo',
       'sys_admin')
ON CONFLICT (rol_id, permiso_id) DO NOTHING;
