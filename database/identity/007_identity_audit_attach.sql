-- Baseline consolidado: identity/007_identity_audit_attach.sql
-- DDL final; no contiene transiciones ni backfills de alpha.

SELECT audit.attach('identity.carreras');

SELECT audit.attach('identity.materias');

SELECT audit.attach('identity.materias_plan');

SELECT audit.attach('identity.permisos');

SELECT audit.attach('identity.personas');

SELECT audit.attach('identity.planes');

SELECT audit.attach('identity.rol_permisos', 'rol_id');

SELECT audit.attach('identity.roles');

SELECT audit.attach('identity.user_roles');

SELECT audit.attach('identity.users');
