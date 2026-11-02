-- Acceso selectivo por perfil. No duplicar índices equivalentes existentes.
DO $$
DECLARE tabla text;
BEGIN
  FOREACH tabla IN ARRAY ARRAY['experiencias','educaciones','certificaciones','proyectos'] LOOP
    IF NOT EXISTS (
      SELECT 1 FROM pg_index i
      JOIN pg_class t ON t.oid = i.indrelid
      JOIN pg_namespace n ON n.oid = t.relnamespace
      JOIN pg_class idx ON idx.oid = i.indexrelid
      JOIN pg_am am ON am.oid = idx.relam
      JOIN pg_attribute a ON a.attrelid = t.oid AND a.attname = 'perfil_id'
      WHERE n.nspname = 'portal' AND t.relname = tabla
        AND i.indisvalid AND i.indisready AND i.indpred IS NULL
        AND i.indnkeyatts = 1 AND i.indkey[0] = a.attnum AND am.amname = 'btree'
    ) THEN
      EXECUTE format('CREATE INDEX %I ON portal.%I (perfil_id)', tabla || '_perfil_idx', tabla);
    END IF;
  END LOOP;
END $$;
