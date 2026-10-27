## Why

`asistente-proveedor-local` dejó el asistente funcionando contra un modelo propio, pero difirió las optimizaciones que cambian el prompt o lo que ve el usuario (`docs/architecture/modelo-local.md` §6, «Evaluado y dejado para después»). Para una GPU de 12 GB eran deseables. Para la prueba que sigue, en una **RTX 3070 (8 GB, Ampere `sm_86`)**, son necesarias:

- con los pesos de un 8B en 4 bits (~5 GB), el prefijo de ~12k tokens del esquema deja lugar para un solo turno en vuelo;
- cada llamada al modelo que se ahorra es tiempo de GPU que no se comparte.

## What Changes

**Todas son opciones nuevas que arrancan apagadas.** Con sus defaults, el prompt que recibe Claude no cambia en un byte y los 109 cassettes siguen valiendo. Los perfiles locales las prenden.

- **Esquema compacto** (`EsquemaCompacto`): el mismo contenido en menos tokens.
  - tipos abreviados y nulabilidad como `?`;
  - claves foráneas en línea con la columna en vez de una sección aparte;
  - sin la frase redundante de las columnas `id`.

  Los comentarios del esquema, que llevan las reglas, se conservan enteros.

- **Ejemplos en el prefijo** (`EjemplosEnElPrefijo`): los ejemplos verificados van todos en el prefijo cacheable y salen del mensaje de cada turno.
- **Reintento con contexto** (`ReintentoConContexto`): el reintento por consulta vacía le dice al modelo qué consulta devolvió cero filas, en vez de repetir el prompt idéntico.
- **Reparación** (`RepararConsultaFallida`): si PostgreSQL rechaza la consulta, una sola ronda de corrección con el error saneado. No viaja ningún literal que no esté en la consulta, así que tampoco ningún dato de una fila.
- **Respuestas sin modelo para resultados triviales** (`RedaccionConPlantillas`): un valor único o una lista corta de una columna se redactan con plantilla, sin llamar al modelo. Sólo cuando el actor ve todo, el resultado no se recortó y no hay datos autodeclarados.
- **Caché de consultas generadas** (`VigenciaDeCacheDeConsultasMinutos`): la misma pregunta, con el mismo rol de lectura y el mismo día, reutiliza la SQL ya generada. Se **re-ejecuta** siempre bajo el alcance del actor: nunca se cachean filas.
- **Reescritura dentro de la generación** (`ReescrituraEnLaGeneracion`): en un seguimiento, la generación recibe las preguntas anteriores y devuelve la pregunta resuelta junto con la SQL. Una llamada al modelo menos por seguimiento.
- **Telemetría del servidor local**: `GET /api/asistente/administracion/servidor-local` y una tarjeta en el panel «Uso del asistente».
  - del servidor: turnos en curso y en espera, uso de KV cache y aciertos de la caché de prefijo, leídos de su `/metrics`;
  - de la compuerta del backend: llamadas en curso y en espera.
- **Streaming de la redacción** (`StreamingDeRedaccion`): `POST /api/asistente/consultas/flujo` responde con eventos SSE y el frontend muestra la respuesta a medida que se escribe. El endpoint de siempre no cambia.
- **Perfil RTX 3070**: `llama-server` con Qwen3-8B Q4_K_M y dos turnos en vuelo, para desarrollo en una PC con Windows o Linux, y la guía paso a paso.

## Capabilities

### New Capabilities

- `asistente-optimizaciones-modelo-local`: optimizaciones de prompt, de llamadas y de experiencia para correr el asistente en una GPU de consumo.

### Modified Capabilities

Ninguna en su comportamiento por defecto.

## Impact

- `backend/src/Modules.Asistente/`: opciones, renderizado del esquema, generador, carril SQL, redactor, capa conversacional, adaptadores (streaming), controllers.
- `frontend/src/features/asistente/`: tarjeta de telemetría, consumo del flujo SSE.
- `docs/architecture/api-contracts.md` (dos endpoints nuevos), `docs/architecture/modelo-local.md`, README del módulo, `infra/compose/`.
