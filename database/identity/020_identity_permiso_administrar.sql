-- Permiso de administrar el uso del asistente conversacional.
--
-- POR QUÉ NO SE REUSÓ NINGÚN PERMISO EXISTENTE
-- `asistente.consultar` admite al asistente; `asistente.ver_consulta` muestra
-- la SQL de la consulta propia; `asistente.leer_historial_ajeno` habilita
-- soporte sobre el historial de otro usuario. Ninguno gobierna presupuestos,
-- el tope organizacional o el modo mantenimiento — administrar el uso del
-- módulo es una cuarta decisión distinta, y conflarla con cualquiera de las
-- otras tres haría que, por ejemplo, admitir a alguien al asistente le
-- abriera sin querer el kill switch.
--
-- POR QUÉ ESTE SÍ SE SIEMBRA A sys_admin, A DIFERENCIA DE LOS OTROS DOS
-- (design.md D13 de asistente-administracion-de-uso)
-- `asistente.ver_consulta` y `asistente.leer_historial_ajeno` se sembraron
-- vacíos porque son superficie de DIAGNÓSTICO/SOPORTE sobre datos que pueden
-- llevar información personal de otra persona: quién debe verla es una
-- decisión del Departamento, tomada desde /membresia-roles caso por caso.
-- `asistente.administrar` es distinto: gobierna presupuestos, el tope de
-- gasto organizacional y el apagado del módulo — control operativo sobre la
-- disponibilidad y el gasto del sistema entero, no sobre datos personales de
-- terceros. `sys_admin` es exactamente el rol que este sistema ya reserva
-- para ese tipo de control cruzado (es NOINHERIT y no concede nada por sí
-- solo hasta que se lo asigna a una persona), así que sembrarlo ahí es seguir
-- el mismo patrón que las migraciones 011/014 documentan como modelo — vía
-- asignación de rol en vez de una concesión posterior desde
-- /membresia-roles.
--
-- POR QUÉ sys_admin NECESITA UN INSERT EXPLÍCITO
-- `sys_admin` NO hereda permisos nuevos: su matriz se sembró en la migración
-- 008 con `ARRAY(SELECT code FROM identity.permisos)`, evaluado en el momento
-- en que esa migración corrió. Un permiso agregado después no le llega solo
-- (la migración 010 es la prueba de que el repositorio ya tropezó con esto).
--
-- Idempotente: los dos INSERT llevan ON CONFLICT DO NOTHING.

INSERT INTO identity.permisos (id, code, nombre, descripcion) VALUES
    ('b2000000-0000-4000-8000-000000000029', 'asistente.administrar', 'Administrar el uso del asistente', 'Administrar presupuestos de uso del asistente (por rol y por usuario), el tope organizacional de gasto, el modo mantenimiento y el panel de uso. No habilita leer el historial de conversaciones de otro usuario ni ver la consulta SQL generada.')
ON CONFLICT (code) DO NOTHING;

INSERT INTO identity.rol_permisos (rol_id, permiso_id)
SELECT r.id, p.id
  FROM identity.roles r
  CROSS JOIN identity.permisos p
 WHERE r.code = 'sys_admin'
   AND p.code = 'asistente.administrar'
ON CONFLICT (rol_id, permiso_id) DO NOTHING;

-- Guarda: a diferencia de 014/019 (donde la guarda espera CERO concesiones),
-- acá el default esperado es exactamente UNA — sys_admin — y ninguna otra.
-- Si algún rol distinto de sys_admin ya tiene este permiso, no es un error de
-- esta migración (no lo tocó), pero conviene que quede visible en el log: es
-- la misma disciplina de "romper a la vista" que 011/014 aplican con
-- RAISE EXCEPTION, atenuada a RAISE NOTICE porque acá no hay una lista
-- cerrada de roles de sistema que decidir uno por uno, sólo un default a
-- verificar.
DO $administrar$
DECLARE
    otros_roles TEXT[];
BEGIN
    SELECT array_agg(r.code ORDER BY r.code) INTO otros_roles
      FROM identity.rol_permisos rp
      JOIN identity.permisos p ON p.id = rp.permiso_id
      JOIN identity.roles r ON r.id = rp.rol_id
     WHERE p.code = 'asistente.administrar'
       AND r.code <> 'sys_admin';

    IF otros_roles IS NOT NULL THEN
        RAISE NOTICE
            'asistente.administrar ya está concedido a rol(es) distintos de sys_admin: %. Esta migración no los toca.',
            otros_roles;
    END IF;
END
$administrar$;
