# Modelo propio del asistente: RTX 5070, 2 a 30 usuarios (y prueba en RTX 3070)

Investigación y dimensionamiento para correr el asistente contra un modelo propio en **una NVIDIA GeForce RTX 5070** (12 GB GDDR7, ~672 GB/s, Blackwell de consumo `sm_120`, tensor cores FP8/FP4) con un **Intel Core i9**, atendiendo **entre 2 y 30 usuarios**.

Las decisiones están en los changes [`asistente-proveedor-local`](../../openspec/changes/asistente-proveedor-local/design.md) y [`asistente-optimizaciones-modelo-local`](../../openspec/changes/asistente-optimizaciones-modelo-local/design.md). Este documento junta la evidencia que las sostiene, lo que se dejó para después y cómo probarlo en una RTX 3070 (§8).

> **Cómo leer los números.** La investigación se hizo el 2026-10-02 desde un entorno cuyo proxy bloqueaba HuggingFace, arXiv, la documentación de vLLM y la mayoría de los blogs. Por eso solo se leyeron completas las fuentes de GitHub (issues, PRs, READMEs). El resto proviene de extractos de búsqueda.
>
> Todo lo marcado **[estimado]** es una extrapolación propia y no una medición. **Ningún número de este documento reemplaza el piloto de la sección 7.**

## 1. Lo que el código le pide a la GPU

Medido sobre los 109 cassettes grabados contra Claude (`backend/tests/ArsDocendi.IntegrationTests/Cassettes`):

| Llamada           | Cuándo                                   | Prefijo estable                                                                 | Entrada total | Salida (mediana / máx.)        |
| ----------------- | ---------------------------------------- | ------------------------------------------------------------------------------- | ------------- | ------------------------------ |
| Reescritura       | Solo en seguimientos con historial       | ~350 tokens                                                                     | 694–733       | 23 / 219                       |
| Generación de SQL | Siempre que el turno llega al carril SQL | **~12.000 tokens** (instrucciones + vocabulario + esquema, 2 variantes por rol) | 12.294–13.399 | 132 / 1.033 (con razonamiento) |
| Reintento         | Consulta vacía de un actor global        | Idéntico a la generación                                                        | Idéntico      | —                              |
| Redacción         | Solo si hubo filas                       | ~190 tokens                                                                     | 363–898       | 83 / 272                       |

Tres consecuencias ordenan todo lo demás:

- **El prefijo de 12k es el costo dominante.** Sin caché de prefijo, cada generación procesa ~12,3k tokens. En una 5070 eso son unos 3–4 s solo de prefill **[estimado: ~3,5k tok/s de prefill con Qwen3-8B Q4 a 4k de contexto]**. Con caché, el servidor procesa solo la parte variable, de ~0,3–1,3k tokens.
- **El prefijo ya es estable byte a byte.** `RenderizadorDeEsquema` es determinista, lo variable va en el mensaje de usuario y solo hay dos variantes (rol básico y rol con datos personales). Es exactamente lo que la caché automática de prefijo necesita.
- **Las salidas son cortas.** Sin razonamiento, un turno típico genera ~210 tokens. El cuello de botella es el **ancho de banda de memoria en el decode** compartido entre turnos, no el cómputo.

## 2. Servidor de inferencia

### Estado en `sm_120` (octubre de 2026)

| Motor                          | Estado                                                                                                                                                                                                            | Para este caso                                                                                                              |
| ------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| **vLLM**                       | Funciona con la imagen oficial (CUDA 13, `TORCH_CUDA_ARCH_LIST` incluye 12.0). AWQ/GPTQ vía Marlin y FP8 W8A8 tienen kernels `sm_120`. NVFP4 sobre GeForce todavía cae a Marlin W4A16, que es más lento (#47749). | **Elegido.** Prefix caching con bloques compartidos entre requests, batching continuo, chunked prefill, métricas Prometheus |
| **llama.cpp** (`llama-server`) | Es el más maduro en `sm_120`; tiene NVFP4 nativo desde el build b8967. Binarios para Windows y Linux.                                                                                                             | **Alternativa.** Guarda una copia del KV del prefijo por slot (ver §4)                                                      |
| SGLang                         | Funcional, con bugs abiertos de FlashInfer/FA4 en `sm_120` (#42012). Solo Linux.                                                                                                                                  | Viable, sin validar en 12 GB                                                                                                |
| Ollama                         | Sobre llama.cpp. Bug abierto en Windows con RTX 50 que detecta 0 B de VRAM (#18581). Menor control de slots y caché.                                                                                              | Descartado                                                                                                                  |
| TensorRT-LLM, LMDeploy         | Soporte `sm_120` parcial; hay que construir un engine por modelo.                                                                                                                                                 | Descartados                                                                                                                 |

### Por qué vLLM y no llama-server

La razón es una sola, y la da el prefijo de 12k.

- **vLLM** guarda el KV de los bloques comunes **una vez** y lo comparte entre todos los requests en vuelo (_automatic prefix caching_).
- **llama-server** reutiliza el prompt **dentro de cada slot**: N slots en paralelo guardan N copias del prefijo.

Con 12k tokens de prefijo, esa diferencia define cuántos turnos entran en 12 GB (§4).

**Windows o Linux:** producción en **Linux nativo** (Ubuntu 24.04, driver NVIDIA ≥ 580 para CUDA 13). vLLM en WSL2 funciona con reparos: hay que desactivar `pin_memory` y hubo cuelgues en la captura de CUDA graphs (#37242, #58849). Si el host tiene que ser Windows, la alternativa es `llama-server` nativo.

### Funciones que importan, por peso

1. **Caché de prefijo compartida.** Con un hit, el TTFT de una generación baja de segundos a ~0,2–0,5 s **[estimado]**.
2. **KV cache en FP8** (`--kv-cache-dtype fp8`): duplica la capacidad con pérdida despreciable (blog de vLLM, abr-2026: >98 % de recuperación en Qwen3).
3. **`max-num-seqs` dimensionado y una cola acotada**, que en este sistema es la compuerta del backend.
4. **CUDA graphs encendidos.** `--enforce-eager` es ~8× más lento (#37242).
5. **Speculative decoding: secundario.** EAGLE-3 rinde con batch 1 y pierde la ganancia al crecer el batch. El _n-gram / prompt lookup_ puede servir para SQL porque copia identificadores del esquema. Vale probarlo después del piloto.

## 3. Modelo

### Candidatos que entran en 12 GB

| Modelo                            | Arquitectura                            | Pros                                                                                           | Contras                                                                                                                                                             |
| --------------------------------- | --------------------------------------- | ---------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Qwen3-8B** (AWQ 4-bit)          | Transformer, GQA (36 capas, 8 KV heads) | Atención pura: prefix caching estable en vLLM. Apache 2.0, buen español. Thinking desactivable | En BIRD queda debajo de los especialistas (~46 % en una evaluación de protocolo dudoso)                                                                             |
| **Qwen3.5-9B** (mar-2026)         | Híbrido Gated DeltaNet 3:1              | El generalista más fuerte de ≤10B, con KV chico (8 capas de atención)                          | **Prefix caching frágil en híbridos**: logits NaN tras un hit en vLLM 0.28 (#55766), corrupción con MTP+APC (#53912), modo `align` experimental. Sin BIRD publicado |
| XiYanSQL-QwenCoder-7B-2504        | Qwen2.5-Coder                           | El mejor 7B en BIRD con soporte de PostgreSQL (62 % con M-Schema)                              | Prompt pensado en chino/M-Schema; no redacta bien en español                                                                                                        |
| Arctic-Text2SQL-R1-7B, OmniSQL-7B | Qwen2.5-Coder + RL                      | BIRD 64–69 %                                                                                   | Razonan largo antes del SQL (latencia) y están entrenados sobre SQLite: pierden mucho en PostgreSQL                                                                 |
| Gemma 4 12B                       | Denso, sliding window                   | Multilingüe                                                                                    | Entra justo con KV; en llama.cpp la ventana deslizante impide reutilizar el prefijo sin `--swa-full`                                                                |
| Qwen3-14B / Phi-4                 | —                                       | —                                                                                              | **No dejan KV en 12 GB** con vLLM. Phi-4 además no es multilingüe                                                                                                   |

**Recomendación:** **Qwen3-8B-AWQ** con thinking apagado. **Qwen3.5-9B** queda como candidato A/B, a medir con el evaluador cuando el prefix caching de los híbridos se estabilice. Para fine-tuning a mediano plazo hay evidencia de que ~800 ejemplos propios verificados llevan a Qwen3-8B a 69 % en BIRD (LIMIT, sep-2026).

### Calidad esperada frente a Claude

- **En BIRD**, un generalista de 7–9B queda **15–25 puntos debajo** de Claude Sonnet 4.5, que obtiene 65 % sin reflexión.
- **En este sistema**, con esquema fijo y comentado, ejemplos verificados, vocabulario cerrado y validador, la brecha en preguntas frecuentes debería achicarse **[estimado]**.
- **En preguntas difíciles** (joins de 4 o más tablas, agregaciones anidadas) es esperable una brecha de 10 puntos o más.

No hay benchmark de text-to-SQL en español útil para modelos chicos: **la única medición válida es el evaluador del repo** (`backend/eval`) corrido contra el servidor real.

### Thinking: apagado

En Qwen3 el modo de razonamiento multiplica la salida por 5–20. El adaptador manda `enable_thinking: false` salvo con esfuerzo `alto`/`maximo`, y el servidor arranca con ese default (`--default-chat-template-kwargs`).

Hubo versiones que no respetaban el flag (vLLM #35574, #37794). Por eso el adaptador también descarta cualquier bloque `<think>…</think>` que se cuele.

## 4. Presupuesto de VRAM

**Fórmula:**

> KV por token = 2 × capas × KV heads × head_dim × bytes por elemento

Para Qwen3-8B (36 / 8 / 128):

| Precisión del KV | Por token | 16k tokens |
| ---------------- | --------- | ---------- |
| BF16             | 144 KiB   | 2,25 GiB   |
| FP8              | 72 KiB    | 1,13 GiB   |

| Configuración                                                    | KV disponible | Capacidad para este workload                                                                                                                                                                                                   |
| ---------------------------------------------------------------- | ------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **vLLM, Qwen3-8B-AWQ (~5,9 GB), util 0,90, KV FP8**              | ~3,5–4 GiB    | Cada prefijo cuesta **una vez** ~0,85 GiB (12,3k × 72 KiB), y hay a lo sumo dos variantes. Cada turno en vuelo suma su parte única, 1–2,5k tokens (~0,1–0,2 GiB). **8 en vuelo usan ~2,5–3 GiB: entran con margen [estimado]** |
| llama-server, Qwen3-8B Q4_K_M (~5 GB), KV `q8_0` (~76 KiB/token) | ~5–5,5 GiB    | Cada slot copia el prefijo: **8 slots de 16k serían ~9,6 GiB y no entran**. Entran **4 slots de 16k** (~4,8 GiB)                                                                                                               |
| vLLM, Qwen3-8B FP8 de pesos (~8,8 GB)                            | ~1,5 GiB      | Dos o tres turnos. No conviene                                                                                                                                                                                                 |
| vLLM, Qwen3-14B-AWQ (~9,5 GB)                                    | ≤1 GiB        | Inviable                                                                                                                                                                                                                       |

**La GPU no debe manejar el monitor:** el escritorio come entre 0,3 y 1 GB de los 12.

## 5. Capacidad para 2–30 usuarios [estimado]

Velocidades de referencia en una 5070:

- **Un stream, llama.cpp:** Qwen3-8B Q4_K da ~86 tok/s de decode a 4k de contexto y ~59 tok/s a 16k.
- **Concurrencia, vLLM sobre Blackwell de consumo:** arXiv 2601.09527 midió 211 TPS agregados con TTFT de 361 ms (5070 Ti, Qwen3-8B NVFP4, concurrencia 8, RAG de 8k).
- **Extrapolación:** la 5070 tiene ~75 % del ancho de banda de la 5070 Ti.

| Carga                                             | Latencia de un turno (generación + redacción, prefijo en caché)                                                                         |
| ------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------- |
| 1–2 en vuelo                                      | ~3–5 s                                                                                                                                  |
| ~8 en vuelo                                       | ~7–12 s (cada stream baja a ~35–45 tok/s)                                                                                               |
| Turno frío (después de arrancar o de un desalojo) | +3–4 s de prefill del prefijo; se paga una vez por variante                                                                             |
| 30 usuarios, un turno por minuto cada uno         | ~0,5 turnos/s, dentro del agregado estimado (~250–350 tok/s de decode con 8 en vuelo)                                                   |
| 30 usuarios disparando **a la vez**               | La compuerta deja 8 en curso y encola el resto: los últimos esperan decenas de segundos y, pasados 45 s, reciben el texto de saturación |

Los techos de tokens importan más en local que en la nube. El servidor reserva KV según `prompt + max_tokens`, y un techo de 4000 sobre un prompt de 13k empuja contra el contexto de 16k. El perfil local los baja a 800 / 400 / 200, con margen sobre las salidas medidas sin razonamiento.

## 6. Qué se aplicó y qué no

### Aplicado en `asistente-proveedor-local`

| Técnica                                   | Dónde                                                                   | Efecto                                                                                                                                           |
| ----------------------------------------- | ----------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------ |
| Adaptador OpenAI-compatible               | `ProveedorLocal`                                                        | Corre contra vLLM, llama-server o SGLang sin tocar el pipeline                                                                                   |
| Prefijo estable primero, como `system`    | `ProveedorLocal.Cuerpo`                                                 | Aprovecha la caché automática de prefijo                                                                                                         |
| Tokens de caché informados                | `cached_tokens` / `timings.cache_n` → `tokens_de_cache`                 | El hit rate se ve en el registro operativo                                                                                                       |
| Thinking apagado salvo esfuerzo alto      | Adaptador + flag del servidor                                           | Salidas 5–20× más cortas                                                                                                                         |
| Decodificación restringida                | `EsquemaDeSalidaJson` de la generación → `response_format: json_schema` | Sin JSON roto: un objeto ilegible era una abstención. Anthropic lo ignora y los cassettes siguen valiendo                                        |
| Compuerta de concurrencia con prioridad   | `CompuertaDelModelo`, `ProveedorConCompuerta`                           | La GPU nunca recibe más de `max-num-seqs`. La espera en cola no cuenta para el timeout ni para el breaker. Los turnos empezados terminan primero |
| Saturación distinta de caída              | `ProveedorSaturado`, `TextoProveedorSaturado`                           | «Probá en unos segundos» en lugar de «más tarde», sin abrir el breaker                                                                           |
| La reescritura que falla no tira el turno | `CapaConversacional`                                                    | Antes era una excepción no prevista                                                                                                              |
| Reintento por vacío apagable              | `ReintentarConsultaVacia`                                               | A temperatura 0 repetía la misma SQL: una llamada menos                                                                                          |
| Techos de tokens y timeouts del perfil    | `compose.asistente-local.yml`                                           | Menos KV reservado. El presupuesto del turno (90 s) queda debajo del corte de 100 s de Cloudflare                                                |
| Servidor compartido                       | `compose.llm.yml`                                                       | vLLM con AWQ, KV FP8, prefix caching y chunked prefill. Alternativa `llama-server` con la caché de slots ociosos apagada por privacidad (#27148) |

### Aplicado en `asistente-optimizaciones-modelo-local`

Todo opt-in: con los defaults el prompt de Claude y los cassettes no cambian. Los perfiles de `infra/compose/` y la guía de la §8 las prenden; las opciones están en el [README del módulo](../../backend/src/Modules.Asistente/README.md#optimizaciones-para-un-modelo-propio).

| Técnica                                      | Opción                              | Efecto esperado                                                                                        |
| -------------------------------------------- | ----------------------------------- | ------------------------------------------------------------------------------------------------------ |
| Esquema compacto                             | `EsquemaCompacto`                   | ~10 % menos prefijo con la misma información: tipos abreviados, `?` para nulables, FK en línea         |
| Ejemplos verificados en el prefijo           | `EjemplosEnElPrefijo`               | Prompt estable y cacheable. Conviene con vLLM; con llama-server cada slot paga la copia                |
| Reintento por vacío con contexto             | `ReintentoConContexto`              | El reintento ya no repite el prompt idéntico: dice qué consulta no trajo filas                         |
| Una ronda de reparación con el error saneado | `RepararConsultaFallida`            | +3–10 puntos en modelos de 7B según la literatura. Ningún literal ajeno a la consulta llega al modelo  |
| Plantillas para resultados triviales         | `RedaccionConPlantillas`            | Una llamada menos cuando el resultado es un valor o una lista corta sin matices                        |
| Caché de SQL por pregunta + rol + día        | `VigenciaDeCacheDeConsultasMinutos` | Cachea la consulta y no las filas: se re-ejecuta siempre bajo RLS                                      |
| Reescritura dentro de la generación          | `ReescrituraEnLaGeneracion`         | Una llamada menos por seguimiento. El modelo devuelve `pregunta_interpretada` junto con la SQL         |
| Streaming SSE de la redacción                | `StreamingDeRedaccion`              | Las primeras palabras apenas termina el prefill de la redacción, en vez de esperar la respuesta entera |
| Telemetría del servidor en el panel          | —                                   | Tarjeta «Servidor del modelo»: en curso, en espera, KV cache, aciertos de prefijo y la compuerta       |

### Evaluado y dejado para después

| Técnica                                                  | Por qué no                                                                                                                                                                                                                                                                             |
| -------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Podar el esquema por pregunta (schema linking, M-Schema) | Es el mayor ahorro de prefill y KV, pero **la literatura es mixta**: en esquemas de tamaño institucional, el schema linking empeoró a modelos de 7–32B (nl2sql-onprem-bench). Además rompe el prefijo estable que la caché necesita. Necesita su propio change medido con el evaluador |
| Self-consistency / votación                              | +0,13 puntos con p95 de 10,6 a 50 s en el benchmark on-prem. **No**                                                                                                                                                                                                                    |

## 7. Piloto antes de pasar un ambiente a `local`

1. **Levantar el servidor y verificar que responde:**
   ```bash
   docker compose -p llm -f infra/compose/compose.llm.yml up -d
   ```
   Verificar `GET /health` y una completación de prueba.
2. **Correr el evaluador** con `Asistente__Proveedor=local` y `Asistente__DirectorioDeCassettes` vacío, ya que los cassettes asumen el formato de Anthropic. Comparar contra la línea de base de Claude: **la métrica es la diferencia, no el valor absoluto**.
3. **Prueba de carga** con prompts reales a concurrencia 1, 4, 8 y 12. Medir:
   - p50 y p95 del turno;
   - `vllm:time_to_first_token_seconds`;
   - hit rate de prefijo (`vllm:prefix_cache_hits` / `queries`, objetivo ≥ 80 %);
   - `kv_cache_usage_perc`.

   Ajustar `--max-num-seqs` y `ASISTENTE_MAX_LLAMADAS_CONCURRENTES` **juntos**.

4. **Si con KV FP8 aparece texto incoherente** (bug de FlashInfer en `sm_120`, #41651), probar `--attention-backend TRITON_ATTN` o KV en BF16, con menos concurrencia.
5. **Recién entonces** cambiar `ASISTENTE_PROVEEDOR=local` en el ambiente. **Rollback:** volver a `anthropic`.

## 8. Probar en una RTX 3070

Para probar el asistente contra un modelo propio en una PC de desarrollo con **RTX 3070** (8 GB GDDR6, ~448 GB/s, Ampere `sm_86`). No es un perfil de producción: es una sola persona probando, y la 3070 tiene dos tercios de la memoria de la 5070.

### Qué cambia respecto de la 5070

| Decisión               | 5070 (ambientes)                      | 3070 (prueba local)                                                          |
| ---------------------- | ------------------------------------- | ---------------------------------------------------------------------------- |
| Servidor               | vLLM, Qwen3-8B-AWQ, KV FP8            | **llama-server**, Qwen3-8B Q4_K_M, KV `q8_0`. vLLM deja muy poco KV con 8 GB |
| Turnos en vuelo        | 8                                     | **2** slots de 13.312 tokens (`--ctx-size 26624 --parallel 2`)               |
| Ejemplos en el prefijo | Sí (vLLM comparte el prefijo)         | **No**: llama-server copia el prefijo por slot y no hay lugar                |
| Techos de tokens       | 800 / 400 / 200                       | **600 / 300 / 150**                                                          |
| Presupuesto del turno  | 90 s (debajo del corte de Cloudflare) | 120 s (no hay Cloudflare en el medio)                                        |

**VRAM [estimado]:** pesos ~4,7 GiB + KV de 2 slots ~1,9 GiB (13.312 × 2 × ~76,5 KiB) + buffers ~0,6 GiB ≈ **7,3 GiB de 8**. Con el monitor conectado a la misma GPU el escritorio come 0,3–1 GB, así que puede no entrar.

### Pasos

1. **Servidor.** Docker Desktop con WSL2 (Windows) o `nvidia-container-toolkit` (Linux), y driver NVIDIA reciente:

   ```bash
   docker compose -p llm-3070 -f infra/compose/compose.llm-3070.yml up -d
   docker compose -p llm-3070 -f infra/compose/compose.llm-3070.yml logs -f   # la primera vez baja ~5 GB
   curl -s http://localhost:8000/health
   curl -s http://localhost:8000/v1/models -H "Authorization: Bearer local-3070"
   ```

   Sin Docker, el mismo servidor nativo (binario de [releases de llama.cpp](https://github.com/ggml-org/llama.cpp/releases) con CUDA):

   ```bash
   llama-server --hf-repo Qwen/Qwen3-8B-GGUF --hf-file Qwen3-8B-Q4_K_M.gguf --alias qwen3-8b \
     --n-gpu-layers 99 --ctx-size 26624 --parallel 2 --flash-attn on \
     --cache-type-k q8_0 --cache-type-v q8_0 --jinja --reasoning-budget 0 \
     --metrics --cache-ram 0 --api-key local-3070 --host 127.0.0.1 --port 8000
   ```

2. **Backend.** En el `.env` de la raíz, descomentar el bloque «Modelo propio en una RTX 3070» de `.env.example` (reemplaza `Asistente__Proveedor` y `Asistente__ClaveDelProveedor`). El Host lo lee sólo en Development. Después, el resto como siempre (README del módulo, «Levantar el asistente entero en local»):

   ```bash
   dotnet run --project backend/src/ArsDocendi.Host
   pnpm --filter frontend dev
   ```

3. **Verificar.**
   - En Sistema → Asistente aparece la tarjeta **«Servidor del modelo · llama.cpp»** con los slots en curso y el uso de KV. Si dice que no responde, revisar `UrlDelProveedorLocal` (debe terminar en `/v1`) y la clave.
   - Una pregunta simple («¿cuántos docentes hay?») responde y la redacción aparece mientras se escribe.
   - En los logs de llama-server, la segunda pregunta del mismo rol debe reusar el prefijo (`n_past` alto, prompt procesado chico). La primera paga ~11k tokens de prefill **[estimado: 3–5 s en una 3070]**.
   - En el registro operativo, `tokens_de_cache` de la generación debe ser casi todo el prompt a partir del segundo turno.

### Si algo falla

| Síntoma                                                                      | Causa probable                                         | Qué hacer                                                                                                           |
| ---------------------------------------------------------------------------- | ------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------- |
| El servidor no arranca: `out of memory` / `failed to allocate`               | No entran pesos + KV                                   | `LLAMA_SLOTS=1 LLAMA_CTX=16384` (y `Asistente__MaximoDeLlamadasConcurrentes=1`), o desconectar el monitor de la GPU |
| El turno degrada y el log del servidor dice que el pedido excede el contexto | El prefijo no entra en un slot de 13.312               | Mismo respaldo: un slot de 16k. Confirmar `Asistente__EsquemaCompacto=true` y `EjemplosEnElPrefijo=false`           |
| Respuestas pobres o abstenciones de más                                      | Un 8B en 4 bits es más débil que Claude                | Esperado. Medir con el evaluador (§7) antes de sacar conclusiones, y probar las opciones de a una                   |
| Todo anda pero la redacción aparece de golpe                                 | Un proxy en el medio bufferea `text/event-stream`      | Con el proxy de Vite no pasa; revisar que no haya otro                                                              |
| `401` en la tarjeta o en el turno                                            | `Asistente__ClaveDelProveedor` distinta de `--api-key` | Igualarlas                                                                                                          |

**Respaldo de modelo:** Qwen3-4B-Instruct-2507 en Q6_K (~3,3 GB) deja lugar para 2 slots de 16k con holgura, a costa de calidad. Se cambia con `LLAMA_HF_REPO` y `LLAMA_GGUF`, y `Asistente__Modelo` no necesita cambiar si se mantiene el `--alias`.

## Fuentes

**Motores y `sm_120`**

- vLLM:
  - Dockerfile: https://github.com/vllm-project/vllm/blob/main/docker/Dockerfile
  - Prefix caching: https://github.com/vllm-project/vllm/blob/main/docs/features/automatic_prefix_caching.md
  - Structured outputs: https://github.com/vllm-project/vllm/blob/main/docs/features/structured_outputs.md
  - Métricas: https://docs.vllm.ai/en/stable/usage/metrics/
  - KV FP8: https://vllm.ai/blog/2026-04-22-fp8-kvcache
- Issues de vLLM:
  - #47749 (NVFP4 en GeForce)
  - #41651 (FlashInfer + KV FP8 en `sm_120`)
  - #37242 (5090 y WSL2: CUDA graphs, FP8)
  - #58849 (pin memory en WSL2)
  - #45238 y #37554 (caché en modelos híbridos)
  - #55766 y #53912 (Qwen3.5: NaN y MTP+APC)
  - #58192 (cola con 503)
  - #35574 y #37794 (thinking que no se apaga)
- llama.cpp:
  - README del server: https://github.com/ggml-org/llama.cpp/blob/master/tools/server/README.md
  - Benchmarks en RTX 50: https://github.com/ggml-org/llama.cpp/discussions/15013
  - Escalado con batch: https://github.com/ggml-org/llama.cpp/discussions/18030
  - #27148 (caché de slots ociosos que restaura KV ajeno)
  - #22384 (reproceso en híbridos)
- SGLang: https://github.com/sgl-project/sglang/issues/19637 · https://github.com/sgl-project/sglang/issues/42012
- Kernels de Blackwell de consumo: https://github.com/lna-lab/blackwell-geforce-nvfp4-gemm
- Ollama: https://github.com/ollama/ollama/issues/18581

**Benchmarks**

- Inferencia privada en Blackwell de consumo: https://arxiv.org/abs/2601.09527
- Qwen3-8B y 14B en RTX 5070: https://smeltcore.com/recipes/qwen3-8b-on-rtx-5070-q4-k-m-gguf-via-ollama-or-llama-cpp/ · https://smeltcore.com/recipes/qwen3-14b-on-rtx-5070-q4-k-m-gguf-via-ollama-or-llama-cpp
- Comparaciones llama.cpp vs vLLM: https://developers.redhat.com/articles/2026/06/15/llamacpp-vs-vllm-choosing-right-local-llm-inference-engine · https://llmkube.com/blog/qwen3-6-27b-bakeoff

**Modelos y text-to-SQL**

- Qwen: https://huggingface.co/Qwen/Qwen3-8B · https://qwen.readthedocs.io/en/latest/deployment/vllm.html · https://huggingface.co/Qwen/Qwen3.5-9B
- Especialistas en SQL:
  - OmniSQL: https://github.com/RUCKBReasoning/OmniSQL
  - XiYanSQL-QwenCoder: https://github.com/XGenerationLab/XiYanSQL-QwenCoder
  - Arctic-Text2SQL-R1: https://arxiv.org/pdf/2505.20315
  - LIMIT: https://arxiv.org/html/2609.24186
- Benchmarks y calidad de los benchmarks:
  - nl2sql-onprem-bench: https://github.com/beskvladimir-create/nl2sql-onprem-bench
  - Reflect-SQL (Claude en BIRD): https://arxiv.org/pdf/2609.02944
  - Errores de anotación en BIRD/Spider: https://www.vldb.org/cidrdb/papers/2026/p5-jin.pdf
- Técnicas:
  - Decodificación restringida (XGrammar): https://arxiv.org/pdf/2411.15100
  - Selección de ejemplos (DAIL-SQL): https://github.com/BeachWang/DAIL-SQL
  - Reescritura vs historial completo: https://www.mdpi.com/1999-5903/17/11/527
  - Prefix caching con tráfico multi-tenant: https://dev.to/marcuswwchen/prefix-caching-in-vllm-under-multi-tenant-agent-traffic-5e2j
