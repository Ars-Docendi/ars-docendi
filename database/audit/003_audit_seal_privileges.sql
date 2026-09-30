-- Corrección forward-only: también se aplica si 002 ya fue ejecutada.
-- Owner de funciones = rol migrador confiable; nunca API/sellador/runner de PR.
-- Nombres de objetos del cuerpo están calificados y search_path no es controlable
-- por el caller. No conceder DML de auditoría a la API para habilitar el trigger.
ALTER FUNCTION audit.log_change() SECURITY DEFINER;
ALTER FUNCTION audit.log_change() SET search_path = pg_catalog;
REVOKE ALL ON FUNCTION audit.next_seal_seq() FROM PUBLIC;
REVOKE ALL ON FUNCTION audit.attach(regclass, text) FROM PUBLIC;
REVOKE ALL ON FUNCTION audit.log_change() FROM PUBLIC;
