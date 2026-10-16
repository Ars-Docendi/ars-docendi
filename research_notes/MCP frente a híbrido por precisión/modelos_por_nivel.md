# Modelos por nivel de despliegue (RTX 3070 8 GB, RTX 5070 12 GB, API de Claude) para (A) agente con tools REST/MCP multi-paso y (B) híbrido intents → tools certificadas → Text-to-SQL con abstención

Fecha de corte: 2026-10-08. Complementa (sin repetir) `Alternativas a Text to SQL local/modelos_locales_tool_calling.md` y `serving_concurrencia.md`; donde una cifra ya estaba allí, se referencia en una línea y se agrega lo nuevo.

Convenciones de etiquetado:
- **[oficial/autoreportado]** = cifra del fabricante (model card, anuncio, system card).
- **[independiente]** = medido por terceros con metodología pública.
- **[agregador]** = leaderboard/blog que recopila cifras de otros.
- **[sólo resumen de buscador]** = el sitio estaba bloqueado por el proxy (arxiv.org, benchlm.ai, artificialanalysis.ai, smeltcore.com, leaderboard.steel.dev, aimultiple.com, datacamp.com) y la cifra se tomó del resumen/snippet del buscador; verificar antes de citar en un informe final.
- **[estimación]** = cálculo propio a partir de cifras citadas.

Línea de base del repo (no se re-midió): Qwen3-8B Q4_K_M en la 3070 vía llama-server, Text-to-SQL 26/34 en capacidad con 7 respuestas falsas, p50 2,4–2,9 s, 7,4 GiB de VRAM; Claude `claude-sonnet-5` ≈30/32 en un dataset anterior (el README dice 31/32 y los JSON 30/32) — [reports/Alternativas a Text to SQL local.md](../../reports/Alternativas%20a%20Text%20to%20SQL%20local.md).

---

## 1. Lineup actual de Claude (IDs, precios, features relevantes para tool use, retención/ZDR, latencia)

### Takeaway
A octubre de 2026 el lineup vigente es Claude Fable 5.1, Opus 5.5, Sonnet 5.5 y Haiku 5.5 (todos 1M de contexto, 128K de salida, tool use y "multilingual"). `claude-sonnet-5` (el modelo de la línea de base del repo) ya es "legacy" pero sigue disponible. Para un chatbot con PII, lo decisivo es que Opus 5.5, Sonnet 5.5 y Haiku 5.5 son elegibles para ZDR, mientras que Fable 5.x **exige 30 días de retención** (no ZDR salvo autorización expresa). Dos cambios de API afectan el diseño del híbrido: en Opus 5.5 y Sonnet 5.5 el `tool_choice` forzado (`any`/`tool`) devuelve 400 (hay que usar `auto` + `strict: true`, o structured outputs), y los JSON Schemas de `strict`/structured outputs se cachean hasta 24 h fuera de la protección ZDR, por lo que no deben contener PII (p. ej. `enum` con nombres de docentes).

### Cited Findings
- Lineup y precios [oficial]: Fable 5.1 `claude-fable-5-1` $10/$50 por MTok, latencia "Slower", effort default `high`; Opus 5.5 `claude-opus-5-5` $4/$20, "Moderate", effort default `medium`, thinking adaptativo siempre activo; Sonnet 5.5 `claude-sonnet-5-5` $2/$10, "Fast", default `high`; Haiku 5.5 `claude-haiku-5-5` desde $0,10/$0,50, "Fastest", default `medium`. Todos: 1M de contexto, 128K de salida, corte de conocimiento confiable jun-2026. Legacy aún disponibles: Fable 5, Opus 5, Opus 4.8/4.7/4.6/4.5, **Sonnet 5**, Sonnet 4.6, Haiku 4.5. Cache reads: 10% del input base (2,5% en Fable 5.1; 5% en Opus 5.5 y Sonnet 5.5); Batch −50%. — [Models overview (platform.claude.com)](https://platform.claude.com/docs/en/about-claude/models/overview)
- Fechas de lanzamiento [oficial]: Opus 5.5 el 2026-09-22; Haiku 5.5 el 2026-10-07; Sonnet 5.5 el 2026-09-28 (esta última sólo por cobertura de prensa). — [Anthropic, Introducing Claude Opus 5.5](https://www.anthropic.com/claude-opus-5-5); [Anthropic, Introducing Claude Haiku 5.5](https://www.anthropic.com/claude-haiku-5-5); [OfficeChai Sonnet 5.5](https://officechai.com/ai/claude-sonnet-5-5-benchmarks/)
- Haiku 5.5 [oficial]: $0,10/$0,50 por MTok con prompts ≤100K tokens y $0,50/$2,50 por encima; cache reads $0,01; "el modelo más rápido que Anthropic lanzó" a velocidad estándar; recomendado para tareas acotadas de alto volumen ("classification, database queries, live support") y como subagente; Anthropic indica que Sonnet 5.5 y Opus 5.5 siguen siendo mejores para agentic coding complejo. Primer Haiku con `effort` ajustable. Clientes reportan ~30% menos latencia (Asana) y ~½ de latencia vs Haiku 4.5 (Box). — [Anthropic, Claude Haiku 5.5](https://www.anthropic.com/claude-haiku-5-5)
- Opus 5.5 [oficial]: salida "más de 30% más rápida" que Opus 5; fast mode hasta 2,5× de velocidad a $8/$40 (sólo Claude API). — [Anthropic, Claude Opus 5.5](https://www.anthropic.com/claude-opus-5-5)
- Retención [oficial]: bajo ZDR, Anthropic no almacena prompts ni respuestas en reposo tras devolver la respuesta; se habilita por organización vía ventas. Fable 5.1/5 y Mythos 5.1/5 son "Covered Models" que requieren 30 días de retención y no están disponibles bajo ZDR salvo autorización expresa (las requests devuelven 400). Los datos retenidos "nunca se usan para entrenamiento sin permiso expreso". — [API and data retention](https://platform.claude.com/docs/en/manage-claude/api-and-data-retention)
- Elegibilidad ZDR por feature [oficial]: Messages API, prompt caching (KV y hashes en memoria sólo durante el TTL), data residency (`inference_geo`), fast mode y **tool search** = elegibles; structured outputs / `strict: true` = "Yes (qualified)": sólo el JSON Schema se cachea hasta 24 h desde el último uso, y para HIPAA se exige no poner datos sensibles en nombres de propiedades, `enum`, `const` ni `pattern`; Batch (29 días), code execution y programmatic tool calling (hasta 30 días) = **no** elegibles. ZDR no cubre la Console, Managed Agents ni productos consumer; en Bedrock/Vertex el procesador es el proveedor cloud. — [API and data retention](https://platform.claude.com/docs/en/manage-claude/api-and-data-retention)
- Features de tool use (referencia del SDK empaquetada, cache 2026-10-06, consistente con la documentación): `strict: true` en la definición de la tool garantiza que `tool_use.input` valide contra el schema (requiere `additionalProperties: false` + `required`); tool search server-side (`tool_search_tool_regex_20251119`, `tool_search_tool_bm25_20251119`) con `defer_loading: true` en las tools diferidas; en Opus 5.5, Sonnet 5.5 y Fable 5.1 el `tool_choice` `any`/`tool` devuelve 400 → usar `auto` + instrucción + `strict`, o `output_config.format` (structured outputs); en Opus 5.5 el thinking no se puede desactivar (sólo bajar `effort`); en Sonnet 5.5 se apaga con `thinking: {type: "between_tools"}`; Haiku 5.5 acepta `disabled` con effort ≤ `high`; Priority Tier no está soportado en los modelos 5.5; los caches de prompt son por modelo (una cascada entre modelos pierde reuso de caché). — [Models overview](https://platform.claude.com/docs/en/about-claude/models/overview); [Tool search tool](https://platform.claude.com/docs/en/agents-and-tools/tool-use/tool-search-tool); [Prompt caching](https://platform.claude.com/docs/en/build-with-claude/prompt-caching) (las dos últimas URLs no se abrieron en esta sesión; contenido tomado de la referencia del SDK).
- Seguridad/honestidad [oficial]: en una tarea de informe con búsqueda web donde cualquier cifra o cita inventada hacía fallar, 16 de 18 informes de Opus 5.5 pasaron; Fable 5.1 y Opus 5 no pasaron en ningún intento. Anthropic no publica tasa de alucinación ni de abstención explícitas. — [Anthropic, Claude Opus 5.5](https://www.anthropic.com/claude-opus-5-5)

### Inferences
- Para el chatbot de Ars Docendi con PII, los candidatos de API son **Opus 5.5, Sonnet 5.5 y Haiku 5.5** con ZDR; Fable 5.1 queda descartado salvo que la UNLaM acepte retención de 30 días.
- En el híbrido (B), la llamada "única" para extraer parámetros no puede forzar una tool en Opus/Sonnet 5.5: lo natural es usar **structured outputs** (`output_config.format`) con el schema de parámetros de la tool ya elegida por el router determinista, o `auto` + `strict`. Esto encaja mejor con (B) que con (A).
- Los valores de dominio (nombres, DNI, legajos) deben ir en el contenido del mensaje o resolverse por búsqueda en backend, nunca como `enum` en schemas estrictos (se cachean 24 h fuera de ZDR).

### Gaps
- No obtuve cifras de latencia (TTFT, tok/s) de Anthropic ni de Artificial Analysis para los modelos 5.5 (artificialanalysis.ai bloqueado). Sólo hay calificativos relativos y testimonios de clientes.
- No confirmé si Haiku 5.5 acepta `tool_choice` forzado (la referencia lista la restricción sólo para Fable 5.1/Mythos 5.1/Opus 5.5/Sonnet 5.5).
- No verifiqué disponibilidad de `inference_geo` con regiones que interesen a Argentina (sólo que es ZDR-elegible).

---

## 2. Benchmarks de tool use y Text-to-SQL de Claude

### Takeaway
Los anuncios de Opus 5.5, Sonnet 5.5 y Haiku 5.5 **no publican** τ²-bench, BFCL, MCP-Atlas, MCPMark ni BIRD/Spider; publican Terminal-Bench 4.0, OSWorld 2.1, GDPval-AA, AutomationBench, HLE con tools. La evidencia de tool use más cercana es de la generación anterior (Opus 5: MCP-Atlas 85,8%) y un dato de consistencia de Sonnet 5.5 en Toolathlon (pass@3 85,2% vs pass^3 68,5%). En Text-to-SQL, un benchmark independiente sobre BIRD da Opus 5.5 87,8% vs Sonnet 5.5 73,8%: la diferencia entre Opus y Sonnet en SQL parece grande, lo que sugiere que para "precisión primero" con Text-to-SQL el techo está en Opus 5.5.

### Cited Findings
- Opus 5.5 [oficial]: Terminal-Bench 4.0 66,4% (xhigh; ±2,6), Fable 5.1 55,8%, Opus 5 52,3%, GPT-6 Astra 57,9%; AutomationBench 40,0% (GPT-6 Astra 41,4%, Fable 5.1 31,4%, Opus 5 26,9%); HLE con tools 67,7%; OSWorld 2.1 (parcial) 81,8%; GDPval-AA v2.1 Elo 1846. El anuncio no reporta τ²-bench, MCP-Atlas, BFCL, Toolathlon, MCPMark, MMMLU ni BIRD/Spider, y aclara que los safeguards pueden haber reducido los puntajes. — [Anthropic, Claude Opus 5.5](https://www.anthropic.com/claude-opus-5-5)
- Haiku 5.5 vs Sonnet 5.5 [oficial]: Terminal-Bench 4.0 39,2% vs 70,6% (Haiku 4.5: 0,0%); OSWorld 2.1 offline 72,4% vs 83,9%; HLE con tools 57,4% vs 64,5%; GDPval-AA 1620 vs 1840; Chartography sin tools 46,4% vs 61,6%. Cliente HubSpot: 92,8% en su eval CRM (promedio de 3 corridas). Sin τ²/MCP-Atlas/BFCL. — [Anthropic, Claude Haiku 5.5](https://www.anthropic.com/claude-haiku-5-5)
- Sonnet 5.5 en Toolathlon Verified: **85,2% pass@3, 68,5% pass^3, 31,6 turnos promedio**; AutomationBench 44,7% [agregador; sólo resumen de buscador]. — [BenchLM Haiku 5.5 vs Sonnet 5.5](https://benchlm.ai/compare/claude-haiku-5-5-vs-claude-sonnet-5-5)
- MCP-Atlas (Scale, snapshot 2026-09-23): Muse Spark 1.1 88,1%, Fable 5.1 87,2%, claude-opus-5 (xhigh) 85,80 ± 2,10; Haiku 4.5 40,2%. Sonnet 5.5, Haiku 5.5 y Opus 5.5 no figuran aún [independiente vía agregador; sólo resumen de buscador]. — [Scale Labs MCP Atlas](https://labs.scale.com/leaderboard/mcp_atlas); [BenchLM MCP Atlas](https://benchlm.ai/benchmarks/scale-mcp-atlas)
- τ-bench oficial: en τ³-Banking, Claude Opus 5 (max) 48,7% pass^1, segundo detrás de Qwen 3.8 Max 55,2%; en la tabla τ²-bench texto los líderes pass^1 son Qwen3.5-397B-A17B 87,9%, Gemini 3.0 Pro 85,4%, Claude Opus 4.5 85,3% [sólo resumen de buscador]. La tabla oficial reporta sólo pass^1, no pass^k. BenchLM advierte que dominio, simulador de usuario, scaffold, prompts, número de trials y métrica deben coincidir para comparar. — [τ-bench leaderboard](http://taubench.com/leaderboard/); [BenchLM τ²-bench](https://benchlm.ai/benchmarks/tau2-bench)
- Toolathlon (paper original): Claude 4.5 Sonnet 38,6% — referencia histórica de lo que era el estado del arte en 2025. — [Toolathlon (arXiv 2510.25726)](https://arxiv.org/pdf/2510.25726)
- Text-to-SQL, AIM benchmark (subset de 759 preguntas de BIRD con selección de base entre 11 candidatas; ejecución estricta/adjudicada; panel revisado de 36 modelos): **Opus 5.5 87,8%**, gemini-3.8-flash 77,2% (342/443), **Sonnet 5.5 73,8% (368/499)**; excluye elecciones de base incorrectas y referencias marcadas como rotas [independiente; sólo resumen de buscador]. — [AIMultiple text-to-SQL](https://aimultiple.com/text-to-sql)
- Spider 2.0-AIFunc: Claude Opus 4.6 70,3% y Sonnet 4.6 69,0% de execution accuracy (los mejores del paper) [sólo resumen de buscador]. Spider 2.0 (tabla del sitio): sistemas agentes con Claude arriba, p. ej. Prism Swarm + Claude-Sonnet-4.5 90,49 y QUVI-3 + Claude-Opus-4.6 86,28; Spider-Agent con Claude antiguos 15–25. Una auditoría 2026 estimó errores en 62,8% de las anotaciones de Spider 2.0-Snow (fuente secundaria, no verificada). — [Spider 2.0-AIFunc (arXiv 2607.06229)](https://arxiv.org/html/2607.06229); [Spider 2.0](https://spider2-sql.github.io/); [knowledge-bases, resumen Spider 2](https://github.com/oleksiyp/knowledge-bases/blob/main/kb/db-ideas-kb/papers/2025-spider-2.md)

### Inferences
- La brecha pass@3 → pass^3 de Sonnet 5.5 en Toolathlon (≈17 puntos) muestra que incluso los modelos frontera no son deterministas en tareas largas multi-paso: un agente (A) con Claude sigue necesitando validación y límites de pasos si "una respuesta falsa" es inaceptable.
- El salto Opus 5.5 vs Sonnet 5.5 en SQL (87,8 vs 73,8, un solo benchmark) indica que, si el fallback Text-to-SQL de (B) se delega a la API, **Opus 5.5 es el candidato de precisión**; Sonnet 5.5 sería suficiente para elegir tools/extraer parámetros, donde los benchmarks agentic lo muestran a la par de Opus.
- La línea de base del repo con `claude-sonnet-5` (≈30/32) es de un modelo ya legacy; re-correrla con Sonnet 5.5, Opus 5.5 y Haiku 5.5 es barato y daría la cifra que los benchmarks públicos no dan.

### Gaps
- No hay τ²-bench, BFCL, MCP-Atlas ni MCPMark publicados para ningún modelo 5.5 (ni en anuncios ni en leaderboards a la fecha encontrada).
- No obtuve Spider 2.0/BIRD oficiales para modelos 5.x; AIM es la única cifra 5.5 y no pude abrirla para ver metodología completa ni el puntaje de Haiku 5.5.
- No encontré pass^k de ningún modelo Claude en τ²-bench publicado en 2026.

---

## 3. Mejores modelos locales en 8 GB (RTX 3070) y 12 GB (RTX 5070), incluidos MoE con offload

### Takeaway
No apareció en 2026 un modelo denso nuevo de 8–14B que desplace a los candidatos ya identificados: en 8 GB el mejor candidato sigue siendo **Qwen3.5-9B Q4_K_M** (BFCL-V4 66,1 y TAU2 79,1 autoreportados; KV pequeño por arquitectura híbrida) frente a Qwen3-8B; en 12 GB, **Qwen3-14B Q4_K_M** (mejor evidencia independiente de tool calling, pero ocupa ~12 GB a 4K de contexto → 1 slot) o **Qwen3.5-9B a Q6_K/Q8_0** con varios slots. Qwen3.6 sólo salió en 27B denso y 35B-A3B MoE (no entran sin offload). Los MoE con offload de expertos (Qwen3-30B-A3B, gpt-oss-20b) generan a ~30 tok/s en 8 GB pero con **prefill de pocos tok/s si los expertos van a CPU**, lo que los hace inviables para prompts con esquema/tools de miles de tokens y concurrencia.

### Cited Findings
- Ya documentado (no se repite en detalle): Qwen3.5-9B BFCL-V4 66,1 / TAU2 79,1, Qwen3.5-4B 50,3 / 79,9 [oficial]; Docker F1 Qwen3-14B Q4_K_M 0,971, Qwen3-8B Q4_K_M 0,919 [independiente]; Gemma 4 12B TAU2 69,0 [agregador]; xLAM-2 y watt-tool caen fuera de BFCL. — [modelos_locales_tool_calling.md](../Alternativas%20a%20Text%20to%20SQL%20local/modelos_locales_tool_calling.md)
- Qwen 3.6 (abril 2026) salió como 27B denso y 35B-A3B MoE; los 9B/4B/2B/0.8B densos son de Qwen 3.5 (feb 2026); Qwen 3.5/3.6 no tienen 14B/32B densos (siguen siendo de Qwen3) [agregador; la guía es internamente inconsistente en fechas]. — [InsiderLLM, Qwen models guide](https://insiderllm.com/guides/qwen-models-guide/)
- Qwen3.5-35B-A3B 81,2 y Qwen3.5-27B 79 en τ²-bench (aggregador, no pass^k) [sólo resumen de buscador]. — [Steel.dev τ-bench leaderboard](https://leaderboard.steel.dev/leaderboards/tau-bench/)
- Qwen3.5-9B (Reasoning) obtiene 32 en el Artificial Analysis Intelligence Index (+15 vs Qwen3 VL 8B) — índice general, no SQL ni tools [sólo resumen de buscador]. — [Artificial Analysis, Qwen3.5 small models](https://artificialanalysis.ai/articles/qwen3-5-small-models)
- Gemma 4 12B: lanzado 2026-06-03 según un blog; fuentes en conflicto sobre licencia/pesos (Apache 2.0 vs "endpoint propietario"); BenchLM no tiene resultados agentic para él [agregador]. — [techsy Gemma 4 12B](https://techsy.io/en/blog/gemma-4-12b); [Roboflow Gemma 4 12B](https://playground.roboflow.com/models/google/gemma-4-12b); [BenchLM Gemma 4 12B](https://benchlm.ai/models/gemma-4-12b)
- RTX 5070, llama.cpp, Qwen3-8B Q4_K_M: prefill 3.487,7 tok/s a 4K, 1.600,8 a 16K, 898,8 a 32K; generación 85,8 tok/s a 4K. Qwen3-14B Q4_K_M: generación 54,2 tok/s a 4K con **pico de 12,0 GB a 4K** [independiente; sólo resumen de buscador]. — [smeltcore Qwen3-8B en RTX 5070](https://smeltcore.com/recipes/qwen3-8b-on-rtx-5070-q4-k-m-gguf-via-ollama-or-llama-cpp/); [smeltcore Qwen3-14B en RTX 5070](https://smeltcore.com/recipes/qwen3-14b-on-rtx-5070-q4-k-m-gguf-via-ollama-or-llama-cpp)
- Referencias de generación 14B Q4 (Hardware Corner, llama.cpp): RTX 4070 32,7 tok/s, RTX 4090 69,1 tok/s [sólo resumen de buscador]. ModelFit estima ~7 tok/s para 14B en 5070 asumiendo offload a CPU, lo que contradice la medición de 54,2 tok/s. — [ModelFit RTX 5070](https://modelfit.io/gpu/rtx-5070/)
- MoE con offload en 8 GB (Qwen3-Coder-30B-A3B, misma arquitectura que Qwen3-30B-A3B): con todos los expertos en CPU (`--cpu-moe`) 13,38 tok/s de generación y **2,78 tok/s de prompt eval**; con `--n-cpu-moe 40` ≈32,49 tok/s y ~760 MiB libres; con 38, 33,64 tok/s y ~361 MiB libres ("demasiado justo") [independiente, un autor]. — [DEV, Qwen3-Coder 30B en 8 GB](https://dev.to/upayanghosh/from-oom-to-262k-context-running-qwen3-coder-30b-locally-on-8gb-vram-1ej1)
- gpt-oss-20b: la guía oficial de llama.cpp muestra una RTX 2060 8 GB con `--n-cpu-moe 16` a 32K de contexto o 22 a contexto completo; sin tok/s para 8 GB; a 128K de contexto la generación cae a ~9 tok/s según una guía [oficial + agregador]. Sin mediciones de exactitud de tool calling con offload. — [llama.cpp Discussion #15396](https://github.com/ggml-org/llama.cpp/discussions/15396); [runaihome gpt-oss-20b](https://runaihome.com/blog/gpt-oss-20b-local-ai-hardware-guide-2026/); [aliteq gpt-oss-20b 8/12 GB](https://aliteq.com/gpt-oss-20b-8gb-12gb-gpu-moe-offload-2026)

### Inferences
- **3070 8 GB**: Qwen3.5-9B Q4_K_M es el único candidato con números agentic decentes y margen de KV para 2–4 slots; Qwen3-8B Q4_K_M (actual) queda como control. Qwen3.5-4B (TAU2 79,9 pero BFCL-V4 50,3) sólo como router/clasificador de intents, no como generador de parámetros o SQL.
- **5070 12 GB**: el pico de 12,0 GB de Qwen3-14B Q4_K_M a 4K implica que, en la práctica, es **monousuario** con contexto corto (o requiere KV q8_0 y contexto reducido); para 4–8 usuarios, Qwen3.5-9B Q6_K/Q8_0 (o Qwen3-8B AWQ en vLLM) es más realista. La 5070 genera ~1,6–1,7× más rápido que una 3060/3070 por ancho de banda (85,8 tok/s medidos vs ~42 en 3060; estimación).
- **MoE con offload**: el cuello de botella no es la generación sino el prefill cuando los expertos están en CPU (2,78 tok/s → un prompt de 4K tokens tardaría >20 min en el peor caso; con offload parcial mejora, pero no hay medición). Con un system prompt de esquema+tools de 6–12K tokens, sólo serían viables con caché de prefijo perfecta y un solo usuario. Descartados para 2–30 usuarios.

### Gaps
- Sin cifras propias o publicadas de Qwen3.5-9B en RTX 3070 (tok/s, pp) ni en 5070.
- Sin prefill medido con offload parcial (`--n-cpu-moe` intermedio) en 8/12 GB.
- Sin verificación en fuente primaria de Gemma 4 12B (pesos/licencia) ni BFCL/τ² para Ministral 3, Granite 4.x, Phi-4 en 2026.

---

## 4. Efecto de la cuantización en function calling y SQL

### Takeaway
La evidencia 2026 converge en que **4 bits (Q4_K_M y similares) preserva casi todo el function calling en modelos ≥4B, sobre todo con razonamiento**, mientras que **3 bits rompe primero la decisión de "llamar o no" (colapso hacia no llamar)** y los modelos muy chicos sin razonamiento se degradan mucho incluso a Q4. Las tareas paralelas/compuestas sufren más que las simples. Para SQL no hay estudios específicos por nivel de cuantización; Q5/Q6/Q8 dan mejoras pequeñas y decrecientes en benchmarks generales.

### Cited Findings
- "Which Decisions Low-Bit Quantization Breaks" (16 modelos, 8 familias, 4/3/2 bits, BFCL como benchmark principal de tools): el factor de margen de decisión tiene mediana 0,86 a 4 bits, 0,33 a 3 y 0,00 a 2; a 4 bits "casi nada cambia de lado"; a 3 bits la decisión de llamar una tool colapsa hacia la inacción mientras la elección de cuál tool queda intacta; el llenado de argumentos en BFCL público cambia en una mediana de 4% a 3 bits (RTN); en tool calling las decisiones "whether-to-call" se rompen antes que las de selección [sólo resumen de buscador; arXiv bloqueado; la tabla 6 con cifras por bit no pudo leerse]. — [arXiv 2608.06564](https://arxiv.org/html/2608.06564v2); [awesomepapers resumen](https://awesomepapers.io/llm-papers/papers/2608.06564)
- "Small Reasoning Models are Instruction Followers in Function Calling": Qwen3-4B (Think) 94,1% en FP16 → **93,5% en Q4_K_M**; en cambio Qwen3-0.6B (No-Think) 62,3% → 16,8% y Gemma-3 1B 23,9% → 8,7% bajo cuantización; los autores atribuyen la resiliencia a la traza de razonamiento [sólo resumen de buscador]. — [arXiv 2608.22472](https://arxiv.org/pdf/2608.22472)
- Medición independiente en BFCL v4 (simple/multiple + irrelevance, n=200 × 3 semillas, greedy): el colapso de validez de schema de Llama en Q4_K_M es ~5× mayor en tareas paralelas/estilo ToolACE que en llamadas simples; en Qwen3-0.6B no hay diferencia estadística entre llama.cpp GGUF y transformers bf16 a igual precisión (la degradación viene de la cuantización, no del motor) [independiente, blog de un autor, no revisado]. — [DEV, Does Quantization Break Tool-Calling?](https://dev.to/happynood/does-quantization-break-tool-calling-i-measured-it-on-a-4gb-laptop-gpu-bfcl-3-seeds-bootstrap-185l)
- K-quants: 8 bits (Q8_0) mejora con rendimientos decrecientes; Q2_K a menudo retiene exactitud aceptable pero algunos modelos pierden mucho (MMLU-Pro, CRUXEval, MuSR; no tool calling) [sólo resumen de buscador]. — [arXiv 2605.19645](https://arxiv.org/abs/2605.19645)
- Ya documentado: NVFP4 de Qwen3.5-9B pierde ~1 punto (BFCL-V4 65,0, TAU2 77,9); Docker no halló diferencia significativa Q4 vs F16 en Qwen3-8B (0,919 vs 0,933); Q3/Q2/IQ producen tool calls malformadas en llama.cpp. — [modelos_locales_tool_calling.md](../Alternativas%20a%20Text%20to%20SQL%20local/modelos_locales_tool_calling.md)

### Inferences
- Q4_K_M es un piso aceptable para 8–9B; en la 5070, subir Qwen3.5-9B a Q6_K/Q8_0 compra poco en tool calling (≈1 punto) pero puede ayudar en SQL/literales (no medido). Nunca bajar a Q3 para ganar slots: el modo de falla (no llamar la tool y responder "de memoria") es exactamente una respuesta falsa.
- Como el thinking hace al modelo más robusto a la cuantización pero multiplica la latencia, el compromiso razonable en (B) es thinking apagado en la extracción de parámetros con decodificación restringida por gramática, y thinking sólo en el fallback Text-to-SQL.

### Gaps
- No hay estudio que mida degradación por nivel de cuantización en Text-to-SQL (BIRD/Spider) para 7–14B.
- No obtuve las cifras por bit de la tabla 6 de arXiv 2608.06564 ni comparación Q4 vs Q8 explícita en BFCL.
- Sin datos de AWQ/FP8 en vLLM sobre tool calling para estos tamaños.

---

## 5. Confiabilidad multi-paso: tasas por paso y composición

### Takeaway
La composición de errores es el argumento central contra (A) en local: con una tasa de acierto por paso realista de 0,90–0,95 para 8–14B, un turno de 4 pasos queda en 0,66–0,81 de éxito, mientras que (B) con una sola llamada de extracción mantiene ~0,92–0,97. Incluso Claude muestra brecha de consistencia (Sonnet 5.5: 85,2% pass@3 vs 68,5% pass^3 en Toolathlon). No hay pass^k publicados para modelos locales chicos en 2026.

### Cited Findings
- Sonnet 5.5 Toolathlon Verified: 85,2% pass@3 vs 68,5% pass^3, 31,6 turnos [agregador; sólo resumen de buscador]. — [BenchLM](https://benchlm.ai/compare/claude-haiku-5-5-vs-claude-sonnet-5-5)
- τ-bench oficial reporta sólo pass^1; pass^k (resolver la tarea en las k corridas) es la métrica de confiabilidad pero no está publicada para estos modelos [sólo resumen de buscador]. — [τ-bench leaderboard](http://taubench.com/leaderboard/)
- Ya documentado: Docker F1 por batería (incluye suite "Complex" multi-paso, hasta 5 rondas) 0,919 Qwen3-8B Q4 / 0,971 Qwen3-14B Q4; xLAM-2-8b τ-bench pass@1 46,7; MCP-Bench: Llama-3.1-8B rezagado en "dependency awareness"; MCPMark excluyó modelos ≤100B. — [modelos_locales_tool_calling.md](../Alternativas%20a%20Text%20to%20SQL%20local/modelos_locales_tool_calling.md)
- Cuantización agrava lo compuesto: degradación ~5× mayor en tareas paralelas que simples en Q4_K_M (Llama). — [DEV, BFCL cuantizado](https://dev.to/happynood/does-quantization-break-tool-calling-i-measured-it-on-a-4gb-laptop-gpu-bfcl-3-seeds-bootstrap-185l)

### Inferences
- [estimación] Éxito por turno ≈ p^k con k pasos dependientes: p=0,92 (≈ Docker Qwen3-8B) → k=1: 0,92; k=3: 0,78; k=5: 0,66. p=0,97 (≈ Qwen3-14B) → k=3: 0,91; k=5: 0,86. p=0,99 (supuesto para Claude 5.5 en tools simples, no medido) → k=5: 0,95. La independencia es un supuesto pesimista (errores correlacionados en preguntas difíciles) pero el orden de magnitud explica por qué la línea de base local tiene 7 respuestas falsas en 34.
- En (B), el número de decisiones del modelo por turno es 1 (extraer parámetros) o 2 (más redactar), y el router determinista no compone error de modelo; la mejora es estructural, no depende de un modelo mejor.
- Para (A) local, la mitigación más efectiva es acotar k (≤2–3 pasos), validar cada argumento en backend con errores estructurados en español y exigir que la respuesta final cite resultados de tools (no texto libre).

### Gaps
- No hay pass^k publicados de Qwen3/3.5, Gemma 4 ni Ministral en τ²-bench o BFCL multi-turn con cuantización.
- No hay medición de tasa por paso para Claude 5.5 en tareas de 3–5 llamadas sobre APIs CRUD simples (el caso de Ars Docendi).

---

## 6. Desempeño en español (tool use y Text-to-SQL) por tamaño

### Takeaway
No encontré evidencia nueva de 2026 específica de español para tool use o Text-to-SQL con modelos ≤14B ni para Claude 5.5. Lo disponible (ya documentado) indica una caída moderada para español respecto del inglés (≈4–5 puntos en τ-Multilingual) y que las lenguas romances rinden comparativamente bien; la caída es mayor en modelos chicos.

### Cited Findings
- Ya documentado: τ-Multilingual, caída vs inglés de 4,6 puntos para español; MASSIVE-Agents: lenguas romances con desempeño comparativamente alto, algunos modelos chicos dan 0 en idiomas difíciles; SEATauBench: definiciones de tools en idiomas mezclados → caída pequeña y meseta. — [modelos_locales_tool_calling.md](../Alternativas%20a%20Text%20to%20SQL%20local/modelos_locales_tool_calling.md)
- Todos los modelos Claude actuales declaran "multilingual capabilities" (sin cifras por idioma en la página de modelos). — [Models overview](https://platform.claude.com/docs/en/about-claude/models/overview)
- Text-to-SQL multi-dialecto (no multilingüe, pero relevante para PostgreSQL vs SQLite): UniQL reporta Qwen3-8B 44,85 EX, Qwen3-4B 46,74 y Qwen3-32B 51,96 en una variante Hive de BIRD; PolySQL lista GPT-OSS-20B 53,2 vs Qwen3-8B 40,9 (columna/dialecto no confirmados) [sólo resumen de buscador]. — [UniQL (arXiv 2606.08018)](https://arxiv.org/pdf/2606.08018); [PolySQL (arXiv 2605.07796)](https://arxiv.org/pdf/2605.07796)

### Inferences
- Para Ars Docendi el riesgo idiomático mayor no es la gramática sino los **literales** (nombres propios con tildes, siglas de materias, "1er cuatrimestre"): eso afecta igual a SQL y a tools, y se mitiga con resolución de valores en backend (búsqueda difusa → IDs) antes de que el modelo los use.
- La caída por dialecto (BIRD-SQLite → Hive) de Qwen3-8B (≈45 EX) sugiere que el SQL PostgreSQL en español con un 8B estará bastante por debajo de lo que dicen los benchmarks en inglés/SQLite; coherente con la línea de base 26/34.

### Gaps
- Sin benchmarks de Text-to-SQL en español (ni rioplatense) sobre PostgreSQL para ningún tamaño.
- Sin MMMLU/multilingual publicado para los modelos 5.5 en sus anuncios.

---

## 7. Latencia por turno: agente 3–5 pasos vs híbrido 1–2 llamadas, con 1/4/8 usuarios concurrentes

### Takeaway
[estimación] En la 3070, un agente de 3–5 pasos con thinking apagado tarda ≈7–13 s con 1 usuario y se degrada a ≈25–50 s con 4–8 usuarios (llama-server, 1–4 slots); el híbrido queda en ≈2–4 s con 1 usuario y ≈6–15 s con 4–8. En la 5070 (≈1,7× más generación) con vLLM/batching, el agente baja a ≈5–8 s con 1 usuario y ≈8–15 s con 4–8, y el híbrido a ≈1,5–3 s / ≈2–5 s. Con thinking activado, cada paso suma 300–800 tokens de decodificación y multiplica el agente por 2–4×. Claude no tiene cifras publicadas de TTFT/tok/s accesibles; la latencia de red + TTFT por llamada hace que un agente de 4 pasos sume varios segundos aun con Haiku 5.5.

### Cited Findings
- Generación medida: 5070 Qwen3-8B Q4_K_M 85,8 tok/s y prefill 3.488 tok/s a 4K / 1.601 a 16K; Qwen3-14B Q4_K_M 54,2 tok/s [sólo resumen de buscador]. — [smeltcore 8B](https://smeltcore.com/recipes/qwen3-8b-on-rtx-5070-q4-k-m-gguf-via-ollama-or-llama-cpp/); [smeltcore 14B](https://smeltcore.com/recipes/qwen3-14b-on-rtx-5070-q4-k-m-gguf-via-ollama-or-llama-cpp)
- Ya documentado: 3060 12 GB 8B Q4 ~42 tok/s; 4 requests concurrentes Ollama serie 54 s vs llama-server 4 slots 35 s vs Ollama NUM_PARALLEL=4 29 s; vLLM escala throughput agregado 3,9–5,4× con batching en 3090; en 8 GB con Qwen3-8B sólo entra 1 slot de 16K. — [serving_concurrencia.md](../Alternativas%20a%20Text%20to%20SQL%20local/serving_concurrencia.md)
- Línea de base medida del repo: p50 2,4–2,9 s para el pipeline Text-to-SQL de 2 llamadas en la 3070. — [reports/Alternativas a Text to SQL local.md](../../reports/Alternativas%20a%20Text%20to%20SQL%20local.md)
- Claude: Haiku 5.5 "el más rápido" (Asana: hasta 2,5× más rápido por turno de agente); Opus 5.5 >30% más rápido en salida que Opus 5; fast mode hasta 2,5× (Opus). — [Anthropic Haiku 5.5](https://www.anthropic.com/claude-haiku-5-5); [Anthropic Opus 5.5](https://www.anthropic.com/claude-opus-5-5)

### Inferences
Modelo de cálculo [estimación]: `T ≈ Σ pasos (prefill_nuevo/pp + tokens_out/tg) + t_tools`. Supuestos: 3070 tg ≈ 50 tok/s y pp ≈ 2.000 tok/s para 8–9B Q4 (escalado por ancho de banda desde 3060/5070; no medido); 5070 tg ≈ 85 tok/s, pp ≈ 3.000 tok/s; paso intermedio = 500 tokens nuevos de prefill (resultado de tool, con caché de prefijo) + 100 tokens de tool call; respuesta final 200 tokens; t_tools ≈ 0,1–0,3 s por llamada REST/SQL; thinking apagado; llama-server con 4 slots en 3070 (sólo viable con Qwen3.5-9B; con Qwen3-8B es 1 slot → cola) con throughput agregado ≈1,5× (dato Ollama/llama-server); vLLM en 5070 con agregado ≈3× a 4 en vuelo.

| Escenario [estimación] | 1 usuario | 4 concurrentes | 8 concurrentes |
|---|---|---|---|
| 3070, agente 4 pasos | ≈8–11 s | ≈25–30 s (4 slots) / ≈35–45 s (1 slot, cola) | ≈50 s+ (cola) |
| 3070, híbrido (router + 1 extracción + redacción) | ≈2–4 s (coherente con p50 2,4–2,9 s medido) | ≈6–10 s | ≈12–18 s |
| 3070, híbrido con respuesta por plantilla (sin redacción LLM) | ≈1–2 s | ≈3–5 s | ≈6–9 s |
| 5070 (vLLM), agente 4 pasos | ≈5–7 s | ≈7–10 s | ≈10–16 s (límite de KV) |
| 5070 (vLLM), híbrido | ≈1,5–2,5 s | ≈2–3,5 s | ≈3–5 s |
| 5070, Qwen3-14B Q4 (1 slot efectivo) agente | ≈8–11 s | cola ≈30–40 s | no viable |

- Con 2–30 usuarios registrados, la concurrencia real en vuelo suele ser 1–4; el híbrido sostiene eso en la 3070, el agente no con latencias aceptables (<10 s).
- En la API de Claude la concurrencia no es problema de capacidad local (rate limits aparte), pero cada paso del agente agrega un round-trip y TTFT; un híbrido con 1 llamada a Haiku 5.5 o Sonnet 5.5 sería del orden de 1–3 s, un agente de 4 pasos varios segundos más (sin cifras publicadas; medir).

### Gaps
- Sin mediciones publicadas de TTFT/tok/s de los modelos Claude 5.5 accesibles en esta sesión.
- Sin medición de pp/tg en RTX 3070 para Qwen3.5-9B ni de llama-server con 4 slots en 8 GB; la tabla debe validarse con `llama-bench` y un benchmark de carga (GuideLLM / `vllm bench serve`).

---

## 8. Patrones de despliegue híbrido: local primario + Claude como fallback/escalamiento, y privacidad

### Takeaway
El patrón coherente con "precisión primero" es una cascada **por confianza y por tipo de ruta**, no por carga: el router determinista y las tools certificadas corren localmente (sin PII saliendo), y sólo las preguntas que caen al fallback Text-to-SQL o que el modelo local no puede resolver con confianza se escalan a Claude (Opus 5.5 para SQL, Sonnet/Haiku 5.5 para extracción), con ZDR y con PII seudonimizada antes de enviar. Si la escalada no está permitida por la política de datos, la alternativa correcta es **abstenerse**, no responder con el modelo local.

### Cited Findings
- ZDR disponible para Messages API, prompt caching, tool search y data residency; no para Batch, code execution ni programmatic tool calling; schemas estrictos cacheados hasta 24 h; Fable 5.x exige 30 días de retención. — [API and data retention](https://platform.claude.com/docs/en/manage-claude/api-and-data-retention)
- Caches de prompt son por modelo: una cascada multi-modelo pierde reuso de caché; la guía oficial recomienda medir primero el modelo más capaz a menor `effort` antes de construir una cascada por costo (referencia del SDK, consistente con documentación de caching). — [Prompt caching](https://platform.claude.com/docs/en/build-with-claude/prompt-caching)
- Haiku 5.5 no tiene fallback server-side ante `stop_reason: "refusal"`; Opus 5.5/Sonnet 5.5 admiten `fallbacks` server-side (beta) — relevante para no devolver vacío al usuario (referencia del SDK). — [Models overview](https://platform.claude.com/docs/en/about-claude/models/overview)
- Ya documentado: recomendación de derivar a la nube cuando la espera estimada en cola supera un umbral, con contrato de tools portable (JSON Schema). — [serving_concurrencia.md](../Alternativas%20a%20Text%20to%20SQL%20local/serving_concurrencia.md)

### Inferences
- Cascada propuesta [diseño propio]: (1) intent determinista → tool certificada local (sin LLM o con extracción local restringida por gramática); (2) si la extracción local no valida contra el schema o falta un parámetro obligatorio → repreguntar al usuario (no escalar); (3) si no hay intent → Text-to-SQL; con el modelo local sólo si una señal de confianza lo permite (p. ej. self-consistency de 3 muestras con mismo resultado de ejecución), si no → Claude Opus 5.5 con esquema y valores seudonimizados; (4) si Claude tampoco produce SQL validado o el resultado es vacío ambiguo → abstención explícita.
- PII: enviar esquema y pregunta, no filas; reemplazar nombres/DNI/legajos por tokens (`<DOCENTE_1>`) resueltos en backend; las filas devueltas se formatean localmente o por plantilla para que no viajen a la API. Así la API ve metadatos de esquema y preguntas, no datos personales.
- Una cascada local→Claude rompe la caché entre modelos; como el costo es irrelevante, el impacto es sólo de latencia (prefill completo en la API en cada escalada).

### Gaps
- No encontré casos publicados de cascada local→Claude con métricas de precisión/abstención en dominios universitarios.
- No revisé el encuadre legal argentino (Ley 25.326 y transferencia internacional de datos personales) ni la región de procesamiento disponible vía `inference_geo`; queda como pregunta para el área legal de la UNLaM.
- Sin estudios que cuantifiquen pérdida de precisión de Text-to-SQL al seudonimizar literales.

---

## 9. Veredicto por nivel: qué arquitectura puede correr cada uno de forma confiable

### Takeaway
- **RTX 3070 (8 GB)**: sólo **(B) híbrido** es confiable. Modelo: Qwen3.5-9B Q4_K_M (o mantener Qwen3-8B Q4_K_M como control), thinking apagado en extracción, gramática JSON, 1–4 slots; Text-to-SQL local sólo con abstención agresiva. (A) no es confiable: composición de errores (≈0,66–0,81 por turno de 3–5 pasos) y latencias de 25–50 s con 4–8 usuarios [estimación].
- **RTX 5070 (12 GB)**: **(B) con holgura**; **(A) limitado** (≤2–3 pasos, ≤5–10 tools visibles) es posible pero no "precisión primero". Modelo: Qwen3.5-9B Q6_K/Q8_0 multi-slot para concurrencia, o Qwen3-14B Q4_K_M monousuario para máxima exactitud por llamada (pico 12,0 GB a 4K). Riesgos de software sm_120 (ver serving_concurrencia.md). MoE con offload (gpt-oss-20b, Qwen3-30B-A3B) descartados por prefill.
- **API de Claude**: ambas arquitecturas son viables; (A) con Sonnet 5.5/Opus 5.5 es la única configuración donde un agente multi-paso tiene evidencia de confiabilidad alta (MCP-Atlas 85,8% para Opus 5; Toolathlon pass^3 68,5% para Sonnet 5.5 muestra que aún no es determinista). Para "precisión primero" con datos con PII, el diseño preferible sigue siendo (B) con Claude como extractor/fallback SQL (Opus 5.5: 87,8% en AIM vs 73,8% Sonnet 5.5), con ZDR y seudonimización. Fable 5.1 queda fuera por retención obligatoria de 30 días.

### Cited Findings
- Capacidad local medida y benchmarks: ver secciones 3–5. — [modelos_locales_tool_calling.md](../Alternativas%20a%20Text%20to%20SQL%20local/modelos_locales_tool_calling.md)
- Claude MCP-Atlas Opus 5 85,8 ± 2,1; Sonnet 5.5 Toolathlon 85,2 pass@3 / 68,5 pass^3; AIM SQL Opus 5.5 87,8 vs Sonnet 5.5 73,8 [sólo resumen de buscador]. — [Scale MCP Atlas](https://labs.scale.com/leaderboard/mcp_atlas); [BenchLM](https://benchlm.ai/compare/claude-haiku-5-5-vs-claude-sonnet-5-5); [AIMultiple](https://aimultiple.com/text-to-sql)
- Retención/ZDR por modelo. — [API and data retention](https://platform.claude.com/docs/en/manage-claude/api-and-data-retention)
- VRAM/velocidad 5070 y offload MoE. — [smeltcore 14B](https://smeltcore.com/recipes/qwen3-14b-on-rtx-5070-q4-k-m-gguf-via-ollama-or-llama-cpp); [DEV Qwen3-Coder 30B 8 GB](https://dev.to/upayanghosh/from-oom-to-262k-context-running-qwen3-coder-30b-locally-on-8gb-vram-1ej1)

### Inferences
Tabla de síntesis [inferencia sobre cifras citadas]:

| Nivel | Mejor candidato (precisión) | Alternativa (concurrencia) | (A) agente REST/MCP multi-paso | (B) híbrido | Restricción dominante |
|---|---|---|---|---|---|
| 3070 8 GB | Qwen3.5-9B Q4_K_M | Qwen3-8B Q4_K_M (actual), Qwen3.5-4B como router | No confiable (error compuesto, 25–50 s con 4–8 usuarios) | Confiable si la extracción es 1 llamada con gramática y SQL con abstención | VRAM: 1–4 slots, KV |
| 5070 12 GB | Qwen3-14B Q4_K_M (1 slot) | Qwen3.5-9B Q6_K/Q8_0 multi-slot; Qwen3-8B AWQ en vLLM | Limitado a ≤2–3 pasos y pocas tools | Confiable, con margen para 4–8 en vuelo | Madurez sm_120; 14B sin margen de KV |
| Claude API | Opus 5.5 (SQL); Sonnet 5.5 (tools) | Haiku 5.5 (intents/extracción, más rápido) | Viable; pass^k aún <100% | Viable y más preciso (1 decisión por turno) | PII fuera de la institución → ZDR + seudonimización; Fable no ZDR |

- La métrica que decide no es el benchmark público sino el set propio (34+ preguntas): re-correr la línea de base con Opus 5.5, Sonnet 5.5 y Haiku 5.5 (reemplazando `claude-sonnet-5`) y con Qwen3.5-9B en ambas GPUs, contando respuestas falsas y abstenciones por separado.

### Gaps
- Ninguna cifra pública compara cabeza a cabeza (A) vs (B) con el mismo modelo y dominio.
- Todas las cifras de Claude 5.5 en tool use/SQL vienen de agregadores no abiertos en esta sesión; los anuncios oficiales no incluyen τ²/MCP-Atlas/BIRD.
