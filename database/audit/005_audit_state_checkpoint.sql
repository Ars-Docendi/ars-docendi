-- Snapshots privados: nunca se envían al firmador ni a los testigos.
-- NULL identifica sellos experimentales anteriores, no se inventa baseline.
ALTER TABLE audit.seal_batches ADD COLUMN state_snapshot JSONB NULL;
ALTER TABLE audit.seal_batches ADD COLUMN state_cursor BIGINT NULL CHECK (state_cursor >= 0);
