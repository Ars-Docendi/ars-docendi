-- Fixtures SINTÉTICAS para ambientes no-prod (staging / pr-N).
--
-- Datos inventados, nunca derivados de producción. Las migraciones de la app
-- (EF Core) crean el esquema "un schema por módulo" ANTES de correr este seed;
-- acá solo se insertan filas de ejemplo.
--
-- Estado: placeholder mínimo. Ampliar con fixtures por módulo a medida que el
-- código de los módulos exista (Designaciones, Aulas, Portal, Tareas). El seed
-- es idempotente: usar INSERT ... ON CONFLICT DO NOTHING o TRUNCATE+INSERT.

-- Marca de sembrado para verificar en validación que el ambiente NO es prod.
CREATE TABLE IF NOT EXISTS public.seed_metadata (
  clave  text PRIMARY KEY,
  valor  text NOT NULL
);

INSERT INTO public.seed_metadata (clave, valor)
VALUES ('origen_datos', 'sintetico'),
       ('sembrado_en', now()::text)
ON CONFLICT (clave) DO UPDATE SET valor = EXCLUDED.valor;
