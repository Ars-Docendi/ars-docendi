-- 002_audit_busqueda.sql
--
-- La búsqueda label-only del feed unificado de auditoría (sistema-seccion-unificada,
-- design.md D4) normaliza acentos con la extensión `unaccent` de PostgreSQL, tanto
-- del lado de la etiqueta (resuelta en C#, ver EtiquetasAuditoria.Normalizar) como
-- del lado del valor mostrado (schema.tabla, row_pk, el nombre del actor), vía
-- unaccent(...) + ILIKE en RepositorioAuditoria.
--
-- Se declara acá — audit/, no asistente/ — porque la búsqueda es del feed de
-- auditoría del Host, no del módulo del asistente, aunque el asistente ya la
-- necesitaba para sus propias columnas (database/asistente/001_asistente_grants.sql
-- ya crea la misma extensión). CREATE EXTENSION IF NOT EXISTS es idempotente: que
-- las dos migraciones la pidan no es un conflicto, es que las dos declaran su propia
-- dependencia sin que una espere el orden de la otra.

CREATE EXTENSION IF NOT EXISTS unaccent;
