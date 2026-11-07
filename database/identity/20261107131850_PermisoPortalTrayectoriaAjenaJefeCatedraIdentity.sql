-- Le concede `portal.ver_trayectoria_ajena` al rol de sistema `jefe_catedra`.
--
-- DECISIÓN DEL EQUIPO, 2026-11-07 (BR-portal-001 y BR-portal-002). Reemplaza el
-- criterio de `20261103000400_PermisoPortalTrayectoriaAjenaIdentity.sql`, que lo
-- dejaba concedido a nadie hasta una respuesta de Secretaría. Esa migración está
-- publicada y no se edita: su encabezado queda superado por este.
--
-- QUÉ CAMBIA, EXACTAMENTE
-- Un jefe de cátedra puede preguntarle al asistente por la formación, la
-- experiencia, las certificaciones, los proyectos y las habilidades de los
-- docentes con designación vigente en las materias que dirige. NADA MÁS: el
-- alcance lo sigue imponiendo `identity.asistente_alcanza_a`, no este permiso.
-- Los candidatos fuera de su cátedra siguen invisibles (limitación aceptada).
--
-- QUÉ NO CAMBIA
-- Ningún otro rol lo recibe. `docente` y `sys_admin` no lo tienen. Tampoco
-- cambia ninguna pantalla ni endpoint: sólo el asistente consume este permiso.
--
-- ROLLBACK
-- Una migración incremental que borre la fila de `identity.rol_permisos` de
-- `jefe_catedra`. Con eso el jefe vuelve a ver únicamente su propio perfil.
--
-- Idempotente: ON CONFLICT DO NOTHING.

INSERT INTO identity.rol_permisos (rol_id, permiso_id)
SELECT r.id, p.id
  FROM identity.roles r
  JOIN identity.permisos p ON p.code = 'portal.ver_trayectoria_ajena'
 WHERE r.es_sistema
   AND r.code = 'jefe_catedra'
ON CONFLICT (rol_id, permiso_id) DO NOTHING;
