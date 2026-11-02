-- Baseline consolidado: designaciones/007_designaciones_catalogos.sql
-- DDL final; no contiene transiciones ni backfills de alpha.

INSERT INTO designaciones.cargos (id, codigo, nombre, abreviatura, orden, activo, created_at) VALUES ('c3000000-0000-4000-8000-000000000001', 'titular', 'Profesor Titular', 'Titular', 1, true, now());

INSERT INTO designaciones.cargos (id, codigo, nombre, abreviatura, orden, activo, created_at) VALUES ('c3000000-0000-4000-8000-000000000002', 'asociado', 'Profesor Asociado', 'Asociado', 2, true, now());

INSERT INTO designaciones.cargos (id, codigo, nombre, abreviatura, orden, activo, created_at) VALUES ('c3000000-0000-4000-8000-000000000003', 'adjunto', 'Profesor Adjunto', 'Adjunto', 3, true, now());

INSERT INTO designaciones.cargos (id, codigo, nombre, abreviatura, orden, activo, created_at) VALUES ('c3000000-0000-4000-8000-000000000004', 'jtp', 'Jefe de Trabajos Prácticos', 'JTP', 4, true, now());

INSERT INTO designaciones.cargos (id, codigo, nombre, abreviatura, orden, activo, created_at) VALUES ('c3000000-0000-4000-8000-000000000005', 'ayudante1', 'Ayudante de Primera', 'Ay. 1ra', 5, true, now());

INSERT INTO designaciones.cargos (id, codigo, nombre, abreviatura, orden, activo, created_at) VALUES ('c3000000-0000-4000-8000-000000000006', 'ayudante2', 'Ayudante de Segunda', 'Ay. 2da', 6, true, now());

INSERT INTO designaciones.dedicaciones (id, codigo, nombre, orden, activo, created_at) VALUES ('d6000000-0000-4000-8000-000000000001', 1, 'Categoría 1', 1, true, now());

INSERT INTO designaciones.dedicaciones (id, codigo, nombre, orden, activo, created_at) VALUES ('d6000000-0000-4000-8000-000000000002', 2, 'Categoría 2', 2, true, now());

INSERT INTO designaciones.dedicaciones (id, codigo, nombre, orden, activo, created_at) VALUES ('d6000000-0000-4000-8000-000000000003', 3, 'Categoría 3', 3, true, now());

INSERT INTO designaciones.dedicaciones (id, codigo, nombre, orden, activo, created_at) VALUES ('d6000000-0000-4000-8000-000000000004', 4, 'Categoría 4', 4, true, now());

INSERT INTO designaciones.dedicaciones (id, codigo, nombre, orden, activo, created_at) VALUES ('d6000000-0000-4000-8000-000000000005', 5, 'Categoría 5', 5, true, now());

INSERT INTO designaciones.dedicaciones (id, codigo, nombre, orden, activo, created_at) VALUES ('d6000000-0000-4000-8000-000000000006', 6, 'Categoría 6', 6, true, now());
