-- 002_asistente_registros.sql
--
-- Los dos registros del asistente: uno operativo y uno analítico. Existen para
-- poder responder «cuánto se usa» y «qué se pregunta» sin poder responder «quién
-- preguntó qué».
--
-- POR QUÉ SON DOS TABLAS Y NO UNA CON UN FLAG
-- Un flag de anonimato no desvincula nada: las dos filas quedan en la misma tabla,
-- con la misma clave y el mismo momento. Separarlas es lo único que hace que el
-- cruce no exista.
--
-- POR QUÉ EL ANALÍTICO GUARDA `dia` Y NO UN TIMESTAMP
-- Con alrededor de treinta usuarios, un timestamp preciso en las dos tablas
-- permitiría reidentificar al autor de cada pregunta con un join por tiempo.
-- Desvincular sin quitar la hora no desvincula nada, así que la precisión se
-- pierde a propósito y no se puede recuperar.
--
-- POR QUÉ EL ANALÍTICO NO TIENE UNA CLAVE SECUENCIAL
-- Una identidad autoincremental sería, ella misma, la clave del join: la fila n de
-- un registro y la fila n del otro serían el mismo turno. Se usa un UUID aleatorio
-- para que el orden de inserción no quede escrito en ninguna columna.
--
-- Riesgo residual, declarado: el orden FÍSICO de las filas (ctid) todavía
-- correlaciona con el orden de los timestamps del registro operativo. Romperlo
-- exigiría escribir en lotes barajados, que es desproporcionado para lo que
-- protege; queda anotado como deuda.
--
-- NUNCA, EN NINGUNO DE LOS DOS: LAS FILAS DEVUELTAS
-- Ni por defecto ni detrás de un flag. Son exactamente los datos que el
-- enmascaramiento acaba de sacar del camino de salida. La consulta generada
-- tampoco: un WHERE puede llevar un documento.
--
-- Idempotente: IF NOT EXISTS en todo. Re-ejecutar converge, que es lo que
-- IMigradorModulo exige.

CREATE SCHEMA IF NOT EXISTS asistente;

-- ---------------------------------------------------------------- operativo
--
-- Quién consultó, cuándo y cuánto costó. NO guarda el texto de la pregunta.
CREATE TABLE IF NOT EXISTS asistente.registro_operativo (
    id                 bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    actor_id           uuid        NOT NULL,
    ocurrido_en        timestamptz NOT NULL,
    carril             text        NOT NULL,
    estado             text        NOT NULL,
    llamadas_al_modelo integer     NOT NULL,
    tokens_de_entrada  integer     NOT NULL,
    tokens_de_salida   integer     NOT NULL,
    latencia_ms        integer     NOT NULL,
    hubo_reintento     boolean     NOT NULL,
    truncado           boolean     NOT NULL,
    proveedor          text        NOT NULL
);

-- `proveedor` guarda quién respondió, con su modelo: `anthropic/claude-sonnet-5`.
-- Es la identidad que expone el puerto —IProveedorDeModelo.Nombre—, nunca la
-- credencial. Sin esta columna, un cambio de proveedor o de modelo dejaría el costo
-- de antes y el de después mezclados en la misma serie, sin forma de separarlos.
--
-- Va sin DEFAULT y sin un ALTER que la agregue a una tabla ya creada, porque este
-- archivo no puede alterar nada: el módulo no lleva historial de migraciones y hay
-- un test de arquitectura que lo sostiene. Una base que ya tenía la tabla no se
-- migra, se vuelve a aprovisionar; los ambientes de este sistema son efímeros y esa
-- es la vía prevista.

-- Sin clave foránea a identity.users a propósito: el registro tiene que poder
-- purgarse y conservarse con independencia del padrón, y una baja de usuario no
-- puede quedar bloqueada por una fila de telemetría.
COMMENT ON TABLE asistente.registro_operativo IS
    'Uso y costo del asistente por actor. No guarda el texto de la pregunta, la consulta generada ni las filas devueltas. Retención de 90 días con purga automática.';

CREATE INDEX IF NOT EXISTS ix_registro_operativo_ocurrido_en
    ON asistente.registro_operativo (ocurrido_en);

-- ---------------------------------------------------------------- analítico
--
-- Qué se pregunta. NO guarda actor ni hora.
CREATE TABLE IF NOT EXISTS asistente.registro_analitico (
    id        uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    pregunta  text NOT NULL,
    categoria text NOT NULL,
    estado    text NOT NULL,
    dia       date NOT NULL
);

COMMENT ON TABLE asistente.registro_analitico IS
    'Qué se le pregunta al asistente. No guarda actor ni hora exacta: con la escala de usuarios de este sistema, cruzarlo con el registro operativo permitiría reidentificar al autor. Retención de 90 días con purga automática.';

CREATE INDEX IF NOT EXISTS ix_registro_analitico_dia
    ON asistente.registro_analitico (dia);

-- ----------------------------------------------------- NO se adjunta auditoría
--
-- DECLARADO EXPLÍCITO, Y ES LO CONTRARIO DE LO QUE HACE EL RESTO DEL REPOSITORIO.
-- Todas las tablas de identity y designaciones terminan su archivo con
-- SELECT audit.attach('schema.tabla'). Acá NO se llama, y el motivo tiene que
-- quedar escrito o el próximo que agregue una tabla lo va a completar por
-- consistencia:
--
--   audit.change_log guarda la fila ENTERA en JSON y no tiene política de
--   retención. Enganchar el registro analítico haría que el texto de cada
--   pregunta sobreviviera a la purga de 90 días en otra tabla, y enganchar el
--   operativo replicaría el actor con timestamp preciso en un tercer lugar que
--   sí se puede cruzar.
--
-- La ausencia de la llamada es una decisión, no un olvido. Hay un test que falla
-- si a alguna de las dos tablas le aparece el disparador.

-- ------------------------------------------- el asistente no lee sus registros
--
-- La decisión de privilegio vive acá, junto al CREATE SCHEMA, porque un REVOKE
-- sobre un schema que todavía no existe falla. Está declarada además en
-- manifiesto-privilegios.json como schema denegado.
--
-- El motivo es directo: el registro analítico tiene el texto de las preguntas de
-- TODOS los usuarios. Un asistente que pudiera consultarlo respondería «qué le
-- preguntó fulano al asistente» a cualquiera con el permiso de consulta.
DO $$
DECLARE
  rol_basico text := current_setting('app.asistente_rol_basico', false);
  rol_pii    text := current_setting('app.asistente_rol_pii', false);
BEGIN
  EXECUTE format('REVOKE ALL ON SCHEMA asistente FROM %I, %I', rol_basico, rol_pii);
  EXECUTE format(
    'REVOKE ALL ON ALL TABLES IN SCHEMA asistente FROM %I, %I', rol_basico, rol_pii);
END
$$;
