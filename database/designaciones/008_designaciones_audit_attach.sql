-- Baseline consolidado: designaciones/008_designaciones_audit_attach.sql
-- DDL final; no contiene transiciones ni backfills de alpha.

SELECT audit.attach('designaciones.cargos');

SELECT audit.attach('designaciones.dedicaciones');

SELECT audit.attach('designaciones.designaciones');

SELECT audit.attach('designaciones.pedido_adjuntos');

SELECT audit.attach('designaciones.pedido_historial');

SELECT audit.attach('designaciones.pedidos');

SELECT audit.attach('designaciones.periodos');
