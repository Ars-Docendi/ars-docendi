# Serving multiusuario de LLM en una GPU de consumo (8 GB / 12 GB): agente con tool-calling vs Text-to-SQL

> Notas de investigación (octubre 2026). Alcance: RTX 3070 8 GB (Ampere sm_86), RTX 3060 12 GB (Ampere), RTX 5070 12 GB (Blackwell sm_120). Workload actual: Qwen3-8B Q4_K_M en llama-server, prefijo estable de ~10–12k tokens (schema + ejemplos + glosario), 2 llamadas LLM por turno. Workload candidato: agente con tool-calling (posiblemente MCP), N llamadas secuenciales por turno.
>
> Limitaciones del entorno de investigación: arxiv.org, huggingface.co, developers.redhat.com y bigiron.cc estaban bloqueados por el proxy, así que varios datos de papers y de Red Hat se tomaron de resúmenes de buscador y no de la fuente primaria. Se marca en cada caso. Toda cifra que no esté acompañada de GPU/modelo/cuantización/concurrencia es porque la fuente no la daba.

## 1. Motores de inferencia: soporte en Ampere 8 GB y Blackwell sm_120, prefix caching, batching, tool-calling

### Takeaway
En 8 GB, llama.cpp (llama-server) sigue siendo la única opción realista y ya no es "1 slot = 1 copia completa del contexto": desde 2025–2026 tiene **KV unificado** (`--kv-unified`, activo por defecto con slots automáticos) y **caché de prompts en RAM del host** (`--cache-ram`, `--cache-idle-slots`), lo que cambia el cálculo del equipo. En 12 GB Blackwell, vLLM/SGLang ofrecen prefix caching real entre requests y continuous batching, pero el soporte sm_120 de consumo sigue siendo frágil (wheels sin kernels sm_120, bugs de FP8 KV y de selección de backend de atención); LMDeploy publica wheels CUDA 12.8 que dice cubrir RTX 50.

### Cited Findings

**llama.cpp / llama-server**
- `--kv-unified`: "use single unified KV buffer shared across all sequences; default: enabled if number of slots is auto" — [Debian manpage llama-server(1)](https://manpages.debian.org/unstable/llama.cpp-tools/llama-server.1.en.html).
- Con KV unificado, `-c` es el pool total compartido por todos los slots y además el máximo de cualquier prompt individual; en mayo 2026 no se podían fijar por separado: "It sets the total unified kv cache size that is shared by all slots during parallel processing, and also sets the max length for any single prompt" — [Discussion #22658](https://github.com/ggml-org/llama.cpp/discussions/22658).
- Semántica del pool unificado (discusión de 2024, verificar contra código actual): "it's allowed to have sequences with more than T tokens as long as the sum of all tokens does not exceed P*T" — [Discussion #4130](https://github.com/ggml-org/llama.cpp/discussions/4130).
- `--kv-unified-per-slot N` (PR de bartowski): limita el contexto por slot en modo unificado; si no se pasa `-c`, el pool se dimensiona `n_parallel*N` (ej.: 4 slots × 4096 = pool de 16384) — [PR #24124](https://github.com/ggml-org/llama.cpp/pull/24124). Hay una duda abierta (agosto 2026) sobre si el tope aplica también con `--no-kv-unified` — [Discussion #27916](https://github.com/ggml-org/llama.cpp/discussions/27916).
- **Prompt cache en RAM del host** (PR #16391, ggerganov, octubre 2025): guarda estados de prompt en RAM del sistema para evitar reprocesar; el caché se comporta como slots extra y el server, midiendo similitud de prefijo, puede "swappear" un estado cacheado al contexto activo. `--cache-ram`/`-cram` en MiB (`-1` sin límite, `0` deshabilita); incompatible con flujos multimodales (mtmd) — [PR #16391](https://github.com/ggml-org/llama.cpp/pull/16391).
- `--cache-ram` default 8192 MiB; `--cache-idle-slots`: "save idle slots to the prompt cache on new task, and clear them when using unified KV (default: enabled, requires cache-ram)" — [Debian manpage](https://manpages.debian.org/unstable/llama.cpp-tools/llama-server.1.en.html).
- Según un commit de LocalAI (mayo 2026, fuente secundaria sobre upstream), con slots automáticos upstream activa `kv_unified` y sube `n_parallel` a 4, lo que habilita `cache_idle_slots`; `checkpoint_every_nt` default 8192 — [LocalAI mirror](https://gitea.varghacsongor.hu/GithubMirror/LocalAI/commit/959de86761acf3d4dc7260c6e252781cbbd7c900.patch).
- Bug sm_120 en llama.cpp: acceso fuera de rango en `mul_mat_q<Q8_0,128>` en RTX 5090, crash intermitente que desaparece quitando pesos Q8_0 — [Issue #24399](https://github.com/ggml-org/llama.cpp/issues/24399). (No afecta a Q4_K_M per se, pero muestra que sm_120 todavía tiene aristas).

**vLLM**
- Wheels/imagenes oficiales fallaron en RTX 50 (sm_120/sm_121) con "no kernel image" — [Issue #35432](https://github.com/vllm-project/vllm/issues/35432) (≈7 meses de antigüedad; verificar si las releases actuales lo resolvieron). Guías comunitarias compilan desde fuente con PyTorch cu128 y `TORCH_CUDA_ARCH_LIST=12.0`, FA2 en lugar de FA3 — [vLLM forum RTX 5090](https://discuss.vllm.ai/t/vllm-on-rtx5090-working-gpu-setup-with-torch-2-9-0-cu128/1492); experiencia en RTX 5070 Ti — [Geogo blog](https://blog.geogo.in/vllm-on-rtx-5070ti-our-approach-to-affordable-and-efficient-llm-serving-b35cf87b7059?gi=154230f67707), [ligma.blog](https://ligma.blog/post1/).
- FP8 KV en sm_120: `kv_cache_dtype="fp8"` autoselecciona FlashInfer sin JIT usable y crashea en vez de caer a TRITON_ATTN (reportado en RTX PRO 4000, misma arquitectura sm_120); workaround: forzar backend TRITON_ATTN — [Issue #60262](https://github.com/vllm-project/vllm/issues/60262).
- AWQ/GPTQ + FP8 KV: un gate de `fp8_e5m2` se disparaba con cualquier checkpoint cuantizado, no sólo FP8 — [Issue #39137](https://github.com/vllm-project/vllm/issues/39137); fix propuesto que, en RTX 5090 con Gemma AWQ, "doubled KV capacity" — [PR #39255](https://github.com/vllm-project/vllm/pull/39255) (no se pudo confirmar si se mergeó).
- `--kv-cache-dtype nvfp4` crashea en el primer request en sm_120 — [Issue #43562](https://github.com/vllm-project/vllm/issues/43562). FP8 KV + `--calculate-kv-scales` corrompe salida en modelos híbridos GDN+Attention (Qwen3.5) — [Issue #37554](https://github.com/vllm-project/vllm/issues/37554). Pesos FP8 en sm_120: "CUTLASS GEMM does not support SM120" (2025) — [Issue #21648](https://github.com/vllm-project/vllm/issues/21648).
- Histórico: FP8 KV + prefix caching crasheaba en Radeon RDNA3 (no NVIDIA, pero muestra que la combinación tiene historia de bugs) — [Issue #13147](https://github.com/vllm-project/vllm/issues/13147).
- Tool calling: requiere `--enable-auto-tool-choice` y `--tool-call-parser`; para Qwen3 se usa `hermes` (junto con `--reasoning-parser qwen3` para modelos con thinking) — [vLLM docs Tool Calling v0.20.2](https://docs.vllm.ai/en/v0.20.2/features/tool_calling/), [Haystack docs](https://docs.haystack.deepset.ai/docs/vllmchatgenerator), [NVIDIA Dynamo tool-call parsing](https://docs.dynamo.nvidia.com/dynamo/dev/parsing/tool-call-parsing.md).
- vLLM **no implementa strict mode** para tools; en modo auto la validez de los argumentos depende del modelo y del parser — [vLLM docs Tool Calling](https://docs.vllm.ai/en/v0.20.2/features/tool_calling/). Structured outputs con backends xgrammar/guidance; los campos `guided_*` se eliminaron en v0.12.0 en favor de `structured_outputs` — [vLLM docs Structured Outputs v0.17](https://docs.vllm.ai/en/v0.17.1/features/structured_outputs/).

**SGLang (RadixAttention)**
- Reportes de "no kernel image is available" con wheels de sgl-kernel en SM_120/CUDA 12.8 — [Issue #9542](https://github.com/sgl-project/sglang/issues/9542), [Discussion #9543](https://github.com/sgl-project/sglang/discussions/9543).
- En RTX 5090 SGLang autoselecciona el backend `trtllm_mha` (sólo SM100) y falla al iniciar; hay que fijar `--attention-backend` — [Issue #14814](https://github.com/sgl-project/sglang/issues/14814).
- Issue de plan de optimización SM120: sólo xqa MHA soporta overlap con speculative decoding en SM120 — [Issue #19637](https://github.com/sgl-project/sglang/issues/19637).
- Parsers Qwen: docs de Qwen3 usan `--tool-call-parser qwen25`; Qwen3.5/3.6 recomiendan `qwen3_coder` + `--reasoning-parser qwen3` — [SGLang Qwen3 cookbook](https://docs.sglang.io/cookbook/autoregressive/Qwen/Qwen3), [Qwen3.6 cookbook](https://docs.sglang.io/cookbook/autoregressive/Qwen/Qwen3.6). Qwen-Agent advierte no agregar flags del parser hermes para Qwen3 bajo vLLM/SGLang cuando se usa Qwen-Agent (que parsea del lado cliente) — [Qwen-Agent docs](https://qwenlm.github.io/Qwen-Agent/en/guide/get_started/quickstart/).

**Ollama**
- `OLLAMA_NUM_PARALLEL` multiplica el contexto: el runner arranca con `num_ctx × slots`; default de 1 slot según la fuente — [SSD Nodes: num_ctx](https://www.ssdnodes.com/learn/lang/zh-hant/ollama-context-length-num-ctx), [Towards AI](https://pub.towardsai.net/how-to-use-unsloth-qwen3-8-27b-gguf-in-claude-code-via-ollama-without-dying-in-the-process-2-2-118f4effd428).
- `OLLAMA_KV_CACHE_TYPE` (f16 default, q8_0 ≈ ½, q4_0 ≈ ¼), requiere flash attention y es global para todos los modelos del server — [SSD Nodes: KV cache quantization](https://www.ssdnodes.com/learn/ollama-kv-cache-quantization).
- Medición (GPU y modelo no especificados en el snippet, mismo GGUF): 4 requests concurrentes → Ollama default (NUM_PARALLEL=1) 18,9 tok/s agregados / 54 s; llama-server 4 slots 28,9 tok/s / 35 s; Ollama NUM_PARALLEL=4 35,0 tok/s / 29 s — [DEV Community](https://dev.to/amareswer/ollama-vs-vllm-vs-llamacpp-which-local-llm-engine-58h6) (atribución por snippet de buscador; no se abrió la página).

**ExLlamaV3 + TabbyAPI**
- ExLlamaV3 lista "continuous, dynamic batching, speculative decoding, multimodal support"; TabbyAPI es el server oficial OpenAI-compatible — [exllamav3 README](https://github.com/turboderp-org/exllamav3). TabbyAPI: "parallel batching using paged attention for Nvidia Ampere GPUs and higher" y tool calling "revamped" sin plantillas Jinja modificadas — [tabbyAPI fork README](https://github.com/Vaasref/tabbyAPI), [DeepWiki tabbyAPI](https://deepwiki.com/theroyallab/tabbyAPI).
- sm_120: issue de illegal memory access en `coop_autotune.cu` en RTX 5060 Ti (WSL2), ExLlamaV3 v1.4.3, marcado cerrado — [Issue #307](https://github.com/turboderp-org/exllamav3/issues/307).

**LMDeploy / TurboMind**
- Desde v0.13.0 los wheels de PyPI se compilan contra CUDA 12.8, "sufficient for typical setups including GeForce RTX 50 series" — [LMDeploy README](https://github.com/InternLM/lmdeploy); feature request histórico — [Issue #3388](https://github.com/InternLM/lmdeploy/issues/3388).

**TensorRT-LLM, NIM, Aphrodite**
- TensorRT-LLM 0.17 agregó soporte Blackwell (sin nombrar GeForce explícitamente) — [NVIDIA dev blog](https://developer.nvidia.com/blog/new-ai-sdks-and-tools-released-for-nvidia-blackwell-geforce-rtx-50-series-gpus/).
- NIM para RTX AI PCs (CES 2025): soporte inicial para GeForce RTX 50, 4090, 4080 y RTX 6000/5000 profesionales — [NVIDIA press release](https://investor.nvidia.com/news/press-release-details/2025/NVIDIA-Launches-AI-Foundation-Models-for-RTX-AI-PCs/default.aspx). La RTX 3070/3060 no figuraba en la lista inicial.
- Aphrodite: fork de vLLM (PagedAttention); últimas releases encontradas de septiembre 2024 (v0.6.x); estado 2026 no confirmado — [PyPI aphrodite-engine](https://pypi.org/project/aphrodite-engine).

### Inferences
- **Corrección al supuesto del equipo**: "cada slot guarda su propia copia del prefijo, sólo entra 1 slot de 16k en 8 GB" describe llama-server con KV particionado y sin caché host. Con builds recientes (KV unificado + `--cache-ram` + `--cache-idle-slots`) el pool de KV se comparte y los estados de prompt inactivos se mueven a RAM y se restauran por similitud de prefijo. Esto **no** equivale a compartir un único bloque de KV del prefijo entre secuencias activas simultáneas como hace el PagedAttention+APC de vLLM o el radix tree de SGLang (no encontré evidencia de deduplicación del prefijo entre slots activos en llama-server; ver Gaps), pero sí reduce mucho el costo de re-prefill del prefijo de 10–12k al alternar conversaciones.
- Tabla de síntesis (inferida de las fuentes de arriba):

| Motor | Ampere 8 GB (3070) | sm_120 (5070) | Prefix cache entre requests | Continuous batching | Tools OpenAI `tools` | Gramáticas/structured |
|---|---|---|---|---|---|---|
| llama-server | Sí (GGUF Q4_K_M, KV q8_0) | Sí (con bugs puntuales) | Por slot + caché en RAM host; no dedupe entre slots activos (no verificado) | Sí (slots, `-np`) | Sí (plantillas Jinja) | GBNF / JSON schema |
| Ollama | Sí | Sí | Hereda llama.cpp, menos control | Sí con NUM_PARALLEL>1 | Sí | JSON schema |
| vLLM | Ajustado: AWQ 8B + poco KV | Frágil (compilar, forzar TRITON_ATTN para FP8 KV) | APC (bloques compartidos) | Sí | Sí (`hermes` para Qwen3), sin strict | xgrammar/guidance |
| SGLang | Ajustado | Frágil (fijar attention backend; imagen blackwell) | RadixAttention | Sí | Sí (`qwen25`/`qwen3_coder`) | xgrammar |
| ExLlamaV3/TabbyAPI | Sí (EXL3) | Bugs reportados | Paged attention + dedupe de prefijo (no verificado) | Sí | Sí | No verificado |
| LMDeploy | Sí (AWQ TurboMind) | Wheels CUDA 12.8 | Prefix caching (no verificado esta sesión) | Sí | No verificado | No verificado |
| TensorRT-LLM / NIM | NIM no lista 30xx | TRT-LLM Blackwell; NIM RTX 50 | Sí (no verificado en consumo) | Sí | Depende de NIM | — |

### Gaps
- No pude abrir el README del server de llama.cpp para confirmar si, con `--kv-unified`, varias secuencias activas comparten celdas del prefijo común (dedupe real) o sólo comparten el pool. Esto es decisivo para la concurrencia en 8 GB: hay que probarlo midiendo memoria con 2–4 slots y mismo prefijo.
- No verifiqué en esta sesión las flags `--slot-save-path` (save/restore de slots) ni `--cache-reuse` (reutilización por KV shifting); existen en versiones previas según conocimiento general, pero no tengo cita.
- Sin datos sobre prefix caching / tool-calling de LMDeploy y TensorRT-LLM en GPUs de consumo; sin datos de estado 2026 de Aphrodite.

## 2. Benchmarks medidos (7–8B y 12–14B; concurrencia 1/4/8/16; tarjetas 8–16 GB)

### Takeaway
Casi no hay benchmarks públicos con metodología clara de vLLM/SGLang bajo concurrencia en tarjetas de 8–12 GB; la evidencia disponible es (a) single-stream ~40 tok/s para 8B Q4 en 3060 12 GB / 4060 8 GB, (b) mediciones en GPUs grandes que muestran que llama.cpp ≈ vLLM a concurrencia 1 y vLLM escala mucho más con carga. Hay que medir en el hardware propio.

### Cited Findings
- RTX 3060 12 GB, llama.cpp, 8B Q4_K_XL, contexto 16K: **~42 tok/s** single-stream (dato de Hardware Corner citado por terceros) — [InsiderLLM](https://insiderllm.com/guides/llamacpp-vs-ollama-vs-vllm/) (atribución vía snippet; no se abrió la página original de Hardware Corner).
- RTX 4060 8 GB, Ollama 0.5.11, Q4, Llama 3.1 8B: **~41,7 tok/s** single-stream (tabla con layout dañado, valor leído por posición) — [DatabaseMart RTX 4060](https://databasemart.com/blog/ollama-gpu-benchmark-rtx4060).
- llama.cpp ≈ 3–10 % más rápido que Ollama single-user en NVIDIA — [InsiderLLM](https://insiderllm.com/guides/llamacpp-vs-ollama-vs-vllm/) / [PromptQuorum](https://www.promptquorum.com/local-llms/llamacpp-vs-ollama-vs-vllm) (atribución exacta incierta entre ambas).
- Red Hat (GuideLLM, H200, Llama 3.1 8B FP16, concurrencia 1→64): a concurrencia 1 rendimiento comparable; en carga pico vLLM >35× request throughput y >44× tokens/s de salida vs llama.cpp — [Red Hat Developer, 2025-09-30](https://developers.redhat.com/articles/2025/09/30/vllm-or-llamacpp-choosing-right-llm-inference-engine-your-use-case) (cifras tomadas de resúmenes secundarios; no pude abrir la página; no se sabe cuántos slots se configuraron en llama.cpp).
- Red Hat Ollama vs vLLM (A100 40 GB, Llama 3.1 8B FP16 en ambos, GuideLLM): pico reportado ~793 tok/s vLLM vs ~41 tok/s Ollama — [Red Hat Ollama vs vLLM](https://developers.redhat.com/articles/2025/08/08/ollama-vs-vllm-deep-dive-performance-benchmarking) (cifras de un resumen de terceros; no verificadas; probablemente Ollama con paralelismo default).
- RTX 3090 24 GB (home lab; modelos Gemma 3 1B, Qwen3-Coder 30B-A3B, Gemma 4 26B-A4B): continuous batching de vLLM escala el throughput agregado 3,9–5,4×; a concurrencia 8 vLLM supera a llama.cpp 2,9–3,7× — [DEV Community (sikamikanikobg)](https://dev.to/sikamikanikobg/vllm-vs-llamacpp-vs-ollama-what-happens-when-your-model-doesnt-fit-in-24gb-vram-56eb) (atribución por snippet, no abierta).
- Una guía sostiene que un 14B Q4_K_M que entra en 12 GB con llama.cpp puede requerir 16 GB con vLLM (no verificado por la fuente que lo resumió) — [InsiderLLM](https://insiderllm.com/guides/llamacpp-vs-ollama-vs-vllm/).
- Una tabla que reporta vLLM en RTX 3060 a 180 tok/s con batch 8 se considera poco confiable (inconsistencias internas) — [PromptQuorum](https://www.promptquorum.com/local-llms/llamacpp-vs-ollama-vs-vllm).
- Referencias vLLM 8B en GPUs grandes (DatabaseMart, 50–100 requests concurrentes, entrada 100 / salida 600 tokens): A6000 48 GB Llama-3.1-8B ~2.658 tok/s totales — [DatabaseMart A6000](https://www.databasemart.com/blog/vllm-gpu-benchmark-a6000). DatabaseMart no publica benchmarks vLLM en 3060/4060/A4000.

### Inferences
- A concurrencia 1, cambiar de llama-server a vLLM no mejora la latencia por usuario; el beneficio aparece cuando hay ≥4 requests simultáneos y depende de que el KV quepa (en 8 GB, vLLM no tiene margen).
- Con un 8B Q4 a ~40 tok/s single-stream en 3060/4060, decodificar una respuesta de 300 tokens tarda ~7,5 s (aritmética sobre las cifras citadas). La RTX 3070 (más ancho de banda que la 3060) debería estar algo por encima, y la 5070 (GDDR7) por encima de ambas, pero no encontré mediciones citables.

### Gaps
- No hay benchmarks citables de vLLM/SGLang/llama-server con concurrencia 4/8/16 en RTX 3070, 3060 12 GB o 5070 con Qwen3-8B, ni TTFT con prefijos de 10–12k. Tampoco cifras de prefill (pp tok/s) para estas tarjetas. Recomendación: medir con `llama-bench` (pp/tg) y con `vllm bench serve` / GuideLLM a concurrencia fija ([guía Arm](https://learn.arm.com/learning-paths/servers-and-cloud-computing/vllm-benchmark-quantisation/4-benchmarking/)).
- Sin datos de 12–14B en estas tarjetas bajo concurrencia.

## 3. Cómo el loop agéntico afecta latencia y concurrencia (vs 2 llamadas por turno)

### Takeaway
Un agente convierte un turno de 2 llamadas en N llamadas secuenciales (típicamente más), cada una con TTFT + decode + tiempo de herramienta; la latencia percibida escala ~linealmente con N salvo que cada llamada reutilice el KV del contexto previo. La literatura 2025–2026 (InferCept, Autellix, KVFlow, Continuum) muestra que la política LRU por defecto descarta el KV justo durante la pausa de la herramienta y que retenerlo/fijarlo con TTL da mejoras de hasta ~2×. Speculative decoding ayuda mucho a batch 1 pero se degrada con concurrencia.

### Cited Findings
- **Continuum** (arXiv 2511.02230, ICLR 2026): para requests que generan tool calls, fija selectivamente el KV en GPU con un time-to-live calculado a partir del costo de recarga y del retraso de cola inducido por el desalojo; combina esto con FCFS a nivel de programa; reporta superar al estado del arte — [arXiv 2511.02230](https://arxiv.org/abs/2511.02230), [ICLR 2026](https://iclr.cc/virtual/2026/10012473), [Berkeley tech report](https://www2.eecs.berkeley.edu/Pubs/TechRpts/2026/EECS-2026-234.pdf). (No pude abrir el paper para extraer las cifras de mejora).
- **Autellix**: scheduler a nivel de programa con Program-Level Attained Service (PLAS), prioriza programas con menos servicio acumulado; Continuum le critica no considerar duración variable de tool calls y usar desalojo al fin de turno (crítica de un competidor) — [Continuum](https://arxiv.org/html/2511.02230v5).
- **InferCept**: introduce la operación "preserve" que retiene el KV entre tool calls — descrito en [Continuum](https://arxiv.org/abs/2511.02230).
- **KVFlow** (arXiv 2507.07400, NeurIPS 2025): los sistemas actuales desalojan con LRU, "which fails to anticipate future agent usage and often discards KV caches shortly before their reuse"; con Agent Step Graph y prefetch CPU→GPU logra hasta 1,83× (workflow único con prompts grandes) y 2,19× (muchos workflows concurrentes) — [arXiv 2507.07400](https://arxiv.org/abs/2507.07400), [NeurIPS poster](https://neurips.cc/virtual/2025/poster/119883).
- Trabajos 2026 que refuerzan que la reutilización futura del KV depende de la semántica de ejecución del agente más que de la recencia: [Learning Agent Execution for KV-Cache Management (arXiv 2608.14624)](https://arxiv.org/pdf/2608.14624), [TraceLab: coding agent workloads (arXiv 2606.30560)](https://arxiv.org/pdf/2606.30560), [Unified AI Gateway (arXiv 2609.06940)](https://arxiv.org/pdf/2609.06940).
- **Speculative decoding en workloads de agente** (AgentSpec, vLLM, Qwen3-8B): EAGLE-3 >2,5× a batch máximo 1, la ganancia cae rápido con batch y por encima de batch 32 es más lenta que decodificación autoregresiva — [AgentSpec arXiv 2608.24004](https://arxiv.org/pdf/2608.24004) (vía snippet).
- SGLang (H100, Llama 3.1 8B, MT-Bench): EAGLE 1,40× a batch 2 → 0,93× a batch 24; EAGLE-3 se mantiene 1,32–1,48× de batch 16 a 64 — [EAGLE-3 paper](https://arxiv.org/pdf/2503.01840). Estudio MLSys 2026 (vLLM 0.10–0.11, H100): batches mayores reducen el speedup de speculative decoding — [MLSys 2026 "Speculative Decoding: Performance or Illusion?"](https://mlsys.org/virtual/2026/oral/3782).
- En SM120 (SGLang), sólo xqa MHA soporta overlap con speculative decoding — [SGLang Issue #19637](https://github.com/sgl-project/sglang/issues/19637).

### Inferences
- **Modelo de latencia por turno** (inferencia propia): `T_turno ≈ Σ_i [TTFT_i + tokens_out_i / tps_stream] + Σ_j t_tool_j`. Text-to-SQL actual: 2 llamadas (generar SQL; redactar respuesta) + 1 ejecución SQL. Agente: típicamente 3–6 llamadas (decidir tool → recibir resultado → otra tool o reintento → respuesta final). Con ~40 tok/s y ~100 tokens por tool call, cada paso intermedio suma ≥2,5 s de decode más TTFT y la herramienta; a 4–5 pasos la latencia percibida se duplica o triplica respecto del pipeline de 2 llamadas, aun con caché perfecta.
- **TTFT por paso con y sin caché**: si el prefijo (system + schema + definiciones de tools) es estable y los resultados de tools se agregan al final, cada paso sólo prefillea los tokens nuevos (resultado de la tool, típicamente cientos a pocos miles). Si el KV se pierde entre pasos (otro slot, desalojo LRU, cambio de plantilla), cada paso re-prefillea 10–12k tokens + historial, y ese costo se multiplica por N pasos y por usuarios concurrentes. Por eso en el agente la tasa de acierto de caché importa N veces más que en Text-to-SQL.
- **Riesgos que rompen el prefijo en agentes**: (a) definiciones de tools que cambian de orden o contenido por request (p. ej. un servidor MCP que lista tools dinámicamente); (b) plantillas de chat que reescriben turnos anteriores (las de Qwen3 eliminan el razonamiento de turnos previos — verificar con la plantilla usada); (c) timestamps o IDs en el system prompt; (d) en llama-server, que la conversación caiga en otro slot. Todos convierten un hit en miss.
- **Concurrencia efectiva**: con un agente, cada usuario mantiene un contexto que crece con cada resultado de tool y permanece "vivo" durante las pausas de la herramienta (que es justo lo que estudian InferCept/Continuum). En una GPU de 8–12 GB eso reduce el número de conversaciones cuyo KV puede quedar residente; en llama-server, `--cache-idle-slots` + `--cache-ram` es lo más parecido a "preservar" el KV en RAM durante la pausa.
- **Speculative decoding**: para 10–30 usuarios con baja concurrencia simultánea real (1–4 en vuelo) en una sola GPU, puede seguir rindiendo; con 8+ en vuelo la ganancia cae. En 8 GB el modelo draft además compite por VRAM con el KV. No es la primera palanca.

### Gaps
- No pude extraer de los papers (arxiv bloqueado) cifras de caracterización: cantidad típica de llamadas por tarea, duración de tools, fracción de tiempo en tools, ni mejoras de latencia de Continuum. Ninguno de esos papers mide GPUs de consumo de 8–12 GB.
- No encontré mediciones de workloads agénticos en GPUs pequeñas.

## 4. Memoria: KV por token y cuántos contextos de 8k–16k entran en 8 vs 12 GB

### Takeaway
Qwen3-8B (36 capas, 8 KV heads, head_dim 128) ocupa **144 KiB/token en FP16** y ~72 KiB/token en FP8 (q8_0 ≈ 76,5 KiB). Un contexto de 16k son ~2,25 GiB FP16 / ~1,1–1,2 GiB en 8 bits. En 8 GB, tras pesos Q4_K_M (~5 GB), queda espacio para ~1,5–2 GB de KV: un contexto de 16k (o ~20–25k tokens totales con q8_0). En 12 GB con AWQ + FP8 KV + APC, el total de tokens residentes ronda 45–55k; la estimación del equipo de ~8 turnos en vuelo es plausible para Text-to-SQL, pero baja a ~3–6 para un agente con contexto creciente.

### Cited Findings
- Qwen3-8B: KV elements/token = 2 × 36 capas × 8 KV heads × 128 = 73.728 → 144 KiB/token en bf16; 32.768 tokens ≈ 4,5 GiB — [Sebastian Raschka, KV cache calculations](https://sebastianraschka.com/llm-architecture-gallery/kv-cache-calculations), [youngju.dev config.json](https://www.youngju.dev/blog/ai-papers/2026-08-12-model-internals-how-to-read-a-config-json.en).
- KV q8_0 ≈ ½ de f16 y q4_0 ≈ ¼ (FAQ de Ollama citada) — [SSD Nodes](https://www.ssdnodes.com/learn/ollama-kv-cache-quantization).
- En vLLM con AWQ, el fix de FP8 KV "doubled KV capacity" (RTX 5090, Gemma AWQ) — [PR #39255](https://github.com/vllm-project/vllm/pull/39255).
- Modelos híbridos GDN+Attention (familia Qwen3.5) usan una arquitectura distinta a la de Qwen3 — [vLLM Issue #37554](https://github.com/vllm-project/vllm/issues/37554).

### Inferences
Cálculos propios (aritmética sobre 144 KiB/token; pesos y overheads son **estimaciones**, no medidas):

| Concepto | FP16 KV | 8-bit KV (FP8 / q8_0) |
|---|---|---|
| Prefijo 12k tokens | 1,69 GiB | 0,84–0,90 GiB |
| Contexto 8k | 1,13 GiB | 0,56–0,60 GiB |
| Contexto 16k | 2,25 GiB | 1,13–1,20 GiB |

- **RTX 3070 8 GB, llama-server, Qwen3-8B Q4_K_M** (~5 GB de pesos; buffers de cómputo y contexto CUDA ~0,7–1 GB; estimación): quedan ~1,5–2 GB de KV → FP16 ≈ 10–14k tokens totales; q8_0 ≈ 20–27k tokens totales. Si el prefijo de 12k no se deduplica entre slots activos, entran **1 conversación de 16k** (como observó el equipo) o 2 de ~10–12k con q8_0. Con `--cache-ram`, conversaciones inactivas se mueven a RAM del host y vuelven sin re-prefill completo.
- **12 GB (3060 12 GB con GGUF, o 5070 con vLLM AWQ)**: pesos AWQ 8B ~5,7–6 GB (embeddings/lm_head sin cuantizar; estimación), con `gpu_memory_utilization` 0,9 y ~1–1,5 GB para activaciones/CUDA graphs quedan ~3,5–4 GB de KV → FP8 ≈ 50–58k tokens. Con APC el prefijo de 12k se almacena una vez: `N ≈ (KV_total − 12k) / contexto_único_por_conversación`.
  - Text-to-SQL (único por turno ~3–5k: pregunta + SQL + filas + respuesta): N ≈ 8–12 → consistente con la estimación del equipo (~8).
  - Agente (único por conversación 6–12k: definiciones de tools si no están en el prefijo común, varios resultados de tools, historial): N ≈ 3–7.
- **Qwen3-4B y Qwen3-14B** (configuración según conocimiento previo, no verificada en esta sesión): 4B ≈ 36 capas × 8 KV heads × 128 → mismo 144 KiB/token que el 8B (el ahorro está en pesos, ~2,5 GB Q4, no en KV); 14B ≈ 40 capas × 8 × 128 → 160 KiB/token, pesos Q4 ~9 GB → en 12 GB deja ~1,5–2 GB de KV (≈1 contexto de 16k con 8-bit) y no entra cómodo en 8 GB.
- Modelos híbridos (GDN/atención lineal en la mayoría de capas, p. ej. Qwen3.5) reducirían drásticamente el KV por token; si el equipo evalúa esa familia, el cálculo cambia y conviene rehacerlo con su config.json.

### Gaps
- No pude abrir los config.json de Hugging Face (bloqueado) para verificar Qwen3-4B/14B ni medir el tamaño exacto de checkpoints AWQ.
- Overheads reales (CUDA graphs, buffers de compute de llama.cpp, fragmentación de paginación de vLLM, display del SO si la GPU también maneja escritorio) deben medirse; pueden mover los números ±0,5–1 GB, que en 8 GB es el 30–50 % del presupuesto de KV.

## 5. Recomendaciones prácticas para 10–30 usuarios en una GPU de consumo; hardware

### Takeaway
Con 10–30 usuarios registrados, la concurrencia simultánea real suele ser baja, pero el agente multiplica el tiempo de ocupación de la GPU por turno. La receta razonable es: cola con admisión acotada al número de contextos que caben, timeouts por paso y por turno, maximizar aciertos de caché de prefijo (orden estable: system → tools → schema → ejemplos → conversación), y desbordar a una API en la nube cuando la cola supera un umbral. No existe RTX 3070 de 12 GB oficial.

### Cited Findings
- Ollama default sirve requests en serie (NUM_PARALLEL=1): 4 requests tardaron 54 s vs 29 s con NUM_PARALLEL=4 — [DEV Community](https://dev.to/amareswer/ollama-vs-vllm-vs-llamacpp-which-local-llm-engine-58h6).
- vLLM es la opción cuando se necesita "predictable time-to-first-token under load"; llama.cpp tiene TTFT rápido local y throughput plano bajo carga — [Markaicode llama.cpp vs vLLM](https://markaicode.com/vs/llamacpp-vs-vllm/).
- KVFlow/Continuum: el desalojo LRU descarta KV justo antes de reutilizarlo en agentes — [KVFlow](https://arxiv.org/abs/2507.07400), [Continuum](https://arxiv.org/abs/2511.02230).
- Speculative decoding: medir el break-even a la concurrencia objetivo antes de activarlo — [PremAI blog](https://blog.premai.io/speculative-decoding-2-3x-faster-llm-inference-2026) (fuente secundaria), respaldado por [AgentSpec](https://arxiv.org/pdf/2608.24004) y [EAGLE-3](https://arxiv.org/pdf/2503.01840).
- **Hardware**: la RTX 3070 Ti 16 GB fue cancelada en 2022 — [Igor's Lab](https://www.igorslab.de/en/the-geforce-rtx-3070-ti-16gb-doesnt-come-into-arc-promises-again-and-the-gddr6x-is-itching/), [TechRadar](https://www.techradar.com/news/nvidias-supercharged-rtx-3090-ti-could-arrive-in-a-few-weeks-but-the-rtx-3070-ti-16gb-is-dead). Existen mods artesanales de RTX 3070 a 16 GB (resoldado de memorias, puentes en PCB, estabilidad variable; sin pruebas de LLM) — [Tom's Hardware](https://www.tomshardware.com/news/16gb-rtx-3070-mod), [Tom's Hardware rendimiento](https://www.tomshardware.com/news/3070-16gb-mod). RTX 3080 12 GB existe (enero 2022; GDDR6X 384 bits, 350 W) — [AnandTech](https://www.anandtech.com/show/17204). La serie RTX 50 incluye 5060 Ti 8/16 GB y 5070 12 GB — [Wikipedia RTX 50](https://en.wikipedia.org/wiki/GeForce_RTX_50_series).

### Inferences
- **No existe RTX 3070 de 12 GB oficial** (stock 8 GB; sólo mods a 16 GB no soportados). Las alternativas Ampere de 12 GB son la RTX 3060 12 GB y la RTX 3080 12 GB; en Blackwell, la 5070 12 GB o la 5060 Ti 16 GB (esta última da más VRAM para KV aunque menos ancho de banda — no verificado en esta sesión).
- **Admisión y cola** (diseño propio, sin fuente de producción citable): limitar turnos en vuelo a lo que cabe en KV (en 8 GB: 1–2; en 12 GB con vLLM: 4–8 para Text-to-SQL, 3–5 para agente); cola FIFO por turno (no por llamada) para que un agente a mitad de loop no pierda su KV frente a turnos nuevos — es la idea de program-level scheduling de Autellix/Continuum aplicada a pequeña escala; límite de pasos del agente (p. ej. 4–6) y timeout por paso y por turno; si la espera estimada en cola supera un umbral (p. ej. 10–15 s) o hay timeouts, derivar el turno a una API en la nube con el mismo contrato de tools.
- **Workload Text-to-SQL vs agente en esta GPU**: el pipeline de 2 llamadas es más amable con hardware de 8–12 GB (menos pasos, contexto único acotado, prefijo estable). Un agente es viable en 12 GB con vLLM/SGLang si (a) las definiciones de tools viven dentro del prefijo estable, (b) los resultados de tools se truncan/resumen antes de reinyectarlos, y (c) se limita el número de pasos. En 8 GB con llama-server, un agente para 10–30 usuarios probablemente requiera fallback a la nube en horas pico.
- **Fallback**: la ruta híbrida evita sobredimensionar la GPU; sólo exige que el contrato de tools (JSON schema) sea portable y que la política de datos permita enviar el contexto (schema, filas) al proveedor externo — tener en cuenta la regla del repo de no exponer PII.

### Gaps
- No encontré reportes de producción citables (blogs con métricas) de 10–30 usuarios sobre una sola GPU de consumo con agentes; la guía de Big Iron sobre colas en llama-server estaba bloqueada.
- No verifiqué en esta sesión los parámetros de cola de Ollama (`OLLAMA_MAX_QUEUE`) ni las métricas de llama-server (`/metrics`, `/slots`) más allá de `LLAMA_SERVER_SLOTS_DEBUG` — [PR #16391](https://github.com/ggml-org/llama.cpp/pull/16391).
- Faltan cifras de ancho de banda/precio actualizadas para comparar 5070 12 GB vs 5060 Ti 16 GB vs 3060 12 GB.
