## Context

El objetivo es correr el asistente contra un modelo propio en **una RTX 5070** (12 GB GDDR7, ~672 GB/s, Blackwell de consumo `sm_120`, tensor cores FP8/FP4) con un **Intel Core i9**, para **entre 2 y 30 usuarios**. La investigación completa, con fuentes, números y la configuración del servidor, está en [`docs/architecture/modelo-local.md`](../../../docs/architecture/modelo-local.md). Acá van sólo las decisiones.

Tres datos del código ordenan todo lo demás:

- **El prefijo estable de la generación pesa unos 12.000 tokens**: instrucciones, vocabulario cerrado y esquema. Medido en los 109 cassettes, el `cache_read` es de 12.056 y 12.211 tokens según el rol. La parte variable (fecha, ejemplos, consultas anteriores, pregunta) agrega entre 300 y 1.300. La salida mediana es de 132 tokens en la generación, 83 en la redacción y 23 en la reescritura.
- **Un turno hace entre 1 y 4 llamadas**: reescritura sólo en seguimientos, generación, reintento si la consulta volvió vacía, y redacción sólo si hubo filas.
- **El prefijo ya es estable byte a byte**: `RenderizadorDeEsquema` es determinista y sólo hay dos variantes, una por rol. Es exactamente lo que necesita la caché automática de prefijo de un servidor local.

## Goals / Non-Goals

**Goals**

- Que el asistente funcione con un servidor local OpenAI-compatible sin tocar el pipeline.
- Que 30 usuarios no puedan tirar abajo el servicio para todos: concurrencia acotada, cola con espera máxima y degradación por saturación separada de la caída.
- Reducir lo que cada turno le pide a la GPU sin cambiar el comportamiento con Anthropic.

**Non-Goals**

- **Elegir el modelo de forma definitiva.** Se recomienda uno (D1), pero la decisión final la toma el evaluador (`backend/eval`) corrido contra el servidor real. Eso no se puede hacer desde CI.
- **Achicar el esquema** (podado por pregunta, M-Schema). Rompe el prefijo cacheado y obliga a regrabar todo el corpus. Queda para un change propio, medido con el evaluador.
- Streaming SSE al navegador, plantillas en lugar del redactor, caché semántica de SQL. Están evaluados en el documento, pero cambian el contrato o el comportamiento visible.
- Telemetría del servidor (GPU, KV cache) en el panel de administración.

## Decisions

### D1 — Servidor: vLLM con Qwen3-8B-AWQ; llama-server como alternativa

**vLLM** (imagen oficial con CUDA 13, que incluye `sm_120`) sirviendo **Qwen3-8B-AWQ**, con estos ajustes:

- `--enable-prefix-caching` y `--kv-cache-dtype fp8`;
- `--max-model-len 16384`;
- `--max-num-seqs 8`;
- `--max-num-queued-reqs 16`;
- thinking apagado por defecto.

Motivos:

1. **vLLM comparte físicamente los bloques KV del prefijo entre requests concurrentes.** Los ~12k tokens del esquema ocupan VRAM **una sola vez**: con KV FP8 son ~72 KiB/token, unos 0,85 GiB. Cada turno en vuelo agrega sólo su parte única (~1–2,5k tokens, ~0,1–0,2 GiB).
   `llama-server`, en cambio, guarda una copia por slot. Con 8 slots de 16k y KV `q8_0` (~76 KiB/token) serían ~9,6 GiB sólo de KV, que **no entran** junto a los ~5 GB de pesos. Con un prefijo de 12k, esa diferencia decide cuántos usuarios atiende la tarjeta.
2. **Qwen3-8B es de atención pura (GQA).** Los modelos híbridos más nuevos (Qwen3.5-9B con Gated DeltaNet, Gemma con sliding window) rinden algo mejor en benchmarks generales, pero su prefix caching todavía es frágil:
   - en vLLM hay logits NaN tras un acierto de caché (issue #55766) y el modo `align` es experimental;
   - en llama.cpp hubo reproceso completo del prompt.

   Qwen3.5-9B queda como el **candidato A/B** a medir con el evaluador.

3. **AWQ (Marlin) funciona en `sm_120`.** NVFP4 en vLLM sobre GeForce todavía cae a kernels lentos (issue #47749). FP8 de pesos (~8,8 GB) deja muy poco KV.

`llama-server` (GGUF Q4_K_M, KV `q8_0`, `-np 4`) queda como **alternativa** para un host Windows sin WSL2 o si la imagen de vLLM falla con el driver.

Al usarlo hay que desactivar `--cache-idle-slots`. El issue #27148 está abierto: bajo carga, un slot puede restaurar el KV de otra conversación. Con datos de alcance por usuario, eso es una fuga.

### D2 — Adaptador `ProveedorLocal` contra `/v1/chat/completions`

**Una sola clase** sirve para vLLM, llama-server y SGLang, porque los tres hablan la misma API.

- **Mensajes.** `PrefijoEstable` va como mensaje `system` y `Mensaje` como `user`, en ese orden. Así la caché automática de prefijo acierta: el template de chat pone el sistema primero.
- **Temperatura.** Se manda. El puerto la conservó para esto.
- **Esfuerzo y thinking.** `chat_template_kwargs.enable_thinking` vale `false` salvo con esfuerzo `Alto` o `Maximo`. Para un modelo local, pensar multiplica la salida entre 5 y 20 veces, y en una GPU compartida eso es latencia para todos.
  - `Medio`, el default de la generación, **no** piensa.
  - Si el modelo igual emite `<think>…</think>`, el bloque se descarta del texto. Hubo bugs de servidores que no respetaban el flag.
- **Salida estructurada.** Si la solicitud trae `EsquemaDeSalidaJson`, se manda `response_format: {type: "json_schema", json_schema: {name, schema, strict: true}}`. vLLM la impone con xgrammar y llama-server la convierte a GBNF.
- **Tokens.**
  - `TokensDeEntrada` = `usage.prompt_tokens`, que ya incluye los cacheados.
  - `TokensDeCache` = `usage.prompt_tokens_details.cached_tokens` cuando el servidor lo informa. En vLLM requiere `--enable-prompt-tokens-details`.
  - `finish_reason == "length"` marca `SeQuedoSinTokens`.
- **Nombre.** `local/<modelo>`, con el mismo formato `proveedor/modelo` que ya parte `CalculadoraDeCosto`. Sin fila en `tabla_de_precios`, esas filas se informan como «sin precio», nunca como costo cero, y el tope organizacional no las cuenta. Es lo correcto: el costo de un modelo propio no es por token.
- **Fallas.** Se traducen igual que en `ProveedorAnthropic`:
  - 401/403 → credencial, no cuenta para el breaker;
  - 400/404/422 → armado, no cuenta;
  - el resto → `HttpRequestException`, que el breaker sí cuenta.
- **Transporte.** Usa el mismo `HttpClient` con nombre, así que el reintento de transporte sigue siendo el único que reintenta.
- **Clave.** Opcional: `ClaveDelProveedor` viaja como `Authorization: Bearer` sólo si está puesta. vLLM la exige con `--api-key`; un servidor en la red interna puede no exigirla.

### D3 — `EsquemaDeSalidaJson` en el puerto, opcional

Es una cadena con un JSON Schema, propiedad `init` no requerida.

- **La generación de SQL lo declara** con las claves que `GeneradorDeSql` ya interpreta, en el mismo orden que piden las instrucciones: `es_contestable`, `sql`, `razonamiento`, `categoria`, más `motivo` y `termino` opcionales.
- **Por qué en el puerto y no en el adaptador.** La forma de la salida es del pipeline, no del proveedor.
- **Anthropic lo ignora.** Su request no cambia en un byte, así que la clave de cassette es la misma y los 109 cassettes siguen sirviendo.
- **No reemplaza al `ValidadorDeSql`.** La gramática garantiza la forma, no que la consulta sea segura ni que respete el alcance.

### D4 — Compuerta de concurrencia, fuera del timeout y del breaker

La cadena pasa a ser:

```
techo del turno → compuerta → breaker + timeout → adaptador
```

- `ProveedorConCompuerta` toma un permiso de un `SemaphoreSlim` **singleton** antes de llamar y lo libera en `finally`. Con `MaximoDeLlamadasConcurrentes = 0`, el default, no se registra nada y la cadena queda igual que antes.
- **Por qué antes del breaker.** Así la espera en cola no consume el timeout de la llamada y una cola larga no se cuenta como fallo del proveedor. Hoy, con la GPU saturada, los pedidos esperan dentro del servidor y su espera sí entra en los 60 s de timeout. Cinco timeouts seguidos abren el breaker y el asistente se apaga 30 s para **todos**, aunque el servidor esté sano y sólo ocupado.
- **Saturación.** Si el permiso no llega en `EsperaMaximaEnColaSegundos`, la compuerta lanza `ProveedorSaturado`. El carril la resuelve como degradación con un texto propio («hay muchas consultas en curso, probá en unos segundos»), y el breaker no se entera.
- **El número correcto es igual a `--max-num-seqs` del servidor.** Así el servidor nunca encola y la cola real es la del backend, que tiene tope de espera y se cancela si el usuario se va.
- **Prioridad de turnos ya empezados.** Una llamada de un turno que ya hizo otra (la redacción después de la generación) toma el permiso antes que una llamada de un turno nuevo. Sin esto, con 30 usuarios los turnos nuevos le ganan a los que ya llevan segundos invertidos, y la latencia de cola se dispara. Se implementa con dos colas: el turno conoce sus propias llamadas por el contador del turno.

### D5 — La reescritura que falla no tira el turno

Hoy la llamada del reescritor está fuera del `try` del carril SQL. Si el proveedor falla en ese punto (timeout, saturación, breaker abierto), el turno termina en «excepción no prevista».

Ahora esas fallas resuelven usando **la pregunta cruda**, que es exactamente lo que el pipeline ya hace cuando no hay modelo («sin él la pregunta sigue cruda»).

### D6 — `ReintentarConsultaVacia`

Default `true`, que conserva el comportamiento de hoy. El reintento por consulta vacía vuelve a llamar con el **mismo** prompt:

- con Claude hay variación entre llamadas y a veces sirve;
- con un modelo local a temperatura 0 la salida es la misma y la llamada se tira.

El perfil local lo pone en `false`. No se cambia el prompt del reintento (por ejemplo, decirle que la consulta vino vacía) porque cambiaría la clave de cassette de los casos con reintento y obligaría a regrabar.

### D7 — Perfil de configuración local

Estos valores se documentan en `compose.llm.yml` y en el README. **No cambian los defaults del código**, que siguen pensados para Anthropic.

| Opción                                               | Local                         | Por qué                                                               |
| ---------------------------------------------------- | ----------------------------- | --------------------------------------------------------------------- |
| `Proveedor`                                          | `local`                       | Selecciona el adaptador                                               |
| `UrlDelProveedorLocal`                               | `http://llm:8000/v1`          | Servidor en la red de compose                                         |
| `Modelo`                                             | `qwen3-8b`                    | `--served-model-name`                                                 |
| `MaximoDeLlamadasConcurrentes`                       | `8`                           | = `--max-num-seqs`                                                    |
| `EsperaMaximaEnColaSegundos`                         | `45`                          | Margen dentro del presupuesto del turno                               |
| `MaximoDeTokensDeGeneracion`                         | `800`                         | p100 medido 1.033 _con_ thinking; sin thinking, la mediana es 132     |
| `MaximoDeTokensDeRedaccion`                          | `400`                         | Mediana 83, máximo 272                                                |
| `MaximoDeTokensDeReescritura`                        | `200`                         | Mediana 23, máximo 219                                                |
| `MaximoDeIntentosDeTransporte`                       | `2`                           | Reintentar un servidor local ocupado sólo agrega carga                |
| `TimeoutDeLlamadaSegundos`                           | `60`                          | Sin cambios: ahora sólo mide el servicio, no la cola                  |
| `PresupuestoDelTurnoSegundos`                        | `90`                          | **Debajo de los 100 s en que Cloudflare corta el origen (error 524)** |
| `ReintentarConsultaVacia`                            | `false`                       | D6                                                                    |
| `EsfuerzoDeGeneracion` / `Redaccion` / `Reescritura` | `medio` / `minimo` / `minimo` | Sin thinking en ninguno                                               |

Los techos de tokens importan más en local que en la nube. vLLM admite un pedido si cabe su `prompt + max_tokens` en el contexto, y un techo de 4000 sobre 13k de prompt empuja hacia el límite de 16k.

### D8 — Capacidad esperada

Estimada, a confirmar con el piloto descrito en el documento:

- **Un turno típico** (generación + redacción, ~210 tokens de salida, prefijo en caché):
  - 1–2 usuarios concurrentes: **~3–5 s**;
  - 8 en vuelo: **~7–12 s**, porque cada stream baja a ~35–45 tok/s.
- **Turno frío**: el primero después de arrancar o de un desalojo de caché tiene ~3–4 s más de prefill (12k tokens a ~3,5k tok/s). Se paga una vez por rol.
- **30 usuarios** con un turno por minuto cada uno son ~0,5 turnos/s, dentro del agregado estimado de ~300 tok/s. Con 30 usuarios disparando **a la vez**, la compuerta encola y los últimos esperan decenas de segundos. Si superan `EsperaMaximaEnColaSegundos`, reciben el texto de saturación en vez de un error.

## Risks / Trade-offs

- **Calidad menor que Claude.** En BIRD, un generalista de 8B queda 15–25 puntos debajo de Claude Sonnet. Con esquema fijo, ejemplos verificados y validador, la brecha esperada en preguntas frecuentes es menor, pero **sólo el evaluador lo puede afirmar**. La decisión de pasar producción a `local` queda supeditada a correrlo.
- **Bugs del ecosistema en `sm_120`.** Hay que fijar el tag de la imagen y validar antes de actualizar. Si KV FP8 + FlashInfer produce texto incoherente, se usa `--attention-backend TRITON_ATTN` o KV en BF16, con menos concurrencia.
- **La compuerta es por proceso.** Con dos réplicas del backend contra la misma GPU, el límite efectivo se duplica. Hoy hay una réplica por ambiente; si eso cambia, el límite hay que dividirlo.
- **El evaluador con cassettes sigue atado al formato de Anthropic** (`ClaveDeCassette` lee `system` de primer nivel). Contra un servidor local no hacen falta cassettes, porque cada llamada es gratis: el evaluador se corre con `DirectorioDeCassettes` vacío.

## Migration Plan

1. Levantar `compose.llm.yml` en el host con GPU y verificar `/health` y la salida de un prompt de prueba.
2. Correr el evaluador (`backend/eval`) con `Asistente__Proveedor=local` y comparar contra la línea de base de Claude.
3. Hacer el piloto de carga (c = 1, 4, 8, 12): medir el p95 del turno y el hit rate de prefijo con `vllm:prefix_cache_hits`.
4. Recién entonces cambiar `ASISTENTE_PROVEEDOR` del ambiente.

**Rollback:** volver la variable a `anthropic`.
