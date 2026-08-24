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

INSERT INTO identity.permisos (id, code, nombre, descripcion) VALUES
    ('b2000000-0000-4000-8000-000000000021', 'asistente.consultar', 'Consultar el asistente', 'Hacer preguntas en lenguaje natural al asistente conversacional. El asistente responde solo con datos que el usuario ya puede ver.')
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
