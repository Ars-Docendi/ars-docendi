-- Heartbeats: rango vacío [cursor+1,cursor], sin eventos ficticios.
ALTER TABLE audit.seal_batches DROP CONSTRAINT seal_batches_check;
ALTER TABLE audit.seal_batches DROP CONSTRAINT seal_batches_event_count_check;
ALTER TABLE audit.seal_batches DROP CONSTRAINT seal_batches_range_unique;
ALTER TABLE audit.seal_batches ADD CONSTRAINT seal_batches_range_check
 CHECK (last_seq >= 0 AND ((event_count = 0 AND first_seq = last_seq + 1)
 OR (event_count > 0 AND last_seq >= first_seq AND event_count = last_seq - first_seq + 1)));
CREATE UNIQUE INDEX seal_batches_nonempty_range_unique
 ON audit.seal_batches(environment, first_seq, last_seq) WHERE event_count > 0;
CREATE UNIQUE INDEX seal_batches_one_pending
 ON audit.seal_batches(environment) WHERE primary_witnessed_at IS NULL OR secondary_witnessed_at IS NULL;
