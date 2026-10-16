# Técnicas para aumentar la precisión y reducir errores en MCP / tool use de LLM (2025–2026)

> Alcance: catálogo de técnicas que **suben la precisión** o **bajan la tasa de error** (tool equivocada, argumentos equivocados o alucinados, respuestas incorrectas presentadas como correctas, no abstenerse cuando corresponde), con evidencia cuantitativa, aplicabilidad a modelos chicos (7–14B: Qwen3-8B/14B, Qwen3.5-9B) frente a frontier (Claude API), costo y aplicabilidad a MCP. Complementa —sin repetir— `Alternativas a Text to SQL local/modelos_locales_tool_calling.md` y `mcp_dotnet_seguridad.md` (que ya cubren RAG-MCP 85%→45% de 5 a 20 tools, Docker F1 de Qwen3, recomendaciones generales de "Writing effective tools", code mode de Cloudflare y bugs de parsers de llama.cpp/vLLM).
>
> **Nota de método (importante para el redactor):** en esta ronda el proxy bloqueó arxiv.org, alphaxiv.org, huggingface.co, aclanthology.org y modelcontextprotocol.io. Pude leer directamente: blog de ingeniería de Anthropic, documentación de Claude (platform.claude.com) y la especificación MCP 2026-07-28 (clon del repo `modelcontextprotocol/modelcontextprotocol`). **Todo lo que proviene de papers de arXiv/ACL se obtuvo vía resúmenes del buscador** (marcado "vía buscador"); las cifras deberían verificarse contra el PDF antes de citarlas como definitivas. Fecha de corte: 2026-10-08.

---

## 1. Selección de tools a escala: retrieval / carga dinámica, jerarquía, namespacing y filtrado

### Takeaway
La técnica con evidencia más fuerte y consistente es **reducir las tools visibles por turno** (retrieval, tool search, filtrado por intención/permiso). Con frontier models la ganancia medida va de +8,6 a +25 puntos; con modelos chicos la degradación por catálogo grande es mucho más severa (JSON-baseline 0–49% a >15 tools en modelos 4–14B en un estudio), por lo que el beneficio relativo de filtrar es mayor. El retrieval en sí es un punto de falla: los recuperadores genéricos son malos para tools (NDCG@10 ≈ 34 en ToolRet), así que para un catálogo chico y estable conviene un router/filtrado determinista por intención antes que búsqueda semántica abierta.

### Cited Findings
**Anthropic Tool Search Tool (`defer_loading`) — frontier**
- Mediciones internas de Anthropic en evaluaciones MCP con bibliotecas grandes: Opus 4 pasa de **49% → 74%** y Opus 4.5 de **79,5% → 88,1%** al activar Tool Search; ejemplo de 5 servidores/58 tools: ~55K–77K tokens de definiciones → ~8,7K (−85%). Publicado 24-nov-2025. — [Anthropic, "Introducing advanced tool use"](https://www.anthropic.com/engineering/advanced-tool-use)
- La documentación oficial afirma que "Claude's ability to pick the right tool degrades once you exceed **30–50 available tools**"; recomienda tool search con ≥10 tools o >10K tokens de definiciones, y **no** usarla con <10 tools o definiciones muy chicas; mantener 3–5 tools más usadas sin diferir; cada búsqueda devuelve 5 tools por defecto; variantes regex y BM25; permite implementar búsqueda propia (embeddings) devolviendo bloques `tool_reference`; los deferred tools no rompen el prompt cache ni recompilan la gramática de strict mode. — [Claude Docs, Tool search tool](https://platform.claude.com/docs/en/agents-and-tools/tool-use/tool-search-tool)
- Recomendaciones de discoverability: namespacing consistente por servicio/recurso (`github_`, `slack_`), palabras clave en descripciones "that match how users describe tasks", y un párrafo de sistema que enumere las categorías de tools. — [ídem](https://platform.claude.com/docs/en/agents-and-tools/tool-use/tool-search-tool)

**Retrieval / descubrimiento dinámico — papers (vía buscador)**
- MCP-Zero (2025, retitulado "Active Tool Discovery..."): el modelo emite una solicitud de tool y se hace routing vectorial jerárquico (servidor → tool) sobre 308 servidores / 2.797 tools. En APIBank, con el pool completo (40× más APIs) la inyección de todos los schemas con Claude-3.5 cae de 97,60 → 69,23 (single-turn) y 100 → 60,22 (multi-turn); MCP-Zero mantiene **95,19 / 90,32**; reducción de prompt 60–98% (6.308 → 111 tokens en el caso extremo). Benchmarks propios, sin replicación independiente. — [MCP-Zero (arXiv 2506.01056)](https://arxiv.org/html/2506.01056v3)
- ToolRet (ACL 2025): 7,6K tareas, corpus de 43K tools; el mejor recuperador (NV-Embed-v1) logra sólo **33,83 NDCG@10** y <45% Completeness@10; ColBERT a veces pierde contra BM25; entrenar con ToolRet-train mejora significativamente. — [ToolRet (arXiv 2503.01763)](https://arxiv.org/pdf/2503.01763)
- ScaleMCP (2025): retriever MCP con almacenamiento auto-sincronizado y embedding TDWA (pondera nombre de tool y preguntas sintéticas); benchmark de 5.000 servidores MCP financieros, 10 LLMs, 5 embeddings. No obtuve cifras. — [ScaleMCP (arXiv 2505.06416)](https://arxiv.org/abs/2505.06416)
- Meta, "How Many Tools Should an LLM Agent See?" (2026): profundidad de shortlist adaptativa por consulta; en BFCL con 370 tools, ~7 tools promedio igualan casi la cobertura de mostrar 50 (90,3% vs 90,8%); con Claude Sonnet 4.6 la selección sube de 87,1% (5 fijas) a **93,1%** (adaptativo). Evidencia frontier. — [arXiv 2605.24660](https://arxiv.org/pdf/2605.24660)
- TinyAgent (EMNLP demo 2024, **antiguo**): un clasificador DeBERTa-v3-small multi-etiqueta selecciona tools (umbral 50%, 3,97 tools promedio vs 6 con RAG básico) antes de un modelo de 1,1B/7B fine-tuneado. — [TinyAgent (ACL Anthology)](https://aclanthology.org/2024.emnlp-demo.9.pdf)

**Modelos chicos y tamaño de catálogo (vía buscador)**
- TSCG (2026, preprint; mismo equipo publica el repo): en ~19.000 llamadas, 12 modelos, el baseline JSON de modelos 4B–14B cae a **0–49%** con >15 tools; Phi-4 14B pasa de **0% → 84,4%** a 20 tools (90,3% a 50) compilando el schema JSON a texto compacto; con sólo 10 tools la compresión **empeoró** a Mistral 7B (83,5% → 73–76%); para Qwen3 14B el perfil "balanced" bajó 5–9 pts y el "conservative" subió ~4,4. Métrica compuesta 0,6×selección + 0,4×F1 de parámetros. Sin replicación independiente. — [TSCG (arXiv 2605.04107)](https://arxiv.org/pdf/2605.04107); [repo SKZL-AI/tscg](https://github.com/SKZL-AI/tscg)
- Ya cubierto en la ronda anterior (no repetir en detalle): RAG-MCP 85%→45% de 5 a 20 tools y >80% hasta 50 tools con retrieval; OSWorld-MCP 20,5→15,5 sin filtrado. — ver `modelos_locales_tool_calling.md` §2.

### Inferences
- Para Ars Docendi, con un catálogo realista de 10–40 operaciones de lectura, la evidencia apunta a **no exponer el catálogo completo** a un 8–14B: filtrar por (a) permisos del usuario y (b) intención/módulo antes de cada turno, dejando 3–7 tools visibles. Esto es exactamente el primer escalón de la arquitectura B (router), y es aplicable también a A (un cliente MCP puede filtrar `tools/list` por intención).
- Con Claude API, Tool Search sólo aporta si el catálogo supera ~10 tools / 10K tokens; por debajo, Anthropic recomienda tool calling directo.
- Un router determinista por intención con catálogo cerrado evita el problema de ToolRet (recuperadores genéricos mediocres); si se usa retrieval semántico, conviene embeddings entrenados/ajustados sobre pares (pregunta en español → tool) propios.

### Gaps
- No encontré curvas "precisión vs número de tools" medidas específicamente para Qwen3-8B/14B o Qwen3.5-9B en español; TSCG trae datos de Qwen3 4B/14B pero autoreportados.
- No obtuve las tablas de ScaleMCP ni comparativas de retrieval por embeddings multilingües en español.

---

## 2. Ingeniería de descripciones y schemas (nombres, detalle, enums, ejemplos, alineación con preentrenamiento, reescritura automática)

### Takeaway
Hay tres palancas con números: (1) **ejemplos de uso** (`input_examples`) suben de 72% a 90% el manejo de parámetros complejos en Claude; (2) **alinear nombres de tools/parámetros con lo que el modelo "espera" del preentrenamiento** sube hasta +17 pts en modelos chicos y reduce 80% los errores de desalineación (ACL 2026); (3) **reescritura automática de descripciones** guiada por trazas mejora robustez con catálogos grandes (+60,89% de éxito promedio en StableToolBench con 150+ tools). Las descripciones son también una superficie frágil: ediciones de descripción multiplican ×10 la tasa de selección de una tool, incluso en Qwen2.5-7B.

### Cited Findings
**Guía y mediciones de Anthropic (frontier)**
- `input_examples`: "accuracy rose from **72% to 90%** on complex parameter handling" (pruebas internas); 1–5 ejemplos por tool; útil para estructuras anidadas, muchos opcionales, convenciones de dominio y tools parecidas; poco útil para tools de un parámetro o formatos estándar. — [Anthropic, advanced tool use](https://www.anthropic.com/engineering/advanced-tool-use)
- Costo: ~20–50 tokens por ejemplo simple, ~100–200 por ejemplo anidado; los ejemplos deben validar contra el `input_schema` (400 si no). — [Claude Docs, Define tools](https://platform.claude.com/docs/en/agents-and-tools/tool-use/define-tools)
- Docs: "Provide extremely detailed descriptions. This is by far the most important factor in tool performance" — qué hace, cuándo usarla **y cuándo no**, qué significa cada parámetro, limitaciones; "at least 3–4 sentences"; consolidar operaciones relacionadas (parámetro `action`) para reducir ambigüedad de selección; namespacing por servicio. — [Claude Docs, Define tools](https://platform.claude.com/docs/en/agents-and-tools/tool-use/define-tools)
- "Writing effective tools for agents" (11-sep-2025): con Claude Sonnet 3.5 lograron SOTA en SWE-bench Verified tras "precise refinements to tool descriptions, dramatically reducing error rates"; los servidores MCP de Slack y Asana "Claude-optimized" superan a los escritos por humanos en test sets held-out (sólo en gráficos, sin cifras en el texto). — [Anthropic](https://www.anthropic.com/engineering/writing-tools-for-agents)

**Alineación de schema con preentrenamiento — modelos chicos (vía buscador)**
- PA-Tool, "Don't Adapt Small Language Models for Tools; Adapt Tool Schemas to the Models" (ACL 2026 long): método sin entrenamiento; el modelo objetivo propone varios nombres candidatos para cada componente (tool, parámetro) a partir de su descripción, y se elige el de mayor "peakedness" (más candidatos similares por distancia de edición) = el nombre que el modelo "ya conoce". En MetaTool y RoTBench: mejora **hasta 17 pts** y **−80% errores de desalineación de schema**; la mayor ganancia (17,0) es justamente en detectar que **no existe tool adecuada** (abstención) y 9,6 en razonar sobre múltiples tools. Falla principal identificada: el modelo alucina nombres de tools plausibles pero inexistentes que reflejan convenciones del preentrenamiento. Experimentos en inglés. — [arXiv 2510.07248](https://arxiv.org/html/2510.07248); [ACL Anthology 2026.acl-long.948](https://aclanthology.org/2026.acl-long.948.pdf)

**Reescritura/optimización automática de documentación (vía buscador)**
- "Learning to Rewrite Tool Descriptions" (Trace-Free+, 2026): reescritor entrenado que no requiere trazas multi-turno; con >150 tools candidatas, **−29,23% de degradación de precisión** y **+60,89% de éxito promedio por consulta** en StableToolBench; SOTA en StableToolBench y RestBench (TMDB 54 tools, Spotify 40 tools) con tools no vistas; sobre Gemini-3-pro-preview sólo +1,4 pts (efecto chico en frontier). — [arXiv 2602.20426](https://arxiv.org/html/2602.20426v2)
- Play2Prompt (ACL 2025 Findings): el sistema "juega" con cada tool (prueba-error, beam search + autorreflexión) para refinar docs y generar ejemplos sin datos etiquetados. DRAFT: refina docs iterativamente con feedback de ejecución. EasyTool: destila documentación larga a instrucciones concisas (críticas: optimizado para ChatGPT/Vicuna antiguos, puede quedar por debajo del baseline con modelos nuevos). DocsChisel (2026) adapta docs con fallas observadas en trazas end-to-end. No obtuve cifras de estos. — [Play2Prompt (ACL Anthology)](https://aclanthology.org/2025.findings-acl.1347/); [DocsChisel (arXiv 2608.10037)](https://arxiv.org/html/2608.10037v1); [EasyTool (arXiv 2401.06201)](https://arxiv.org/pdf/2401.06201)

**Fragilidad / sensibilidad de descripciones y nombres (vía buscador)**
- "Tool Preferences in Agentic LLMs are Unreliable" (EMNLP 2025): tools con descripciones editadas reciben **>10× más uso** de GPT-4.1 y de **Qwen2.5-7B** que con la descripción original. — [ACL Anthology 2025.emnlp-main.1060](https://aclanthology.org/2025.emnlp-main.1060/)
- ToolTweak (2025): dos tools idénticas que difieren sólo en sufijo "1" vs "2" muestran tasas de selección "drásticamente" distintas en BFCL. — [arXiv 2510.02554](https://arxiv.org/pdf/2510.02554)
- Hammer (ICLR 2025): "function masking" (entrenar ocultando/aleatorizando nombres de función y parámetros) desplaza la atención del modelo de los **nombres** a las **descripciones**, reduciendo malinterpretaciones; ver §7. — [Hammer (arXiv 2410.04587)](https://arxiv.org/abs/2410.04587)

**Enums y nombres de parámetros**
- No encontré una ablación controlada que aísle "enum vs string libre" o la calidad de descripción de parámetros sobre la exactitud de argumentos; un benchmark 2026 ("Getting the Parameters Right") señala que la generación de valores de parámetros casi no se estudió como problema separado. — [arXiv 2608.03071](https://arxiv.org/pdf/2608.03071)
- Docs de Claude/strict mode: con `strict: true`, un `enum` en el schema queda garantizado por gramática (p. ej. `passengers: 2` en vez de `"two"`). — [Claude Docs, Strict tool use](https://platform.claude.com/docs/en/agents-and-tools/tool-use/strict-tool-use)

### Inferences
- Para 8–14B locales, PA-Tool sugiere una práctica barata y medible: **dejar que el propio modelo proponga los nombres** de tools y parámetros (p. ej. si Qwen3-8B tiende a `buscar_designaciones_docente` en vez de `designaciones_por_agente`) y quedarse con el más frecuente. Como el repo usa identificadores en español, hay que verificar si el efecto se sostiene con nombres en español (el paper es en inglés).
- `input_examples` es nativo de la API de Claude; para modelos locales el equivalente es incluir 1–3 ejemplos de llamada en la descripción o en el system prompt (few-shot), con su costo de tokens/KV por turno.
- Enums cerrados (períodos, estados, módulos, sedes) son el punto de mayor rendimiento combinado con decodificación restringida: el enum convierte un error de alucinación en una elección restringida; el riesgo restante es elegir el valor legal equivocado.
- La sensibilidad ×10 a ediciones de descripción implica que los cambios en descripciones deben pasar por la batería de evaluación como cualquier cambio de código (regresiones de selección).

### Gaps
- Sin cifras públicas de Play2Prompt/DRAFT/EasyTool en esta ronda (papers bloqueados).
- Sin estudios de nombres/descripciones de tools en español para modelos chicos.
- Sin ablación cuantitativa de "descripción corta vs detallada" en 7–14B; la guía de Anthropic (detallada) es para Claude, mientras que OSWorld-MCP (ronda anterior) reportó que descripciones largas reducen el uso de tools: posible conflicto según tamaño de modelo.

---

## 3. Forma de las respuestas de las tools (outputSchema/structuredContent, formato conciso, paginación, IDs + nombres, errores accionables)

### Takeaway
La especificación MCP vigente (2026-07-28) formaliza `outputSchema` + `structuredContent` y distingue errores de protocolo de **errores de ejecución con `isError: true` pensados para que el modelo se autocorrija**. La evidencia cuantitativa directa sobre el formato de las respuestas es escasa: Anthropic afirma (sin cifras) que resolver UUIDs a nombres "significantly improves precision... by reducing hallucinations" y reporta respuestas concisas de ~1/3 de tokens. Programmatic tool calling demuestra que **sacar los resultados intermedios del contexto** mejora la precisión en frontier (+2,9 y +4,7 pts).

### Cited Findings
- Spec MCP 2026-07-28, tools: `structuredContent` puede ser cualquier JSON que cumpla `outputSchema`; si hay `outputSchema`, "Servers **MUST** provide structured results that conform to this schema" y "Clients **SHOULD** validate"; por compatibilidad, el server SHOULD también devolver el JSON serializado como TextContent. Aclaración explícita: `structuredContent` "is unrelated to LLM 'structured outputs'". — [spec 2026-07-28 server/tools.mdx (repo oficial)](https://github.com/modelcontextprotocol/modelcontextprotocol/blob/main/docs/specification/2026-07-28/server/tools.mdx)
- Spec, errores: "Tool Execution Errors contain actionable feedback that language models can use to self-correct and retry with adjusted parameters" (validación de entrada, reglas de negocio, fallas de API) con `isError: true`; los errores de protocolo (tool desconocida, request malformado) "models are less likely to be able to fix"; "Clients **SHOULD** provide tool execution errors to language models to enable self-correction". Ejemplo: "Invalid departure date: must be in the future. Current date is 08/08/2025." — [ídem](https://github.com/modelcontextprotocol/modelcontextprotocol/blob/main/docs/specification/2026-07-28/server/tools.mdx)
- Spec, seguridad: servers MUST validar todas las entradas, controlar acceso y sanitizar salidas; clients SHOULD validar resultados antes de pasarlos al LLM. Nuevo en 2026-07-28: estado entre llamadas como **handles explícitos** devueltos en `structuredContent` (p. ej. `basket_id`), con errores claros de expiración. — [ídem](https://github.com/modelcontextprotocol/modelcontextprotocol/blob/main/docs/specification/2026-07-28/server/tools.mdx)
- Anthropic: "merely resolving arbitrary alphanumeric UUIDs to more semantically meaningful and interpretable language ... significantly improves Claude's precision in retrieval tasks by reducing hallucinations"; parámetro enum `response_format` (`concise`/`detailed`); errores "specific and actionable" en lugar de "opaque error codes or tracebacks" (sin cifras). — [Anthropic, Writing tools for agents](https://www.anthropic.com/engineering/writing-tools-for-agents)
- Docs de Claude: devolver identificadores semánticos y estables y "only the fields Claude needs to reason about its next step. Bloated responses waste context". — [Claude Docs, Define tools](https://platform.claude.com/docs/en/agents-and-tools/tool-use/define-tools)
- Programmatic Tool Calling (frontier): procesar resultados en código antes de que lleguen al contexto: tokens 43.588 → 27.297 (−37%); knowledge retrieval interno 25,6% → 28,5%; GIA 46,5% → 51,2%; ejemplo de 200KB de datos crudos → 1KB al contexto. — [Anthropic, advanced tool use](https://www.anthropic.com/engineering/advanced-tool-use)
- Formato JSON vs texto: "Natural Language Tools" (2025) reporta +18,4 pts de exactitud de tool calling y −70% de varianza al reemplazar JSON por salidas en lenguaje natural (10 modelos, 6.400 ensayos); una replicación (jul-2026, vía write-up secundario) da 62,3% vs 47,4% con 11 modelos que mejoran, 3 que empeoran y 77 pts de dispersión. Se refiere al formato de la **llamada**, no del resultado. — [Natural Language Tools (arXiv 2510.14453)](https://arxiv.org/html/2510.14453v1)
- "Capacity, Not Format" (2026): JSON por instrucción con schema de 1.774 caracteres rinde 34% vs 51% con JSON mode de API (razonamiento matemático): el tamaño/complejidad del schema pesa más que el formato. — [arXiv 2606.09410](https://arxiv.org/pdf/2606.09410)

### Inferences
- Para el Asistente, cada tool debería devolver: IDs **y** nombres legibles (docente, materia, aula, período), sólo los campos necesarios, conteo total + indicación de truncado/paginación, y en errores de validación un mensaje en español que diga qué valor es inválido y cuáles son válidos (p. ej. lista de períodos existentes). Esto ataca directamente "respuestas incorrectas presentadas como correctas" (resultado truncado leído como completo) y argumentos alucinados (el error devuelve los valores válidos).
- `outputSchema` + `structuredContent` permite que el **cliente** (o el router de B) verifique de forma determinista la respuesta antes de que el LLM la narre, y que se renderice la tabla directamente desde datos estructurados en vez de confiar en el texto generado: reduce la superficie de "respuesta inventada".
- Para modelos locales, el resultado de la tool ocupa KV cache por slot: respuestas concisas son a la vez precisión y concurrencia.

### Gaps
- No encontré ablaciones controladas sobre formato de **resultado** (JSON vs tabla markdown vs texto) ni sobre "IDs+nombres vs sólo IDs" con números, para ningún tamaño de modelo.
- No hay evidencia medida del efecto de `isError` con mensajes accionables vs genéricos sobre la tasa de recuperación en modelos 8–14B (Fission-GRPO, ronda anterior, indica que la recuperación ante errores es una debilidad de modelos chicos).

---

## 4. Grounding de argumentos, resolución de entidades, clarificación (elicitation) y abstención

### Takeaway
La abstención y la clarificación son los puntos más débiles de los modelos chicos y **mejoran mucho con entrenamiento específico** (When2Call: alucinación de tools 19% → 1,2% en 8B con preference optimization) o con nombres alineados (PA-Tool: +17 pts en "no hay tool adecuada"). Del lado del sistema, MCP 2026-07-28 ofrece elicitation (form/URL) vía el nuevo patrón de multi round-trip, pero no hay evidencia cuantitativa de su efecto en precisión. El patrón "buscar → elegir → obtener" con resolución de entidades del lado del servidor es recomendación de practicantes, sin estudios controlados.

### Cited Findings
- When2Call (NAACL 2025, NVIDIA/Harvard): benchmark de decisión entre responder, llamar tool, preguntar o admitir que no puede; 3.652 ítems de test; "most community models are unwilling to admit they cannot answer", con F1 de modelos comunitarios de sólo 16,6–37,8 (vía buscador). Resultados del repo oficial (Mistral-NeMo-Minitron): 8B SFT baseline **19% de alucinación de tools**, F1 31,9; 8B con RPO (preference optimization) **1,2%**, F1 52,4, BFCL AST 62,5% (≈igual que 62,2% del baseline) y BFCL Irrelevance 78,1% (vs 36,3%); 4B con RPO 1,9%. SFT simple sobre When2Call volvió al modelo sobre-conservador; RPO no tuvo ese costo. — [NVIDIA/When2Call (GitHub)](https://github.com/NVIDIA/When2Call); [ACL Anthology 2025.naacl-long.174](https://aclanthology.org/2025.naacl-long.174/)
- PA-Tool: la mayor ganancia (+17,0) se da en el caso "no existe tool adecuada" (ver §2). — [arXiv 2510.07248](https://arxiv.org/html/2510.07248)
- ClarifyBench / SAGE-Agent (2025, vía buscador): primer benchmark de desambiguación multi-turno en tool calling (pedidos explícitos, ambiguos e infactibles con usuario simulado); SAGE-Agent sube la cobertura en tareas ambiguas **7–39%** haciendo **1,5–2,7× menos preguntas** que prompting/baselines de incertidumbre. — [arXiv 2511.08798](https://www.alphaxiv.org/abs/2511.08798)
- NoisyToolBench ("Learning to Ask", 2024, **antiguo**): categoriza instrucciones con información faltante, referencias ambiguas, inexactitudes e infactibles; un resumen secundario afirma que sin política explícita de "preguntar si falta" los modelos **alucinan los parámetros faltantes** (no verificado en el paper). — [arXiv 2409.00557](https://arxiv.org/pdf/2409.00557)
- ASPI (Scale Labs): pedir clarificación en tareas ambiguas puede **aumentar** la vulnerabilidad a prompt injection (728 escenarios). — [Scale Labs, ASPI](https://labs.scale.com/papers/aspi)
- Spec MCP 2026-07-28, elicitation: sigue existiendo, en modos **form** (datos estructurados validados por JSON Schema) y **URL** (interacciones sensibles fuera del cliente); servers MUST NOT pedir secretos por form. Cambio mayor: las solicitudes iniciadas por el server (`elicitation/create`, sampling, roots) se reemplazan por el patrón **Multi Round-Trip Requests**: el server devuelve `InputRequiredResult` (`resultType: "input_required"`) y el cliente reintenta con `inputResponses`; se eliminó `notifications/elicitation/complete` y `elicitationId` en modo URL. (Corrige la nota previa: no se revirtió elicitation entera, se rediseñó su transporte.) — [spec 2026-07-28 changelog y client/elicitation.mdx (repo oficial)](https://github.com/modelcontextprotocol/modelcontextprotocol/blob/main/docs/specification/2026-07-28/changelog.mdx)
- "Reasoning Trap" (ver §5): la tasa de alucinación de tools de Qwen3-8B con **sólo tools distractoras** es 36,2% sin thinking y 56,8% con thinking: la falta de abstención es frecuente en 8B aun sin razonamiento. — [arXiv 2510.22977](https://arxiv.org/pdf/2510.22977)

### Inferences
- Para el Asistente, los errores de argumento más probables son de **entidad** (nombre de docente, materia, aula, período escrito de forma coloquial en español rioplatense). La mitigación con mejor relación evidencia/costo es mover la resolución al servidor: una tool `buscar_<entidad>(texto)` con búsqueda difusa (trigram/unaccent de PostgreSQL) que devuelva candidatos con ID + nombre + desambiguadores, y que las tools de consulta acepten **sólo IDs** (validables). Si hay >1 candidato plausible, el servidor responde "input required"/pregunta de desambiguación en lugar de elegir.
- La abstención no debería depender del LLM chico: en B es un estado explícito del router (sin intent reconocido + Text-to-SQL con baja confianza → "no puedo responder"); en A puede imitarse con una tool explícita `no_puedo_responder` / instrucción, pero la evidencia (When2Call, Reasoning Trap) muestra que los modelos chicos sin entrenamiento específico tienden a llamar alguna tool igual.
- Elicitation MCP es útil como canal de clarificación estructurada (formularios con enum de candidatos), pero el soporte en clientes y su efecto sobre precisión no están medidos.

### Gaps
- No encontré mediciones del efecto de "search-then-get" o de tools de resolución de entidades sobre la tasa de argumentos alucinados.
- No hay evaluación cuantitativa de elicitation MCP; tampoco cifras de When2Call para Qwen3-8B/14B o Qwen3.5-9B.

---

## 5. Decodificación y serving: decodificación restringida, strict mode y thinking on/off

### Takeaway
La decodificación restringida (xgrammar, Outlines, llguidance, GBNF; strict mode de Claude; `tool_choice=required` en vLLM) **elimina los errores estructurales** (JSON inválido, tipos, campos requeridos, nombres de tool inexistentes) pero **no mejora —y en modelos <4B puede empeorar— la exactitud semántica**. Para modelos chicos, activar **thinking aumenta la alucinación de tools** (Qwen3-8B: 36,2% → 56,8% con tools distractoras), aunque en tareas de cadena de 2 tools desactivarlo empeoró a Qwen3-8B/14B: el efecto depende del tipo de tarea.

### Cited Findings
**Decodificación restringida (vía buscador salvo docs)**
- "Constrained Decoding Eliminates Structural Failures in Small LLMs but Reveals a Scale-Dependent Semantic Gap" (2026, 0,6B–4B: Qwen3, Llama 3.2, Phi-4-mini; 14 tareas, 3 de function calling; nativo vs Outlines vs XGrammar): validez de schema de ~79–93% → **100%**; errores de tipo (número como string) se eliminan; errores de nivel instrucción (p. ej. function calling multi-paso) son "CD-resistant". — [arXiv 2609.23742](https://arxiv.org/html/2609.23742v1)
- "The Constraint Tax" (may-2026, preprint, modelos 0,5–1,7B): schema duro sube validez 61,5% → 100% pero baja exactitud de respuesta **19,7% → 11,0%**; en una tarea de tool call de calendario, Qwen2.5-1.5B cae de **91,5% → 48,0%** de exactitud ejecutable con el schema duro; propone razonar libre y restringir sólo al empaquetar la respuesta final. — [alphaxiv 2605.26128](https://www.alphaxiv.org/abs/2605.26128)
- StructureBench (IJCAI 2026; 0,5B–8B on-device; prompt-only vs Outlines/XGrammar/Guidance): la decodificación restringida garantiza validez sintáctica pero "does not reliably improve semantic accuracy and may even degrade performance for smaller models or complex grammars". — [papers.cool IJCAI 2026](https://papers.cool/venue/267@2026@IJCAI)
- JSONSchemaBench (ene-2025): 10K schemas reales; compara Guidance, Outlines, llama.cpp, XGrammar, OpenAI y Gemini en eficiencia, cobertura y calidad (no específico de tool calling). — [arXiv 2501.10868](https://arxiv.org/abs/2501.10868)
- vLLM: con `tool_choice="required"` o tool nombrada usa structured outputs (backends xgrammar/guidance, default `auto`) y garantiza una llamada parseable — "not a high-quality one"; el modo `auto` no aparece documentado como restringido (inferencia, verificar por versión). — [vLLM docs, Tool Calling](https://docs.vllm.ai/en/latest/features/tool_calling/)

**Strict mode de Claude (docs, leído directamente)**
- `strict: true` compila el `input_schema` a gramática ("grammar-constrained sampling"); garantiza que `input` cumpla el schema y que `name` sea una tool válida; evita `"2"`/`"two"` en vez de `2`; requiere `additionalProperties: false` y un subconjunto de JSON Schema; los schemas compilados se cachean hasta 24 h (no poner PII/PHI en nombres de propiedades ni enums). — [Claude Docs, Strict tool use](https://platform.claude.com/docs/en/agents-and-tools/tool-use/strict-tool-use)
- En Opus 5.5 / Sonnet 5.5 el forzado de tool (`tool_choice: any/tool`) devuelve 400; la recomendación es `auto` + strict, o structured outputs para respuestas JSON fijas. — [Claude Docs, Define tools](https://platform.claude.com/docs/en/agents-and-tools/tool-use/define-tools)

**Thinking on/off (vía buscador)**
- "The Reasoning Trap" (ACL 2026 long; Penn State + Ant Group; SimpleToolHalluBench): reforzar razonamiento (RL, SFT o simplemente activar thinking en inferencia) **aumenta la alucinación de tools**. Tabla 1: Qwen3-8B sin thinking NTA 4,1% / DT 36,2%; con thinking **5,4% / 56,8%**; Qwen3-32B sin thinking 5,1% / 46,6%, con thinking 8,8% / 50,7% (NTA = no hay tool disponible; DT = sólo tools distractoras). Mitigaciones (prompting, DPO) bajan alucinación pero también utilidad. — [arXiv 2510.22977](https://arxiv.org/pdf/2510.22977); [ACL Anthology 2026.acl-long.376](https://aclanthology.org/2026.acl-long.376/)
- AgentFloor (2026): desactivar el razonamiento en Qwen3 **ayuda** al 32B en el tier B (cadena de 2 tools, +12 pp) pero **perjudica** al 8B (−9 pp) y al 14B (−12 pp). — [AgentFloor (arXiv 2605.00334)](https://arxiv.org/html/2605.00334v1)
- "Small Reasoning Models are Instruction Followers in Function Calling" (2026): los modelos aciertan más el function calling cuando se plantea como tarea de seguimiento de instrucciones que como "tool calling"; proponen delegar la emisión de la llamada a un modelo chico dedicado (IFFC), con mayores ganancias en modelos de razonamiento y robusto a cuantización agresiva (sin cifras en el abstract). — [arXiv 2608.22472](https://arxiv.org/pdf/2608.22472)
- "Think-Augmented Function Calling" (2026) propone razonamiento embebido para mejorar la exactitud de parámetros (sin cifras obtenidas). — [arXiv 2601.18282](https://arxiv.org/pdf/2601.18282)

### Inferences
- Con modelos locales, conviene restringir la **forma** (gramática de tool call/JSON schema con enums) pero dejar al modelo decidir libremente **si** llamar y razonar antes, o bien —el patrón que sugiere Constraint Tax— generar la intención en libre y aplicar la restricción sólo en el paso de empaquetado. Restringir agresivamente en `required` obliga a llamar alguna tool y elimina la abstención.
- Thinking: para la decisión "¿hay una tool adecuada o debo abstenerme?" la evidencia en Qwen3-8B desaconseja thinking (+20,6 pts de alucinación con distractores); para cadenas de 2 llamadas lo favorece. Una arquitectura que separa decisión (router determinista o clasificador) de ejecución (tool call) puede usar thinking sólo donde ayuda.
- Con Claude API, strict mode es esencialmente gratis en precisión (frontier) y elimina la clase "argumento con tipo inválido"; no resuelve "valor legal pero equivocado".

### Gaps
- Sin cifras de efecto de xgrammar/llguidance/GBNF sobre exactitud **semántica** de tool calling en 7–14B (los estudios encontrados son ≤4B o ≤8B sin desglose).
- Sin datos de thinking on/off para Qwen3.5-9B ni en español.

---

## 6. Estrategias del loop de agente: ReAct vs plan-and-execute/ReWOO/LLMCompiler, verificación, programmatic tool calling / code mode, llamadas paralelas

### Takeaway
Los modelos 8–14B **se desploman a partir de la tercera llamada dependiente**: en AgentFloor Qwen3-14B logra 92% con una tool y 84% con cadena de 2, pero **16%** al ramificar según un resultado intermedio y 4% en síntesis/planificación larga (Qwen3-8B: 76/64/24/0/0). Las estrategias "plan-first" (ReWOO, LLMCompiler) y programmatic tool calling muestran ganancias con modelos capaces, pero no hay evidencia de que resuelvan esto en modelos chicos. Para precisión, la palanca principal es **diseñar tools de nivel tarea para que cada pregunta típica se resuelva con 1–2 llamadas**.

### Cited Findings
- AgentFloor (2026, Ollama, pass rates por tier): Qwen3-14B — A0 sin tools 88%, A una tool 92%, B cadena de 2 tools 84%, C ramificación sobre resultado intermedio 16%, D síntesis multi-fuente con recuperación de conflictos 4%, E planificación larga 4%; Qwen3-8B — 80/76/64/24/0/0; overall 48% (14B) y 40,7% (8B); ningún modelo chico alcanza el umbral de fiabilidad del paper; ministral-3:8b y qwen3:14b quedan a 2 pts (84% [78–90]). — [AgentFloor (arXiv 2605.00334)](https://arxiv.org/html/2605.00334v1)
- LLMCompiler (2023–2024, **antiguo**): planificación de llamadas en DAG con ejecución paralela; hasta 3,7× de latencia, 6,7× de costo y ~9% de exactitud sobre ReAct (modelos grandes). — [arXiv 2312.04511](https://arxiv.org/pdf/2312.04511)
- ReWOO (2023, **antiguo**, gpt-3.5): −64% tokens y +4,4 pts promedio en 6 benchmarks; más robusto que ReAct ante fallas de tools. — [arXiv 2305.18323](https://arxiv.org/pdf/2305.18323)
- ToolSandbox (2024): ReAct casi no cambia el score de modelos grandes (GPT-4o 73,0 → 73,6). WorkBench (2024): la falla dominante de modelos débiles es no seguir el formato ReAct. — [ToolSandbox](https://arxiv.org/pdf/2408.04682); [WorkBench](https://arxiv.org/pdf/2405.00823)
- Programmatic Tool Calling de Anthropic (frontier): +2,9 pts (knowledge retrieval) y +4,7 pts (GIA), −37% tokens; recomendado para ≥3 llamadas dependientes, datasets grandes o filtrado de resultados; **no** recomendado para llamadas simples o consultas rápidas. — [Anthropic, advanced tool use](https://www.anthropic.com/engineering/advanced-tool-use)
- "The Bitter Lesson of Tool Calling" (2026, 14 modelos nov-2024→jul-2026, subconjunto de 309 ítems de BFCL v4): PTC iguala o supera a JSON en 11/14 modelos (13/14 en fan-out paralelo); GPT-5.6 +10,6%; pero el promedio macro es 78,6 (JSON) vs 77,0 (PTC) y 3 modelos OpenAI fallan por escapar `\n` en scripts. No obtuve resultados para modelos open-weight chicos. — [arXiv 2608.06370](https://arxiv.org/pdf/2608.06370); [write-up secundario](https://codex.danielvaughan.com/2026/08/12/bitter-lesson-tool-calling-programmatic-vs-json-codex-cli-mcp-agent-tool-use/)
- CodeAct (2024, **antiguo**): formato de acción por código mejor en 12/17 modelos en M3ToolEval; las ganancias de 7B provienen de fine-tuning (Mistral-7B 25,6 → CodeActAgent 42,5), no aíslan el formato. — [CodeAct (arXiv 2402.01030)](https://www.alphaxiv.org/overview/2402.01030v4)
- Verificación previa a la ejecución: TOOLVERIFIER (2024, **antiguo**) separa selección y generación de parámetros y verifica cada paso con preguntas autogeneradas (ganancias en 4 tareas de ToolBench, sólo tareas de una tool); "Look Before You Leap" (2026) propone verificación pre-acción para fallas silenciosas (sin cifras obtenidas). — [TOOLVERIFIER](https://arxiv.org/pdf/2402.14158); [arXiv 2609.11957](https://arxiv.org/pdf/2609.11957)
- MCP vs CLI (mcp-vs-cli-bench, 2026): la relación de costo MCP/CLI varió de 0,43× a 29× según el scaffold (7 scaffolds, 5 modelos) y los agentes a menudo no usaron la interfaz asignada; conclusión: el andamiaje importa más que la interfaz. — [agentpatterns.ai](https://agentpatterns.ai/tool-engineering/mcp-cli-cost-ratio-scaffolding-bound/)

### Inferences
- Los datos de AgentFloor son el argumento más directo para el diseño: con 8–14B, **las preguntas que exigen ramificar según un resultado intermedio fallan ~80% de las veces**. Para precisión, el sistema debería (a) ofrecer tools compuestas de nivel tarea (p. ej. `resumen_docente(id, periodo)` que resuelva internamente lo que de otro modo serían 3 llamadas) o (b) codificar esas cadenas en el router/plantillas (B).
- Code mode / PTC con modelos locales: sin evidencia de beneficio en 7–14B y con superficie de seguridad nueva (sandbox); con Claude podría usarse, pero para preguntas de 1–2 llamadas Anthropic mismo lo desaconseja.
- Una verificación determinista post-llamada (¿los IDs devueltos existen?, ¿la respuesta del LLM cita sólo valores presentes en `structuredContent`?) es implementable en ambos diseños y no depende de capacidades del modelo.

### Gaps
- No hay comparación ReAct vs plan-and-execute vs LLMCompiler con 7–14B open-weight 2025–2026.
- No hay datos de PTC/code mode para Qwen3/Qwen3.5 ≤14B.
- No encontré estudios cuantitativos de self-verification de tool calls en modelos chicos (2025–2026).

---

## 7. Fine-tuning de modelos chicos para un catálogo específico (LoRA/SFT con datos sintéticos, ToolACE, APIGen/xLAM, Hammer)

### Takeaway
El fine-tuning sobre el catálogo propio produce las mayores ganancias absolutas medidas en modelos chicos (TinyAgent 7B: 41% → 83–85%, superando a GPT-4-Turbo en su dominio; When2Call RPO: alucinación 19% → 1,2%), pero casi toda la evidencia es sobre datasets propios, sintéticos y estrechos, y la ronda anterior mostró que los especialistas (xLAM-2, watt-tool) **rinden peor en conversación real** que el modelo base. El riesgo de overfitting al formato y de pérdida de abstención está documentado; los mitigadores son datos de irrelevancia (~10%), function masking y preference optimization.

### Cited Findings
- TinyAgent (2024, **antiguo**): 80K ejemplos sintéticos generados con GPT-4-Turbo para un catálogo de 16 tools de macOS; 1,1B: 12,71% → ~79–80%; 7B: 41,25% → ~83–85%; GPT-4-Turbo 79,08% en la misma tarea; modelos cuantizados a 4 bits. Las cifras varían entre versiones del paper. — [TinyAgent (ACL Anthology)](https://aclanthology.org/2024.emnlp-demo.9.pdf)
- AWS, "Small Language Models for Efficient Agentic Tool Calling" (dic-2025, preprint no replicado): OPT-350M con 1 época de SFT en ToolBench → 77,55% pass rate vs ChatGPT-CoT 26,00% y ToolLLaMA-DFS 30,18% (evaluador ToolEval basado en ChatGPT). — [arXiv 2512.15943](https://arxiv.org/pdf/2512.15943)
- When2Call (NAACL 2025): RPO sobre datos de cuándo (no) llamar: 8B alucinación 19% → 1,2%, BFCL Irrelevance 36,3% → 78,1% sin perder BFCL AST; SFT simple causó sobre-conservadurismo. — [NVIDIA/When2Call](https://github.com/NVIDIA/When2Call)
- Hammer (ICLR 2025, 1,5B–7B): agrega 7.500 ejemplos de irrelevancia a xLAM-function-calling-60k y usa function masking (nombres ofuscados → foco en descripciones); el paper afirma que Hammer-7B compite con GPT-4/GPT-4o en BFCL v2 (un resumen secundario da 83,92% overall); en una tabla posterior (CoALM) Hammer2.0-7b obtiene 52,13% overall, 95,12% relevance, 73,20% irrelevance y **0,38% multi-turn**. Un resumen secundario afirma que ~10% de datos de irrelevancia es el "sweet spot" y que al mejorar la ejecución empeora la detección de "no hace falta tool" (no verificado). — [Hammer (arXiv 2410.04587)](https://arxiv.org/abs/2410.04587); [CoALM (arXiv 2502.08820)](https://arxiv.org/html/2502.08820v2)
- ToolACE-8B (ICLR 2025; Llama-3.1-8B + datos sintéticos ToolACE): BFCL-v1 91,41 overall / irrelevancia 89,17 (snapshot 08/2024); BFCL-v2 85,77 / 81,44. Snapshots de 2024, no comparables con BFCL v4. — [ToolACE (ICLR 2025)](https://proceedings.iclr.cc/paper_files/paper/2025/file/663865ea167425c6c562cb0b6bcf76c7-Paper-Conference.pdf)
- Ya cubierto (ronda anterior): xLAM-2-8b-fc-r 72,04% BFCL-v3 pero fallas de "eager invocation" y selección en la evaluación de Docker; licencia CC-BY-NC; LoopTool-8B +8,59 sobre Qwen3-8B en BFCL-v3. — ver `modelos_locales_tool_calling.md` §1 y §3.

### Inferences
- Para Ars Docendi (catálogo cerrado, preguntas repetitivas), un LoRA de Qwen3-8B/Qwen3.5-9B sobre pares sintéticos (pregunta en español rioplatense → tool + argumentos / abstención / pregunta de clarificación) es la técnica con mayor techo de ganancia para modelos locales, **siempre que** incluya ~10% de casos de irrelevancia/abstención, variantes de redacción y un set held-out con preguntas reales; y que se re-evalúe al cambiar el catálogo (el modelo queda acoplado a los nombres/descripciones).
- El riesgo principal no es el overfitting clásico sino **romper la abstención y la conversación general** (evidencia Docker/xLAM y When2Call-SFT); preferir preference optimization (DPO/RPO) para la decisión llamar/no llamar.
- Con Claude API no aplica fine-tuning; las palancas equivalentes son descripciones, `input_examples` y strict mode.

### Gaps
- No encontré estudios 2025–2026 que midan ganancia de LoRA de 7–14B en un catálogo de dominio **y** su degradación en tools/preguntas fuera de distribución.
- No hay resultados de fine-tuning de tool calling en español.

---

## 8. Práctica de evaluación para servidores MCP (métricas, LLM-as-judge)

### Takeaway
No existe todavía un estándar consolidado de evaluación de servidores MCP; la práctica convergente (Anthropic, MCP-Bench, herramientas comerciales) combina **verificadores deterministas** (tool correcta, argumentos válidos, estado resultante, call order, acciones prohibidas) con **LLM-as-judge sólo para la respuesta final**, y recomienda medir también llamadas totales, errores de tools, tokens y latencia. Para la comparación A vs B, lo crítico es puntuar por separado selección, argumentos, abstención y "respuesta incorrecta presentada como correcta", contra el estado real de la base y no contra el autoinforme del agente.

### Cited Findings
- Anthropic: verificador desde "exact string comparison" hasta "enlisting Claude to judge"; "Avoid overly strict verifiers that reject correct responses due to spurious differences"; recolectar "total runtime of individual tool calls and tasks", "total number of tool calls, the total token consumption, and tool errors"; usar test sets held-out para no sobreajustar las tools a la evaluación. — [Anthropic, Writing tools for agents](https://www.anthropic.com/engineering/writing-tools-for-agents)
- MCP-Bench: combina "rule-based execution checks with rubric-based LLM-as-a-Judge scoring", con juicios basados sólo en evidencia observable (definición de la tarea, solución final y ejecución). — [MCP-Bench (arXiv 2508.20453)](https://arxiv.org/pdf/2508.20453)
- MCPAgentBench (2025): rúbrica de 4 dimensiones (cumplimiento de tarea, pertinencia de tools, grounding en tools, exactitud de parámetros), juez o4-mini. — [arXiv 2512.24565](https://arxiv.org/pdf/2512.24565)
- mcp-vs-cli-bench: puntúa leyendo el estado del repositorio vía API después de la corrida, "never by accepting the agent's own report"; detectó que los agentes a menudo no usaban la interfaz asignada. — [agentpatterns.ai](https://agentpatterns.ai/tool-engineering/mcp-cli-cost-ratio-scaffolding-bound/)
- Learning to Rewrite Tool Descriptions: usa F1 a nivel de tool (no accuracy) para selección por la gran cantidad de verdaderos negativos, y tasa de éxito de ejecución para uso; corrigieron schemas defectuosos de StableToolBench para todos los métodos. — [arXiv 2602.20426](https://arxiv.org/html/2602.20426v2)
- When2Call: la evaluación multiple-choice vs LLM-as-judge da cifras distintas para el mismo modelo (8B RPO: F1 52,4 vs 66,1). — [NVIDIA/When2Call](https://github.com/NVIDIA/When2Call)
- Guías de vendors (no independientes): precisión/recall de selección de tools con objetivos ~85%/90%, "chain efficiency" = llamadas mínimas/llamadas reales, p95 por tool. — [Future AGI](https://futureagi.com/blog/step-by-step-guide-mcp-evaluation-2026/); [Braintrust](https://www.braintrust.dev/articles/best-mcp-testing-tools-agent-evals-2026)
- ToolEval (ToolBench) usa un juez ChatGPT para pass rate, lo que hace incomparables las cifras entre papers con distinto juez. — [arXiv 2512.15943](https://arxiv.org/pdf/2512.15943)

### Inferences
- Set de evaluación propio recomendado (para A y B por igual): preguntas reales en español con (i) respuesta esperada calculada por SQL de referencia, (ii) tool/args esperados cuando aplique, (iii) casos de abstención obligatoria (fuera de alcance, sin permisos, datos inexistentes) y (iv) casos ambiguos que exigen clarificación. Métricas: exactitud de respuesta final contra datos reales, tasa de "confidently wrong" (respuesta incorrecta sin advertencia), tasa de abstención correcta/incorrecta, F1 de selección, exactitud de argumentos, llamadas por pregunta, latencia p95. LLM-as-judge sólo para equivalencia semántica de la respuesta, validado contra una muestra etiquetada por humanos.
- Correr cada configuración varias veces (≥3–5 semillas) por la varianza reportada (Natural Language Tools: −70% de varianza es en sí un resultado; AgentFloor reporta intervalos de confianza).

### Gaps
- No encontré estudios sobre sesgos de LLM-as-judge específicos de trazas de tool use (posición, autopreferencia).
- No hay benchmark público de MCP en español.

---

## 9. ¿Qué técnicas favorecen más a (A) agente MCP sobre endpoints REST o a (B) router híbrido con tools certificadas y Text-to-SQL con abstención?

### Takeaway
Casi todas las técnicas con mayor efecto medido en modelos chicos —**pocas tools visibles, decisiones de abstención fuera del LLM, cadenas de 1–2 llamadas, argumentos por ID validados en servidor, restricción de forma sin forzar la llamada**— son nativas de B y sólo parcialmente reproducibles en A con filtrado y tools compuestas. Con Claude API la brecha se reduce: tool search, strict mode, `input_examples` y PTC llevan a A a niveles altos de selección (88% en catálogos grandes con Opus 4.5), pero ninguna técnica documentada lleva la abstención de modelos chicos al nivel de un estado determinista.

### Cited Findings
- Selección: frontier con tool search 79,5% → 88,1% (Opus 4.5); chicos 4–14B con >15 tools 0–49% en JSON baseline. — [Anthropic](https://www.anthropic.com/engineering/advanced-tool-use); [TSCG](https://arxiv.org/pdf/2605.04107)
- Profundidad de cadena: Qwen3-14B 92% (1 tool) / 84% (2 tools) / 16% (ramificación). — [AgentFloor](https://arxiv.org/html/2605.00334v1)
- Abstención: Qwen3-8B 36,2% de alucinación con distractores (56,8% con thinking); preference optimization lleva un 8B a 1,2%. — [Reasoning Trap](https://arxiv.org/pdf/2510.22977); [When2Call](https://github.com/NVIDIA/When2Call)
- Forma vs contenido: constrained decoding → 100% validez sin garantía semántica; puede costar exactitud en modelos muy chicos. — [arXiv 2609.23742](https://arxiv.org/html/2609.23742v1); [Constraint Tax](https://www.alphaxiv.org/abs/2605.26128)
- Errores accionables y `structuredContent` validable están estandarizados en MCP 2026-07-28 y sirven a ambos diseños. — [spec MCP](https://github.com/modelcontextprotocol/modelcontextprotocol/blob/main/docs/specification/2026-07-28/server/tools.mdx)

### Inferences
Catálogo resumido (efecto medido; chicos vs frontier; costo; aplicabilidad a MCP):

| Técnica | Efecto medido (fuente) | ¿Sirve en 7–14B? | Costo | MCP |
|---|---|---|---|---|
| Filtrar/recuperar tools (router, tool search, retrieval) | Opus 4: 49→74%; Opus 4.5: 79,5→88,1%; MCP-Zero 69→95 (Claude-3.5); Sonnet 4.6 87,1→93,1 | Sí, más necesario (0–49% con >15 tools) | Bajo; retrieval agrega 1 paso | Filtrar `tools/list`; `defer_loading` en Claude |
| Nombres alineados al preentrenamiento (PA-Tool) | Hasta +17 pts; −80% errores de schema | Sí (diseñado para chicos) | Una vez, offline | Directo (nombres de tools/params) |
| `input_examples` / few-shot | 72→90% params complejos (Claude) | Probable vía prompt; sin datos | 20–200 tokens/ejemplo | Nativo en Claude; en MCP vía descripción |
| Reescritura automática de descripciones | +60,89% éxito con 150+ tools (StableToolBench); +1,4 en Gemini-3-pro | Sí, mayor efecto con catálogos grandes | Offline | Directo |
| Decodificación restringida / strict | Validez →100%; semántica sin mejora (≤4B puede bajar) | Forma sí; contenido no | Compilación de gramática | Cliente/servidor de inferencia |
| Thinking off para decidir llamar/abstener | Qwen3-8B DT 56,8%→36,2% al desactivar | Sí; pero −9/−12 pp en cadenas de 2 (8B/14B) | Menor latencia | Independiente |
| Tools compuestas de nivel tarea (1–2 llamadas) | Qwen3-14B 84–92% vs 16% al ramificar | Clave | Más endpoints | Directo |
| Resolución de entidades en servidor + IDs validados + errores accionables | Sin cifras (Anthropic: "significantly improves precision") | Sí | Endpoints de búsqueda | `isError`, elicitation/MRTR |
| Fine-tuning (SFT + irrelevancia + RPO) | TinyAgent 7B 41→83–85%; When2Call 19%→1,2% alucinación | Sí, máximo techo | Datos sintéticos + reentrenar por cambio de catálogo | Independiente |
| PTC / code mode | +2,9 / +4,7 pts frontier; 11/14 modelos ≥ JSON en BFCL v4 | Sin evidencia | Sandbox | Variante de cliente |

- Para B, estas técnicas se aplican así: el router hace la selección y la abstención de forma determinista; las tools certificadas usan enums, IDs y strict/gramática; el LLM sólo extrae slots y redacta; Text-to-SQL queda como fallback con abstención por baja confianza (fuera de este documento).
- Para A, el mismo resultado exige: filtrado de `tools/list` por permisos+intención, tools compuestas, resolución de entidades en servidor, `isError` accionables, y —con modelos locales— probablemente fine-tuning con datos de abstención. Con Claude API, A es competitivo en selección; la diferencia residual estaría en abstención y en preguntas que requieren ramificar.

### Gaps
- No existe comparación publicada "mismo dominio, mismo modelo: agente MCP sobre REST vs router híbrido"; la decisión debe apoyarse en la evaluación propia descrita en §8.
- Las cifras de papers obtenidas vía buscador (arXiv bloqueado) deben verificarse en las fuentes primarias antes de un informe final.
