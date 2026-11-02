-- No datos del dataset; guarda de procedencia/primer uso en cada sesión psql.
CREATE OR REPLACE FUNCTION pg_temp.huella_bootstrap() RETURNS text
LANGUAGE plpgsql AS $$
DECLARE tabla record; filas text; conjunto text := '';
BEGIN
  FOR tabla IN
    SELECT schemaname, tablename FROM pg_tables
    WHERE schemaname NOT IN ('pg_catalog', 'information_schema')
      AND schemaname NOT LIKE 'pg_toast%'
      AND tablename <> 'bootstrap_metadata'
    ORDER BY schemaname, tablename
  LOOP
    EXECUTE format('SELECT md5(COALESCE(string_agg(fila, ''|'' ORDER BY fila), '''')) FROM (SELECT to_jsonb(t)::text AS fila FROM %I.%I t) q', tabla.schemaname, tabla.tablename) INTO filas;
    conjunto := conjunto || tabla.schemaname || '.' || tabla.tablename || ':' || filas || ';';
  END LOOP;
  RETURN md5(conjunto);
END
$$;
BEGIN;
SELECT pg_advisory_xact_lock(hashtextextended('arsdocendi:seed:sintetico', 0));
SELECT set_config('arsdocendi.seed_ambiente', :'ambiente', true) AS ambiente
\gset
DO $$
DECLARE marca record; huella text;
BEGIN
  IF to_regclass('public.bootstrap_metadata') IS NULL THEN
    RAISE EXCEPTION 'base sin autorización de inicialización; no se sobrescribe';
  END IF;
  SELECT * INTO marca FROM public.bootstrap_metadata WHERE id FOR UPDATE;
  IF NOT FOUND OR marca.origen <> 'provision-db/v1' OR marca.ambiente <> current_setting('arsdocendi.seed_ambiente') THEN
    RAISE EXCEPTION 'bootstrap no reconocido';
  END IF;
  IF marca.estado = 'completado' THEN
    IF to_regclass('public.seed_metadata') IS NULL THEN
      RAISE EXCEPTION 'bootstrap inconsistente: falta marcador del dataset';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM public.seed_metadata WHERE clave = 'inicializacion_completada' AND valor = 'sintetico/v1') THEN
      RAISE EXCEPTION 'marcador de inicialización incompleto';
    END IF;
  ELSE
    huella := pg_temp.huella_bootstrap();
    IF marca.huella_inicial IS NOT NULL AND marca.huella_inicial <> huella THEN
      RAISE EXCEPTION 'base modificada después de inicialización fallida; no se sobrescribe';
    END IF;
    UPDATE public.bootstrap_metadata SET huella_inicial = huella WHERE id;
  END IF;
END
$$;
