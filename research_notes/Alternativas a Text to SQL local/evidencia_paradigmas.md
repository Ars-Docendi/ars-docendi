# Evidencia comparada de paradigmas de acceso a datos en lenguaje natural (Text-to-SQL, tools/MCP, capa semántica, intents+plantillas, híbridos)

> Nota metodológica del investigador: el proxy de salida bloqueó la lectura directa de arxiv.org, docs.getdbt.com y cube.dev. Las cifras de esos documentos provienen de los resúmenes devueltos por el buscador (snippets de la fuente primaria); se citan con la URL primaria. Conviene verificar las tablas en los PDF antes de usar números en una decisión formal. Se marcan como **[vendor]** las fuentes de proveedores y como **[peer-reviewed]/[benchmark]** las académicas. Fecha de corte de la búsqueda: octubre 2026.

## 1. ¿Qué dicen los benchmarks de Text-to-SQL realistas/empresariales sobre la precisión, en especial para modelos abiertos pequeños (7–14B)?

### Takeaway
En benchmarks académicos "limpios" (BIRD dev) un 7B especializado llega a ~60–69 % de execution accuracy, pero en esquemas empresariales realistas (Spider 2.0, BEAVER, BIRD-Interact/LiveSQLBench) incluso los modelos frontera caen a 10–60 % y los 7B quedan en un dígito o poco más; la brecha entre benchmark académico y entorno empresarial es mucho mayor que la brecha entre modelo chico y frontera.

### Cited Findings
**Spider 2.0 (ICLR 2025 oral) [benchmark, peer-reviewed]**
- El paper original: el mejor agente (o1-preview) resolvía sólo 21,3 % de las tareas de Spider 2.0, contra 91,2 % en Spider 1.0 y 73,0 % en BIRD con el mismo tipo de modelos — [Spider 2.0, ICLR 2025](https://proceedings.iclr.cc/paper_files/paper/2025/file/46c10f6c8ea5aa6f267bcdabcb123f97-Paper-Conference.pdf)
- En el subconjunto SQLite de 135 preguntas, GPT-4o y DeepSeek-V3 logran 15,6 % y el modelo especializado abierto OmniSQL-7B 10,4 % (dato reportado en trabajo posterior que usa ese subconjunto) — [Human-Level Text-to-SQL via RL on Verified Data, arXiv 2603.20004](https://arxiv.org/pdf/2603.20004) (vía resumen de búsqueda)
- LinkAlign reportó 33,09 % en Spider 2.0-Lite usando sólo LLMs open-source (1.º del leaderboard al momento de envío) — resumen de búsqueda sobre [Spider 2.0 topic page](https://www.emergentmind.com/topics/spider-2-0-benchmark) (fuente secundaria; verificar en el paper de LinkAlign)
- ProSPy (2026): Spider 2.0-Lite 60,15 % con Claude Opus 4.5 vs 41,32 % con DeepSeek V3.2 (abierto, pero ~670B, no "pequeño"); Spider 2.0-Snow 60,51 % vs 40,77 % — [ProSPy, arXiv 2606.05836](https://arxiv.org/pdf/2606.05836)
- Spider 2.0-AIFunc (2026): propietarios ~67–70 %, mejor abierto 58,1 %, resto de abiertos 44,9–57,0 % — [Spider 2.0-AIFunc, arXiv 2607.06229](https://arxiv.org/pdf/2607.06229)
- Caveat de leaderboard: el leaderboard Spider 2.0-Snow muestra entradas de ~96–97 % (mayo 2026) mientras ReFoRCE figura con 62,89 %; sólo 120 de 547 consultas tienen ground truth público, por lo que muchas cifras son auto-reportadas y difíciles de verificar — resumen de búsqueda sobre [xlang-spider2 GitHub](https://github.com/RelationalAI/xlang-spider2) y [SpotIt, arXiv 2510.26840](https://arxiv.org/pdf/2510.26840) (este último cuestiona la validez de la evaluación de Text-to-SQL)

**BEAVER (MIT, 2024; revisión posterior) [benchmark]**
- Dos data warehouses reales: DW con 99 tablas / 1.544 columnas y NW con 366 tablas / 2.708 columnas; ~203 preguntas (cifra de un resumen secundario) — [BEAVER, arXiv 2409.02038](https://arxiv.org/html/2409.02038v2); [review](https://www.themoonlight.io/review/beaver-an-enterprise-benchmark-for-text-to-sql)
- En la versión revisada, frameworks agénticos avanzados con GPT-5.2 alcanzan sólo 10,8 % de execution accuracy; con anotaciones de sub-tareas como "oráculo" suben a 30,1 % — [BEAVER, arXiv 2409.02038](https://arxiv.org/pdf/2409.02038)
- La versión 2024 ya concluía que los LLM "perform poorly in this environment, even when standard prompt engineering and RAG techniques are utilized"; causas: esquemas complejos, preguntas de negocio con joins y agregaciones, y desconocimiento de warehouses privados — [BEAVER v1](https://arxiv.org/html/2409.02038v1)

**BIRD dev, modelos ~7B [peer-reviewed / model cards]**
- OmniSQL-7B: 63,89 % EX en BIRD dev con 1 muestra; 66,95 % con voto mayoritario sobre 32 muestras; 68,90 % con un outcome reward model eligiendo entre 32 candidatos — [GradeSQL, arXiv 2509.01308](https://arxiv.org/pdf/2509.01308); [Test-Time Verification, arXiv 2606.30851](https://arxiv.org/pdf/2606.30851)
- CogniSQL-R1-Zero (7B, RL): 59,97 % en BIRD dev vs CodeS-7B SFT ~50 % — [CogniSQL-R1-Zero, arXiv 2507.06013](https://arxiv.org/pdf/2507.06013)
- Qwen2.5-Coder-7B-Instruct base: 27,0 % en BIRD dev, 52,1 % tras fine-tuning con CoT y 58,5 % con self-consistency K=8 (model card comunitaria, no revisada por pares) — [HF model card](https://huggingface.co/jk200201/qwen2.5-coder-7b-bird-cot)
- Referencia: humanos 92,96 % en BIRD test; top del leaderboard comunitario en BIRD dev en los ~77 % — [benchmarklist BIRD](https://benchmarklist.com/benchmarks/bird_sql/); [BIRD-bench](https://bird-bench.github.io/)

**BIRD-Interact / LiveSQLBench (ICLR 2026 oral) [benchmark]**
- GPT-5 completa sólo 8,67 % de tareas en c-Interact y 17,00 % en a-Interact sobre el set completo — [BIRD-Interact, arXiv 2510.05318](https://arxiv.org/pdf/2510.05318)
- El sitio del proyecto reporta ~24 % (c-Interact) y ~18 % (a-Interact) en Lite y ~16 % en Full para los mejores modelos de razonamiento — [bird-interact.github.io](https://bird-interact.github.io/)
- LiveSQLBench Base-Lite: o3-mini 44,81 % o 47,78 % según la fuente (inconsistencia entre noticias y leaderboard) — [LiveSQLBench](https://livesqlbench.ai/)

**Idioma**
- MultiSpider (AAAI 2023, **fuente antigua**): caída absoluta promedio de 6,1 % en idiomas no ingleses (incluye español) respecto a inglés; el método SAVe mejora ~1,8 % — [MultiSpider, AAAI 2023](https://ojs.aaai.org/index.php/AAAI/article/view/26499)

### Inferences
- Para UNLaM: Qwen3-8B Q4 en Text-to-SQL libre debería esperarse, en el mejor caso, en el rango de un 7B genérico no especializado en SQL (BIRD dev ~27–55 %) y bastante por debajo en preguntas que requieren conocimiento institucional no presente en el esquema; la cuantización Q4 y el español (~-6 pp histórico) restan más. No hay cifras publicadas para Qwen3-8B Q4 en BIRD/Spider 2.0.
- La caída "académico → empresarial" (73 % → 21 % en Spider 2.0 con el mismo modelo; 10,8 % en BEAVER con GPT-5.2) se debe sobre todo a esquema grande + semántica de negocio implícita, que es exactamente lo que una capa semántica o un set de tools curado encapsula.
- Un esquema institucional acotado y bien documentado (vistas, nombres en español, comentarios) está más cerca de BIRD que de BEAVER, lo que matiza el pesimismo, pero no elimina el riesgo de respuestas plausibles e incorrectas.

### Gaps
- No encontré resultados de Qwen3-8B (ni cuantizado) en Spider 2.0, BEAVER o BIRD-Interact; las filas de modelos abiertos de BIRD-Interact no fueron accesibles.
- No hay cifra específica de caída para español en benchmarks recientes (2025–2026).
- No se pudo verificar el número exacto de preguntas de BEAVER en la fuente primaria.

## 2. ¿Qué evidencia hay de que una capa semántica / knowledge graph / vistas curadas mejora la precisión? Caveats metodológicos

### Takeaway
Toda la evidencia apunta en la misma dirección (más contexto de negocio modelado ⇒ más precisión y, sobre todo, menos respuestas plausibles-pero-incorrectas), pero casi toda proviene de proveedores interesados y de benchmarks diminutos (11 preguntas en el caso dbt/data.world); la capa semántica falla "cerrado" fuera de su cobertura (0 % en preguntas no modeladas).

### Cited Findings
- **data.world / Sequeda et al. (2023, fuente antigua, autores afiliados a vendor de KG)**: GPT-4 zero-shot 16,7 % sobre SQL directo vs 54,2 % sobre representación de Knowledge Graph del mismo esquema de seguros; en preguntas simples sobre esquema complejo, SQL cayó a 0 % — [Sequeda et al. benchmark](https://www.semanticscholar.org/paper/A-Benchmark-to-Understand-the-Role-of-Knowledge-on-Sequeda-Allemang/b66c5d17424b37c46980d50bd2796c568e1e926f); [ScienceDirect, J. Web Semantics](https://www.sciencedirect.com/science/article/pii/S1570826824000441)
- Seguimiento: con validación basada en ontología (OBQC) la precisión sube de 54,2 % a 72 % — [Ontologies to the Rescue, arXiv 2405.11706](https://arxiv.org/pdf/2405.11706)
- Crítica: la ontología del benchmark es demasiado pequeña para ser representativa de ontologías empresariales — resumen de búsqueda sobre [ResearchGate](https://www.researchgate.net/publication/381292240_A_Benchmark_to_Understand_the_Role_of_Knowledge_Graphs_on_Large_Language_Model's_Accuracy_for_Question_Answering_on_Enterprise_SQL_Databases)
- **dbt Labs 2026 [vendor]**: mismo benchmark ACME Insurance (11 preguntas × 20 corridas). Sin modelado adicional, Text-to-SQL (Sonnet 4.6) 64,5 % global (era 32,7 % con modelos 2023) vs capa semántica 72,7 % global; desagregado: dentro del alcance 62,5 % (SQL) vs 100 % (SL); fuera del alcance (demasiados saltos de entidad) 70,0 % (SQL) vs 0 % (SL) — [dbt Labs, Semantic Layer vs Text-to-SQL 2026](https://docs.getdbt.com/blog/semantic-layer-vs-text-to-sql-2026)
- Tras agregar 3 modelos que unen tablas: SL 98,2 % vs Text-to-SQL 90,0 % (Sonnet 4.6); SL 100 % vs SQL 84,1 % (GPT-5.3 Codex). El modelado también mejoró a Text-to-SQL — [dbt Labs 2026](https://docs.getdbt.com/blog/semantic-layer-vs-text-to-sql-2026)
- dbt enfatiza que los modos de falla difieren: Text-to-SQL devuelve un número plausible pero erróneo; la capa semántica devuelve un error — [dbt Labs 2026](https://docs.getdbt.com/blog/semantic-layer-vs-text-to-sql-2026)
- Réplicas de terceros sobre el mismo set: 220/220 correctas (PyDough/Bodo) y Cube 100 % en el setup 2023 — [Bodo.ai](https://www.bodo.ai/blog/how-pydough-reached-100-accuracy-on-dbts-semantic-layer-benchmark); [Delphi](https://delphihq.substack.com/p/delphi-at-100-dbt-semantic-layer)
- **Cube 2026, "paired benchmark" con tres modelos frontera [vendor, preprint]**: agregar un documento semántico mejoró la precisión entre +17 y +23 puntos porcentuales en los tres modelos; el estudio mide también alucinación — [Semantic Layers for Reliable LLM-Powered Data Analytics, arXiv 2604.25149](https://arxiv.org/pdf/2604.25149); [Cube blog](https://cube.dev/blog/why-semantic-layers-make-llm-analytics-reliable-a-paired-benchmark-across-three-frontier-models)
- **Snowflake Cortex Analyst [vendor, evaluación interna]**: "90%+ SQL accuracy on real-world use cases" con agente + modelo semántico; ~2× sobre GPT-4o single-prompt y ~14 % sobre una solución competidora no nombrada — [Snowflake engineering blog](https://www.snowflake.com/en/engineering-blog/cortex-analyst-text-to-sql-accuracy-bi/). VentureBeat reportó cifras internas: GPT-4o directo ~51 %, sistemas Text-to-SQL dedicados incl. Databricks Genie ~79 %, Cortex Analyst ~90 % — [VentureBeat](https://venturebeat.com/ai/snowflake-launches-cortex-analyst-an-agentic-ai-system-for-accurate-data-analytics)
- Snowflake argumenta que Spider/BIRD tienen relevancia limitada para BI real ("one might assume that text-to-SQL is a solved problem") — [Snowflake engineering blog](https://www.snowflake.com/en/engineering-blog/cortex-analyst-text-to-sql-accuracy-bi/)
- Hay trabajo académico 2026 sobre agentes NL2SQL mediados por capa semántica en bases heterogéneas — [arXiv 2606.31041](https://arxiv.org/html/2606.31041v1) (no se extrajeron cifras)

### Inferences
- Para un modelo de 8B, el beneficio relativo de una capa semántica/vistas curadas debería ser mayor que para frontera: transfiere el razonamiento de joins y la semántica de negocio del modelo (débil) a artefactos deterministas (fuertes). Esto es inferencia; ningún estudio encontrado lo mide con 7–14B.
- La capa semántica se comporta como un "Text-to-API" estructurado: precisión casi perfecta dentro de cobertura, 0 % fuera. El costo de mantenimiento se traslada a modelar métricas/entidades.
- Para UNLaM, "vistas curadas en PostgreSQL + catálogo de métricas en español" es el equivalente pobre de dbt/Cube sin introducir dependencias nuevas.

### Gaps
- No encontré benchmark independiente (no-vendor) de AtScale ni cifras públicas con metodología de LookML/Looker.
- Ningún estudio de capa semántica usa modelos locales pequeños ni español.
- No pude leer las tasas exactas de alucinación del preprint de Cube (arXiv bloqueado).

## 3. ¿Qué evidencia compara tool/API calling contra generación de SQL? ¿Dónde falla cada uno?

### Takeaway
La única comparación directa robusta encontrada (Live API-Bench, EACL 2026) muestra que, sobre las mismas preguntas de BIRD, convertir el acceso a secuencias de APIs es *más difícil* para los LLM que escribir SQL cuando la respuesta requiere componer varias llamadas (7–47 % de completitud, ~50 % con ReAct); el tool calling brilla en preguntas de una sola llamada. Los modelos de 8B son flojos en tool-use multi-turno (τ-bench ~25–45 %).

### Cited Findings
- **Live API-Bench (Elder et al., IBM; EACL 2026) [peer-reviewed]**: convierte consultas BIRD-SQL en secuencias de API ejecutables en tres formulaciones (SLOT, SEL, REST); 11 bases, >2.500 tools, con ground truth verificado — [arXiv 2506.11266](https://arxiv.org/abs/2506.11266); [ACL Anthology](https://aclanthology.org/2026.eacl-long.143.pdf)
- Tasas de completitud de tareas bajas: 7–47 % según dataset, mejorando sólo a ~50 % con agentes ReAct sobre el entorno vivo; un resumen de alphaxiv de la v1 menciona 0–7 % en tareas multi-paso (diferencia entre versiones) — [arXiv 2506.11266v2](https://arxiv.org/html/2506.11266v2); [alphaxiv](https://www.alphaxiv.org/overview/2506.11266v1)
- Los autores señalan que los modelos "are sometimes able to exploit SQL better than APIs" y que dependen fuertemente de pistas semánticas en los nombres de tools — [arXiv 2506.11266](https://arxiv.org/pdf/2506.11266)
- Trabajo relacionado (declarativo sobre bases + APIs): los sistemas de tool-calling "are not trained, fine-tuned or optimized to perform complex API sequencing, merging, and aggregation tasks"; rinden bien cuando basta una llamada, pero incluso consultas fáciles de Spider requieren varias llamadas secuenciadas — [Declarative Techniques for NL Queries over Heterogeneous Data, arXiv 2510.16470](https://arxiv.org/html/2510.16470)
- **Capacidad de tool-use de Qwen3-8B [mixto, auto-reportado]**: BFCL-v4 global 42,21; τ-bench promedio 35,83 (retail 35,65 / airline 36,00); τ²-bench 34,64 — [ToolMind, arXiv 2511.15718](https://arxiv.org/html/2511.15718v2). Modo thinking: BFCL v3 68,2, τ-bench retail 45,2 / airline 25,0; non-thinking: 59,8 / 35,7 / 12,0 — [Klear-AgentForge, arXiv 2511.05951](https://arxiv.org/pdf/2511.05951). BFCL v3 multi-turn 41,8 % — [benchmarklist Qwen3-8B](https://benchmarklist.com/models/qwen-qwen3-8b/)
- Con post-training específico para recuperarse de errores de ejecución, una variante de Qwen3-8B llega a 46,75 % en BFCL v4 multi-turn y 51,3 % / 40,0 % en τ-bench retail/airline — [Fission-GRPO, arXiv 2601.15625](https://arxiv.org/pdf/2601.15625)
- **Agentes SQL con herramientas de exploración de esquema**: ReFoRCE 62,89 % en Spider 2.0-Snow (leaderboard) — [xlang-spider2](https://github.com/RelationalAI/xlang-spider2); existen enfoques de RL multi-turno con herramientas sobre esquemas desconocidos (TRUST-SQL) — [arXiv 2603.16448](https://arxiv.org/pdf/2603.16448) (sin cifras extraídas)
- BIRD-Interact: un agente propio (vendor Motley, con su capa "SLayer" + Claude SDK) reporta top score; un agente raw-SQL con Claude SDK habría llegado a 71,7 % — [Motley blog [vendor]](https://motley.ai/blog-posts/bird-interact-benchmark-top-score/) (no verificado, setup propietario)

### Inferences
- Tools/MCP sobre endpoints existentes es lo más robusto para preguntas operativas puntuales ("¿qué designaciones tiene el docente X?", "¿qué aula está libre el martes?"), que mapean a una llamada; es lo menos robusto para preguntas analíticas compuestas (rankings, agregaciones cruzando módulos), donde el modelo debe encadenar y agregar en su contexto — justo donde un 8B es débil.
- Exponer endpoints CRUD "tal cual" como tools reproduce el problema Live API-Bench; diseñar tools de grano grueso orientadas a preguntas (agregaciones hechas en backend) reduce el número de llamadas requeridas.
- Con ~40 % en BFCL/τ-bench, Qwen3-8B no garantiza selección correcta de tool + argumentos en multi-turno; conviene reducir la cantidad de tools por turno (router previo) y validar argumentos en backend.

### Gaps
- No hay estudio que compare cabeza a cabeza los cinco paradigmas sobre el mismo dataset y el mismo modelo pequeño.
- No encontré evaluaciones publicadas de StructGPT/TableGPT/MAC-SQL con modelos 7–8B en esquemas empresariales en 2025–2026 (no se buscaron en profundidad por límite de llamadas).
- Sin evidencia cuantitativa específica de MCP vs function-calling nativo (MCP es transporte; la precisión depende del modelo y del diseño de tools).

## 4. Cobertura vs respuestas incorrectas silenciosas y abstención

### Takeaway
El trade-off está documentado: la capa semántica/tools fallan "ruidosamente" (error o no-respuesta) fuera de cobertura, mientras que Text-to-SQL contesta la cola larga pero con respuestas plausibles e incorrectas; en TrustSQL, ningún método superó consistentemente a abstenerse siempre cuando los errores se penalizan fuerte.

### Cited Findings
- dbt 2026: dentro de cobertura SL 100 % vs SQL 62,5 %; fuera de cobertura SL 0 % (devuelve error) vs SQL 70 % (pero sin garantía) — [dbt Labs 2026](https://docs.getdbt.com/blog/semantic-layer-vs-text-to-sql-2026)
- **TrustSQL (KAIST, 2024, fuente antigua)**: el modelo debe devolver SQL o abstenerse ante preguntas infactibles o errores probables; "incorrect model outputs for both feasible and infeasible questions can go unnoticed"; bajo penalizaciones altas, ningún método superó consistentemente un baseline que se abstiene en todas las preguntas — [TrustSQL, arXiv 2403.15879](https://arxiv.org/html/2403.15879v4)
- Confidence Estimation for Text-to-SQL (2025): compara 7 métodos; los basados en consistencia fueron los mejores black-box, y el grounding por ejecución aporta señal complementaria — [arXiv 2508.14056](https://arxiv.org/html/2508.14056v1)
- RTS (2025): abstención en la etapa de schema linking con predicción conformal, con opción de pedir aclaración al usuario; evaluado en BIRD — [Reliable Text-to-SQL with Adaptive Abstention, arXiv 2501.10858](https://arxiv.org/html/2501.10858v1)
- "Never the Number" (2026) argumenta por abstención estructural: en sistemas cuyas respuestas se consumen como hecho, las solicitudes no respondibles deberían ser irrepresentables (no requiere estimar confianza) — [arXiv 2608.13926](https://tldr.takara.ai/p/2608.13926)
- Self-consistency/voto mayoritario y reward models aportan +3–5 pp en 7B (63,89 → 66,95 → 68,90 % en BIRD dev) — [GradeSQL](https://arxiv.org/pdf/2509.01308); [arXiv 2606.30851](https://arxiv.org/pdf/2606.30851)

### Inferences
- Para un sistema institucional (datos que se toman como oficiales), una respuesta incorrecta silenciosa cuesta más que un "no puedo responder"; la evidencia favorece intents deterministas/tools/capa semántica como camino principal, con Text-to-SQL sólo como fallback rotulado ("respuesta exploratoria, verificar") y con abstención.
- El voto por consistencia (varias muestras + comparar resultados) es una señal de abstención barata, pero multiplica el costo de inferencia en GPU de 8–12 GB con usuarios concurrentes.

### Gaps
- No encontré estudios que cuantifiquen la tasa de "respuesta incorrecta silenciosa" en producción para chatbots de datos, ni para el paradigma intent+plantilla (d), más allá de argumentos de diseño.
- Sin cifras de cobertura típica de catálogos de intents en dominios universitarios.

## 5. Seguridad: Text-to-SQL vs tool calling/MCP

### Takeaway
Text-to-SQL convierte la inyección de prompts en inyección SQL (P2SQL, ICSE 2025) y las restricciones por prompt no alcanzan; la mitigación real está en la base (rol de sólo lectura, RLS, vistas, timeouts). Tool calling hereda la autorización de endpoints existentes, pero abre otra superficie: inyección indirecta vía datos devueltos y "tool poisoning" en descripciones MCP (ASR promedio 36,5 %, hasta 72,8 % en MCPTox).

### Cited Findings
- **P2SQL (Pedro et al., arXiv 2023; ICSE 2025) [peer-reviewed]**: apps LangChain son "highly susceptible to P2SQL injection attacks"; con la plantilla por defecto un "DROP TABLE users CASCADE" pasa directo a la consulta; las reglas en el prompt para bloquear escrituras fueron evadidas por dos ataques independientes; incluye ataques indirectos (datos almacenados que inyectan instrucciones); evaluado en 7 LLMs y 5 aplicaciones reales vulnerables; proponen 4 defensas como extensiones de LangChain — [arXiv 2308.01990](https://arxiv.org/pdf/2308.01990); [ICSE 2025](https://conf.researchr.org/details/icse-2025/icse-2025-research-track/31/Prompt-to-SQL-Injections-in-LLM-Integrated-Web-Applications-Risks-and-Defenses)
- **OWASP Top 10 for LLM Applications 2025**: relevantes LLM01 Prompt Injection (incluye indirecta vía respuestas de herramientas o documentos), LLM02 Sensitive Information Disclosure (escenario de exfiltración de registros por prompt), LLM05 Improper Output Handling (salida insegura → SQL injection/ejecución de código), LLM06 Excessive Agency (permisos excesivos de agentes), LLM07 System Prompt Leakage — [OWASP LLM Top 10 2025 (resumen)](https://opensourcesecurity.substack.com/p/a-deep-dive-into-the-owasp-top-10); [Invicti](https://voltron81.invicti.com/blog/web-security/owasp-top-10-risks-llm-security-2025)
- El Top 10 LLM cubre entrada/salida del modelo, no tools; OWASP GenAI publica listas separadas (incl. un Top 10 específico de MCP) — [Obot](https://obot.ai/?p=3542); [MCP Security 2026](https://mcpplaygroundonline.com/blog/mcp-security-tool-poisoning-owasp-top-10-mcp-scan.md) (fuentes secundarias)
- **MCPTox (AAAI 2026) [peer-reviewed]**: 45 servidores MCP reales, 353 tools, 1.312 casos maliciosos; ASR máximo 72,8 % (o1-mini), Phi-4 70,2 %, GPT-4o-mini 61,8 %, Qwen3-32B 58,5 % (reasoning); promedio 36,5 %; tasa de rechazo máxima <3 %; modelos más capaces suelen ser más susceptibles porque siguen mejor instrucciones — [MCPTox, arXiv 2508.14925](https://arxiv.org/html/2508.14925v1); [AAAI](https://ojs.aaai.org/index.php/AAAI/article/view/40895)
- Invariant Labs (abril 2025) documentó tool poisoning: una tool "calculadora" maliciosa exfiltró una clave SSH y el config MCP desde Cursor — [hol.org](https://hol.org/blog/mcp-tool-poisoning-ai-agent-protocol-attack-surface); CyberArk: "no output from your MCP server is safe" (envenenamiento también vía outputs) — [CyberArk](https://www.cyberark.com/resources/home/poison-everywhere-no-output-from-your-mcp-server-is-safe); CSA whitepaper sobre tool poisoning y rug pulls — [CSA 2026](https://labs.cloudsecurityalliance.org/research-rb/csa-whitepaper-mcp-security-tool-poisoning-20260506-csa-styl/)

### Inferences
- Para UNLaM con MCP propio (servidor y tools controlados por el equipo, no de terceros), el riesgo de tool poisoning por supply chain es bajo; el riesgo relevante es inyección indirecta vía datos devueltos (p. ej., texto libre cargado por usuarios en observaciones) y "excessive agency" si se exponen endpoints de escritura.
- Tool calling sobre endpoints que ya aplican autorización por usuario (identidad propagada, no cuenta de servicio) preserva el modelo de permisos existente; Text-to-SQL exige replicarlo en la base (rol read-only, RLS por ámbito, vistas sin PII, statement_timeout, límite de filas), porque el filtro por prompt es evadible.
- Exponer sólo tools de lectura con argumentos tipados y validados en backend elimina la clase P2SQL completa.

### Gaps
- No encontré estudios cuantitativos sobre efectividad de RLS de PostgreSQL contra ataques P2SQL en chatbots.
- No hay mapping oficial verificado de tool poisoning a una categoría del OWASP LLM Top 10 2025 (existe el OWASP MCP Top 10, no leído en primaria).

## 6. Evaluación multi-turno/conversacional por paradigma

### Takeaway
Los benchmarks interactivos muestran que el cuello de botella en conversación no es generar SQL sino gestionar ambigüedad y aclaraciones: incluso GPT-5 resuelve 8,67–17 % en BIRD-Interact; en tool-use conversacional (τ-bench) un 8B ronda 25–45 %. No hay benchmark que compare paradigmas en multi-turno sobre los mismos datos.

### Cited Findings
- BIRD-Interact: simulador de usuario con función, dos modos (c-Interact protocolo fijo; a-Interact el agente decide cuándo preguntar), tareas CRUD completas; 600 tareas full + 300 lite — [BIRD-Interact, ICLR 2026](https://proceedings.iclr.cc/paper_files/paper/2026/file/496b549556509bbb9770bf9d335c5800-Paper-Conference.pdf)
- GPT-5: 8,67 % (c-Interact) y 17,00 % (a-Interact) en el set completo — [arXiv 2510.05318](https://arxiv.org/pdf/2510.05318)
- "Memory grafting": dar a GPT-5 los historiales de resolución de ambigüedad de Qwen-3-Coder u o3-mini mejoró notablemente su SQL final ⇒ la debilidad es de comunicación/aclaración, no de generación — [arXiv 2510.05318](https://arxiv.org/pdf/2510.05318)
- Trabajo VLDB 2026 (workshop) sobre estrategias de preguntas de aclaración: combinar dos estrategias sube el recall ≥6 pp sobre cualquiera sola en tres modelos abiertos — [AIDB 2026](https://www.vldb.org/2026/Workshops/VLDB-Workshops-2026/AIDB/aidb26_10.pdf)
- τ-bench Qwen3-8B: 35,7 % retail / 12,0 % airline (non-thinking) vs 45,2 / 25,0 (thinking) — [Klear-AgentForge](https://arxiv.org/pdf/2511.05951)

### Inferences
- En multi-turno, el paradigma de slot-filling determinista (d) tiene una ventaja estructural: la aclaración es explícita (falta slot ⇒ pregunta), no depende de que el modelo "decida" preguntar, que es justo lo que BIRD-Interact muestra como débil.
- El modo thinking de Qwen3-8B duplica el rendimiento en τ-bench airline pero aumenta latencia/tokens, tensionando la concurrencia en 8–12 GB VRAM.

### Gaps
- No encontré resultados recientes de CoSQL con modelos 7–8B ni de BIRD-Interact para modelos abiertos pequeños.
- Ningún estudio evalúa intent+plantillas (d) contra LLM en multi-turno con métricas comparables; la evidencia para (d) es de diseño, no empírica.
