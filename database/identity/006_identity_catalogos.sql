-- Baseline consolidado: identity/006_identity_catalogos.sql
-- DDL final; no contiene transiciones ni backfills de alpha.

INSERT INTO identity.permisos (id, code, nombre, descripcion, created_at) VALUES ('b2000000-0000-4000-8000-000000000001', 'designaciones.ver', 'Ver designaciones', 'Consultar el estado y detalle de designaciones sin modificarlas.', now());

INSERT INTO identity.permisos (id, code, nombre, descripcion, created_at) VALUES ('b2000000-0000-4000-8000-000000000002', 'designaciones.gestionar', 'Gestionar designaciones', 'Crear y editar proyectos docentes e iniciar el flujo de designación.', now());

INSERT INTO identity.permisos (id, code, nombre, descripcion, created_at) VALUES ('b2000000-0000-4000-8000-000000000003', 'designaciones.aprobar_coordinacion', 'Aprobar designaciones — Coordinación', 'Aprobar o rechazar designaciones en la instancia de coordinación de carrera.', now());

INSERT INTO identity.permisos (id, code, nombre, descripcion, created_at) VALUES ('b2000000-0000-4000-8000-000000000004', 'designaciones.aprobar_secretaria', 'Aprobar designaciones — Secretaría', 'Aprobar o rechazar designaciones en la instancia de secretaría académica.', now());

INSERT INTO identity.permisos (id, code, nombre, descripcion, created_at) VALUES ('b2000000-0000-4000-8000-000000000005', 'designaciones.aprobar_decanato', 'Aprobar designaciones — Decanato', 'Aprobar o rechazar designaciones en la instancia final del decanato.', now());

INSERT INTO identity.permisos (id, code, nombre, descripcion, created_at) VALUES ('b2000000-0000-4000-8000-000000000006', 'aulas.ver', 'Ver reservas de aulas', 'Consultar el calendario de reservas de aulas y laboratorios.', now());

INSERT INTO identity.permisos (id, code, nombre, descripcion, created_at) VALUES ('b2000000-0000-4000-8000-000000000007', 'aulas.gestionar', 'Gestionar reservas de aulas', 'Solicitar y asignar aulas o laboratorios para mesas de examen.', now());

INSERT INTO identity.permisos (id, code, nombre, descripcion, created_at) VALUES ('b2000000-0000-4000-8000-000000000008', 'aulas.aprobar', 'Aprobar reservas de aulas', 'Confirmar o rechazar pedidos de reserva realizados por administrativos.', now());

INSERT INTO identity.permisos (id, code, nombre, descripcion, created_at) VALUES ('b2000000-0000-4000-8000-000000000009', 'usuarios.ver', 'Ver usuarios', 'Consultar el listado de usuarios registrados en el sistema.', now());

INSERT INTO identity.permisos (id, code, nombre, descripcion, created_at) VALUES ('b2000000-0000-4000-8000-000000000010', 'usuarios.administrar', 'Administrar usuarios', 'Crear, editar, activar y desactivar cuentas de usuario del sistema.', now());

INSERT INTO identity.permisos (id, code, nombre, descripcion, created_at) VALUES ('b2000000-0000-4000-8000-000000000011', 'roles.ver', 'Ver roles', 'Consultar el listado de roles y sus descripciones.', now());

INSERT INTO identity.permisos (id, code, nombre, descripcion, created_at) VALUES ('b2000000-0000-4000-8000-000000000012', 'roles.administrar', 'Administrar roles', 'Crear y modificar roles del sistema.', now());

INSERT INTO identity.permisos (id, code, nombre, descripcion, created_at) VALUES ('b2000000-0000-4000-8000-000000000013', 'roles.gestionar_membresia', 'Gestionar membresía de roles', 'Asignar y revocar permisos a cada rol.', now());

INSERT INTO identity.permisos (id, code, nombre, descripcion, created_at) VALUES ('b2000000-0000-4000-8000-000000000014', 'periodos.administrar', 'Administrar períodos', 'Gestionar los períodos académicos habilitados para designaciones y reservas.', now());

INSERT INTO identity.permisos (id, code, nombre, descripcion, created_at) VALUES ('b2000000-0000-4000-8000-000000000015', 'sistema.parametrizar', 'Parametrizar sistema', 'Configurar parámetros generales (umbrales, textos, fechas de corte).', now());

INSERT INTO identity.permisos (id, code, nombre, descripcion, created_at) VALUES ('b2000000-0000-4000-8000-000000000016', 'tareas.ver', 'Ver tareas', 'Consultar el tablero de tareas internas del departamento.', now());

INSERT INTO identity.permisos (id, code, nombre, descripcion, created_at) VALUES ('b2000000-0000-4000-8000-000000000017', 'tareas.gestionar', 'Gestionar tareas', 'Crear, editar, asignar y cerrar tareas internas del departamento.', now());

INSERT INTO identity.permisos (id, code, nombre, descripcion, created_at) VALUES ('b2000000-0000-4000-8000-000000000018', 'portal.ver', 'Ver portal personal', 'Acceder al portal propio con datos personales y horas disponibles.', now());

INSERT INTO identity.permisos (id, code, nombre, descripcion, created_at) VALUES ('b2000000-0000-4000-8000-000000000019', 'portal.editar', 'Editar portal personal', 'Actualizar datos personales, horas disponibles y áreas de experticia.', now());

INSERT INTO identity.permisos (id, code, nombre, descripcion, created_at) VALUES ('b2000000-0000-4000-8000-000000000020', 'reportes.ver', 'Ver reportes globales', 'Acceder a reportes consolidados de designaciones, aulas y actividad docente.', now());

INSERT INTO identity.permisos (id, code, nombre, descripcion, created_at) VALUES ('b2000000-0000-4000-8000-000000000021', 'designaciones.revisar', 'Revisar designaciones', 'Consultar y revisar pedidos según la etapa y el ámbito del actor.', now());

INSERT INTO identity.permisos (id, code, nombre, descripcion, created_at) VALUES ('b2000000-0000-4000-8000-000000000022', 'docentes.ver', 'Ver docentes', 'Consultar docentes según el ámbito autorizado del actor.', now());

INSERT INTO identity.permisos (id, code, nombre, descripcion, created_at) VALUES ('b2000000-0000-4000-8000-000000000023', 'sistema.estado.ver', 'Ver estado del sistema', 'Consultar el estado actual de los módulos API y la conectividad de PostgreSQL.', now());

INSERT INTO identity.permisos (id, code, nombre, descripcion, created_at) VALUES ('b2000000-0000-4000-8000-000000000024', 'auditoria.ver', 'Consultar auditoría', 'Consultar eventos de auditoría en modo de solo lectura.', now());

INSERT INTO identity.roles (id, code, name, description, scope, es_sistema, is_active, created_at) VALUES ('a1000000-0000-4000-8000-000000000001', 'docente', 'Docente', NULL, 'materia', true, true, now());

INSERT INTO identity.roles (id, code, name, description, scope, es_sistema, is_active, created_at) VALUES ('a1000000-0000-4000-8000-000000000002', 'jefe_catedra', 'Jefe de Cátedra', NULL, 'materia', true, true, now());

INSERT INTO identity.roles (id, code, name, description, scope, es_sistema, is_active, created_at) VALUES ('a1000000-0000-4000-8000-000000000003', 'coordinador_carrera', 'Coordinador de Carrera', NULL, 'carrera', true, true, now());

INSERT INTO identity.roles (id, code, name, description, scope, es_sistema, is_active, created_at) VALUES ('a1000000-0000-4000-8000-000000000004', 'secretaria', 'Secretaría Académica', NULL, 'global', true, true, now());

INSERT INTO identity.roles (id, code, name, description, scope, es_sistema, is_active, created_at) VALUES ('a1000000-0000-4000-8000-000000000005', 'decanato', 'Decanato', NULL, 'global', true, true, now());

INSERT INTO identity.roles (id, code, name, description, scope, es_sistema, is_active, created_at) VALUES ('a1000000-0000-4000-8000-000000000006', 'administrativo', 'Administrativo', NULL, 'global', true, true, now());

INSERT INTO identity.roles (id, code, name, description, scope, es_sistema, is_active, created_at) VALUES ('a1000000-0000-4000-8000-000000000007', 'sys_admin', 'Administrador de Sistemas', NULL, 'global', true, true, now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000001', 'b2000000-0000-4000-8000-000000000019', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000001', 'b2000000-0000-4000-8000-000000000018', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000002', 'b2000000-0000-4000-8000-000000000006', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000002', 'b2000000-0000-4000-8000-000000000002', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000002', 'b2000000-0000-4000-8000-000000000001', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000002', 'b2000000-0000-4000-8000-000000000022', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000002', 'b2000000-0000-4000-8000-000000000019', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000002', 'b2000000-0000-4000-8000-000000000018', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000003', 'b2000000-0000-4000-8000-000000000006', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000003', 'b2000000-0000-4000-8000-000000000003', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000003', 'b2000000-0000-4000-8000-000000000021', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000003', 'b2000000-0000-4000-8000-000000000001', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000003', 'b2000000-0000-4000-8000-000000000019', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000003', 'b2000000-0000-4000-8000-000000000018', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000003', 'b2000000-0000-4000-8000-000000000020', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000004', 'b2000000-0000-4000-8000-000000000008', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000004', 'b2000000-0000-4000-8000-000000000007', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000004', 'b2000000-0000-4000-8000-000000000006', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000004', 'b2000000-0000-4000-8000-000000000004', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000004', 'b2000000-0000-4000-8000-000000000021', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000004', 'b2000000-0000-4000-8000-000000000001', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000004', 'b2000000-0000-4000-8000-000000000022', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000004', 'b2000000-0000-4000-8000-000000000014', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000004', 'b2000000-0000-4000-8000-000000000019', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000004', 'b2000000-0000-4000-8000-000000000018', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000004', 'b2000000-0000-4000-8000-000000000020', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000004', 'b2000000-0000-4000-8000-000000000012', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000004', 'b2000000-0000-4000-8000-000000000013', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000004', 'b2000000-0000-4000-8000-000000000011', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000004', 'b2000000-0000-4000-8000-000000000015', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000004', 'b2000000-0000-4000-8000-000000000017', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000004', 'b2000000-0000-4000-8000-000000000016', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000004', 'b2000000-0000-4000-8000-000000000010', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000004', 'b2000000-0000-4000-8000-000000000009', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000005', 'b2000000-0000-4000-8000-000000000005', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000005', 'b2000000-0000-4000-8000-000000000021', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000005', 'b2000000-0000-4000-8000-000000000001', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000005', 'b2000000-0000-4000-8000-000000000019', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000005', 'b2000000-0000-4000-8000-000000000018', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000005', 'b2000000-0000-4000-8000-000000000020', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000006', 'b2000000-0000-4000-8000-000000000008', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000006', 'b2000000-0000-4000-8000-000000000007', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000006', 'b2000000-0000-4000-8000-000000000006', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000006', 'b2000000-0000-4000-8000-000000000021', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000006', 'b2000000-0000-4000-8000-000000000001', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000006', 'b2000000-0000-4000-8000-000000000022', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000006', 'b2000000-0000-4000-8000-000000000019', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000006', 'b2000000-0000-4000-8000-000000000018', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000006', 'b2000000-0000-4000-8000-000000000017', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000006', 'b2000000-0000-4000-8000-000000000016', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000006', 'b2000000-0000-4000-8000-000000000010', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000006', 'b2000000-0000-4000-8000-000000000009', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000007', 'b2000000-0000-4000-8000-000000000008', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000007', 'b2000000-0000-4000-8000-000000000007', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000007', 'b2000000-0000-4000-8000-000000000006', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000007', 'b2000000-0000-4000-8000-000000000003', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000007', 'b2000000-0000-4000-8000-000000000005', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000007', 'b2000000-0000-4000-8000-000000000004', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000007', 'b2000000-0000-4000-8000-000000000002', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000007', 'b2000000-0000-4000-8000-000000000021', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000007', 'b2000000-0000-4000-8000-000000000001', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000007', 'b2000000-0000-4000-8000-000000000022', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000007', 'b2000000-0000-4000-8000-000000000014', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000007', 'b2000000-0000-4000-8000-000000000019', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000007', 'b2000000-0000-4000-8000-000000000018', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000007', 'b2000000-0000-4000-8000-000000000020', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000007', 'b2000000-0000-4000-8000-000000000012', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000007', 'b2000000-0000-4000-8000-000000000013', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000007', 'b2000000-0000-4000-8000-000000000011', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000007', 'b2000000-0000-4000-8000-000000000015', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000007', 'b2000000-0000-4000-8000-000000000017', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000007', 'b2000000-0000-4000-8000-000000000016', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000007', 'b2000000-0000-4000-8000-000000000010', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000007', 'b2000000-0000-4000-8000-000000000009', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000006', 'b2000000-0000-4000-8000-000000000011', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000006', 'b2000000-0000-4000-8000-000000000012', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000006', 'b2000000-0000-4000-8000-000000000013', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000007', 'b2000000-0000-4000-8000-000000000023', now());

INSERT INTO identity.rol_permisos (rol_id, permiso_id, created_at) VALUES ('a1000000-0000-4000-8000-000000000007', 'b2000000-0000-4000-8000-000000000024', now());
