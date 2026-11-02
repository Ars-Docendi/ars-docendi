-- Baseline consolidado: storage/002_storage_audit_attach.sql
-- DDL final; no contiene transiciones ni backfills de alpha.

SELECT audit.attach('storage.archivos');
