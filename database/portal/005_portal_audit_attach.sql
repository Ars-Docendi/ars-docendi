-- Baseline consolidado: portal/005_portal_audit_attach.sql
-- DDL final; no contiene transiciones ni backfills de alpha.

SELECT audit.attach('portal.certificaciones');

SELECT audit.attach('portal.contactos');

SELECT audit.attach('portal.cvs');

SELECT audit.attach('portal.docente_habilidades', 'perfil_id');

SELECT audit.attach('portal.educaciones');

SELECT audit.attach('portal.experiencias');

SELECT audit.attach('portal.habilidades');

SELECT audit.attach('portal.perfiles');

SELECT audit.attach('portal.proyecto_documentos');

SELECT audit.attach('portal.proyectos');
