-- Dataset sintético autoritativo para Development, staging y ambientes pr-N.
-- Todos los UUID de fixtures son estables y reservados. Este archivo nunca se
-- ejecuta en producción (la barrera está en seed.sh) y no contiene datos reales.

BEGIN;
SELECT pg_advisory_xact_lock(hashtextextended('arsdocendi:seed:sintetico', 0));

CREATE TABLE IF NOT EXISTS public.seed_metadata (
    clave  TEXT PRIMARY KEY,
    valor  TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS public.seed_identities (
    user_id          UUID PRIMARY KEY REFERENCES identity.users(id) ON DELETE CASCADE,
    dataset_version  TEXT NOT NULL,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT now()
);

INSERT INTO public.seed_metadata (clave, valor) VALUES
    ('origen_datos', 'sintetico'),
    ('dataset_version', '2026.08.1'),
    ('ambiente_permitido', 'no-productivo'),
    ('sembrado_en', now()::TEXT)
ON CONFLICT (clave) DO UPDATE SET valor = EXCLUDED.valor;

-- Carreras y materias: dos ámbitos permiten probar filtros y denegaciones.
INSERT INTO identity.carreras (id, code, name, is_active) VALUES
    ('c0000000-0000-4000-8000-000000000201', 'INF', 'Ingeniería en Informática', TRUE),
    ('c0000000-0000-4000-8000-000000000202', 'IND', 'Ingeniería Industrial', TRUE)
ON CONFLICT (id) DO UPDATE SET
    code = EXCLUDED.code, name = EXCLUDED.name, is_active = EXCLUDED.is_active;

INSERT INTO identity.materias (id, code, name, carrera_id, is_active) VALUES
    ('70000000-0000-4000-8000-000000000101', '03500', 'Ingeniería de Software', 'c0000000-0000-4000-8000-000000000201', TRUE),
    ('70000000-0000-4000-8000-000000000102', '03620', 'Algoritmos y Estructuras de Datos', 'c0000000-0000-4000-8000-000000000201', TRUE),
    ('70000000-0000-4000-8000-000000000103', '03710', 'Bases de Datos', 'c0000000-0000-4000-8000-000000000201', TRUE),
    ('70000000-0000-4000-8000-000000000104', '03800', 'Arquitectura de Computadoras', 'c0000000-0000-4000-8000-000000000201', TRUE),
    ('70000000-0000-4000-8000-000000000201', '04100', 'Organización Industrial', 'c0000000-0000-4000-8000-000000000202', TRUE),
    ('70000000-0000-4000-8000-000000000202', '04220', 'Investigación Operativa', 'c0000000-0000-4000-8000-000000000202', TRUE)
ON CONFLICT (id) DO UPDATE SET
    code = EXCLUDED.code, name = EXCLUDED.name,
    carrera_id = EXCLUDED.carrera_id, is_active = EXCLUDED.is_active;

-- Personas inventadas. La última no tiene cuenta para cubrir altas docentes.
INSERT INTO identity.personas
    (id, documento, cuil, legajo, nombre, apellido, fecha_nacimiento, telefono) VALUES
    ('d0000000-0000-4000-8000-000000000001', '28341567', '27-28341567-3', '0421', 'Carla', 'López', DATE '1980-03-14', '11-4000-0001'),
    ('d0000000-0000-4000-8000-000000000002', '22156789', '20-22156789-2', '0115', 'Gustavo', 'Ruiz', DATE '1975-07-22', '11-4000-0002'),
    ('d0000000-0000-4000-8000-000000000003', '31089234', '27-31089234-8', '0033', 'Marina', 'Díaz', DATE '1985-11-05', '11-4000-0003'),
    ('d0000000-0000-4000-8000-000000000004', '19876543', '27-19876543-6', '0007', 'Lucía', 'Fernández', DATE '1970-01-30', '11-4000-0004'),
    ('d0000000-0000-4000-8000-000000000005', '15432109', '20-15432109-4', '0002', 'Roberto', 'Sosa', DATE '1965-09-18', '11-4000-0005'),
    ('d0000000-0000-4000-8000-000000000006', '35678901', '27-35678901-9', '0058', 'Paula', 'Gómez', DATE '1992-06-11', '11-4000-0006'),
    ('d0000000-0000-4000-8000-000000000007', '26543210', '20-26543210-7', '0299', 'Ernesto', 'Vidal', DATE '1978-12-03', '11-4000-0007'),
    ('d0000000-0000-4000-8000-000000000008', '38901234', '27-38901234-1', '0387', 'Sofía', 'Peralta', DATE '1995-04-27', '11-4000-0008'),
    ('d0000000-0000-4000-8000-000000000009', '40000009', '20-40000009-1', '0909', 'Demo', 'Multirol', DATE '1990-09-09', '11-4000-0009'),
    ('d0000000-0000-4000-8000-000000000010', '40111010', '27-40111010-2', '1010', 'Valeria', 'Suárez', DATE '1991-10-10', '11-4000-0010'),
    ('d0000000-0000-4000-8000-000000000011', '40222011', '20-40222011-3', '1111', 'Pablo', 'Herrera', DATE '1988-11-11', '11-4000-0011'),
    ('d0000000-0000-4000-8000-000000000012', '40333012', '27-40333012-4', NULL, 'Brenda', 'Ortiz', DATE '1998-12-12', '11-4000-0012'),
    ('d0000000-0000-4000-8000-000000000013', '40444013', '20-40444013-5', '1313', 'Julián', 'Torres', DATE '1987-01-13', '11-4000-0013'),
    ('d0000000-0000-4000-8000-000000000014', '40555014', '27-40555014-6', '1414', 'Natalia', 'Castro', DATE '1989-02-14', '11-4000-0014'),
    ('d0000000-0000-4000-8000-000000000015', '40666015', '20-40666015-7', '1515', 'Martín', 'Acosta', DATE '1986-03-15', '11-4000-0015'),
    ('d0000000-0000-4000-8000-000000000016', '40777016', '27-40777016-8', '1616', 'Laura', 'Giménez', DATE '1993-04-16', '11-4000-0016')
ON CONFLICT (id) DO UPDATE SET
    documento = EXCLUDED.documento, cuil = EXCLUDED.cuil, legajo = EXCLUDED.legajo,
    nombre = EXCLUDED.nombre, apellido = EXCLUDED.apellido,
    fecha_nacimiento = EXCLUDED.fecha_nacimiento, telefono = EXCLUDED.telefono;

INSERT INTO identity.users
    (id, azure_oid, upn, display_name, is_active, persona_id) VALUES
    ('a0000000-0000-4000-8000-000000000001', 'a9000000-0000-4000-8000-000000000001', 'carla.lopez@unlam.edu.ar', 'Carla López', TRUE, 'd0000000-0000-4000-8000-000000000001'),
    ('a0000000-0000-4000-8000-000000000002', 'a9000000-0000-4000-8000-000000000002', 'gustavo.ruiz@unlam.edu.ar', 'Gustavo Ruiz', TRUE, 'd0000000-0000-4000-8000-000000000002'),
    ('a0000000-0000-4000-8000-000000000003', 'a9000000-0000-4000-8000-000000000003', 'marina.diaz@unlam.edu.ar', 'Marina Díaz', TRUE, 'd0000000-0000-4000-8000-000000000003'),
    ('a0000000-0000-4000-8000-000000000004', 'a9000000-0000-4000-8000-000000000004', 'secretaria.academica@unlam.edu.ar', 'Lucía Fernández', TRUE, 'd0000000-0000-4000-8000-000000000004'),
    ('a0000000-0000-4000-8000-000000000005', 'a9000000-0000-4000-8000-000000000005', 'decanato@unlam.edu.ar', 'Roberto Sosa', TRUE, 'd0000000-0000-4000-8000-000000000005'),
    ('a0000000-0000-4000-8000-000000000006', 'a9000000-0000-4000-8000-000000000006', 'administracion@unlam.edu.ar', 'Paula Gómez', TRUE, 'd0000000-0000-4000-8000-000000000006'),
    ('a0000000-0000-4000-8000-000000000007', 'a9000000-0000-4000-8000-000000000007', 'sistemas@unlam.edu.ar', 'Ernesto Vidal', TRUE, 'd0000000-0000-4000-8000-000000000007'),
    ('a0000000-0000-4000-8000-000000000008', 'a9000000-0000-4000-8000-000000000008', 'sofia.peralta@unlam.edu.ar', 'Sofía Peralta', FALSE, 'd0000000-0000-4000-8000-000000000008'),
    ('a0000000-0000-4000-8000-000000000009', 'a9000000-0000-4000-8000-000000000009', 'demo@unlam.edu.ar', 'Demo Multirol', TRUE, 'd0000000-0000-4000-8000-000000000009')
ON CONFLICT (id) DO UPDATE SET
    azure_oid = EXCLUDED.azure_oid, upn = EXCLUDED.upn,
    display_name = EXCLUDED.display_name, is_active = EXCLUDED.is_active,
    persona_id = EXCLUDED.persona_id;

INSERT INTO public.seed_identities (user_id, dataset_version)
SELECT id, '2026.08.1'
FROM identity.users
WHERE id BETWEEN 'a0000000-0000-4000-8000-000000000001'::UUID
             AND 'a0000000-0000-4000-8000-000000000009'::UUID
ON CONFLICT (user_id) DO UPDATE SET dataset_version = EXCLUDED.dataset_version;

-- Rol editable de ejemplo. Los siete roles de sistema y veinte permisos son DDL.
INSERT INTO identity.roles (id, code, name, description, scope, es_sistema, is_active) VALUES
    ('a1000000-0000-4000-8000-000000000101', 'observador_departamento', 'Observador del departamento', 'Rol sintético editable.', 'global', FALSE, TRUE)
ON CONFLICT (id) DO UPDATE SET
    code = EXCLUDED.code, name = EXCLUDED.name, description = EXCLUDED.description,
    scope = EXCLUDED.scope, is_active = EXCLUDED.is_active;

INSERT INTO identity.rol_permisos (rol_id, permiso_id) VALUES
    ('a1000000-0000-4000-8000-000000000101', 'b2000000-0000-4000-8000-000000000001'),
    ('a1000000-0000-4000-8000-000000000101', 'b2000000-0000-4000-8000-000000000009'),
    ('a1000000-0000-4000-8000-000000000101', 'b2000000-0000-4000-8000-000000000011')
ON CONFLICT (rol_id, permiso_id) DO NOTHING;

-- Asignaciones con los tres tipos de ámbito y un usuario multirol.
INSERT INTO identity.user_roles
    (id, user_id, role_id, materia_id, carrera_id, granted_by, deleted_at) VALUES
    ('e0000000-0000-4000-8000-000000000001', 'a0000000-0000-4000-8000-000000000001', 'a1000000-0000-4000-8000-000000000001', '70000000-0000-4000-8000-000000000101', 'c0000000-0000-4000-8000-000000000201', 'a0000000-0000-4000-8000-000000000007', NULL),
    ('e0000000-0000-4000-8000-000000000002', 'a0000000-0000-4000-8000-000000000002', 'a1000000-0000-4000-8000-000000000002', '70000000-0000-4000-8000-000000000101', 'c0000000-0000-4000-8000-000000000201', 'a0000000-0000-4000-8000-000000000007', NULL),
    ('e0000000-0000-4000-8000-000000000003', 'a0000000-0000-4000-8000-000000000003', 'a1000000-0000-4000-8000-000000000003', NULL, 'c0000000-0000-4000-8000-000000000201', 'a0000000-0000-4000-8000-000000000007', NULL),
    ('e0000000-0000-4000-8000-000000000004', 'a0000000-0000-4000-8000-000000000004', 'a1000000-0000-4000-8000-000000000004', NULL, NULL, 'a0000000-0000-4000-8000-000000000007', NULL),
    ('e0000000-0000-4000-8000-000000000005', 'a0000000-0000-4000-8000-000000000005', 'a1000000-0000-4000-8000-000000000005', NULL, NULL, 'a0000000-0000-4000-8000-000000000007', NULL),
    ('e0000000-0000-4000-8000-000000000006', 'a0000000-0000-4000-8000-000000000006', 'a1000000-0000-4000-8000-000000000006', NULL, NULL, 'a0000000-0000-4000-8000-000000000007', NULL),
    ('e0000000-0000-4000-8000-000000000007', 'a0000000-0000-4000-8000-000000000007', 'a1000000-0000-4000-8000-000000000007', NULL, NULL, 'a0000000-0000-4000-8000-000000000007', NULL),
    ('e0000000-0000-4000-8000-000000000008', 'a0000000-0000-4000-8000-000000000008', 'a1000000-0000-4000-8000-000000000001', '70000000-0000-4000-8000-000000000102', 'c0000000-0000-4000-8000-000000000201', 'a0000000-0000-4000-8000-000000000007', NULL),
    ('e0000000-0000-4000-8000-000000000009', 'a0000000-0000-4000-8000-000000000009', 'a1000000-0000-4000-8000-000000000001', '70000000-0000-4000-8000-000000000101', 'c0000000-0000-4000-8000-000000000201', 'a0000000-0000-4000-8000-000000000007', NULL),
    ('e0000000-0000-4000-8000-000000000010', 'a0000000-0000-4000-8000-000000000009', 'a1000000-0000-4000-8000-000000000002', '70000000-0000-4000-8000-000000000101', 'c0000000-0000-4000-8000-000000000201', 'a0000000-0000-4000-8000-000000000007', NULL),
    ('e0000000-0000-4000-8000-000000000011', 'a0000000-0000-4000-8000-000000000009', 'a1000000-0000-4000-8000-000000000002', '70000000-0000-4000-8000-000000000102', 'c0000000-0000-4000-8000-000000000201', 'a0000000-0000-4000-8000-000000000007', NULL),
    ('e0000000-0000-4000-8000-000000000012', 'a0000000-0000-4000-8000-000000000009', 'a1000000-0000-4000-8000-000000000003', NULL, 'c0000000-0000-4000-8000-000000000201', 'a0000000-0000-4000-8000-000000000007', NULL),
    ('e0000000-0000-4000-8000-000000000013', 'a0000000-0000-4000-8000-000000000009', 'a1000000-0000-4000-8000-000000000004', NULL, NULL, 'a0000000-0000-4000-8000-000000000007', NULL),
    ('e0000000-0000-4000-8000-000000000014', 'a0000000-0000-4000-8000-000000000009', 'a1000000-0000-4000-8000-000000000005', NULL, NULL, 'a0000000-0000-4000-8000-000000000007', NULL),
    ('e0000000-0000-4000-8000-000000000015', 'a0000000-0000-4000-8000-000000000009', 'a1000000-0000-4000-8000-000000000006', NULL, NULL, 'a0000000-0000-4000-8000-000000000007', NULL)
ON CONFLICT (id) DO UPDATE SET
    user_id = EXCLUDED.user_id, role_id = EXCLUDED.role_id,
    materia_id = EXCLUDED.materia_id, carrera_id = EXCLUDED.carrera_id,
    granted_by = EXCLUDED.granted_by, deleted_at = NULL;

-- El catálogo de cargos existe desde migración; el seed restaura sus filas fixture.
INSERT INTO designaciones.cargos (id, codigo, nombre, abreviatura, orden, activo) VALUES
    ('c3000000-0000-4000-8000-000000000001', 'titular', 'Profesor Titular', 'Titular', 1, TRUE),
    ('c3000000-0000-4000-8000-000000000002', 'asociado', 'Profesor Asociado', 'Asociado', 2, TRUE),
    ('c3000000-0000-4000-8000-000000000003', 'adjunto', 'Profesor Adjunto', 'Adjunto', 3, TRUE),
    ('c3000000-0000-4000-8000-000000000004', 'jtp', 'Jefe de Trabajos Prácticos', 'JTP', 4, TRUE),
    ('c3000000-0000-4000-8000-000000000005', 'ayudante1', 'Ayudante de Primera', 'Ay. 1ra', 5, TRUE),
    ('c3000000-0000-4000-8000-000000000006', 'ayudante2', 'Ayudante de Segunda', 'Ay. 2da', 6, TRUE)
ON CONFLICT (id) DO UPDATE SET
    codigo = EXCLUDED.codigo, nombre = EXCLUDED.nombre,
    abreviatura = EXCLUDED.abreviatura, orden = EXCLUDED.orden, activo = EXCLUDED.activo;

INSERT INTO designaciones.periodos
    (id, nombre, carga_desde, carga_hasta, impacto_desde, impacto_hasta, activo) VALUES
    ('d4000000-0000-4000-8000-000000000001', 'Segundo cuatrimestre 2026', DATE '2026-06-01', DATE '2026-07-31', DATE '2026-08-01', DATE '2026-12-31', TRUE),
    ('d4000000-0000-4000-8000-000000000002', 'Primer cuatrimestre 2026', DATE '2025-12-01', DATE '2026-02-28', DATE '2026-03-01', DATE '2026-07-31', FALSE),
    ('d4000000-0000-4000-8000-000000000003', 'Segundo cuatrimestre 2025', DATE '2025-06-01', DATE '2025-07-31', DATE '2025-08-01', DATE '2025-12-31', FALSE)
ON CONFLICT (id) DO UPDATE SET
    nombre = EXCLUDED.nombre, carga_desde = EXCLUDED.carga_desde,
    carga_hasta = EXCLUDED.carga_hasta, impacto_desde = EXCLUDED.impacto_desde,
    impacto_hasta = EXCLUDED.impacto_hasta, activo = EXCLUDED.activo;

INSERT INTO designaciones.designaciones
    (id, persona_id, materia_id, cargo_id, dedicacion, horas, vigente_desde, vigente_hasta, origen_pedido_id) VALUES
    ('d6000000-0000-4000-8000-000000000001', 'd0000000-0000-4000-8000-000000000001', '70000000-0000-4000-8000-000000000101', 'c3000000-0000-4000-8000-000000000003', 'Categoría 2', 10, DATE '2026-03-01', NULL, NULL),
    ('d6000000-0000-4000-8000-000000000002', 'd0000000-0000-4000-8000-000000000002', '70000000-0000-4000-8000-000000000101', 'c3000000-0000-4000-8000-000000000001', 'Categoría 1', 20, DATE '2025-03-01', NULL, NULL),
    ('d6000000-0000-4000-8000-000000000003', 'd0000000-0000-4000-8000-000000000003', '70000000-0000-4000-8000-000000000102', 'c3000000-0000-4000-8000-000000000004', 'Categoría 3', 8, DATE '2026-03-01', NULL, NULL),
    ('d6000000-0000-4000-8000-000000000004', 'd0000000-0000-4000-8000-000000000010', '70000000-0000-4000-8000-000000000201', 'c3000000-0000-4000-8000-000000000005', 'Categoría 4', 6, DATE '2026-03-01', NULL, NULL)
ON CONFLICT (id) DO UPDATE SET
    persona_id = EXCLUDED.persona_id, materia_id = EXCLUDED.materia_id,
    cargo_id = EXCLUDED.cargo_id, dedicacion = EXCLUDED.dedicacion,
    horas = EXCLUDED.horas, vigente_desde = EXCLUDED.vigente_desde,
    vigente_hasta = EXCLUDED.vigente_hasta, origen_pedido_id = EXCLUDED.origen_pedido_id;

-- Un pedido por persona para cada estado soportado.
INSERT INTO designaciones.pedidos
    (id, numero, periodo_id, persona_id, materia_id, novedad, estado, prioritario,
     cargo_solicitado_id, dedicacion_solicitada, horas, justificacion,
     tipo_baja, tipo_baja_detalle, etapa_retorno, propietario_actual, snapshot) VALUES
    ('d5000000-0000-4000-8000-000000000001', '2026-9001', 'd4000000-0000-4000-8000-000000000001', 'd0000000-0000-4000-8000-000000000001', '70000000-0000-4000-8000-000000000101', 'Cambio de cargo o dedicación', 'borrador', FALSE, 'c3000000-0000-4000-8000-000000000002', 'Categoría 1', 20, 'Ampliación de responsabilidades', NULL, NULL, NULL, NULL, NULL),
    ('d5000000-0000-4000-8000-000000000002', '2026-9002', 'd4000000-0000-4000-8000-000000000001', 'd0000000-0000-4000-8000-000000000010', '70000000-0000-4000-8000-000000000201', 'Sin novedad', 'en_revision_coordinador', FALSE, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '{"cargo":"Ayudante de Primera","dedicacion":"Categoría 4","horas":6,"materia":"Organización Industrial"}'::JSONB),
    ('d5000000-0000-4000-8000-000000000003', '2026-9003', 'd4000000-0000-4000-8000-000000000001', 'd0000000-0000-4000-8000-000000000011', '70000000-0000-4000-8000-000000000102', 'Alta', 'en_revision_secretaria', TRUE, 'c3000000-0000-4000-8000-000000000005', 'Categoría 4', 8, 'Cobertura de comisión adicional', NULL, NULL, NULL, NULL, '{"cargo":null,"dedicacion":null,"horas":null,"materia":"Algoritmos y Estructuras de Datos"}'::JSONB),
    ('d5000000-0000-4000-8000-000000000004', '2026-9004', 'd4000000-0000-4000-8000-000000000001', 'd0000000-0000-4000-8000-000000000012', '70000000-0000-4000-8000-000000000103', 'Alta', 'en_revision_decanato', FALSE, 'c3000000-0000-4000-8000-000000000006', 'Categoría 5', 6, 'Nueva comisión', NULL, NULL, NULL, NULL, '{"cargo":null,"dedicacion":null,"horas":null,"materia":"Bases de Datos"}'::JSONB),
    ('d5000000-0000-4000-8000-000000000005', '2026-9005', 'd4000000-0000-4000-8000-000000000001', 'd0000000-0000-4000-8000-000000000013', '70000000-0000-4000-8000-000000000101', 'Cambio de cargo o dedicación', 'devuelto', FALSE, 'c3000000-0000-4000-8000-000000000003', 'Categoría 2', 10, 'Actualización de dedicación', NULL, NULL, 'en_revision_coordinador', 'jefe_catedra', '{"cargo":"JTP","dedicacion":"Categoría 3","horas":8,"materia":"Ingeniería de Software"}'::JSONB),
    ('d5000000-0000-4000-8000-000000000006', '2026-9006', 'd4000000-0000-4000-8000-000000000001', 'd0000000-0000-4000-8000-000000000014', '70000000-0000-4000-8000-000000000102', 'Sin novedad', 'en_lote', FALSE, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '{"cargo":"Adjunto","dedicacion":"Categoría 2","horas":10,"materia":"Algoritmos y Estructuras de Datos"}'::JSONB),
    ('d5000000-0000-4000-8000-000000000007', '2026-9007', 'd4000000-0000-4000-8000-000000000001', 'd0000000-0000-4000-8000-000000000015', '70000000-0000-4000-8000-000000000103', 'Baja', 'rechazado', FALSE, NULL, NULL, NULL, 'Renuncia informada', 'Renuncia', NULL, NULL, NULL, '{"cargo":"JTP","dedicacion":"Categoría 3","horas":8,"materia":"Bases de Datos"}'::JSONB),
    ('d5000000-0000-4000-8000-000000000008', '2026-9008', 'd4000000-0000-4000-8000-000000000001', 'd0000000-0000-4000-8000-000000000016', '70000000-0000-4000-8000-000000000104', 'Sin novedad', 'cancelado', FALSE, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL)
ON CONFLICT (id) DO UPDATE SET
    numero = EXCLUDED.numero, periodo_id = EXCLUDED.periodo_id,
    persona_id = EXCLUDED.persona_id, materia_id = EXCLUDED.materia_id,
    novedad = EXCLUDED.novedad, estado = EXCLUDED.estado,
    prioritario = EXCLUDED.prioritario, cargo_solicitado_id = EXCLUDED.cargo_solicitado_id,
    dedicacion_solicitada = EXCLUDED.dedicacion_solicitada, horas = EXCLUDED.horas,
    justificacion = EXCLUDED.justificacion, tipo_baja = EXCLUDED.tipo_baja,
    tipo_baja_detalle = EXCLUDED.tipo_baja_detalle, etapa_retorno = EXCLUDED.etapa_retorno,
    propietario_actual = EXCLUDED.propietario_actual, snapshot = EXCLUDED.snapshot;

INSERT INTO designaciones.pedido_adjuntos (id, pedido_id, tipo, nombre, uri) VALUES
    ('d8000000-0000-4000-8000-000000000001', 'd5000000-0000-4000-8000-000000000003', 'cv', 'cv-pablo-herrera.pdf', 'synthetic://pedidos/9003/cv'),
    ('d8000000-0000-4000-8000-000000000002', 'd5000000-0000-4000-8000-000000000003', 'dni_frente', 'dni-frente-pablo.pdf', 'synthetic://pedidos/9003/dni-frente'),
    ('d8000000-0000-4000-8000-000000000003', 'd5000000-0000-4000-8000-000000000003', 'dni_dorso', 'dni-dorso-pablo.pdf', 'synthetic://pedidos/9003/dni-dorso'),
    ('d8000000-0000-4000-8000-000000000004', 'd5000000-0000-4000-8000-000000000004', 'cv', 'cv-brenda-ortiz.pdf', 'synthetic://pedidos/9004/cv'),
    ('d8000000-0000-4000-8000-000000000005', 'd5000000-0000-4000-8000-000000000004', 'dni_frente', 'dni-frente-brenda.pdf', 'synthetic://pedidos/9004/dni-frente'),
    ('d8000000-0000-4000-8000-000000000006', 'd5000000-0000-4000-8000-000000000004', 'dni_dorso', 'dni-dorso-brenda.pdf', 'synthetic://pedidos/9004/dni-dorso'),
    ('d8000000-0000-4000-8000-000000000007', 'd5000000-0000-4000-8000-000000000007', 'justificativo', 'renuncia-martin-acosta.pdf', 'synthetic://pedidos/9007/justificativo')
ON CONFLICT (id) DO UPDATE SET
    pedido_id = EXCLUDED.pedido_id, tipo = EXCLUDED.tipo,
    nombre = EXCLUDED.nombre, uri = EXCLUDED.uri;

-- Eventos mínimos pero coherentes con la etapa alcanzada.
INSERT INTO designaciones.pedido_historial
    (id, pedido_id, accion, rol_id, actor_id, etapa, comentario, created_at) VALUES
    ('d7000000-0000-4000-8000-000000000001', 'd5000000-0000-4000-8000-000000000001', 'crear', 'a1000000-0000-4000-8000-000000000002', 'a0000000-0000-4000-8000-000000000002', 'borrador', NULL, TIMESTAMPTZ '2026-06-10 12:00:00+00'),
    ('d7000000-0000-4000-8000-000000000002', 'd5000000-0000-4000-8000-000000000002', 'enviar', 'a1000000-0000-4000-8000-000000000002', 'a0000000-0000-4000-8000-000000000009', 'en_revision_coordinador', NULL, TIMESTAMPTZ '2026-06-11 12:00:00+00'),
    ('d7000000-0000-4000-8000-000000000003', 'd5000000-0000-4000-8000-000000000003', 'enviar', 'a1000000-0000-4000-8000-000000000002', 'a0000000-0000-4000-8000-000000000009', 'en_revision_coordinador', NULL, TIMESTAMPTZ '2026-06-12 12:00:00+00'),
    ('d7000000-0000-4000-8000-000000000004', 'd5000000-0000-4000-8000-000000000003', 'aceptar', 'a1000000-0000-4000-8000-000000000003', 'a0000000-0000-4000-8000-000000000003', 'en_revision_secretaria', 'Documentación completa', TIMESTAMPTZ '2026-06-13 12:00:00+00'),
    ('d7000000-0000-4000-8000-000000000005', 'd5000000-0000-4000-8000-000000000003', 'priorizar', 'a1000000-0000-4000-8000-000000000004', 'a0000000-0000-4000-8000-000000000004', 'en_revision_secretaria', 'Cobertura urgente', TIMESTAMPTZ '2026-06-14 12:00:00+00'),
    ('d7000000-0000-4000-8000-000000000006', 'd5000000-0000-4000-8000-000000000004', 'enviar', 'a1000000-0000-4000-8000-000000000002', 'a0000000-0000-4000-8000-000000000009', 'en_revision_coordinador', NULL, TIMESTAMPTZ '2026-06-12 13:00:00+00'),
    ('d7000000-0000-4000-8000-000000000007', 'd5000000-0000-4000-8000-000000000004', 'aceptar', 'a1000000-0000-4000-8000-000000000003', 'a0000000-0000-4000-8000-000000000003', 'en_revision_secretaria', NULL, TIMESTAMPTZ '2026-06-13 13:00:00+00'),
    ('d7000000-0000-4000-8000-000000000008', 'd5000000-0000-4000-8000-000000000004', 'aceptar', 'a1000000-0000-4000-8000-000000000004', 'a0000000-0000-4000-8000-000000000004', 'en_revision_decanato', NULL, TIMESTAMPTZ '2026-06-14 13:00:00+00'),
    ('d7000000-0000-4000-8000-000000000009', 'd5000000-0000-4000-8000-000000000005', 'devolver', 'a1000000-0000-4000-8000-000000000003', 'a0000000-0000-4000-8000-000000000003', 'devuelto', 'Corregir la dedicación solicitada', TIMESTAMPTZ '2026-06-15 12:00:00+00'),
    ('d7000000-0000-4000-8000-000000000010', 'd5000000-0000-4000-8000-000000000006', 'aceptar', 'a1000000-0000-4000-8000-000000000005', 'a0000000-0000-4000-8000-000000000005', 'en_lote', 'Aprobación final', TIMESTAMPTZ '2026-06-16 12:00:00+00'),
    ('d7000000-0000-4000-8000-000000000011', 'd5000000-0000-4000-8000-000000000007', 'rechazar', 'a1000000-0000-4000-8000-000000000003', 'a0000000-0000-4000-8000-000000000003', 'rechazado', 'El justificativo no corresponde al período', TIMESTAMPTZ '2026-06-17 12:00:00+00'),
    ('d7000000-0000-4000-8000-000000000012', 'd5000000-0000-4000-8000-000000000008', 'cancelar', 'a1000000-0000-4000-8000-000000000002', 'a0000000-0000-4000-8000-000000000009', 'cancelado', NULL, TIMESTAMPTZ '2026-06-18 12:00:00+00')
ON CONFLICT (id) DO UPDATE SET
    pedido_id = EXCLUDED.pedido_id, accion = EXCLUDED.accion,
    rol_id = EXCLUDED.rol_id, actor_id = EXCLUDED.actor_id,
    etapa = EXCLUDED.etapa, comentario = EXCLUDED.comentario,
    created_at = EXCLUDED.created_at;

COMMIT;
