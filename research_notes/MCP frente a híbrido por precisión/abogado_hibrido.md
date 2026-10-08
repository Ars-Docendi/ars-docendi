# Abogado del híbrido: por qué (B) debería producir menos respuestas incorrectas que (A), dónde no, y qué variante de (B) minimiza el error

> **Alcance y método.** Ronda adversarial. (A) = agente LLM que llama endpoints REST expuestos por MCP, con libertad total para agregar o rediseñar endpoints y con todas las técnicas conocidas (tool search, `strict`, `input_examples`, resolución de entidades en servidor, errores accionables, PTC, fine-tuning en local). (B) = híbrido: intenciones deterministas → pocas herramientas certificadas parametrizadas → Text-to-SQL de respaldo con abstención. Único criterio: precisión / tasa mínima de respuestas incorrectas. Niveles: RTX 3070 8 GB, RTX 5070 12 GB, API de Claude (Opus 5.5, Sonnet 5.5, Haiku 5.5, Fable 5.1).
>
> **Base de evidencia.** Notas de la ronda 1 (`tasas_de_error_arquitecturas.md`, `modelos_por_nivel.md`, `tecnicas_optimizacion_mcp.md`, `diseno_superficie_herramientas.md`), `Alternativas a Text to SQL local/arquitectura_actual.md` e informe previo. No se repiten sus cifras salvo cuando son imprescindibles para el argumento; se cita la fuente primaria que esas notas ya citaban. Lectura de código propia en el worktree `feature/asistente-modelo-local` (rutas relativas a él). Búsqueda web nueva: 14 consultas.
>
> **Acceso.** El proxy bloqueó en esta ronda arxiv.org (vía fetch), alphaxiv.org, huggingface.co y emergentmind.com. **Todas las cifras web nuevas de esta ronda vienen de resúmenes del buscador** sobre la fuente primaria; se marcan **[resumen]**. Etiquetas de evidencia: **[medido]** (medición independiente o del repo, con metodología pública), **[vendor]** (fabricante o proveedor con interés comercial), **[agregador]**, **[inferencia]** (razonamiento o cálculo propio sobre cifras citadas). Fecha de corte: 2026-10-08.

---

## 1. El caso más fuerte a favor de (B), por nivel de modelo

### Takeaway
El argumento de (B) no es "un modelo mejor" sino **menos decisiones del modelo por turno y abstención estructural**: en (B) el error de la consulta certificada es ≈0 por construcción y todo el error restante vive en tres puntos medibles (enrutador, slots, respaldo), mientras que en (A) cada pregunta suma selección + argumentos + composición + decisión de abstenerse + redacción, todas a cargo del LLM y todas no deterministas. La ventaja es máxima en la 3070 (un 8B pierde en *cada* una de esas decisiones), grande en la 5070 y estrecha en la API de Claude, donde sobrevive sobre todo por tres cosas que (A) no tiene por defecto: determinismo (pass^k = pass^1 en la capa 0), abstención que no depende del LLM y redacción sin números generados.

### Cited Findings
**Núcleo estructural (vale para todos los niveles)**
- La precisión por tarea cae aproximadamente como p^n con el número de llamadas dependientes: GPT-4o 80,6 % por llamada → 60,5 % por tarea; Qwen2.5-7B 18 % → 5 % (ComplexFuncBench) **[medido]** — [README ComplexFuncBench](https://github.com/THUDM/ComplexFuncBench)
- Con modelos de 8–14B el colapso empieza en la tercera decisión dependiente: Qwen3-14B 92 % con una tool, 84 % con cadena de 2, **16 %** al ramificar sobre un resultado intermedio; Qwen3-8B 76 / 64 / 24 % **[medido, resumen]** — [AgentFloor, arXiv 2605.00334](https://arxiv.org/html/2605.00334v1)
- Un error a nivel de parámetro termina en respuesta final incorrecta con probabilidad ≈0,62 (rango 0,46–0,73 entre 9 modelos de producción), y la capacidad de *rechazar* un parámetro malo no correlaciona con la de *recuperarse* después de aceptarlo (Spearman ρ = 0,126, p = 0,747) **[medido, resumen]** — [AgentProp-Bench, arXiv 2604.16706](https://arxiv.org/pdf/2604.16706)
- Abstención estadística vs. estructural: en la estructural "las solicitudes sin forma autorizada no tienen respuesta expresable" y no requiere estimar confianza; los autores proponen estructural para lo inexpresable y estadística sólo sobre un respaldo generativo **[resumen]** — ["Never the Number", arXiv 2608.13926](https://arxiv.org/pdf/2608.13926)
- La decisión de *no actuar* es débil incluso en frontera: el mejor agente (Gemini 3.1 Pro) logra 59,5 % de exactitud pareada en AgentAbstain, y la capacidad de abstención es en gran parte independiente de la capacidad general **[medido, resumen]** — [AgentAbstain, arXiv 2607.10059](https://arxiv.org/pdf/2607.10059)
- Inconsistencia entre corridas en frontera: en Toolathlon-Verified (108 tareas, 3 corridas) Claude Opus 4.8 tiene Pass@1 76,23 %, Pass@3 84,26 % y **Pass³ 66,67 %** (≈19,9 turnos); GPT-5.5 Pass³ 62,04 % **[medido, resumen]** — [Toolathlon-Verified trajectories (HF)](https://huggingface.co/datasets/hkust-nlp/Toolathlon-Verified_Trajectories); BenchLM lista Opus 5 con Pass³ 73,1 / Pass@3 87,0 atribuidos al harness interno de Anthropic **[agregador, probablemente autoreportado]** — [BenchLM Toolathlon](https://benchlm.ai/benchmarks/toolathlonVerifiedPass3); Sonnet 5.5: 85,2 % pass@3 vs 68,5 % pass^3 **[agregador, resumen]** — [BenchLM](https://benchlm.ai/compare/claude-haiku-5-5-vs-claude-sonnet-5-5)
- Multi-turno: en >200.000 conversaciones simuladas, todos los LLM probados (de chicos open-weight a Gemini 2.5 Pro) bajan en promedio 39 % al pasar de instrucción completa a instrucción subespecificada repartida en turnos (90 → 65 en v1); el componente dominante es *falta de fiabilidad* (supuestos tempranos sobre los que el modelo se apoya), no pérdida de aptitud, y aparece ya con dos turnos **[medido, resumen]** — [Laban et al., LLMs Get Lost in Multi-Turn Conversation, ICLR 2026](https://arxiv.org/abs/2505.06120v1) (el caption de v1 dice −35 %; el abstract/OpenReview −39 %)
- "Trusted" en la industria es estructural, no estadístico: Genie marca una respuesta como Trusted sólo cuando se usa el texto exacto de una consulta de ejemplo parametrizada o de una función SQL verificada; "the answer comes from this verified logic" **[vendor]** — [Databricks, Genie concepts](https://docs.databricks.com/aws/en/genie/concepts); [trusted assets](https://docs.databricks.com/aws/en/genie/trusted-assets)
- Snowflake: el VQR "puede ayudar" a la exactitud; las consultas verificadas inválidas la empeoran; la evaluación compara el SQL generado contra las verificadas y cuenta regresiones. **No publica cuantía de la mejora** **[vendor]** — [Cortex Analyst evaluations](https://docs.snowflake.com/de/user-guide/snowflake-cortex/cortex-analyst-evaluations); [VQR](https://docs.snowflake.com/pt/user-guide/snowflake-cortex/cortex-analyst/verified-query-repository)
- Capa semántica runtime vs. Text-to-SQL: 100 % vs 62,5 % dentro de cobertura; fuera, 0 % (error ruidoso) vs 70 % **[vendor]** — [dbt Labs 2026](https://docs.getdbt.com/blog/semantic-layer-vs-text-to-sql-2026)
- Redacción: mantener los números fuera del LLM. FACTS genera offline plantillas SQL + Jinja2 y envía al LLM sólo esquemas, no valores **[resumen]** — [FACTS, arXiv 2510.13920](https://rbcborealis.com/publications/facts-table-summarization-via-offline-template-generation-with-agentic-workflows/); TabFaith (2026) documenta que los resúmenes de tablas por LLM alucinan valores, atribuyen mal filas/columnas, fabrican rankings y confunden referencias temporales **[resumen]** — [TabFaith, SURGeLLM 2026](https://aclanthology.org/2026.surgellm-1.21/)

**3070 8 GB (Qwen3-8B / Qwen3.5-9B Q4)**
- Línea de base del repo: Text-to-SQL Qwen3-8B Q4_K_M 26/34 con **7 respuestas falsas** en capacidad **[medido, repo]** — `docs/architecture/modelo-local.md §3`
- Qwen3-8B alucina una tool en 36,2 % de los casos cuando sólo hay tools distractoras (56,8 % con thinking) **[medido, resumen]** — [Reasoning Trap, arXiv 2510.22977](https://arxiv.org/pdf/2510.22977)
- BFCL multi-turno *miss_param*: Qwen3-8B pide el parámetro faltante sólo 4–21,5 % según thinking/harness; el resto adivina **[medido, resumen]** — [olmo-eval PR #449](https://github.com/allenai/olmo-eval/pull/449)
- Con ~5 tools, Qwen3-8B Q4_K_M F1 0,919 **[medido]** — [Docker](https://www.docker.com/blog/local-llm-tool-calling-a-practical-evaluation/)

**5070 12 GB (Qwen3-14B Q4 monousuario o Qwen3.5-9B Q6/Q8 multi-slot)**
- Qwen3-14B Q4_K_M F1 0,971 con ~5 tools **[medido]** — [Docker](https://www.docker.com/blog/local-llm-tool-calling-a-practical-evaluation/); 92/84/16 % en AgentFloor (ver arriba).

**API de Claude**
- Tool Search: Opus 4.5 79,5 → 88,1 % en evals MCP internas; ejemplos de uso 72 → 90 % en parámetros complejos; "the most common failures are wrong tool selection and incorrect parameters" **[vendor]** — [Anthropic, Advanced tool use](https://www.anthropic.com/engineering/advanced-tool-use)
- Opus 5.5: 16 de 18 informes pasaron una prueba donde cualquier cifra o cita inventada hacía fallar; Fable 5.1 y Opus 5, ninguno **[vendor]** — [Anthropic, Claude Opus 5.5](https://www.anthropic.com/claude-opus-5-5)
- Text-to-SQL (AIM, subset BIRD): Opus 5.5 87,8 % vs Sonnet 5.5 73,8 % **[independiente, resumen]** — [AIMultiple](https://aimultiple.com/text-to-sql)
- Fable 5.x exige 30 días de retención (no ZDR); Opus/Sonnet/Haiku 5.5 son ZDR-elegibles; `tool_choice` forzado devuelve 400 en Opus/Sonnet 5.5 **[vendor]** — [API and data retention](https://platform.claude.com/docs/en/manage-claude/api-and-data-retention)

### Inferences
**Ecuación de error que sostiene el caso (inferencia).** Con c₀, c₁, c₂ = fracción de preguntas atendidas por capa 0 (intención determinista), capa 1 (herramienta elegida por LLM) y capa 2 (respaldo), y A = fracción abstenida:

- E_B ≈ c₀·FP₀ + c₁·(e_sel + e_slot) + c₂·r₂ + e_red, con r₂ = riesgo selectivo del respaldo tras abstención, y e_red ≈ 0 si la redacción es por plantilla.
- E_A ≈ e_sel + e_arg + (1 − p^(n−1))·[preguntas con n > 1] + P(no abstenerse | infactible)·[infactibles] + e_fabricación + e_red_LLM, con todos los términos aleatorios entre corridas.

Los términos de (B) son *menos* y *más verificables*: FP₀ es una propiedad de reglas (se testea exhaustivamente sobre negativos), e_slot se reduce con resolución de entidades con estado explícito, r₂ se puede llevar a 0 apagando el respaldo, y e_red se elimina con plantillas.

**Steelman por nivel (inferencia):**
- **3070.** El 8B falla en cada una de las decisiones que (A) le delega: elegir con distractores (36 %), pedir parámetros faltantes (4–21 %), encadenar (64 % en cadena de 2), ramificar (24 %). (B) le quita todas salvo una (elegir herramienta + extraer slots en *una* llamada con gramática), y le quita también la decisión de abstenerse en lo infactible si la capa 0 y las reglas de frontera la toman. La línea de base de 7/34 falsas proviene del *SQL libre*; (B) mínimo-error no deja que ese SQL llegue al usuario (ver §3).
- **5070.** Mismo argumento con márgenes menores dentro de una llamada (0,971 vs 0,919), pero el colapso por composición sigue (84 → 16 %). Más VRAM no compra abstención: el sesgo a "llamar alguna tool" no se cura con 6 B más de parámetros (Qwen3-32B: 46,6 % DT sin thinking en Reasoning Trap).
- **Claude.** Dentro de cobertura y con una sola llamada, (A) y la capa 1 de (B) son casi la misma cosa. La ventaja de (B) se reduce a: (i) capa 0 determinista → mismo resultado para la misma pregunta siempre (la brecha Pass@3–Pass³ de ~17 pts en Toolathlon es inconsistencia pura; en una pregunta de 1 llamada será mucho menor, pero no cero); (ii) abstención estructural fuera de cobertura en vez de "componer algo parecido"; (iii) cero números redactados por el LLM; (iv) estado de conversación como objeto de slots en vez de historial libre (Laban: −39 % por supuestos tempranos).

### Gaps
- No existe medición publicada de un híbrido "router → consultas certificadas → SQL con abstención" con tasas de error por capa, ni vendor ni académica (confirmado otra vez en esta ronda; Snowflake y Databricks no publican cuantía).
- No hay pass^k para modelos 8–14B en tareas de 1–2 llamadas, ni para Claude 5.5 en tareas cortas tipo CRUD de lectura.
- La cifra "fabricación en hasta 37,5 % de las trazas" de AgentProp-Bench apareció en un resumen del buscador y **no** apareció en otro; no se usa como dato.

---

## 2. Ataque a (A): cada vía por la que (A) produce respuestas incorrectas que (B) evitaría, con frecuencia y severidad estimadas

### Takeaway
Aun con la superficie ideal (las mismas 5–6 herramientas de nivel tarea que usaría la capa 1 de (B)), (A) conserva **ocho modos de falla que (B) elimina o vuelve visibles**: forzar una herramienta ante preguntas fuera de cobertura, adivinar parámetros omitidos, componer llamadas, agregar/contar en contexto, fabricar o "rellenar" ante fallas o vacíos de la tool, deriva conversacional, redacción numérica infiel e inconsistencia entre corridas. Los tres más dañinos para "respuesta incorrecta presentada como correcta" son el forzado de herramienta, el parámetro adivinado y la fabricación ante vacío/falla, porque producen salidas *plausibles* y no dejan rastro en la respuesta.

### Cited Findings
- **Forzar herramienta / no abstenerse.** Qwen3-8B 36,2 % (DT) y Qwen3-32B 46,6 % de alucinación de tool con sólo distractores; con "ninguna tool disponible" 4,1–5,1 % **[medido, resumen]** — [Reasoning Trap](https://arxiv.org/pdf/2510.22977). Mejor agente en AgentAbstain 59,5 % **[medido, resumen]** — [arXiv 2607.10059](https://arxiv.org/pdf/2607.10059). When2Call: modelos comunitarios F1 16,6–37,8; "most community models are unwilling to admit they cannot answer" **[medido]** — [NVIDIA/When2Call](https://github.com/NVIDIA/When2Call)
- **Parámetro omitido adivinado.** BFCL *miss_param*: Qwen3-8B 0,04–0,215; Claude Haiku 4.5 0,535 **[medido, resumen]** — [olmo-eval PR #449](https://github.com/allenai/olmo-eval/pull/449); [Inspect evals BFCL](https://ukgovernmentbeis.github.io/inspect_evals/evals/assistants/bfcl/index.html). Caso vendor: un filtro "this quarter" descartado devolvió todos los deals **[vendor]** — [CData whitepaper](https://www.cdata.com/lp/ai-accuracy-whitepaper/)
- **Propagación del error de parámetro.** P(respuesta final incorrecta | error de parámetro) ≈ 0,62 **[medido, resumen]** — [AgentProp-Bench](https://arxiv.org/pdf/2604.16706)
- **Composición.** ComplexFuncBench, AgentFloor, NESTFUL (GPT-4o 28 % de secuencia exacta), Live API-Bench (7–47 % de completitud; "a veces explotan mejor el SQL que las API") **[medido, resumen]** — [NESTFUL](https://github.com/IBM/NESTFUL); [Live API-Bench](https://aclanthology.org/2026.eacl-long.143/)
- **Fabricación ante falla o vacío de la tool.** Benchmark de 16 dominios × 8 tipos de falla con llamada obligatoria y payload inutilizable: ninguno de 9 frameworks de agentes de producción especifica cómo debe comportarse el modelo ante una falla de tool; si la falla está *señalizada* es el factor dominante; una defensa a nivel de prompt reduce la deshonestidad "en un orden de magnitud" **[resumen, sin cifras base]** — [Fabrication After Tool Failure, arXiv 2609.14758](https://arxiv.org/pdf/2609.14758). ToolFailBench: el mejor de 19 modelos llega a 86,33 % de "Clean Tool-Use Rate" (incluye categoría Output-Fabrication) **[medido, resumen]** — [ToolFailBench, arXiv 2607.04686](https://arxiv.org/abs/2607.04686). AgentProp-Bench: varios modelos fabrican ejecuciones de tools, falla invisible a las métricas end-to-end; un interceptor en runtime bajó ~23 pp la alucinación en GPT-4o-mini **[resumen]** — [arXiv 2604.16706](https://arxiv.org/pdf/2604.16706). Los agentes tienden a aceptar la salida de la tool "wholesale" **[resumen]** — [When the Tool Decides, arXiv 2606.14476](https://arxiv.org/pdf/2606.14476)
- **"Cero filas" = "no hay".** En el repo: "Zero rows from valid SQL is indistinguishable from 'there is none'"; con RLS, "no tenés permiso" tiene "exactamente la misma firma" **[repo]** — `openspec/changes/asistente-recuperacion-de-valores/proposal.md`; `openspec/changes/asistente-carril-sql/design.md D13`
- **Agregación en contexto / redacción infiel.** Mejor modelo en FaithJudge 6,65 % de alucinación global (incluye data-to-text) **[medido, resumen]** — [FaithJudge, EMNLP 2025 Industry](https://aclanthology.org/2025.emnlp-industry.54/); corregir problemas de entrada en ToTTo reduce errores factuales 52–76 % según modelo; errores numéricos de Llama 2-13B 6 → 1 en 40 muestras **[medido, resumen]** — [arXiv 2404.04103](https://arxiv.org/pdf/2404.04103)
- **Deriva multi-turno.** −39 % promedio; ya con 2 turnos **[medido, resumen]** — [Laban et al.](https://arxiv.org/abs/2505.06120v1); ToolSandbox: GPT-4 alucinó el timestamp actual y llamó `timestamp_diff` **[resumen]** — [ToolSandbox](https://aclanthology.org/2025.findings-naacl.65/)
- **Inconsistencia entre corridas.** Pass@3 − Pass³ ≈ 17 pts en Opus 4.8 y Sonnet 5.5 (Toolathlon) **[medido/agregador, resumen]** — ver §1. LiveMCPBench: corridas repetidas del mismo setup entre 57,9 % y 76,8 % **[resumen]** — [arXiv 2508.01780](https://arxiv.org/abs/2508.01780)
- **Sensibilidad a descripciones/prompt.** Descripciones editadas → >10× más uso de una tool en GPT-4.1 y Qwen2.5-7B **[medido, resumen]** — [EMNLP 2025](https://aclanthology.org/2025.emnlp-main.1060/); en el repo, 8 ejemplos más cambiaron la selección de 22/34 ítems **[repo]** — `docs/architecture/modelo-local.md §8`. Herramientas con nombres duplicados entre servidores MCP hacen que el LLM elija mal o no elija **[práctica, no medido]** — [Stacklok, mcp-tef](https://stacklok.com/blog/introducing-mcp-tef-testing-your-mcp-tool-descriptions-before-they-cause-problems/)
- **Jueces que validan mal al agente.** Juez por substring κ = 0,049 vs humanos; ensemble de 3 LLM κ = 0,432 **[medido, resumen]** — [AgentProp-Bench](https://arxiv.org/pdf/2604.16706): la evaluación de (A) con juez LLM subestima sus errores.

### Inferences
Tabla de modos de falla de (A) "steelman" (mismas herramientas que la capa 1 de (B), resolución de entidades en servidor, `strict`, filtrado de tools, errores accionables). Frecuencias = **rangos inferidos** a partir de las cifras citadas, por pregunta afectada; severidad: **S3** respuesta incorrecta plausible sin señal, **S2** incorrecta pero con interpretación visible, **S1** abstención o pregunta de más.

| # | Modo de falla en (A) | Ejemplo en Ars Docendi | 3070 (8–9B) | 5070 (14B) | Claude 5.5 | Sev. | ¿Cómo lo evita (B)? |
|---|---|---|---|---|---|---|---|
| A1 | Forzar herramienta ante pregunta fuera de cobertura o infactible | cap-030 "mail personal" → `buscar_docentes_por_perfil`; cap-019 "aula" → plantel | 20–40 % de las infactibles | 15–35 % | 2–10 % | S3 | Capa 0 + regla "término de dominio no explicado ⇒ abstención" (estructural); capa 1 con salida `ninguna` |
| A2 | Parámetro omitido adivinado o descartado (período, carrera, vigencia) | "¿cuántas bajas hubo?" → período activo por defecto implícito | 10–30 % de las subespecificadas | 8–20 % | 3–10 % | S3 | Defaults declarados en la herramienta certificada y *eco obligatorio* en plantilla ("período: todos") |
| A3 | Composición de 2+ llamadas | "plantel de la materia con más nombrados" | 35–75 % de las compuestas | 15–80 % (según ramifique) | 5–20 % | S3 | No compone: herramienta compuesta certificada o abstención/escalada |
| A4 | Contar/sumar/rankear filas en contexto, o sobre página truncada | "¿cuántos ayudantes hay?" con `medida=listado` | 5–15 % de agregaciones | 3–10 % | 1–5 % | S3 | `medida=conteo` en SQL; `total` separado; prohibido contar con truncado |
| A5 | Fabricar o "rellenar" ante falla, vacío o error de tool | 403/timeout → inventa lista; vacío → "no hay" sin ámbito | 5–20 % de las fallas | 5–15 % | 1–5 % | S3 | Estados `vacio/no_encontrado/sin_permiso` mapeados a plantillas; el LLM no ve la falla |
| A6 | Deriva conversacional (arrastra o pierde filtros) | "¿y en Ingeniería?" pierde `estado=pendiente` | 10–30 % de seguimientos | 8–25 % | 5–15 % | S3 | Diferencia de slots sobre la llamada canónica, con eco |
| A7 | Redacción infiel de números/entidades | "23" redactado como "32"; mezcla de filas | 2–8 % de respuestas con datos | 1–5 % | 0,5–3 % | S3 | Plantillas desde `structuredContent`; verificador numérico |
| A8 | Inconsistencia entre corridas (misma pregunta, distinta respuesta) | Dos usuarios, dos cifras | alta (temp > 0, cuantización) | media | baja pero >0 | S3 | Capa 0 determinista; capa 1 temp 0 + gramática; caché de consulta canónica |
| A9 | Inyección indirecta desde texto libre devuelto por una tool que guía la siguiente llamada | `pedido_historial.comentario` con instrucciones | no medido | no medido | no medido | S3 | Capa 0/1 no reinyecta filas al modelo antes de decidir; las columnas `sensible-texto` se suprimen |

**Lectura (inferencia).** A1, A2 y A5 dominan el error de (A) en local; A3 domina si el set real tiene preguntas compuestas; A6 y A8 dominan en Claude, donde el resto se achica. Ninguna técnica de la ronda 1 lleva A1 de un 8B a nivel estructural: When2Call con RPO (19 % → 1,2 %) es lo más cerca, pero es *fine-tuning* con datos de abstención, mide un benchmark propio y la ronda 1 ya mostró que los especialistas pierden en conversación real.

**Concesiones honestas al ataque.**
- A4, A5 y A7 **no son inherentes a (A)**: un (A) con `medida=conteo`, estados explícitos y plantillas los elimina igual. Si se los concede, (A) se convierte en "(B) sin capa 0 y sin respaldo".
- (A) tiene una ventaja real: el 403 explícito de la API distingue "sin permiso" de "no hay" — (B) la iguala sólo si cada herramienta certificada consulta `identity.asistente_tiene_permiso` antes de ejecutar (`diseno_superficie_herramientas.md §4`).
- A3 sólo es un costo si la pregunta exige componer. Si el set real se resuelve en una llamada, A3 desaparece y la ventaja de (B) se concentra en A1, A2, A6 y A8.

### Gaps
- No hay medición de A1 (forzado de herramienta) con el catálogo propuesto en `diseno_superficie_herramientas.md §6.2` sobre los 6 `no_contestable` de capacidad y los 3 de social; es el número más importante que falta.
- Las tasas de fabricación ante falla (2609.14758, 2607.04686) no se pudieron leer por modelo; no hay cifras para Qwen3/3.5 ni para Claude 5.5.
- No hay estudio de inyección indirecta que compare un agente multi-paso con un pipeline de una llamada sobre los mismos datos.

---

## 3. El punto más débil de (B) y la variante de (B) que minimiza el error

### Takeaway
El punto más débil de (B) **no es el respaldo SQL sino la frontera del enrutador**: un falso positivo de la capa 0 o de la capa 1 produce una respuesta incorrecta *certificada* (el peor caso posible, porque el usuario recibe el sello de confianza). El código actual lo demuestra: `ResolutorDeIntenciones` captura cuando los términos de la intención son subconjunto de la pregunta y los slots resuelven uno a uno, **sin exigir que la pregunta quede explicada por completo**, de modo que modificadores no modelados (período, carrera, negación, comparativos) se ignoran en silencio. El segundo punto débil es el respaldo SQL local: con la evidencia disponible no hay señal de confianza que lo lleve a un riesgo aceptable sin perder casi toda su cobertura. La variante de mínimo error es: capa 0 con regla de "explicación completa" y eco, capa 1 de una sola llamada con verificación determinista de slots y `ninguna`, redacción por plantilla, reglas de frontera duras, y respaldo **apagado en local** o **escalado a Opus 5.5 con verificador y umbral por clase**, siempre rotulado como no certificado.

### Cited Findings
**Frontera del enrutador**
- Código: `candidatas = catalogo.Intenciones.Where(i => i.Terminos.IsSubsetOf(terminos) && !i.Excluye.Overlaps(terminos))`; cada slot exige exactamente una mención; gana la primera intención candidata que resuelve. No hay chequeo de términos sobrantes **[repo]** — `backend/src/Modules.Asistente/Application/Determinista/ResolutorDeIntenciones.cs`. Intenciones vigentes: p. ej. `pedidos-en-un-estado` = términos {cuantos, pedido} + slot `estado` **[repo]** — `backend/src/Modules.Asistente/Recursos/intenciones.json`
- El propio catálogo usa `excluye` porque "capturar la de conteo con la intención de listado la respondería mal" — el equipo ya identificó el modo de falla, pero lo resolvió para un solo eje (conteo vs listado) **[repo]** — `intenciones.json` (`$comentario`)
- Política vigente: "El default es SQL, nunca API... Enrutar mal hacia la API devuelve cero filas" **[repo]** — `Application/Determinista/EnrutadorDeDominio.cs`
- El repo descartó clasificar intención con LLM: "60% de F1 en triage de 5 clases; 77,4% en 9 vías" **[repo]** — `docs/product/designs/asistente-conversacional-definicion.md §6`
- Routers de intención: recall out-of-scope en CLINC150 Claude 85,6 vs RoBERTa fine-tuneado 58,1 vs TF-IDF 36,4 **[medido, resumen]** — [arXiv 2608.20371](https://arxiv.org/html/2608.20371); detección OOS por grafo de palabras ≈9,9 % de error en el dataset de Larson **[medido, resumen, 2020]** — [Cavalin et al., EMNLP 2020](https://preview.aclanthology.org/nschneid-patch-1/2020.emnlp-main.324); en routers semánticos por embeddings, el umbral es "el hiperparámetro más importante": bajo → rutas equivocadas, alto → todo al LLM; algunos usan doble umbral con zona gris derivada a clasificador **[práctica]** — [NetFoundry semantic routing](https://netfoundry.io/docs/llm-gateway/semantic-routing); [semantic-router route filter](https://docs.aurelio.ai/semantic-router/user-guide/features/route-filter.md)
- Evaluación en español: un benchmark de 2026 encontró que la evaluación sobre texto *traducido* da consistentemente más que sobre texto nativo, con brechas mayores en intenciones de cola larga **[resumen]** — [arXiv 2603.23172](https://arxiv.org/pdf/2603.23172.pdf)

**Respaldo SQL y abstención**
- Señales black-box para predecir corrección en Text-to-SQL: AUROC 0,61–0,68; ensemble de dos proveedores 0,82 → p. ej. 27 % de cobertura con 24 % de riesgo selectivo; "self-consistency offers no valid low-risk subset" **[medido, resumen]** — [arXiv 2607.06799](https://arxiv.org/html/2607.06799v1)
- En el repo, self-consistency on-prem: +0,13 puntos con p95 de 10,6 a 50 s **[repo]** — `docs/architecture/modelo-local.md §6`
- Cascadas: un fallback puede "rescatar" o "dañar" **[resumen]** — [Signed Rescue Routing, arXiv 2609.07786](https://arxiv.org/pdf/2609.07786); políticas heurísticas de abstención en cascadas incumplen el riesgo objetivo por 7,5–12,5 % **[resumen]** — [UCCI, arXiv 2605.18796](https://arxiv.org/html/2605.18796); permitir abstención temprana en el modelo chico (que anticipa cuándo el grande se abstendría) baja la tasa de error 5,0 % y el costo 13,0 % a cambio de +4,1 % de abstención, en 6 benchmarks **[medido, resumen]** — [Zellinger et al., arXiv 2502.09054](https://arxiv.org/pdf/2502.09054); un análisis decisional concluye que cascadas fijas largas rinden por debajo del par óptimo y que subsecuencias optimizadas agregan poco en held-out (sólo abstract) **[resumen]** — [Is Escalation Worth It?, ICML 2026](https://icml.cc/virtual/2026/75267)
- Opus 5.5 87,8 % vs Sonnet 5.5 73,8 % en Text-to-SQL (AIM) **[independiente, resumen]** — [AIMultiple](https://aimultiple.com/text-to-sql)
- Programmatic tool calling y code execution **no** son ZDR-elegibles; structured outputs sí, con schemas cacheados 24 h (sin PII en `enum`) **[vendor]** — [API and data retention](https://platform.claude.com/docs/en/manage-claude/api-and-data-retention)

### Inferences
**Debilidades de (B), ordenadas por severidad (inferencia).**

| # | Debilidad | Mecanismo concreto | Severidad | Mitigación estructural |
|---|---|---|---|---|
| B1 | Falso positivo de capa 0 → incorrecta **certificada** | "¿Cuántos pedidos rechazados hubo en Ingeniería Industrial el ciclo pasado?" satisface {cuantos, pedido} + `estado=rechazado` y el resolutor ignora carrera y período; "¿cuántos pedidos **no** están rechazados?" también captura | Máxima (S3 + sello de confianza) | **Regla de explicación completa**: cada token de contenido de la pregunta debe ser consumido por un término de la intención, un slot, un modificador declarado o una lista de palabras vacías; si sobra uno (p. ej. "ingenieria", "pasado", "no", "más"), la capa 0 no captura. Lista de negadores/comparativos/temporales que bloquea captura. Eco obligatorio de la interpretación. Test de mutación del router: por cada intención, N variantes con un modificador agregado deben **no** capturar |
| B2 | Error de definición en la consulta certificada (error sistemático, replicado a todos) | "vigente", "posgrado", "Categoría 0..6" (TD-025), "período por defecto" (cap-007 vs cap-012/014) | Alta, correlacionada | BR-* con fuente y test; validación con el Departamento (ARS-65); definiciones explícitas en el eco |
| B3 | Capa 1 elige mal o no elige `ninguna` | Mismo riesgo que (A) en una llamada | Alta en local, baja en Claude | Una sola llamada, gramática, `ninguna` explícita, **chequeo de anclaje**: cada valor de slot debe trazarse a un span de la pregunta o a un default declarado; término de dominio no consumido ⇒ abstención; thinking apagado en la decisión en 8B (Reasoning Trap) |
| B4 | Respaldo SQL contesta mal | 7/33 contestadas falsas con Qwen3-8B | Alta en local | Apagado en local o escalado (ver variantes) |
| B5 | Respaldo "rescata" lo que una herramienta se abstuvo de contestar | Herramienta da `no_encontrado` → SQL responde "no hay" | Alta | Reglas de frontera (a)(b)(c) de `diseno_superficie_herramientas.md §5`: si la familia está cubierta y la herramienta devolvió `no_encontrado/ambiguo/sin_permiso`, el respaldo no corre |
| B6 | Arrastre de slots erróneo en seguimientos | Hereda `carrera` cuando el usuario pivotó | Media (visible si hay eco) | `DetectorDeCambioDeTema` + eco; ante duda, preguntar |
| B7 | Desactualización ante cambios de esquema/regla | Consulta certificada ya no refleja la regla | Media | Tests contra fixture en CI; la consulta falla ruidosa, no silenciosa |
| B8 | Sesgo de evaluación (herramientas diseñadas desde el dataset) | Cobertura 26/26 por construcción | Metodológica | §4 |

**Por qué el respaldo local no se justifica hoy (cálculo).** Con la regla del repo (penalización p por falsa ⇒ contestar sólo si P(correcta) > p/(1+p)), con p = 2 el umbral es 66,7 %. La precisión condicional local medida es 26/33 = 78,8 %, pero el límite inferior de Wilson al 95 % con n = 33 es ≈62 %, **por debajo** de 66,7 %. Es decir: con la evidencia disponible no se puede afirmar que el respaldo local supere el umbral ni siquiera en promedio, y mucho menos en las clases con más falsas. Con p = 1 (umbral 50 %) sí lo supera en promedio, pero no por clase.

**Variante de (B) que minimiza el error (B\*), común a todos los niveles (inferencia):**
1. **Capa 0** (0 tokens): intenciones con regla de explicación completa, bloqueo por negación/comparativo/temporal no modelado, eco obligatorio; si no captura, pasa a capa 1 (nunca responde "a medias").
2. **Capa 1** (1 llamada): structured output con {herramienta ∈ catálogo ∪ `ninguna`, slots}; gramática/`strict`; resolución de entidades en servidor con estados `ok/ambiguo/no_encontrado/sin_permiso/vacio`; chequeo de anclaje de slots; chequeo de permiso de familia antes de ejecutar.
3. **Redacción** por plantilla desde el resultado estructurado (nunca números redactados por LLM); el LLM, si interviene, sólo une texto y pasa por un verificador que extrae cada número/entidad y lo busca en el resultado.
4. **Reglas de frontera** duras (B5).
5. **Respaldo** según nivel:
   - **3070 y 5070**: respaldo SQL local **desactivado** (abstención con sugerencia de preguntas soportadas), salvo clases de pregunta donde el evaluador muestre límite inferior de confianza > p/(1+p). Alternativa preferida si la política de datos lo permite: **escalar el respaldo a Claude** (Opus 5.5 con ZDR, esquema + pregunta seudonimizada, sin filas) en lugar de usar el 8B/14B.
   - **Claude**: respaldo en dos escalones, ambos rotulados "no certificado" y con interpretación visible: (i) Opus 5.5 componiendo **sólo herramientas certificadas** (máx. 2–3 llamadas, sin PTC por ZDR); (ii) si no alcanza, Opus 5.5 Text-to-SQL con verificador independiente (otro modelo como juez, p. ej. Sonnet 5.5 sobre SQL de Opus; el ensemble de dos jueces es la única señal con AUROC ≥ 0,8 publicada) y umbral por clase calibrado con el evaluador; si no supera, abstención.
6. **MCP como transporte**: exponer la capa 1 por MCP no cambia la precisión (es transporte); sólo importa *quién decide*. Si un cliente externo (Claude Desktop, Copilot) consume esas herramientas por MCP, ese cliente **es** (A) y hereda §2; el sello "certificado" debería reservarse a respuestas pasadas por capa 0/1 del propio backend.

### Gaps
- No hay medición de falsos positivos de la capa 0 actual: la tabla dorada mide capturas (0/39), no capturas incorrectas. Hace falta un set de negativos con modificadores.
- No hay evidencia publicada del efecto de una regla de "explicación completa" en routers léxicos; la propuesta es diseño propio.
- No hay cifras de verificadores/jueces de SQL en español ni sobre PostgreSQL; el umbral por clase sólo puede calibrarse con el evaluador del repo.
- No se verificó si la política de datos de la UNLaM (Ley 25.326) permite escalar preguntas seudonimizadas a la API.

---

## 4. Qué debe incluir un experimento justo para que (B) no gane por construcción

### Takeaway
Un experimento justo compara (A) y (B) **con las mismas herramientas de backend, el mismo modelo por nivel y un set de preguntas ciego escrito antes de congelar el catálogo**, y puntúa por separado respuestas incorrectas, incorrectas certificadas, abstenciones correctas/incorrectas y consistencia entre corridas. Con 34 ítems no se puede distinguir 2 % de 5 % de error; hacen falta del orden de cientos de preguntas por condición.

### Cited Findings
- El repo ya exige disjunción entre dataset y ejemplos: "si se solaparan, la métrica mediría cuán bien el sistema reproduce ejemplos que ya vio" **[repo]** — `backend/eval/datasets/capacidad.json`
- Anthropic recomienda test sets held-out para no sobreajustar las tools a la evaluación y medir llamadas, tokens y errores de tool **[vendor]** — [Writing tools for agents](https://www.anthropic.com/engineering/writing-tools-for-agents)
- Puntuar por el estado real, "never by accepting the agent's own report" **[práctica]** — [agentpatterns.ai](https://agentpatterns.ai/tool-engineering/mcp-cli-cost-ratio-scaffolding-bound/); los jueces LLM tienen κ moderado (0,432 en ensemble) y aprueban "hallucinated completions" **[resumen]** — [AgentProp-Bench](https://arxiv.org/pdf/2604.16706)
- Benchmarks de tool use ruidosos: Epoch encontró defectos en 24/50 tareas muestreadas de BFCL v4 **[resumen]** — [Epoch AI](https://epoch.ai/benchmarks/berkeley-function-calling-leaderboard/review); el oráculo SQL también tiene errores (6,91 % de gold SQL de BIRD dev) **[resumen]** — [NL2SQL-BUGs](https://nl2sql-bugs.github.io/)
- Evaluación en traducido sobreestima respecto de nativo **[resumen]** — [arXiv 2603.23172](https://arxiv.org/pdf/2603.23172.pdf)
- La métrica del repo penaliza con 0,5/1,0/2,0 las respuestas falsas y las infactibles respondidas **[repo]** — `backend/eval/README.md`
- τ-bench define pass^k como éxito en las k corridas i.i.d. **[resumen]** — [τ-bench](https://arxiv.org/pdf/2406.12045)

### Inferences
**Protocolo propuesto (inferencia):**
1. **Set ciego y estratificado.** Preguntas escritas por usuarios del Departamento (ARS-65) en español rioplatense nativo, *antes* de congelar el catálogo de herramientas e intenciones; partición dev (para diseñar herramientas de ambos lados) / test sellado. Estratos: (i) dentro de cobertura de una llamada, (ii) componibles con 2 herramientas, (iii) fuera de cobertura pero respondibles con SQL, (iv) infactibles/denegadas/sin permiso, (v) ambiguas, (vi) seguimientos multi-turno, (vii) **adversariales para el router**: variantes de preguntas cubiertas con un modificador no modelado (negación, período, carrera, comparativo) — este estrato existe para castigar B1 y evitar que (B) gane por construcción.
2. **Paridad de superficie.** (A) recibe exactamente las mismas herramientas certificadas, la misma resolución de entidades con estados, el mismo chequeo de permiso y la misma forma de salida; además puede usar endpoints extra diseñados *sólo* con el split dev. (A) no se evalúa con endpoints de UI (`catalogos`, `docentes`), que tienen filtros silenciosos.
3. **Ablación de componentes**, para atribuir la diferencia: (A) con redacción LLM vs (A) con plantilla; (A) con y sin pre-router determinista; (B) con respaldo apagado / local con umbral / escalado; (B) con y sin regla de explicación completa. Si "(A) + plantilla + pre-router" iguala a (B) sin respaldo, la conclusión correcta es "los componentes de (B) son los que bajan el error", no "MCP es peor".
4. **Mismo modelo por nivel** y mismas condiciones de serving (cuantización, temperatura de producción, thinking) en ambas arquitecturas; (A) con su mejor configuración en cada nivel (p. ej. tool search sólo si >10 tools en Claude).
5. **Métricas por pregunta**: {correcta, incorrecta, incorrecta-certificada, abstención correcta, abstención sobre lo factible, aclaración correcta}; reportar tasa de incorrectas sobre total, **riesgo selectivo** (incorrectas/contestadas), cobertura, utilidad con p ∈ {0,5; 1; 2}, y **pass^k con k ≥ 5** a la temperatura de producción. Severidad ponderada: incorrecta-certificada cuenta más que incorrecta rotulada.
6. **Oráculo determinista**: respuesta esperada calculada con SQL de referencia sobre un fixture congelado *y* sobre una o dos variantes del fixture con datos mutados (para que ninguna arquitectura acierte por memoria del dataset); juez LLM sólo para equivalencia textual, validado contra una muestra etiquetada por humanos.
7. **Actores**: varios ámbitos (global, carrera, materia, sin permiso), elegidos de forma que el alcance coincida en ambos sistemas (o (A) reutilizando las funciones SQL de alcance), para que la diferencia no sea de autorización.
8. **Potencia estadística**: para distinguir 5 % de 2 % de incorrectas con 80 % de potencia y α = 0,05 en comparación no pareada hacen falta ≈590 preguntas por brazo (cálculo de dos proporciones); con diseño pareado (McNemar) menos, según la discordancia. Con 34 ítems sólo se detectan diferencias groseras (p. ej. 20 % vs 3 %). Corolario: la evidencia actual del repo no permite afirmar diferencias finas en el nivel Claude.
9. **Pre-registro** de umbrales de abstención y del catálogo antes de abrir el test.

### Gaps
- No existe aún un corpus de preguntas reales (hueco 1 de la definición; ARS-65 abierta); sin él, cualquier cifra de cobertura es optimista.
- No se conoce la distribución real entre estratos (cuánto es "una llamada" vs "compuesto" vs "fuera de cobertura"), que es lo que decide el resultado agregado.

---

## 5. Evaluación final honesta: ¿gana (B) en todos los niveles? ¿Dónde es ajustado?

### Takeaway
En la **3070 y la 5070, (B\*) gana con claridad** y por razones estructurales que la evidencia sostiene con margen (abstención y composición son las debilidades más documentadas de 8–14B, y (B\*) se las quita al modelo). En la **API de Claude, (B\*) probablemente gana, pero por poco y no por la arquitectura en sí**: la ventaja proviene de componentes (capa 0 determinista con explicación completa, abstención estructural, plantillas, estado de slots) que un (A) puede adoptar — y al adoptarlos (A) se convierte en (B) sin respaldo. Donde (B) puede **perder** es (i) si la capa 0 se despliega sin la regla de explicación completa (falsos positivos certificados) y (ii) si el respaldo SQL local se habilita sin una cota de confianza que supere p/(1+p).

### Cited Findings
- Local: abstención (36,2 % DT Qwen3-8B), composición (AgentFloor 64/24 % en 8B; 84/16 % en 14B), *miss_param* (4–21,5 %) **[medido, resumen]** — [Reasoning Trap](https://arxiv.org/pdf/2510.22977); [AgentFloor](https://arxiv.org/html/2605.00334v1); [olmo-eval](https://github.com/allenai/olmo-eval/pull/449)
- Claude: selección 88,1 % con tool search en catálogos grandes (Opus 4.5), Pass³ 66,7 % (Opus 4.8) / 68,5 % (Sonnet 5.5) en tareas largas, 87,8 % SQL Opus 5.5 **[vendor/medido/agregador, resumen]** — ver §1
- Multi-turno −39 % para todos los modelos **[medido, resumen]** — [Laban et al.](https://arxiv.org/abs/2505.06120v1)
- Línea de base local 7/34 falsas; Claude Sonnet 5 en su línea de base (otro dataset de 32): 0 traducciones incorrectas, 2 abstenciones sobre lo factible **[repo]** — `backend/eval/lineas-de-base/capacidad.json`

### Inferences
**Estimación ilustrativa de tasa de respuestas incorrectas por estrato (inferencia; rangos derivados de §1–§3, no medidos).** Supuestos de mezcla (hipotéticos): 60 % una llamada en cobertura, 10 % componibles, 15 % fuera de cobertura respondibles, 10 % infactibles/denegadas, 5 % ambiguas; seguimientos tratados aparte.

| Nivel | Estrato | (A) steelman | (B\*) | Comentario |
|---|---|---|---|---|
| 3070 | 1 llamada | 5–15 % | 2–7 % | (B\*) gana por capa 0 (FP≈0–2 % si hay explicación completa) + anclaje de slots |
| 3070 | Componibles | 35–75 % | 0 % (abstiene/escalado) | Cobertura menor en (B\*) |
| 3070 | Fuera de cobertura | 30–60 % | 0 % (respaldo apagado) o riesgo del escalado | Respaldo local ungated sería ≈21 % |
| 3070 | Infactibles | 20–40 % | 3–15 % | Capa 1 sigue decidiendo `ninguna` con un 8B |
| 3070 | **Agregado** | **≈15–30 %** | **≈2–7 %** | **Gana (B\*) con margen amplio** |
| 5070 | Agregado | ≈10–25 % | ≈1–5 % | Gana (B\*) con margen claro |
| Claude (Sonnet/Opus 5.5) | 1 llamada | 1–4 % | 0,5–3 % | Prácticamente iguales; diferencia = capa 0 + plantilla |
| Claude | Componibles | 5–20 % | 2–8 % (Opus compone sólo certificadas, rotulado) | (B\*) usa a (A) como sub-componente |
| Claude | Fuera de cobertura | 10–25 % | 3–10 % (Opus SQL + juez + umbral) o 0 % apagado | Depende del umbral |
| Claude | Infactibles | 2–10 % | 1–5 % | Opus es bueno abstiniéndose, pero no estructuralmente |
| Claude | **Agregado** | **≈3–8 %** | **≈1–4 %** | **Ajustado**: los intervalos se solapan; requiere medición con ~cientos de ítems |

**Dónde es ajustado o dónde (A) podría ganar (inferencia):**
- **Claude, preguntas compuestas**: Opus componiendo 2–3 herramientas certificadas puede ser *más* preciso que Opus generando SQL libre como respaldo de (B) (aunque Live API-Bench sugiere lo contrario para APIs finas, con herramientas gruesas la cadena es corta). Por eso (B\*) en Claude pone la composición sobre herramientas certificadas *antes* del SQL: ese escalón es literalmente (A) acotado.
- **Claude, multi-turno con referencia a resultados** (dia-005 "esa materia"): (A) lo resuelve naturalmente con las filas en contexto; (B) necesita el arrastre de consulta adaptado. Si (B) lo implementa mal, pierde ahí.
- **Si la capa 0 se despliega tal como está en el código** (subconjunto de términos, sin explicación completa), (B) puede producir incorrectas **certificadas** que (A)-Claude no produciría (Claude sí lee "Ingeniería Industrial" y "ciclo pasado"). En el nivel Claude, una capa 0 ingenua puede hacer que (B) **pierda** frente a (A). En local, aun ingenua, probablemente no pierde (el 8B también ignora modificadores, a tasas mayores).
- **Si la cobertura real es baja**, (B\*) "gana" en tasa de error a costa de abstenerse mucho; con penalización p = 0,5 la utilidad puede favorecer a un (A)-Claude que contesta más. El criterio pedido (sólo precisión) favorece a (B\*); el criterio del evaluador del repo con p bajo podría no hacerlo.

**Conclusión por nivel:**
- **3070**: (B\*) sin respaldo local; (A) no es defendible para "mínimo error".
- **5070**: idem; el 14B mejora la capa 1 pero no cambia el ranking.
- **Claude**: (B\*) ≥ (A) con alta probabilidad *si* la capa 0 tiene explicación completa y la redacción es por plantilla; la diferencia es chica y debe demostrarse con el experimento de §4. La arquitectura óptima en este nivel es un híbrido que **contiene** a (A) como escalón de respaldo acotado a herramientas certificadas, no una alternativa excluyente.
- **Fable 5.1**: fuera por retención obligatoria de 30 días (no ZDR) con datos con PII; Haiku 5.5 es candidato para la capa 1 (rápido, ZDR), Opus 5.5 para respaldo.

### Gaps
- Ninguna de las cifras agregadas de la tabla está medida; son rangos inferidos de benchmarks heterogéneos (otros dominios, inglés, otras métricas). Su función es ordenar hipótesis, no reemplazar la medición.
- Falta la corrida de Claude 5.5 (Opus/Sonnet/Haiku) sobre el dataset vigente de 34 ítems y, sobre todo, un set ciego de cientos de preguntas.
- No se encontró ninguna comparación publicada de un agente sobre herramientas certificadas vs. un pipeline de una sola llamada sobre las mismas herramientas con el mismo modelo frontera.
