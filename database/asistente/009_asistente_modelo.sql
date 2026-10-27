-- 009_asistente_modelo.sql
--
-- Modelo en caliente (asistente-modelo-en-caliente, slice 1): credencial cifrada
-- por empresa, selección vigente del modelo y catálogo de modelos definido por el
-- repositorio. Ver design.md D3-D5.
--
-- INERTE HASTA QUE ALGUIEN LA USE. Nada lee estas tablas al arrancar: sin una
-- fila en modelo_vigente el módulo sigue con la configuración del servidor, igual
-- que antes de este archivo.
--
-- SIN CLAVE FORÁNEA A identity NI ENTRE ELLAS, A PROPÓSITO. Mismo criterio que
-- 006: `actor_id` es el administrador que escribió, sin FK para que una baja en
-- identity no quede bloqueada por una fila de configuración. Y modelo_vigente no
-- apunta al catálogo: que un modelo salga del catálogo (un PR) no puede dejar
-- sin poder arrancar a una selección ya guardada; la aplicación valida contra el
-- catálogo al APLICAR, no al leer.
--
-- IDEMPOTENTE COMO 002-008: `IF NOT EXISTS` y `ON CONFLICT DO NOTHING`. Las
-- tres tablas viven en el schema `asistente`, ya denegado entero a los dos roles
-- de solo lectura (manifiesto-privilegios.json), así que nacen inalcanzables
-- para ellos y no necesitan entrada en ningún manifiesto.

-- ------------------------------------------------------ credencial_de_proveedor
--
-- Una clave de API por empresa nube, cifrada con AES-GCM por la aplicación
-- (design.md D4). La base nunca ve el texto plano ni la clave maestra. Los
-- servidores locales no tienen fila: sus credenciales son configuración del
-- servidor.
CREATE TABLE IF NOT EXISTS asistente.credencial_de_proveedor (
    empresa        text        PRIMARY KEY,
    clave_cifrada  text        NOT NULL,
    pista          text        NOT NULL,
    validada_en    timestamptz NULL,
    actor_id       uuid        NOT NULL,
    actualizado_en timestamptz NOT NULL,
    CONSTRAINT credencial_de_proveedor_pista_corta CHECK (char_length(pista) <= 4)
);

COMMENT ON TABLE asistente.credencial_de_proveedor IS
    'Clave de API por empresa nube del modelo, cifrada con AES-GCM por la aplicación. A lo sumo una por empresa. Nunca se devuelve en una respuesta, un log ni la auditoría: sólo la pista.';
COMMENT ON COLUMN asistente.credencial_de_proveedor.empresa IS
    'Empresa del modelo (anthropic, openai, google, mistral). Es también el dato asociado (AAD asistente:{empresa}) que ata el cifrado a esta fila.';
COMMENT ON COLUMN asistente.credencial_de_proveedor.clave_cifrada IS
    'Formato v1:{kid}:{base64(iv|tag|texto cifrado)}. kid nombra la clave maestra que cifró el valor; se recifra con la vigente al leer.';
COMMENT ON COLUMN asistente.credencial_de_proveedor.pista IS
    'Los últimos 4 caracteres de la clave: lo único que se muestra.';
COMMENT ON COLUMN asistente.credencial_de_proveedor.validada_en IS
    'Momento de la última prueba de conexión exitosa con esta clave. Nulo si nunca se probó.';
COMMENT ON COLUMN asistente.credencial_de_proveedor.actor_id IS
    'Administrador que cargó o reemplazó la clave.';
COMMENT ON COLUMN asistente.credencial_de_proveedor.actualizado_en IS
    'Momento de la última escritura de la fila, incluido el recifrado por rotación de la clave maestra.';

-- ----------------------------------------------------------------- modelo_vigente
--
-- Selección del administrador, fila única (mismo patrón que modo_mantenimiento
-- en 006: `id` fijo en 1 con un CHECK impide una segunda fila sin trigger).
-- NO se siembra: la ausencia de fila ES el estado "configuración del servidor".
-- Volver a ese estado es borrar la fila.
CREATE TABLE IF NOT EXISTS asistente.modelo_vigente (
    id             smallint    PRIMARY KEY DEFAULT 1,
    empresa        text        NOT NULL,
    modelo         text        NOT NULL,
    actor_id       uuid        NOT NULL,
    actualizado_en timestamptz NOT NULL,
    CONSTRAINT modelo_vigente_fila_unica CHECK (id = 1)
);

COMMENT ON TABLE asistente.modelo_vigente IS
    'Modelo elegido en caliente por un administrador, fila única. SIN FILA significa configuración del servidor (variables de ambiente): es el estado normal y el de rollback, no un error.';
COMMENT ON COLUMN asistente.modelo_vigente.id IS
    'Siempre 1: el CHECK impide una segunda fila.';
COMMENT ON COLUMN asistente.modelo_vigente.empresa IS
    'Empresa del modelo elegido: anthropic, openai, google, mistral, o un servidor local (ollama, local).';
COMMENT ON COLUMN asistente.modelo_vigente.modelo IS
    'Identificador del modelo dentro de la empresa. Se validó contra catalogo_de_modelos al aplicar, no se vuelve a validar al leer.';
COMMENT ON COLUMN asistente.modelo_vigente.actor_id IS
    'Administrador que aplicó la selección.';
COMMENT ON COLUMN asistente.modelo_vigente.actualizado_en IS
    'Momento en que se aplicó la selección.';

-- ------------------------------------------------------------ catalogo_de_modelos
--
-- Qué modelos se pueden elegir, agrupados por empresa (design.md D5). Lo define
-- el repositorio: agregar un modelo es un PR contra ESTE archivo o uno posterior
-- (nunca se edita uno ya publicado). No hay listado en vivo contra las APIs de
-- los proveedores ni catálogo editable desde la pantalla: un modelo que nunca se
-- evaluó no puede llegar a producción sin revisión.
--
-- Los identificadores de la semilla son los nombres con que cada empresa publica
-- el modelo. Los de ollama y local son los que usan compose.asistente-ollama.yml
-- y compose.asistente-local.yml. Los cuatro de la nube se confirman contra los
-- resultados de evaluación (task 1.3) antes de ofrecerse en producción.
CREATE TABLE IF NOT EXISTS asistente.catalogo_de_modelos (
    empresa text NOT NULL,
    modelo  text NOT NULL,
    PRIMARY KEY (empresa, modelo)
);

COMMENT ON TABLE asistente.catalogo_de_modelos IS
    'Modelos elegibles desde la pantalla, por empresa. Definido por el repositorio (semilla de este archivo); no se lista desde las APIs de los proveedores.';
COMMENT ON COLUMN asistente.catalogo_de_modelos.empresa IS
    'Empresa del modelo: anthropic, openai, google, mistral, ollama o local.';
COMMENT ON COLUMN asistente.catalogo_de_modelos.modelo IS
    'Identificador del modelo tal como lo recibe el proveedor, y tal como queda en registro_operativo.proveedor y tabla_de_precios (parte derecha de empresa/modelo).';

INSERT INTO asistente.catalogo_de_modelos (empresa, modelo)
VALUES
    ('anthropic', 'claude-sonnet-5'),
    ('anthropic', 'claude-haiku-4-5'),
    ('openai', 'gpt-5'),
    ('openai', 'gpt-5-mini'),
    ('google', 'gemini-2.5-pro'),
    ('google', 'gemini-2.5-flash'),
    ('mistral', 'mistral-large-latest'),
    ('mistral', 'mistral-small-latest'),
    ('ollama', 'qwen3.5:9b'),
    ('local', 'qwen3-8b')
ON CONFLICT (empresa, modelo) DO NOTHING;
