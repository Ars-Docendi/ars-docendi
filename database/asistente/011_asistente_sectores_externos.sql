-- 011_asistente_sectores_externos.sql
--
-- Docentes externos (change dedicacion-catalogo-y-externos): lectura del catálogo de sectores
-- externos y de la columna sector_externo_id de designaciones y pedidos. Sin ellas, el
-- asistente no puede nombrar el sector de una designación externa (sin cargo ni dedicación).
--
-- Sólo AGREGA privilegios por columna, igual que 001 y con los mismos roles (GUC
-- app.asistente_rol_basico / app.asistente_rol_pii). Idempotente: GRANT repetido no cambia nada.
-- Las columnas y la tabla están clasificadas en manifiesto-privilegios.json y
-- manifiesto-sensibilidad.json.

DO $asistente_sectores_externos$
DECLARE
  rol_basico text := current_setting('app.asistente_rol_basico', true);
  rol_pii    text := current_setting('app.asistente_rol_pii', true);
BEGIN
  IF coalesce(rol_basico, '') = '' OR coalesce(rol_pii, '') = '' THEN
    RAISE EXCEPTION 'Faltan los nombres de los roles del asistente (app.asistente_rol_basico / app.asistente_rol_pii)';
  END IF;

  EXECUTE format('GRANT SELECT (id, codigo, nombre, activo, created_at) ON designaciones.sectores_externos TO %I', rol_basico);
  EXECUTE format('GRANT SELECT (id, codigo, nombre, activo, created_at) ON designaciones.sectores_externos TO %I', rol_pii);
  EXECUTE format('GRANT SELECT (sector_externo_id) ON designaciones.designaciones TO %I', rol_basico);
  EXECUTE format('GRANT SELECT (sector_externo_id) ON designaciones.designaciones TO %I', rol_pii);
  EXECUTE format('GRANT SELECT (sector_externo_id) ON designaciones.pedidos TO %I', rol_basico);
  EXECUTE format('GRANT SELECT (sector_externo_id) ON designaciones.pedidos TO %I', rol_pii);
END
$asistente_sectores_externos$;
