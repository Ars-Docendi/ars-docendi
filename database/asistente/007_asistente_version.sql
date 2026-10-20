-- Versión aplicada del DDL del asistente.
--
-- El asistente no versiona su DDL con un historial de EF: sus scripts convergen
-- (`IF NOT EXISTS`, GRANT repetido) y se reaplican enteros. Lo que el deploy
-- necesita saber es otra cosa: si el DDL embebido en ESTA imagen ya se aplicó a
-- ESTA base. Sin esa respuesta, `--estado-migraciones` no reporta pendientes del
-- asistente y `spin-up.sh` saltea `--migrate` cuando el único cambio de una
-- release es un GRANT nuevo del asistente.
--
-- Una sola fila: la huella del conjunto de scripts + nombres de rol que se
-- aplicó por última vez. La escribe `MigradorAsistente` después de aplicar todo.
-- Vive en el schema `asistente`, que ya está denegado entero a los roles de
-- lectura (002_asistente_registros.sql).
CREATE TABLE IF NOT EXISTS asistente.version_aplicada (
    unica       BOOLEAN PRIMARY KEY DEFAULT TRUE CHECK (unica),
    huella      TEXT NOT NULL,
    aplicada_en TIMESTAMPTZ NOT NULL DEFAULT now()
);
