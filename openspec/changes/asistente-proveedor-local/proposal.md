## Why

El asistente sólo puede hablar con Anthropic. El Departamento necesita poder correrlo contra un **modelo propio en una sola máquina**: una RTX 5070 (12 GB de VRAM, Blackwell `sm_120`) y un Intel Core i9, para entre 2 y 30 usuarios. La migración estaba prevista desde el principio (el puerto `IProveedorDeModelo` existe por eso, y `asistente-turno-exclusivo-del-actor` y `asistente-rechazos-dinamicos` ya nombran este change), pero nunca se escribió.

Con el código actual no alcanza con apuntar a otro servidor. La investigación de `docs/architecture/modelo-local.md` encontró cinco problemas:

1. **No hay adaptador OpenAI-compatible.** Es la API que exponen vLLM, llama.cpp (`llama-server`) y SGLang.
2. **No hay límite global de llamadas concurrentes.** Con N usuarios salen N pedidos a la GPU. Cuando la GPU se satura, los timeouts **abren el breaker para todo el proceso** y el asistente se apaga 30 s para todos.
3. **El reintento por consulta vacía repite el prompt byte a byte.** Con temperatura 0 en un modelo local devuelve la misma SQL: es una llamada entera tirada.
4. **La salida de la generación no tiene forma garantizada.** Un modelo de 8B rompe el JSON más seguido que Claude, y cada objeto roto termina en una abstención.
5. **Los techos de tokens están pensados para modelos que razonan** (4000/2000/1000). En un servidor local, el techo de cada pedido reserva KV cache en una GPU de 12 GB.

## What Changes

- **Adaptador `ProveedorLocal`** (`Asistente__Proveedor=local`) contra cualquier servidor OpenAI-compatible (`/v1/chat/completions`), configurado con `Asistente__UrlDelProveedorLocal`. El adaptador:
  - manda la temperatura;
  - apaga el razonamiento (`enable_thinking: false`) salvo con esfuerzo `alto`/`maximo`;
  - pide salida JSON con esquema cuando la llamada lo declara;
  - informa los tokens servidos desde la caché de prefijo;
  - traduce las fallas al vocabulario del módulo, igual que `ProveedorAnthropic`.
- **El puerto suma `EsquemaDeSalidaJson`**, opcional: la generación de SQL declara la forma de su objeto. El adaptador local la impone con decodificación restringida; el de Anthropic la ignora y su request no cambia en un byte, así que los cassettes siguen valiendo.
- **Compuerta de concurrencia** (`MaximoDeLlamadasConcurrentes`, `EsperaMaximaEnColaSegundos`): un decorador nuevo en la cadena del proveedor limita las llamadas simultáneas al modelo y encola las demás. La espera en cola **no cuenta** contra el timeout de la llamada ni contra el breaker. Si la cola no se libera a tiempo, el turno degrada como saturación, no como proveedor caído. Default `0`, sin límite: Anthropic no cambia.
- **`ReintentarConsultaVacia`** (default `true`): permite apagar el reintento que repite el prompt idéntico.
- **Infraestructura**: `infra/compose/compose.llm.yml` con vLLM sirviendo Qwen3-8B-AWQ (prefix caching, KV FP8, chunked prefill) y un perfil alternativo con `llama-server`, más el perfil del backend que `spin-up.sh` suma con `ASISTENTE_PROVEEDOR=local`.
- **Documentación**: `docs/architecture/modelo-local.md` (investigación, dimensionamiento, configuración recomendada y fuentes), tablas de configuración del README del módulo e `infrastructure.md`.

## Capabilities

### New Capabilities

- `asistente-proveedor-local`: el asistente funciona contra un modelo propio servido por un servidor OpenAI-compatible, con concurrencia acotada y salida estructurada.

### Modified Capabilities

Ninguna en su comportamiento por defecto: con `Proveedor` en `simulado` o `anthropic` y las opciones nuevas en sus defaults, todo queda como estaba.

## Impact

- `backend/src/Modules.Asistente/`: `Application/Modelo` (puerto y falla nueva), `Infrastructure` (adaptador y compuerta), `Configuracion` (opciones y validación), `ModuleExtensions` (composición), `CarrilSql` (degradación por saturación y reintento opcional), `GeneradorDeSql` (esquema de salida).
- `infra/compose/` (`compose.llm.yml`, `compose.asistente-local.yml`) e `infra/scripts/spin-up.sh`.
- `docs/architecture/modelo-local.md` (nuevo), `docs/architecture/infrastructure.md`, `backend/src/Modules.Asistente/README.md`.
- Tests: adaptador contra un transporte falso, compuerta, opciones y composición.
