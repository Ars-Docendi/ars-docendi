# Modelos locales (8 GB / 12 GB VRAM) para tool calling / MCP vs Text-to-SQL

> Notas de investigación (fecha de corte: octubre 2026). Limitación metodológica: los dominios huggingface.co, arxiv.org y benchmarklist.com estaban bloqueados para lectura directa en este entorno; varias cifras se obtuvieron de fragmentos de búsqueda que citan esas páginas (model cards / papers). Se marca en cada caso si el número es **oficial** (model card / paper del autor) o **independiente** (leaderboard o evaluación de terceros), y cuándo no se pudo verificar contra la fuente primaria.

## 1. Resultados de benchmarks de tool-use para los modelos candidatos

### Takeaway
En el rango 4–14B, la familia Qwen domina consistentemente tanto en leaderboards (BFCL) como en evaluaciones independientes "realistas" (Docker); en 2026 el candidato más fuerte que entra en 8 GB es **Qwen3.5-9B** (oficial: BFCL-V4 66.1, TAU2-Bench 79.1), y en 12 GB siguen siendo Qwen3-14B o Qwen3.5-9B con más contexto/slots. Los modelos "especialistas" de function calling (xLAM-2-8B, watt-tool-8B, ToolACE-2-8B) puntúan alto en BFCL pero rindieron mal en una evaluación conversacional independiente, y xLAM-2 tiene licencia no comercial. No hay números publicados de modelos ≤14B en los benchmarks MCP más duros (MCPMark), y gpt-oss-20b no entra completo en 12 GB.

### Cited Findings

**BFCL (Berkeley Function Calling Leaderboard)**
- Snapshot de BFCL-v3 (agregador, etiquetado "Verified", muestreado 2026-08-27): xLAM-2-8b-fc-r (FC) 72.04% (4.º), ToolACE-2-8B (FC) 68.73% (9.º), Qwen3-8B (FC) 66.34% (16.º). Independiente/agregador; no se pudo abrir la página directamente. — [benchmarklist BFCL-v3](https://benchmarklist.com/benchmarks/bfcl_v3/)
- Verificación cruzada: el paper LoopTool reporta que LoopTool-8B logra 74.93% en BFCL-v3, "+8.59" sobre Qwen3-8B original → ≈66.34%, consistente con el snapshot. — [LoopTool (arXiv 2511.09148)](https://arxiv.org/pdf/2511.09148)
- En BFCL v4, sección Multi-Turn, xLAM-2-8b-fc-r aparece 2.º (detrás de un modelo 70B); en Live Accuracy v4, Qwen3-8B tiene 80.53% (10.º). Datos por categoría, no overall. — [benchmarklist BFCL-V4](https://benchmarklist.com/benchmarks/bfcl_v4/)
- Un agregador lista "ToolACE-8B ≈91.5 en BFCLv4" sin fecha ni fuente; inconsistente con todo lo demás → descartar. — [benchmarklist BFCL-V4](https://benchmarklist.com/benchmarks/bfcl_v4/)
- Qwen3-4B (oficial, model cards 2507): BFCL-v3 Qwen3-4B Thinking 65.9 → Qwen3-4B-Thinking-2507 71.2; Qwen3-4B Non-Thinking 57.6 (card de Instruct-2507). — [Qwen3-4B-Thinking-2507](https://huggingface.co/Qwen/Qwen3-4B-Thinking-2507); [Qwen3-4B-Instruct-2507](https://huggingface.co/Qwen/Qwen3-4B-Instruct-2507)
- El Qwen3 Technical Report evalúa BFCL v3 en formato FC, extiende contexto con YaRN a 64k para multi-turn y para baselines toma el mayor entre FC y Prompt. No pude extraer con fiabilidad los valores por modelo (thinking/non-thinking) para 8B/14B. — [Qwen3 Technical Report](https://arxiv.org/pdf/2505.09388)

**Qwen3.5 (2026; modelos pequeños 0.8B/2B/4B/9B, arquitectura híbrida Gated DeltaNet + Gated Attention)**
- Oficial (model card Qwen/Qwen3.5-9B): **BFCL-V4: 9B = 66.1, 4B = 50.3**; **TAU2-Bench: 9B = 79.1, 4B = 79.9**. En la misma tabla Qwen3-30B-A3B-Thinking-2507 obtiene 42.4 (BFCL-V4) y 41.9 (TAU2). Advertencia del card: TAU2 corrido con setup oficial salvo el dominio airline, donde aplicaron las correcciones del system card de Claude Opus 4.5 → no es directamente comparable con otros reportes de TAU2. — [Qwen/Qwen3.5-9B](https://huggingface.co/Qwen/Qwen3.5-9B); confirmado por [Together AI](https://www.together.ai/models/qwen3-5-9b)
- Cuantización: una variante NVFP4 (tercero) reporta BFCL-V4 65.0 y TAU2 77.9 → pérdida de ~1 punto. — [AxionML/Qwen3.5-9B-NVFP4](https://huggingface.co/AxionML/Qwen3.5-9B-NVFP4)
- Arquitectura: 8 bloques de (3× Gated DeltaNet→FFN + 1× Gated Attention→FFN); contexto nativo 262,144 tokens. "only 8/32 layers have full attention". — [Qwen/Qwen3.5-9B](https://huggingface.co/Qwen/Qwen3.5-9B); [mlabonne, Qwen3.5 blog](https://huggingface.co/blog/mlabonne/qwen35)
- Fecha de lanzamiento contradictoria (febrero vs 2 de marzo de 2026). — [apxml](https://apxml.com/models/qwen35-9b); [codersera](https://codersera.com/blog/qwen-3-5-complete-guide-2026/)
- En los Qwen3.5 0.8B/2B/4B/9B el razonamiento (thinking) está **desactivado por defecto**; se habilita vía chat-template kwarg. — [Unsloth Qwen3.5](https://unsloth.ai/docs/models/qwen3.5)
- Existen generaciones posteriores (Qwen3.6, Qwen3.7, Qwen3.8-27B), pero las referencias encontradas son a tamaños ≥27B o MoE grandes; Qwen3.7 reporta MCP-Mark sólo para modelos grandes (Qwen3.7-Max 60.8). — [Qwen3.7 blog](https://qwen.ai/blog?id=qwen3.7); [llama.cpp #27756](https://github.com/ggml-org/llama.cpp/issues/27756)

**Gemma 4 (Google, 2026)**
- Según el model card citado por agregadores/búsqueda: Tau2 (promedio de 3 corridas) 31B = 76.9%, 26B-A4B (MoE, ~3.8B activos) = 68.2%, 12B = 69.0%, E4B = 42.2%, E2B = 24.5%; Gemma 3 27B = 16.2% en la misma medida. No pude verificar directamente el card; la existencia de un "Gemma 4 12B" la reportan varios agregadores pero no la confirmé en fuente primaria. Sin cifras BFCL. — [google/gemma-4-26B-A4B-it](https://huggingface.co/google/gemma-4-26B-A4B-it); [llm-stats Gemma 4 12B vs Qwen3.5-9B](https://llm-stats.com/models/compare/gemma-4-12b-it-vs-qwen3.5-9b); [Gemma 4 Technical Report](https://arxiv.org/html/2607.02770v1)
- Comparativa cabeza a cabeza (tercero): TAU2 Gemma 4 12B 69.0% vs Qwen3.5-9B 79.1%. — [betterclaw](https://www.betterclaw.io/blog/gemma-4-12b-vs-qwen-3-5-9b)

**Evaluación independiente Docker (junio 2025)** — la más cercana al caso "agente con pocas tools"
- Metodología: framework propio `model-test`, escenario e-commerce con pocas herramientas (≈5, p. ej. `search_products`, `add_to_cart`), suites Simple y Complex (multi-step / encadenamiento), hasta 5 rondas de agente, 21 modelos, 3,570 casos, 210 corridas, MacBook Pro M4 Max. Métrica: F1 de selección de herramienta (invocación + selección + parámetros); latencia aparte. — [Docker blog](https://www.docker.com/blog/local-llm-tool-calling-a-practical-evaluation/)
- Resultados F1 (locales): qwen3:14B-Q4_K_M **0.971** (~142 s), qwen3:14B-Q6_K 0.943, qwen3:8B-F16 0.933, **qwen3:8B-Q4_K_M 0.919** (~84 s), llama3.1:8B-F16 0.835, qwen2.5:14B-Q4_K_M 0.812, llama3.1:8B-Q4_K_M 0.793, qwen2.5:7B-Q4_K_M 0.753, gemma3:4B 0.733, llama3.2:3B 0.727, llama3.3:70B-Q4_K_M 0.607, **llama-xlam:8B-Q4_K_M 0.570**, **watt-tool:8B-Q4_K_M 0.484**. Referencias hosted: GPT-4 0.974, Claude 3 Haiku 0.933, GPT-4o 0.857. — [Docker blog](https://www.docker.com/blog/local-llm-tool-calling-a-practical-evaluation/)
- Docker: "no significant difference" entre variantes cuantizadas y no cuantizadas en sus escenarios; recomiendan Qwen3 14B u 8B para máxima precisión. Nota: la cifra 0.971 del 14B Q4 > 0.943 del Q6 sugiere ruido entre corridas. — [Docker blog](https://www.docker.com/blog/local-llm-tool-calling-a-practical-evaluation/)

**τ-bench / tau2-bench**
- xLAM-2-8b-fc-r (oficial, paper APIGen-MT): τ-bench pass@1 retail 58.2, airline 35.2, overall 46.7. Licencia CC-BY-NC-4.0 (no comercial). — [APIGen-MT (arXiv 2504.03601)](https://arxiv.org/html/2504.03601v2); [vdf.ai roundup](https://vdf.ai/blog/best-small-language-models)

**MCP-específicos**
- MCPMark: los autores **excluyeron deliberadamente** modelos open-source ≤100B por la dificultad; el mejor open-source evaluado ≈25% pass@1, varios <10%; mejor global (variante GPT-5) ≈53%. — [MCPMark (arXiv 2509.24002)](https://arxiv.org/html/2509.24002)
- MCP-Bench: Llama-3.1-8B-Instruct obtiene 0.428 overall, rezagado en "dependency awareness" y paralelismo; los modelos más débiles degradan más al aumentar el número de servidores MCP. No vi cifra para Qwen3-8B en los fragmentos. — [MCP-Bench (arXiv 2508.20453)](https://arxiv.org/pdf/2508.20453)
- MCP-Universe (231 tareas, 11 servidores MCP) y LiveMCPBench (95 tareas, 70 servidores, 527 tools): no encontré resultados de modelos pequeños. — [MCP-Universe](https://ar5iv.labs.arxiv.org/html/2508.14704); [HF papers MCP-Bench](https://huggingface.co/papers?q=MCP-Bench)

**Otros candidatos**
- IBM Granite: card de Granite-4.0-H-Micro (3B) lista BFCL v3 57.56 (oficial); una corrida local independiente de Granite 4.0 obtuvo 34.8% en BFCL v3 (1,000 prompts), atribuyendo la diferencia al peso de prompts simples. Granite 4.2 (card): BFCL v4 3B 52.41, 8B 52.39, 30B 61.39 (otra guía reporta 50.29 para el 8B → conflicto). — [docker/model-cards granite-4.0-h-micro](https://github.com/docker/model-cards/blob/main/ai/granite-4.0-h-micro.md); [Abivarma/Granite4-1](https://github.com/Abivarma/Granite4-1); [ibm-granite/granite-4.2-3b](https://huggingface.co/ibm-granite/granite-4.2-3b); [intuitionlabs Granite 4.2](https://intuitionlabs.ai/articles/ibm-granite-4-2-model-guide)
- Ministral 3 (3B/8B/14B, dic. 2025, Apache 2.0, 256K contexto, function calling nativo, parser `mistral` en vLLM): no encontré cifras BFCL ni τ²; el 14B sin cuantizar requiere ~24 GB. — [mistralai/Ministral-3-8B-Instruct-2512](https://huggingface.co/mistralai/Ministral-3-8B-Instruct-2512); [Unsloth Ministral 3](https://unsloth.ai/docs/models/tutorials/ministral-3)
- gpt-oss-20b (MoE, MXFP4): GGUF ≈12.1 GB / 11.27 GiB; la guía oficial de llama.cpp estima 14.9 GB a 8K de contexto → **no entra completo en 12 GB**; receta en RTX 3080 Ti (12 GB) con `--n-cpu-moe` deja ~600 MB libres a 16K y ~64 tok/s. OpenAI afirma buen desempeño en τ-bench pero sin cifra numérica para el 20B en lo encontrado; sin BFCL. — [smeltcore](https://smeltcore.com/recipes/gpt-oss-20b-on-rtx-3080-ti-mxfp4-chat-in-12-gb-via-llama-cpp-expert-offload/); [OpenAI, Introducing gpt-oss](https://openai.com/index/introducing-gpt-oss/); [bartowski GGUF](https://huggingface.co/bartowski/openai_gpt-oss-20b-GGUF)

**VRAM / KV-cache (relevante para concurrencia)**
- Qwen3.5-9B Q4_K_M: pesos ≈5.8 GB; KV ≈0.98 GB a 32K y ≈1.97 GB a 64K → ≈6.8 / 7.8 GB totales; "32K entra en 8 GB, 64K se pone justo". — [insiderllm](https://insiderllm.com/guides/qwen-3-5-9b-setup-guide/); [willitrunai](https://willitrunai.com/blog/qwen-3-gpu-requirements)
- En llama-server, el buffer de memoria recurrente de Qwen3.5 creció de 50 MiB (1 slot) a 201 MiB (4 slots); el default `-np -1` suele resolver a 4 slots con KV unificado. — [architecture-performance.fr](https://architecture-performance.fr/en/blog/running-a-local-llm-on-a-consumer-gpu-8-gb-vram)

### Inferences
- Para 8 GB: Qwen3.5-9B (Q4_K_M) parece la mejor opción 2026 por BFCL-V4/TAU2 oficiales y, sobre todo, por su KV-cache pequeño (sólo 1/4 de capas con atención completa), que deja margen para varios slots concurrentes — algo que Qwen3-8B (atención completa en todas las capas) no ofrece igual. Qwen3-8B Q4_K_M sigue siendo una base sólida con evidencia independiente (Docker F1 0.919).
- Para 12 GB: Qwen3-14B Q4_K_M (≈9 GB de pesos, estimación propia, no verificada) tiene la mejor evidencia independiente (Docker F1 0.971) pero deja poco espacio de KV para concurrencia; Qwen3.5-9B a Q5/Q6 con más slots probablemente sea mejor compromiso en una 5070 12 GB. Gemma 4 12B (si existe tal como se reporta) queda por debajo de Qwen3.5-9B en TAU2.
- Los modelos especialistas (xLAM-2, watt-tool, ToolACE) están sobre-ajustados al formato BFCL; su caída en evaluaciones conversacionales (Docker) y la licencia NC de xLAM-2 los descartan para producción institucional.
- Los números BFCL/TAU2 de Qwen3.5 son autoreportados; no hay todavía verificación independiente amplia.

### Gaps
- No obtuve cifras BFCL v4 overall verificadas en el leaderboard oficial (gorilla.cs.berkeley.edu) para Qwen3-8B/14B, Gemma 4, Ministral 3, Phi-4-mini, Hammer 2.x, GLM-4-9B ni DeepSeek-R1-distill.
- No encontré resultados de modelos ≤14B en MCP-Universe, LiveMCPBench, MCPToolBench++ ni MCP-RADAR; tampoco ACEBench/NexusRaven/StableToolBench actualizados para estos modelos.
- No verifiqué la existencia/especificaciones de "Gemma 4 12B" en fuente primaria (Google).
- No hay medición pública de tool calling de Qwen3-14B con varios usuarios concurrentes en 12 GB.

## 2. Degradación con número de tools, esquemas largos, cadenas multi-paso y consultas en español

### Takeaway
La precisión de selección cae fuertemente al crecer el número de tools visibles (RAG-MCP: 85% con 5 tools → 45% con 20, en modelos grandes), y los modelos pequeños degradan más; recuperar 3–5 tools relevantes por consulta es la mitigación más respaldada. El español está entre los idiomas que menos pierden respecto del inglés, pero la caída existe (≈5 puntos en τ-Multilingual) y casi no hay datos específicos para modelos pequeños.

### Cited Findings
- RAG-MCP: precisión baseline de selección cae de 85% (5 tools) a 45% (20 tools); con retrieval se mantiene >80% hasta 50 tools; en el stress test MCP la precisión pasa de 13.62% a 43.13% y los tokens de prompt bajan >50%; k óptimo de tools recuperadas entre 3 y 5 (más k = más distractores). — [RAG-MCP (arXiv 2505.03275)](https://www.alphaxiv.org/abs/2505.03275)
- En ToolBench, un baseline ingenuo cae de 92.4% (N=15) a 78.2% (N=640); a N=3,616 un método híbrido logra 81.6% vs 72.1% RAG-MCP y 70.6% ToolLLM. — [Scalable LLM Agent Tool Access (arXiv 2607.15593)](https://arxiv.org/pdf/2607.15593)
- Con Claude Sonnet 4.6, mostrar menos tools mejora la elección: 93.1% con ~2.2 candidatas vs 87.1% con 5 fijas. — [How Many Tools Should an LLM Agent See? (arXiv 2605.24660)](https://arxiv.org/html/2605.24660v1)
- OSWorld-MCP: sin filtrado por retrieval sobre 158 tools, la precisión cae de 20.5 a 15.5; las descripciones largas "markedly reduce the model's tendency to use tools". — [OSWorld-MCP (arXiv 2510.24563)](https://arxiv.org/pdf/2510.24563)
- Umbrales en conflicto: blog de practicante habla de degradación medible a partir de ~10–15 tools; un estudio formal reporta >90% sólo hasta ≈30 candidatas, cayendo fuerte más allá de ≈100. — [TianPan.co](https://tianpan.co/blog/2026-04-19-over-tooled-agent-problem); [MachineLearningMastery](https://machinelearningmastery.com/the-complete-guide-to-tool-selection-in-ai-agents/)
- Esquemas: "schema misalignment is the predominant failure mode in small language models"; renombrar componentes de las tools para alinearlos con patrones de preentrenamiento mejoró hasta 17% en MetaTool y RoTBench (ACL 2026, experimentos en inglés). — [Don't Adapt SLMs for Tools; Adapt Tool Schemas (ACL 2026)](https://aclanthology.org/2026.acl-long.948/)
- Multi-paso: en MCP-Bench, Llama-3.1-8B queda rezagado en "dependency awareness" y paralelismo, y los modelos débiles degradan más con más servidores. — [MCP-Bench](https://arxiv.org/pdf/2508.20453)
- Español — MASSIVE-Agents (52 idiomas, plantilla BFCL): lenguas romances (incl. español) "consistently show comparatively high performance"; mejor modelo 57.37% AST en inglés, promedio 34.05%; algunos modelos pequeños dan 0 en idiomas difíciles. — [MASSIVE-Agents (EMNLP Findings 2025)](https://aclanthology.org/2025.findings-emnlp.1099/)
- τ-Multilingual (agentes de voz): caída vs inglés de 4.6 puntos para español (3.7 portugués, 12.6 coreano, 11.8 mandarín). — [τ-Multilingual (arXiv 2609.35820)](https://arxiv.org/pdf/2609.35820)
- Multi-lingual Functional Evaluation: ejemplos de modelos con 74.93 (inglés) vs 49.05 (español) y 56.06 vs 33.70 en benchmarks funcionales traducidos (no necesariamente tool calling; indicativo). — [arXiv 2506.20793](https://arxiv.org/html/2506.20793)
- "Arabic Prompts with English Tools": caída media 5–10%, con tareas que bajan de 80–90% a 40–60%; relevante como análogo de consulta en un idioma + tools en inglés. — [arXiv 2601.05101](https://arxiv.org/pdf/2601.05101)
- SEATauBench: con definiciones de tools en idiomas mezclados, caída inicial pequeña y luego meseta (GPT-5-mini, Qwen3-235B). — [SEATauBench (arXiv 2606.28715)](https://arxiv.org/pdf/2606.28715)
- Ticket-Bench evalúa function calling regionalizado en 6 idiomas incluido español (no obtuve sus cifras para español). — [Ticket-Bench (arXiv 2509.14477)](https://arxiv.org/pdf/2509.14477)

### Inferences
- Para un 8–14B, el diseño debería mantener ≤5–10 tools visibles por turno (idealmente con un router/retrieval previo de 3–5), descripciones cortas y nombres/parámetros "convencionales" (p. ej. `buscar_designaciones(docente_id, periodo)` con enums), en vez de exponer decenas de endpoints vía MCP.
- Mantener nombres y descripciones de tools en un único idioma coherente (español, alineado con la regla del repo) parece de bajo riesgo según SEATauBench; la consulta en español rioplatense implica una pérdida pequeña pero no medida para modelos pequeños.
- Las cadenas secuenciales largas son el punto débil documentado de los 8B; conviene que cada pregunta típica se resuelva con 1–2 llamadas.

### Gaps
- No hay curvas "precisión vs N tools" medidas específicamente para modelos 4–14B cuantizados.
- No encontré datos de tool calling con español rioplatense ni con modelos pequeños en español.

## 3. Modos de falla en loops de agente y mitigaciones (decodificación restringida, parsers, thinking on/off)

### Takeaway
Los fallos típicos de los modelos chicos (invocación ansiosa, tool equivocada, argumentos inventados/malformados, ignorar resultados) están bien documentados; en la práctica, muchos fallos con Qwen en llama.cpp/vLLM vienen del **stack de serving** (plantilla jinja, parser equivocado, thinking capturando el tool call, cuantizaciones <4 bits), no sólo del modelo. Con Qwen3.5 small, el thinking viene apagado por defecto.

### Cited Findings
- Fallos observados por Docker con xLAM-2-8b-fc-r y watt-tool-8B: "eager invocation" (llamar tools ante un "Hi there!"), selección errónea (buscar en vez de agregar; quitar de un carrito vacío), argumentos inválidos/faltantes, y respuestas que ignoran el output de la tool. Watt 8B falla sobre todo en parámetros; xLAM 8B en elegir el camino correcto. Tests que exceden 5 rondas cuentan como fallo (proxy de loops). — [Docker blog](https://www.docker.com/blog/local-llm-tool-calling-a-practical-evaluation/)
- Latencia: los modelos con más razonamiento son más precisos pero mucho más lentos (Qwen3-14B ~142 s vs Qwen3-8B ~84 s por batería, M4 Max). — [Docker blog](https://www.docker.com/blog/local-llm-tool-calling-a-practical-evaluation/)
- llama.cpp sin `--jinja` cae a un parser heurístico y los delimitadores `<tool_call>` de Qwen3/Qwen3-Coder se filtran como texto. Cuantizaciones por debajo de 4 bits (Q3, Q2, IQ) producen tool calls malformadas aun con plantilla correcta. — [netclaw troubleshooting](https://netclaw.dev/troubleshooting/llama-cpp/)
- llama.cpp #20809: build b8429 trataba Qwen3-Instruct-2507 como modelo "thinking" y capturaba los tool calls en `reasoning_content`; workaround `--reasoning off`. — [llama.cpp #20809](https://github.com/ggml-org/llama.cpp/issues/20809)
- llama.cpp #21158: con Qwen3.5-27B y thinking desactivado, el parser PEG falla si hay texto antes de `<tool_call>`; fix parcial (PR #20424), pendiente deshabilitar triggers de gramática dentro de bloques de razonamiento. — [llama.cpp #21158](https://github.com/ggml-org/llama.cpp/issues/21158)
- llama.cpp #22684: en Qwen3.5/3.6 el bloque de tool call se emitía dentro de `delta.reasoning_content` en lugar de `delta.tool_calls` (cerrado "not planned"). — [llama.cpp #22684](https://github.com/ggml-org/llama.cpp/issues/22684)
- Bug con slots paralelos (HIP/ROCm, modelos Qwen3.5 grandes): un slot reutilizado arrastraba el estado recurrente GDN de la petición anterior, filtrando texto de prompts previos — riesgo de fuga entre usuarios si se reprodujera. — [llama.cpp #29092](https://github.com/ggml-org/llama.cpp/issues/29092)
- Unsloth: el fix de plantilla de tool calling de Qwen3-Coder afectaba a todas las GGUF; recomiendan re-descargar o usar `--chat-template-file` y actualizar llama.cpp. — [Unsloth Qwen3-Coder](https://huggingface.co/unsloth/Qwen3-Coder-30B-A3B-Instruct-GGUF/discussions/10); [Unsloth docs](https://unsloth.ai/docs/models/tutorials/qwen3-coder-how-to-run-locally)
- vLLM: Qwen3-Coder con `--tool-call-parser hermes` produce muchos errores (`json.loads` en `hermes_tool_parser`, vLLM 0.11.0); para Qwen3-Coder usar `qwen3_coder`; Qwen recomienda estilo Hermes para Qwen3 general, combinado con un reasoning parser. — [vLLM #26561](https://github.com/vllm-project/vllm/issues/26561); [Qwen Function Calling docs](https://qwen.readthedocs.io/en/latest/framework/function_call.html); [vLLM recipe Qwen3-Coder](https://docs.vllm.ai/projects/recipes/en/stable/Qwen/Qwen3-Coder-480B-A35B.html)
- Fission-GRPO (2026) aborda específicamente la recuperación ante errores de ejecución de tools como debilidad de modelos chicos. — [Fission-GRPO (arXiv 2601.15625)](https://arxiv.org/pdf/2601.15625)

### Inferences
- Mitigaciones prácticas para el agente: (a) servir con `--jinja`, build reciente y plantilla actualizada; (b) cuantización ≥Q4_K_M; (c) thinking desactivado por defecto para tool calls simples (latencia y menos bugs de parsing), activándolo sólo si se mide mejora; (d) validar los argumentos contra JSON Schema en backend y devolver errores estructurados en español que el modelo pueda corregir; (e) límite duro de iteraciones (p. ej. 3–5) y respuesta de fallback; (f) tests de regresión con un set de preguntas reales.
- La decodificación restringida (GBNF/xgrammar/JSON schema) garantiza JSON válido pero no la tool ni los valores correctos; los fallos semánticos dominan en modelos chicos.
- Con varios usuarios concurrentes y modelos híbridos (Qwen3.5), conviene probar explícitamente aislamiento entre slots antes de producción.

### Gaps
- No encontré mediciones cuantitativas comparando thinking on/off en precisión de tool calling para Qwen3-8B/14B o Qwen3.5-9B (el Qwen3 Tech Report tiene tablas separadas, no pude extraer valores).
- No encontré estudio cuantitativo del efecto de GBNF/xgrammar sobre precisión de tool calling en modelos ≤14B.

## 4. Modelos Text-to-SQL especializados pequeños (BIRD) como comparación

### Takeaway
Los mejores 7B especializados alcanzan ~64–69% de ejecución correcta en BIRD-dev (Arctic-Text2SQL-R1-7B 68.9%, OmniSQL-7B 63.9%), mientras que un coder genérico 7B ronda 51%; es decir, aún en el mejor caso ~1 de cada 3 consultas SQL de un benchmark complejo es incorrecta, frente a F1 >0.9 de Qwen3-8B/14B en tool calling con pocas tools.

### Cited Findings
- Arctic-Text2SQL-R1-7B (oficial, model card / Snowflake): BIRD-dev 68.9%, BIRD-test 68.5% (leaderboard 68.47%). — [Snowflake blog](https://www.snowflake.com/en/engineering-blog/arctic-text2sql-r1-sql-generation-benchmark/); [Snowflake/Arctic-Text2SQL-R1-7B](https://huggingface.co/Snowflake/Arctic-Text2SQL-R1-7B)
- OmniSQL-7B: BIRD-dev 63.9 (greedy), 66.1 (majority vote). — [OmniSQL (arXiv 2503.02240)](https://arxiv.org/html/2503.02240v1); [Arctic-Text2SQL-R1 paper](https://arxiv.org/pdf/2505.20315)
- XiYanSQL-QwenCoder-7B: 59.78% BIRD-dev (2502), 62.13% (2504) con M-Schema. — [XiYanSQL-QwenCoder-7B-2502](https://huggingface.co/XGenerationLab/XiYanSQL-QwenCoder-7B-2502)
- Qwen2.5-Coder-7B-Instruct: 50.9 BIRD-dev (tabla Arctic); ~61.3 con majority voting (tabla OmniSQL). Métodos con pipeline adicional sobre el mismo modelo: CSC-SQL 69.19%, OpenSQL 67.2%. — [Arctic paper](https://arxiv.org/pdf/2505.20315); [CSC-SQL](https://arxiv.org/pdf/2505.13271); [OpenSQL (VLDB)](https://www.vldb.org/pvldb/vol19/p1628-li.pdf)
- Los autores de Arctic advierten que la elección de prompt afecta dramáticamente a modelos open-source; decodificación y formato de schema también cambian los scores. — [Arctic paper](https://arxiv.org/pdf/2505.20315)

### Inferences
- Los benchmarks no son comparables (BIRD = SQL sobre bases desconocidas y complejas; Docker/BFCL = elección y parámetros de tool), pero el orden de magnitud es claro: el paradigma Text-to-SQL con 7–9B deja un error residual alto aun con modelos especializados, y además el error SQL puede ser "silencioso" (consulta válida que devuelve datos incorrectos), mientras que un tool call erróneo suele fallar validación o devolver algo acotado.
- Qwen3-8B genérico (el modelo actual del equipo) probablemente rinde por debajo de los especializados en Text-to-SQL; no encontré su cifra BIRD.
- Text-to-SQL libre además obliga a resolver autorización/ámbitos en SQL (row-level security, vistas), mientras que con tools las reglas de negocio y autorización quedan en el backend (alineado con la regla del repo "Las autorizaciones y reglas de negocio se validan en backend").

### Gaps
- No encontré BIRD de Qwen3-8B, Qwen3.5-9B ni Qwen3-14B; tampoco benchmarks Text-to-SQL en español sobre PostgreSQL.

## 5. Conclusión: ¿qué paradigma es más fiable con 7–9B en 8 GB y ~12–14B en 12 GB?

### Takeaway
Con la evidencia disponible, **tool calling con pocas herramientas bien diseñadas (≤5–10 visibles por turno, esquemas cortos, validación en backend)** es más fiable que SQL libre para modelos de 7–14B: F1 ≈0.92–0.97 en evaluación independiente con pocas tools vs ~51–69% de ejecución correcta en BIRD para 7B (incluso especializados). Recomendación de modelos: 8 GB → Qwen3.5-9B Q4_K_M (o Qwen3-8B Q4_K_M como base probada); 12 GB → Qwen3.5-9B a mayor precisión con más slots, o Qwen3-14B Q4_K_M si la concurrencia es baja. gpt-oss-20b no entra completo en 12 GB.

### Cited Findings
- Tool calling con pocas tools: Qwen3-8B Q4_K_M F1 0.919, Qwen3-14B Q4_K_M F1 0.971 (independiente, Docker 2025). — [Docker blog](https://www.docker.com/blog/local-llm-tool-calling-a-practical-evaluation/)
- Agentes multi-turno: Qwen3.5-9B TAU2 79.1, BFCL-V4 66.1 (oficial). — [Qwen/Qwen3.5-9B](https://huggingface.co/Qwen/Qwen3.5-9B)
- La precisión cae rápido con más tools (85%→45% de 5 a 20 tools) y retrieval de 3–5 la recupera. — [RAG-MCP](https://www.alphaxiv.org/abs/2505.03275)
- Text-to-SQL 7B: 50.9% (coder genérico) a 68.9% (Arctic-R1-7B) en BIRD-dev. — [Arctic paper](https://arxiv.org/pdf/2505.20315)
- VRAM: Qwen3.5-9B Q4_K_M ≈6.8 GB a 32K de contexto; gpt-oss-20b ≈14.9 GB a 8K. — [insiderllm](https://insiderllm.com/guides/qwen-3-5-9b-setup-guide/); [smeltcore](https://smeltcore.com/recipes/gpt-oss-20b-on-rtx-3080-ti-mxfp4-chat-in-12-gb-via-llama-cpp-expert-offload/)
- MCP con muchos servidores/tools es difícil incluso para modelos grandes (MCPMark: mejor ≈53%, open-source ≤25%) y excluye modelos ≤100B. — [MCPMark](https://arxiv.org/html/2509.24002)

### Inferences
- 8 GB (RTX 3070): Qwen3.5-9B Q4_K_M con contexto moderado (8–16K por slot) y 2–4 slots parece viable gracias al KV reducido de su arquitectura híbrida; Qwen3-8B Q4_K_M con atención completa deja menos margen para concurrencia (estimación propia; medir). Alternativa conservadora: Qwen3-4B-Instruct-2507 / Qwen3.5-4B (TAU2 79.9 pero BFCL-V4 50.3) si se prioriza concurrencia sobre precisión.
- 12 GB (RTX 5070): Qwen3-14B Q4_K_M maximiza precisión por llamada con evidencia independiente, pero con pocos slots; Qwen3.5-9B Q6_K/Q8_0 con 4+ slots es probablemente mejor para "varios usuarios simultáneos". Gemma 4 (12B / 26B-A4B) es alternativa con menor TAU2 reportado; el 26B-A4B requeriría offload.
- MCP es un detalle de transporte: lo que determina la fiabilidad es cuántas tools ve el modelo y cuán simples son. Exponer todos los endpoints del sistema como un servidor MCP plano replicaría el problema de "tool overload"; mejor un conjunto curado de tools de consulta, posiblemente con router/retrieval.
- Un híbrido razonable: tools curadas para el 80–90% de preguntas frecuentes y, si se conserva Text-to-SQL, restringirlo a vistas de sólo lectura con plantillas/validación, como fallback.
- La decisión final debería apoyarse en un set de evaluación propio (preguntas reales en español rioplatense, con tools y SQL esperados), midiendo precisión, latencia y concurrencia en el hardware objetivo.

### Gaps
- No hay comparación directa publicada "mismo modelo, mismo dominio: tool calling vs Text-to-SQL"; la comparación aquí es entre benchmarks distintos.
- No encontré mediciones de throughput concurrente (tokens/s por usuario con N slots) para Qwen3.5-9B en RTX 3070 o RTX 5070.
- No se verificó en fuente primaria la cifra exacta de VRAM de Qwen3-14B Q4_K_M ni el soporte/estabilidad de Qwen3.5 en llama-server con CUDA en múltiples slots.
