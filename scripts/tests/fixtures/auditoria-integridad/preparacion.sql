-- Datos exclusivamente sintéticos; ejecutar solo en el PostgreSQL creado por el runner.
INSERT INTO identity.personas (id, documento, nombre, apellido)
VALUES ('10000000-0000-4000-8000-000000000001', 'PROBE-001', 'Persona', 'Sintetica');
INSERT INTO identity.users (id, upn, display_name)
VALUES ('10000000-0000-4000-8000-000000000002', 'probe@example.invalid', 'Cuenta sintetica');
INSERT INTO identity.carreras (id, code, name)
VALUES ('10000000-0000-4000-8000-000000000003', 'PROBE', 'Carrera sintetica');
INSERT INTO identity.materias (id, code, name)
VALUES ('10000000-0000-4000-8000-000000000004', '99001', 'Materia sintetica');
INSERT INTO identity.roles (id, code, name, scope)
VALUES ('10000000-0000-4000-8000-000000000005', 'probe', 'Rol sintetico', 'global');
INSERT INTO identity.user_roles (user_id, role_id)
VALUES ('10000000-0000-4000-8000-000000000002', '10000000-0000-4000-8000-000000000005');
INSERT INTO designaciones.periodos (id, nombre, carga_desde, carga_hasta, impacto_desde, impacto_hasta)
VALUES ('10000000-0000-4000-8000-000000000006', 'Periodo sintetico', '2026-01-01', '2026-12-31', '2026-01-01', '2026-12-31');
INSERT INTO portal.perfiles (id, persona_id)
VALUES ('10000000-0000-4000-8000-000000000007', '10000000-0000-4000-8000-000000000001');
INSERT INTO tareas.tareas (id, titulo, fecha_inicio, fecha_fin, prioridad_id, tipo_id, estado_id, responsable_id, creado_por_id)
SELECT '10000000-0000-4000-8000-000000000008', 'Tarea sintetica', '2026-01-01', '2026-12-31',
       (SELECT id FROM tareas.prioridades WHERE codigo = 'media'),
       (SELECT id FROM tareas.tipos_tarea WHERE codigo = 'administrativa'),
       (SELECT id FROM tareas.estados_tarea WHERE codigo = 'pendiente'),
       '10000000-0000-4000-8000-000000000002', '10000000-0000-4000-8000-000000000002';
INSERT INTO designaciones.designaciones (id, persona_id, materia_id, carrera_id, cargo_id, dedicacion_id, horas, vigente_desde)
SELECT '10000000-0000-4000-8000-000000000009', '10000000-0000-4000-8000-000000000001',
       '10000000-0000-4000-8000-000000000004', '10000000-0000-4000-8000-000000000003',
       (SELECT id FROM designaciones.cargos WHERE activo ORDER BY id LIMIT 1),
       (SELECT id FROM designaciones.dedicaciones WHERE activo ORDER BY id LIMIT 1), 10, '2026-10-13';
