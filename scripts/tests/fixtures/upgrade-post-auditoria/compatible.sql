-- Exclusivamente sintético. Se aplica una sola vez sobre el runner real f10e0a5.
BEGIN;
INSERT INTO identity.personas(id,documento,nombre,apellido) VALUES
('90000000-0000-4000-8000-000000000001','TEST-UPGRADE-1','Persona','Sintética');
INSERT INTO identity.users(id,upn,display_name,persona_id,azure_oid,azure_tid) VALUES
('90000000-0000-4000-8000-000000000002','upgrade@example.test','Cuenta sintética','90000000-0000-4000-8000-000000000001','90000000-0000-4000-8000-000000000003','90000000-0000-4000-8000-000000000004');
INSERT INTO identity.roles(id,code,name,scope,es_sistema,is_active) VALUES
('90000000-0000-4000-8000-000000000005','upgrade_sintetico','Rol sintético upgrade','global',false,true);
INSERT INTO identity.user_roles(id,user_id,role_id) VALUES
('90000000-0000-4000-8000-000000000006','90000000-0000-4000-8000-000000000002','90000000-0000-4000-8000-000000000005');
INSERT INTO identity.rol_permisos(rol_id,permiso_id)
SELECT '90000000-0000-4000-8000-000000000005',id FROM identity.permisos ORDER BY code LIMIT 2;
INSERT INTO identity.carreras(id,code,name) VALUES ('90000000-0000-4000-8000-000000000007','UPG','Carrera sintética');
INSERT INTO identity.materias(id,code,name) VALUES ('90000000-0000-4000-8000-000000000008','99999','Materia sintética');
INSERT INTO designaciones.periodos(id,nombre,carga_desde,carga_hasta,impacto_desde,impacto_hasta)
VALUES ('90000000-0000-4000-8000-000000000009','Período sintético','2026-01-01','2026-02-01','2026-03-01','2026-12-31');
INSERT INTO designaciones.pedidos(id,numero,periodo_id,persona_id,materia_id,carrera_id,novedad,estado,cargo_solicitado_id,dedicacion_solicitada_id,horas,snapshot)
SELECT '90000000-0000-4000-8000-000000000010','2026-8001','90000000-0000-4000-8000-000000000009','90000000-0000-4000-8000-000000000001','90000000-0000-4000-8000-000000000008','90000000-0000-4000-8000-000000000007','Alta','en_revision_coordinador',
(SELECT id FROM designaciones.cargos ORDER BY codigo LIMIT 1),(SELECT id FROM designaciones.dedicaciones ORDER BY codigo LIMIT 1),8,'{"fixture":"snapshot legacy inmutable","horas":8}'::jsonb;
INSERT INTO designaciones.designaciones(id,persona_id,materia_id,carrera_id,cargo_id,dedicacion_id,horas,vigente_desde,origen_pedido_id)
SELECT '90000000-0000-4000-8000-000000000011','90000000-0000-4000-8000-000000000001','90000000-0000-4000-8000-000000000008','90000000-0000-4000-8000-000000000007',
(SELECT id FROM designaciones.cargos ORDER BY codigo LIMIT 1),(SELECT id FROM designaciones.dedicaciones ORDER BY codigo LIMIT 1),8,'2026-03-01','90000000-0000-4000-8000-000000000010';
INSERT INTO storage.archivos(id,proposito,ambiente,bucket,clave_objeto,nombre_original,mime_declarado,mime_detectado,tamano_bytes,sha256,estado,propietario_id,expira_en,confirmado_at,revisado_at)
VALUES ('90000000-0000-4000-8000-000000000012','cv','staging','arsdocendi-staging','upgrade/legacy.pdf','sintetico.pdf','application/pdf','application/pdf',:tamano,:'hash','disponible','90000000-0000-4000-8000-000000000002','2030-01-01',now(),now());
INSERT INTO portal.perfiles(id,persona_id) VALUES ('90000000-0000-4000-8000-000000000013','90000000-0000-4000-8000-000000000001');
INSERT INTO portal.cvs(id,perfil_id,nombre,archivo_id) VALUES ('90000000-0000-4000-8000-000000000014','90000000-0000-4000-8000-000000000013','sintetico.pdf','90000000-0000-4000-8000-000000000012');
INSERT INTO designaciones.pedido_adjuntos(id,pedido_id,tipo,nombre,archivo_id) VALUES ('90000000-0000-4000-8000-000000000015','90000000-0000-4000-8000-000000000010','cv','sintetico.pdf','90000000-0000-4000-8000-000000000012');
INSERT INTO tareas.tareas(id,titulo,fecha_inicio,fecha_fin,prioridad_id,tipo_id,estado_id,responsable_id,creado_por_id)
SELECT id,titulo,'2026-01-01','2026-12-31',
(SELECT id FROM tareas.prioridades WHERE codigo='media'),(SELECT id FROM tareas.tipos_tarea WHERE codigo='administrativa'),(SELECT id FROM tareas.estados_tarea WHERE codigo='pendiente'),
'90000000-0000-4000-8000-000000000002','90000000-0000-4000-8000-000000000002'
FROM (VALUES ('90000000-0000-4000-8000-000000000016'::uuid,'Raíz sintética'),('90000000-0000-4000-8000-000000000017'::uuid,'Hija sintética')) AS filas(id,titulo);
UPDATE tareas.tareas SET tarea_padre_id='90000000-0000-4000-8000-000000000016' WHERE id='90000000-0000-4000-8000-000000000017';
UPDATE identity.users SET is_active=false WHERE id='90000000-0000-4000-8000-000000000002';
COMMIT;
