# Abogado del diablo de (A): el mejor caso posible para el agente MCP sobre REST frente al híbrido (B), y el ataque a (B)

> **Alcance y método.** Ronda adversarial. El objetivo es construir el caso más fuerte posible, con evidencia, de que **(A)**, un agente que llama endpoints REST expuestos por MCP, logra **más precisión, es decir menos respuestas incorrectas**, que **(B)**, el híbrido intenciones deterministas → pocas herramientas certificadas parametrizadas → Text-to-SQL de respaldo con abstención. En (A) los endpoints se agregan o rediseñan libremente para cobertura y se aplican todas las técnicas de optimización conocidas. Después, el objetivo es atacar a (B). El único criterio es la precisión; el esfuerzo de implementación no cuenta.
>
> **Bases de evidencia:**
> - las cuatro notas de esta carpeta (`tasas_de_error_arquitecturas.md`, `tecnicas_optimizacion_mcp.md`, `modelos_por_nivel.md` y `diseno_superficie_herramientas.md`);
> - `Alternativas a Text to SQL local/arquitectura_actual.md`;
> - el informe `reports/Alternativas a Text to SQL local.md`;
> - lectura nueva del código del worktree (`/tmp/claude-0/-home-user-ars-docendi/caaad005-9e8c-59cb-9cfd-725e9fa71d55/scratchpad/asistente`; las rutas de repo se citan relativas a ese worktree);
> - 13 búsquedas o fetch web nuevos.
>
> **Etiquetas:**
> - **[medido]**: medición publicada con metodología visible, o medición del repo.
> - **[vendor]**: fuente con interés comercial o autoevaluación del fabricante.
> - **[resumen]**: cifra tomada del resumen del buscador, sin poder abrir la fuente primaria. El proxy bloqueó arxiv.org, software.strategy.com y otros dominios.
> - **[inferencia]**: razonamiento propio, no medido.
>
> Fecha de corte: 2026-10-08.

---

## 1. El caso más fuerte para (A), por nivel de modelo

### Takeaway
El caso fuerte para (A) existe **solo en el nivel de API de Claude** y se apoya en tres puntos:
1. Un modelo frontera sobre pocas herramientas compuestas, bien descritas y con abstención por estado del servidor, iguala a (B) dentro de la cobertura.
2. Fuera de la cobertura de una sola herramienta, componer primitivas certificadas puede equivocarse menos que el SQL libre del respaldo de (B).
3. Un LLM frontera detecta mejor que un enrutador léxico las restricciones que la herramienta no soporta.

En los niveles locales (RTX 3070 8 GB y RTX 5070 12 GB), lo mejor que se le puede atribuir a (A) es que, configurado de forma ultraconservadora (una llamada, abstención por estado del servidor, sin respaldo SQL), se vuelve **estructuralmente idéntico a "B sin capa 0 y sin respaldo"**. La ventaja viene entonces de quitar el respaldo SQL, no del agente ni de MCP.

### Cited Findings
**Nivel API de Claude: evidencia que favorece a A**
- **MCP-Atlas.**
  - Las tareas usan prompts que no nombran herramientas ni servidores. Cada una exige orquestar **3 a 6 llamadas** entre varios servidores y se puntúa por afirmaciones en la respuesta final — [MCP-Atlas, arXiv 2602.00933](https://arxiv.org/html/2602.00933v1) [resumen].
  - En el paper original el mejor modelo fue Claude Opus 4.5, con 62,3 %. En el snapshot de julio de 2026, Claude Opus 5 llega a 85,8 % (88,1 % el líder) y a 89,1 % de "claim coverage" — [Scale Labs MCP Atlas](https://labs.scale.com/leaderboard/mcp_atlas); [BenchLM MCP Atlas](https://benchlm.ai/benchmarks/mcpatlas) [resumen, agregador].
  - Opus 5.5, Sonnet 5.5 y Haiku 5.5 todavía no figuran.
  - Lectura: en dos generaciones, la tarea multi-llamada sobre MCP real pasó de ~62 % a ~86 %.
- **Corrección ante feedback de las herramientas.** En CCTU (tool use bajo restricciones complejas, mar-2026), **Claude Opus 4.6 corrige 65,36 %** de los errores tras el feedback de validación de restricciones. Los demás modelos quedan por debajo de 60 % y o3 en 18,57 %. El razonamiento extendido ayudó a algunos modelos y perjudicó a otros, que "doblan la apuesta" — [CCTU, arXiv 2603.15309](https://arxiv.org/pdf/2603.15309) [resumen]. Es la única cifra 2026 de "tasa de corrección" que encontré, y favorece a Claude.
- **Errores accionables.** La spec MCP 2026-07-28 define los errores de ejecución (`isError: true`) como "actionable feedback that language models can use to self-correct and retry", y pide a los clientes que se los pasen al modelo — [spec MCP 2026-07-28, tools.mdx](https://github.com/modelcontextprotocol/modelcontextprotocol/blob/main/docs/specification/2026-07-28/server/tools.mdx) (vía `tecnicas_optimizacion_mcp.md` §3).
- **Alucinación medida en invocaciones MCP.** En LiveMCPBench se analizaron 814 invocaciones de Claude-Sonnet-4: **9,00 % con alucinación**, sobre todo información intermedia inventada en razonamiento multi-paso — [LiveMCPBench, arXiv 2508.01780](https://arxiv.org/pdf/2508.01780) [resumen].
- **Las alucinaciones de herramienta o argumento se pueden bloquear deterministamente.** Un resolvedor "closed-world" (sin entrenamiento) valida cada llamada contra el registro de herramientas. Según el paper, rechazó las **322** alucinaciones reales emitidas por 10 modelos hosteados y las **154** alucinaciones MCP (colisión de nombres, shadowing, definiciones viejas) de modelos frontera incluidos, mientras que un host MCP ingenuo las ejecutó todas. Las herramientas inventadas sobrevivieron a una API con schema estricto solo en los open-weight más débiles — [Closed-World Resolution, arXiv 2609.19425](https://arxiv.org/pdf/2609.19425) [resumen; autoreportado, sin réplica].
- **Precisión de las herramientas en Claude.**
  - Tool Search (Opus 4.5): 79,5 → 88,1 %.
  - `input_examples`: 72 → 90 % en parámetros complejos.
  - Resolver UUIDs a nombres "significantly improves Claude's precision".

  — [Anthropic, Advanced tool use](https://www.anthropic.com/engineering/advanced-tool-use); [Anthropic, Writing tools for agents](https://www.anthropic.com/engineering/writing-tools-for-agents) [vendor] (vía `tecnicas_optimizacion_mcp.md`).
- **Fuera de alcance: LLM contra clasificador entrenado.** En CLINC150, el recall fuera de alcance de Claude es 85,6 contra 58,1 de RoBERTa fine-tuneado y 36,4 de TF-IDF, con exactitud dentro de alcance estadísticamente empatada — [arXiv 2608.20371](https://arxiv.org/html/2608.20371) [resumen] (vía `tasas_de_error_arquitecturas.md` §3). Es el argumento de que un LLM frontera se "da cuenta" mejor que un clasificador o una tabla léxica de que una pregunta no cabe.
- **Honestidad de Opus 5.5.** En una tarea de informe donde cualquier cifra o cita inventada hacía fallar, 16 de 18 informes de Opus 5.5 pasaron; Fable 5.1 y Opus 5 no pasaron en ningún intento — [Anthropic, Claude Opus 5.5](https://www.anthropic.com/claude-opus-5-5) [vendor] (vía `modelos_por_nivel.md` §1).
- **Reglas de negocio en una capa gobernada en vez de tablas crudas.** Toda la evidencia es de proveedores:
  - dbt: Sonnet 4.6 pasa de 90,0 % a 98,2 % y GPT-5.3-Codex de 84,1 % a 100 % al rutear por la capa semántica — [dbt Labs 2026](https://docs.getdbt.com/blog/semantic-layer-vs-text-to-sql-2026) [vendor].
  - Strategy, sobre 28 tablas de seguros: Text-to-SQL directo 88,2 % global pero **0 % en consultas multi-tabla complejas**; la capa gobernada, 100 % en ambas. Las fallas fueron **silenciosas** (ningún error SQL) y algunas inflaban resultados **5–10×** por fan-out de joins — [Strategy blog](https://software.strategy.com/blog/70-80-percent-accuracy-isnt-good-enough-for-enterprise-ai) [vendor; resumen, sitio bloqueado].
  - Dialpad, "Beyond Text-to-SQL": un agente sobre APIs analíticas gobernadas en lugar de SQL, evaluado en 90 casos construidos por expertos. Sostiene que las APIs "package business logic" y que dejar agregaciones al LLM es un riesgo — [alphaxiv 2605.21027](https://www.alphaxiv.org/abs/2605.21027); [blog de Dialpad](https://www.dialpad.com/blog/beyond-text-to-sql-why-enterprise-analytics-needs-governed-apis/) [vendor/académico; **sin cifras accesibles**].
- **Reglas en código contra reglas en el prompt.** ToolGuard (Zwerdling et al., 2025) genera guardas en código desde la política, que impiden acciones violatorias en τ-bench Airlines (resultados preliminares). La evidencia de τ-bench y HANDBOOK.md indica que la adherencia a políticas solo por prompt decae con el horizonte — [arXiv 2507.16459](https://arxiv.org/pdf/2507.16459v1) [resumen]. Aplica a A **y** a las herramientas certificadas de B, pero **no** al respaldo SQL de B.

**Niveles locales (RTX 3070 / RTX 5070): lo mejor que se puede decir de A**
- Con una sola llamada y unas 5 herramientas, Qwen3-8B Q4_K_M logra F1 0,919 y Qwen3-14B Q4_K_M 0,971 — [Docker](https://www.docker.com/blog/local-llm-tool-calling-a-practical-evaluation/) [medido, independiente] (vía informe previo).
- AgentFloor:
  - Qwen3-14B: 92 % con una herramienta, 84 % con cadena de 2.
  - Qwen3-8B: 76 % y 64 %.

  — [AgentFloor, arXiv 2605.00334](https://arxiv.org/html/2605.00334v1) [resumen].
- When2Call: preference optimization sobre un 8B baja la alucinación de herramientas de 19 % a **1,2 %** sin perder BFCL AST — [NVIDIA/When2Call](https://github.com/NVIDIA/When2Call) [medido]. Es la técnica "todas las optimizaciones" que en A reemplazaría a la abstención determinista.
- PA-Tool: alinear los nombres al preentrenamiento da hasta +17 puntos, justo en "no existe herramienta adecuada" — [arXiv 2510.07248](https://arxiv.org/html/2510.07248) [resumen].

### Inferences
**Steelman por nivel** [inferencia]:

| Nivel | Configuración de A más fuerte | Argumento a favor de A | Por qué el argumento es débil |
|---|---|---|---|
| **Claude API** (Opus 5.5 / Sonnet 5.5; Haiku 5.5 como opción rápida; Fable 5.1 solo por precisión, sin ZDR) | 5–6 herramientas de familia, con medida y agrupación cerradas y agregación en el servidor; resolución de entidades con estado `ok/ambiguo/no_encontrado/sin_permiso/vacio`; `interpretacion` y `total`/`truncado` en `structuredContent`; `strict: true`; `input_examples`; descripciones refinadas con transcripciones held-out; resolvedor closed-world; techo de 4–6 llamadas; respuesta numérica por plantilla desde `structuredContent`; instrucción y herramienta explícita de abstención | (1) Dentro de la cobertura, A y B usan las mismas herramientas: error equivalente. (2) Para preguntas que caben en 2–3 primitivas, A compone llamadas certificadas, mientras B cae a SQL libre (fallas silenciosas por joins y literales, según dbt y Strategy). (3) El LLM frontera detecta restricciones no soportadas que la capa 0 léxica ignora (§2). (4) Corrección por `isError` en la misma vuelta (CCTU 65 %) | Claude con Text-to-SQL ya tiene **0 respuestas falsas en 78 ítems** del evaluador (§5). En este dataset, A no puede ganar; solo podría hacerlo en una cola más difícil |
| **RTX 5070 12 GB** (Qwen3-14B Q4 monousuario o Qwen3.5-9B Q6/Q8) | Las mismas herramientas; una llamada, sin cadenas; LoRA con RPO estilo When2Call (~10 % de irrelevancia); gramática solo en el empaquetado; thinking apagado al decidir "llamar o abstenerse" | Si el respaldo SQL local se equivoca en ~21 % de lo que contesta, un A que **no tiene respaldo** y se abstiene fuera de cobertura puede tener menos respuestas falsas que B | Ese A es "B sin capa 0 y sin respaldo" (brazo A4 del informe). La ganancia pertenece a quitar el respaldo, no a A. Cualquier cadena (84 % → 16 % al ramificar) le hace perder |
| **RTX 3070 8 GB** (Qwen3.5-9B Q4 o Qwen3-8B Q4) | Igual que en la 5070, más estricto (≤5 herramientas, una llamada) | Igual que en la 5070 | Igual, con peor abstención (Reasoning Trap: 36,2 % de alucinación con distractores en Qwen3-8B sin thinking) y peor composición (Qwen3-8B: 64 % con 2 herramientas, 24 % al ramificar) |

- **Reformulación central.** Con todas las optimizaciones, A converge a la capa 1 de B: las mismas herramientas certificadas, la misma resolución en el servidor y la misma selección por el LLM. La diferencia neta entre arquitecturas se reduce a cuatro términos:
  1. la capa 0 léxica de B (que A no tiene);
  2. el respaldo SQL de B (que A no tiene);
  3. la composición multi-llamada de A (que B no hace en su capa 1);
  4. la gestión del multiturno (contexto del agente en A, slots o reescritor en B).

  El veredicto depende del signo de esos cuatro términos, no de "MCP contra híbrido". MCP como transporte no cambia la precisión (informe previo, brazo A2).

### Gaps
- No hay τ²-bench, BFCL, MCP-Atlas ni BIRD publicados para Opus 5.5, Sonnet 5.5 ni Haiku 5.5. El mejor dato agentic MCP es de Opus 5.
- CCTU, LiveMCPBench y Closed-World Resolution son preprints vistos solo vía resumen, y Closed-World está autoreportado.
- No encontré ninguna medición de que `outputSchema`/`structuredContent`, las anotaciones (`readOnlyHint`) o los resources de MCP mejoren la exactitud del modelo. Una guía señala que, según el cliente, el modelo puede ver solo el bloque de texto y no `structuredContent`; `readOnlyHint` es una señal de UX o seguridad, no de corrección — [sunpeak MCP Apps](https://sunpeak.ai/docs/mcp-apps/server/tool-results-model-context); [azukiazusa, tool annotations](https://azukiazusa.dev/en/blog/mcp-tool-annotations) [practicantes].
- Contra A: "Help or Hurdle?" reporta que integrar MCP bajó en promedio ~9,5 % la efectividad de los LLM por ruido o señales en conflicto — [arXiv 2508.12566](https://arxiv.org/pdf/2508.12566) [resumen]. Se refiere a MCP genérico de terceros, no a un servidor propio curado.

---

## 2. Cómo produce (B) respuestas incorrectas que (A) evitaría: modos de falla, frecuencia y severidad

### Takeaway
El ataque más sólido contra B no viene de la literatura sino del **código del repo**. El resolvedor de intenciones de la capa 0 captura una pregunta si contiene los términos de la intención y resuelve sus slots. **No verifica que el resto de la pregunta quede cubierto**, y el propio código declara que esa guarda "no sirve acá". Por eso cualquier restricción, negación, período o medida que la intención no modela se descarta en silencio, y la respuesta sale como **certificada**: el peor tipo de error para "precisión primero". A esto se suman:
- el respaldo SQL, con ~21 % de falsas sobre lo contestado con Qwen3-8B, sin señal de confianza que encuentre un subconjunto de bajo riesgo;
- la capa 0, que en multiturno corre **después** de un reescritor LLM, así que hereda sus errores con sello de certificado;
- las fronteras entre carriles;
- la inconsistencia entre paráfrasis que caen en carriles distintos.

### Cited Findings
**Evidencia del repo (leída en esta ronda)**
- `ResolutorDeIntenciones.ResolverAsync` acepta una intención si `i.Terminos.IsSubsetOf(terminos) && !i.Excluye.Overlaps(terminos)` y cada slot exigido resuelve a exactamente un valor. No hay control sobre los términos sobrantes. El comentario de la clase dice textualmente: "La guarda que hace viable el enrutador social —interceptar solo si no queda ningún token de contenido— **no sirve acá**: distinguir «¿cuál es el estado del pedido de Pérez?» de una pregunta arbitraria exige intención Y slots" — [backend/src/Modules.Asistente/Application/Determinista/ResolutorDeIntenciones.cs](backend/src/Modules.Asistente/Application/Determinista/ResolutorDeIntenciones.cs).
- Catálogo actual (5 intenciones) — [backend/src/Modules.Asistente/Recursos/intenciones.json](backend/src/Modules.Asistente/Recursos/intenciones.json):
  - `designaciones-de-un-cargo` = términos `["cuantos"]` + slot `Cargo`;
  - `pedidos-en-un-estado` = `["cuantos","pedido"]` + `Estado`;
  - `pedidos-de-una-novedad` = `["pedido"]`, excluye `cuantos`, + `Novedad`;
  - `plantel-de-una-materia` = `["plantel"]` + `Materia`;
  - `estado-del-pedido-de-una-persona` = `["estado","pedido"]` + `Persona`.
- El normalizador ya pliega «solicitud/solicitudes» → `pedido` y «nombramiento(s)» → `designacion` — [backend/src/Modules.Asistente/Application/Lexico/NormalizadorLexico.cs](backend/src/Modules.Asistente/Application/Lexico/NormalizadorLexico.cs).
- Los valores de `Estado` y `Novedad` son los literales del CHECK (`pedidos_estado_valido`, `pedidos_novedad_valida`), y los cargos usan su "forma preguntable" — [backend/src/Modules.Asistente/Infrastructure/CatalogoDelDominioReal.cs](backend/src/Modules.Asistente/Infrastructure/CatalogoDelDominioReal.cs). Esto explica en parte el 0/39 de capturas: «rechazadas» no coincide con `rechazado`.
- El enrutador "corre **después del reescritor** y antes del detector de ambigüedad" — [backend/src/Modules.Asistente/Application/Determinista/EnrutadorDeDominio.cs](backend/src/Modules.Asistente/Application/Determinista/EnrutadorDeDominio.cs). El reescritor es la "única llamada al modelo de la capa" conversacional — [README del módulo](backend/src/Modules.Asistente/README.md) (vía `arquitectura_actual.md` §1).
- El propio repo ya vio una falla léxica de este tipo: con el glosario, los sinónimos «cerrado/terminado» "arrastraron preguntas al estado del pedido", y capacidad bajó a 25/34 — [docs/architecture/modelo-local.md §6](docs/architecture/modelo-local.md) (vía `arquitectura_actual.md` §7).
- Ítem `dia-004` del dataset de diálogo:
  - t2: «¿Cuántas solicitudes de baja se presentaron?»
  - t3: «¿y cuántas de esas fueron rechazadas?», con referencia `novedad = 'Baja' AND estado = 'rechazado'`.

  — [backend/eval/datasets/dialogo.json](backend/eval/datasets/dialogo.json).
- **El respaldo SQL hoy.** Qwen3-8B Q4 (perfil B) acierta 26/34 con 7 falsas en capacidad. La línea de base de Claude (`claude-sonnet-5`) tiene 30/32 en capacidad, con 2 `abstencion_sobrelo_factible` (cap-004, cap-008) y **0 traducciones incorrectas**; en robustez 14/15 (1 sobre-abstención, rob-011), en diálogo 11/11 y en social 20/20 — [backend/eval/lineas-de-base/capacidad.json](backend/eval/lineas-de-base/capacidad.json), [robustez.json](backend/eval/lineas-de-base/robustez.json), [dialogo.json](backend/eval/lineas-de-base/dialogo.json), [social.json](backend/eval/lineas-de-base/social.json) [medido, repo].

**Evidencia externa sobre los mecanismos de falla de B**
- **Routers.** "Every downstream component conditions on the predicted intent". Ejemplo: un router con 0,92 de macro-accuracy en eval enviaba cancelaciones a facturación y mandaba una clase nueva de cola larga a facturación ~80 % de las veces. Un 0,92 agregado puede ocultar 0,97 en las 5 clases comunes y 0,31 en las 45 de la cola — [Future AGI, intent classification eval 2026](https://futureagi.com/blog/intent-classification-evaluation-pipeline-2026/) [vendor, anecdótico].
- **Abstención del respaldo.**
  - En BIRD, self-consistency, log-prob y las señales estructurales dan un AUROC de ~0,61–0,68 y "no valid low-risk subset".
  - El mejor ensemble de jueces contesta 27 % con 24 % de riesgo selectivo.

  — [arXiv 2607.06799](https://arxiv.org/html/2607.06799v1) [resumen] (vía `tasas_de_error_arquitecturas.md` §3).
- **Cascadas.** El fallback puede "rescatar" o "dañar" — [Signed Rescue Routing, arXiv 2609.07786](https://arxiv.org/pdf/2609.07786) [resumen]. Las políticas heurísticas de abstención en cascadas no cumplen el riesgo objetivo por 7,5–12,5 % — [UCCI, arXiv 2605.18796](https://arxiv.org/html/2605.18796) [resumen].
- **Text-to-SQL falla en silencio.** Devuelve "a plausible, fluent, wrong answer"; la capa curada, en cambio, devuelve un error explícito — [dbt Labs 2026](https://docs.getdbt.com/blog/semantic-layer-vs-text-to-sql-2026) [vendor]. Fan-out de joins de 5–10× sin error — [Strategy](https://software.strategy.com/blog/70-80-percent-accuracy-isnt-good-enough-for-enterprise-ai) [vendor, resumen].
- **Restricción descartada.** En el benchmark de CData, un filtro "this quarter" descartado devolvió todos los deals — [CData whitepaper](https://www.cdata.com/lp/ai-accuracy-whitepaper/) [vendor] (vía `diseno_superficie_herramientas.md` §1). Es el mismo patrón que la capa 0 sin cierre.
- **Negación en reglas.** En sistemas de reglas, una negación que las reglas no cubren "is simply not captured" — [YARBUS, arXiv 1507.06837](https://arxiv.org/pdf/1507.06837) [resumen; fuente antigua]. Los LLM también tienen debilidades con la negación, pero mejoran con la escala — [arXiv 2306.08189](https://arxiv.org/pdf/2306.08189) [resumen; antiguo].

### Inferences
**Catálogo de modos de falla de B** que A (optimizado, con Claude) evitaría o reduciría. Frecuencias y severidades son **[inferencia]**, salvo donde se cita una medición.

| # | Modo de falla de B | Ejemplo concreto en Ars Docendi | ¿Por qué A lo evitaría? | Frecuencia estimada | Severidad |
|---|---|---|---|---|---|
| B1 | **Sobrecaptura de la capa 0 por restricción ignorada** (sin cierre de tokens) | «¿Cuántos ayudantes de primera hay **en Bases de Datos**?» → `designaciones-de-un-cargo` (cuantos + cargo); la materia no es slot de esa intención y se ignora → conteo global certificado | El LLM ve toda la pregunta y elige `consultar_designaciones(cargo, materia, medida=conteo)`; si la restricción no es expresable, el `interpretacion` en eco lo delata | Baja en el dataset actual (5 intenciones, 0/39 capturas). **Crece con cada intención y sinónimo que se agrega para subir cobertura**, que es la recomendación de la ronda 1. Orden de magnitud en tráfico real: 1–5 % de las capturas [inferencia] | **Máxima**: respuesta falsa con rótulo de certificada; el usuario no tiene motivo para dudar |
| B2 | **Medida equivocada en la capa 0** | «¿Cuántas **materias** tienen titular?» → `designaciones-de-un-cargo` (cuantos + «titular») cuenta designaciones de titulares, no materias distintas | El LLM elige `medida=conteo, agrupar_por=materia` o pide aclarar | Ídem B1 | Máxima |
| B3 | **Negación o exclusión ignorada** | «¿Qué solicitudes de Alta **no** fueron aceptadas?» → `pedidos-de-una-novedad` (novedad=Alta) lista todas | Un LLM frontera modela la negación o se abstiene si la herramienta no tiene `estado_excluido` | Baja, pero no nula en lenguaje real | Máxima |
| B4 | **Período o vigencia ignorados** | «¿Cómo era el plantel de Bases de Datos **en 2024**?» → `plantel-de-una-materia` devuelve el plantel vigente | El LLM detecta que la herramienta no tiene parámetro de fecha y se abstiene o lo dice | Media en tráfico real de gestión (las preguntas por ciclo son comunes) [inferencia] | Máxima |
| B5 | **Error del reescritor LLM certificado aguas abajo** | dia-004 t3: si el reescritor produce «¿Cuántas solicitudes fueron rechazadas?» (pierde «de baja»), y el catálogo se amplía para que «rechazadas» resuelva a `rechazado` (necesario para subir cobertura), `pedidos-en-un-estado` responde el total de rechazadas | En A, el modelo conserva el contexto completo y emite la diferencia de parámetros (`novedad=Baja` sigue puesto); su error, si lo hay, queda visible en los argumentos | Media en multiturno (los seguimientos elípticos son el caso de uso del reescritor) [inferencia] | Alta: el turno sale como certificado aunque dependió de un LLM |
| B6 | **Respaldo SQL que responde mal la cola** | Lo que no cubren la capa 0 ni la capa 1 cae a SQL libre sobre 21 tablas base con comentarios que mienten (TD-025: «Categoría 0..6» contra un catálogo 1..6) | A compone primitivas certificadas cuando la pregunta cabe en 2–3 llamadas, o se abstiene | Qwen3-8B: ~21 % de falsas sobre lo contestado [medido, repo]. Con un umbral de abstención fuerte, ~10–20 % sobre lo contestado [inferencia, desde 2607.06799]. Claude Sonnet 5: 0/22 traducciones incorrectas en capacidad [medido, repo], aunque la cola real es más difícil (AIM BIRD: Sonnet 5.5 73,8 %, Opus 5.5 87,8 % [resumen]) | Media-alta, mitigada si la respuesta se rotula como "no certificada", aunque el usuario igual puede creerla |
| B7 | **Falso negativo del router hacia SQL** | Una pregunta cubierta por herramienta («nombramientos abiertos de JTP en Física») no matchea términos o slots y la capa 1 no la elige → SQL libre | En A no existe un camino peor al cual caer | Probablemente el modo más frecuente en local (la capa 0 captura poco y la capa 1 la decide un 8B) [inferencia] | Media (degrada a la precisión del respaldo) |
| B8 | **Frontera: el respaldo contesta lo que la herramienta rechazó** | La herramienta devuelve `no_encontrado` («Ingeniería Informática» contra «Ingeniería en Informática») y el respaldo genera SQL con el literal mal copiado → cero filas → "no hay" | A no tiene respaldo: el `no_encontrado` con sugerencias llega al usuario | Depende de que las reglas (a)–(c) de `diseno_superficie_herramientas.md` §5 se implementen; si se implementan, ~0 | Máxima si ocurre (falso "no hay") |
| B9 | **Inconsistencia entre paráfrasis** (dependencia del camino) | «solicitudes urgentes» va a herramienta (`prioritario`) y «pedidos con prioridad» cae a SQL, que interpreta otra columna o estado | En A, una sola política de selección sobre las mismas herramientas da respuestas más estables | Desconocida; medible con robustez (rob-*) y paráfrasis | Media: al menos una de las dos respuestas está mal |
| B10 | **Dos implementaciones de la regla de negocio** | Las herramientas de B (y el SQL) reimplementan en SQL reglas que viven en servicios: vigencia, estados agrupados `en_revision_*`, ámbito por roles fijos `JefeCatedra/CoordinadorCarrera` en la API contra permisos en vivo en RLS | A reusa la implementación única de los servicios (la "verdad" que también ve la UI) | Rara hoy; aumenta cuando Secretaría crea roles en runtime (`diseno_superficie_herramientas.md` §4) | Media-alta: contradice la UI que el usuario usa para verificar |
| B11 | **"Sin permiso" indistinguible de "no hay" en el respaldo** | El respaldo con actor acotado ve cero filas por RLS y debe abstenerse | A recibe un 403 explícito | B ya se abstiene ahí (precisión intacta, cobertura menor) | Baja en precisión |

- **Patrón común de B1–B5.** La capa 0 es "determinista" en el **cálculo**, no en la **interpretación**. Convierte una comprensión parcial de la pregunta en una respuesta con la máxima confianza del sistema. Un LLM frontera en A comprende peor algunas cosas (adivina parámetros), pero el error queda visible en los argumentos y en la `interpretacion` en eco. Además, ese error tiene un piso de recall fuera de alcance (~86 % en CLINC150) que una tabla léxica sin cierre no tiene (0 % para restricciones no modeladas, por construcción).
- **B tiene una cura**: exigir que todos los tokens de contenido queden consumidos por términos o slots, o que una llamada barata de "verificación de cobertura" confirme que la intención captura toda la pregunta. Pero el repo dice que esa guarda "no sirve" para intención más slots, y aplicarla empujaría la cobertura de la capa 0 hacia cero. La capa 0 dejaría de aportar precisión y quedaría solo como optimización de latencia.

### Gaps
- No ejecuté el resolvedor sobre preguntas adversariales: B1–B5 son casos construidos leyendo el código, no fallas observadas. El primer paso del experimento (§4) debería ser una batería de ~50 preguntas con restricciones extra, negaciones, períodos y medidas distintas sobre los términos del catálogo.
- No hay medición de cuántas preguntas reales contienen restricciones no modeladas (no hay corpus real; ARS-65 sigue abierta).
- La regla de negocio "verdadera" (API contra RLS) en B10 es una decisión institucional, no técnica.
- Contra el ataque a B: un estudio sobre un asistente de voz desplegado (3.030 fallbacks anotados) concluye que los **clasificadores livianos por embeddings superan a los LLM en enrutamiento de intenciones**, con menor latencia, y recomienda LLM solo para la aclaración — [Not All Fallbacks Are Failures, arXiv 2608.30738](https://arxiv.org/pdf/2608.30738); [HF papers](https://huggingface.co/papers/2608.30738) [resumen; un solo sistema, monoturno, salud]. Esto respalda el enrutamiento no-LLM de B en general, aunque no un matching léxico sin cierre.

---

## 3. Condiciones bajo las cuales (A) ganaría en precisión

### Takeaway
A gana solo si se cumplen **a la vez**:
1. un modelo frontera (Opus 5.5 o Sonnet 5.5);
2. herramientas de familia con agregación en el servidor y estados explícitos (`ambiguo`, `no_encontrado`, `sin_permiso`, `vacio`);
3. abstención estricta, en la que el modelo solo responde desde `structuredContent` y declara la interpretación;
4. una cola de preguntas que se resuelve componiendo 2–3 primitivas certificadas;
5. un B comparado con capa 0 sin cierre y con respaldo SQL activo.

Si B se implementa con cierre de tokens y sin respaldo, o con respaldo muy conservador, la ventaja de A se reduce a la composición multi-llamada, que en local es un pasivo.

### Cited Findings
- **Composición con modelos chicos.** Qwen3-14B: 92 % (1 herramienta), 84 % (2), **16 %** al ramificar sobre un resultado intermedio. Qwen3-8B: 76 / 64 / 24 % — [AgentFloor](https://arxiv.org/html/2605.00334v1) [resumen].
- **Composición con frontera.** Opus 5: 85,8 % en tareas MCP de 3–6 llamadas — [Scale MCP Atlas](https://labs.scale.com/leaderboard/mcp_atlas) [agregador]. Sonnet 5.5 en Toolathlon: 85,2 % pass@3 contra **68,5 % pass^3** — [BenchLM](https://benchlm.ai/compare/claude-haiku-5-5-vs-claude-sonnet-5-5) [agregador, resumen] (vía `modelos_por_nivel.md`).
- **Composición frente a SQL en APIs genéricas.** Live API-Bench: 7–47 % (≈50 % con ReAct) en preguntas BIRD expresadas como APIs; los modelos "a veces explotan mejor el SQL que las APIs" — [ACL 2026.eacl-long.143](https://aclanthology.org/2026.eacl-long.143/) [resumen]. Usa APIs tipo SLOT o SEL, no herramientas de familia, y modelos 2024–2025: es la evidencia más fuerte **contra** la condición 4.
- **Programmatic Tool Calling** (Claude): +2,9 y +4,7 puntos en tareas de recuperación, con −37 % de tokens, recomendado para ≥3 llamadas dependientes — [Anthropic, Advanced tool use](https://www.anthropic.com/engineering/advanced-tool-use) [vendor]. No es elegible para ZDR — [API and data retention](https://platform.claude.com/docs/en/manage-claude/api-and-data-retention) (vía `modelos_por_nivel.md` §1).
- **Calibración de Claude.** En los tests de Anthropic, Opus 5 "often would state an answer as certain, when it was unsure" — [Zvi, Opus 5 system card](https://thezvi.substack.com/i/208361260/alignment-6) [secundario]. Según una sola fuente secundaria, su tasa de alucinación en AA-Omniscience (closed-book) habría subido ~14 puntos, a ~50 %, con +7 de exactitud — [eesel, Opus 5 review](https://www.eesel.ai/blog/claude-opus-5-review) [secundario, no verificado]. Es un argumento para que la abstención de A dependa del **estado del servidor** y no del juicio del modelo.
- **Autocorrección propia.** Los modelos detectan errores originados por el usuario, pero fallan mucho más al reparar los propios — [ReflecTool-Bench, ACL 2026 Findings](https://preview.aclanthology.org/ingest-acl/2026.findings-acl.86/) [resumen]. En 7 modelos, la autocorrección intrínseca ayudó a pocos y perjudicó al resto — [Self-Correction as Feedback Control](https://www.opentrain.ai/papers/self-correction-as-feedback-control-error-dynamics-stability-thresholds-and-prom--arxiv-2604.22273/) [resumen]. La corrección vía `isError` (feedback externo, CCTU) sí funciona mejor con Claude.

### Inferences
**Condición de equilibrio** [inferencia; modelo de error explícito]. Se definen, como fracciones del tráfico:
- c_in: preguntas que resuelve una llamada certificada;
- c_comp: preguntas que resuelven 2–3 primitivas certificadas;
- c_out: preguntas que solo resuelve el SQL sobre tablas;
- c_inf: preguntas infactibles.

Errores:
- **A:** E_A ≈ c_in·e_sel + c_comp·e_comp + c_out·f_A + c_inf·g_A, donde f_A es la tasa a la que A "fuerza" una herramienta parecida en lugar de abstenerse.
- **B:** E_B ≈ c_in·(q0·FP0 + (1−q0)·e_sel + FN·r_fb) + c_comp·r_fb + c_out·r_fb + c_inf·g_B, donde q0 es la fracción capturada por la capa 0, FP0 su tasa de sobrecaptura, FN la tasa de falsos negativos hacia el respaldo y r_fb el riesgo selectivo del respaldo tras la abstención.

A gana si **c_comp·(r_fb − e_comp) + c_out·(r_fb·cob_fb − f_A) + c_in·(q0·FP0 + FN·r_fb) > 0**, donde cob_fb es la fracción que el respaldo contesta en lugar de abstenerse.

**Lectura por nivel con valores plausibles** (ilustrativos, no medidos):

- **Claude (Opus 5.5).**
  - Valores: e_sel ≈ 1–3 %; e_comp ≈ 5–10 % (MCP-Atlas 86 % de éxito, no todo fallo es respuesta falsa); f_A ≈ 5–15 %; r_fb ≈ 5–15 % sobre lo contestado (repo: 0/22; AIM BIRD: 12 % de error de Opus 5.5); FP0 ≈ 1–5 %.
  - Con c_comp = 15 % y c_out = 10 %, el término de composición favorece a A por ~0–1,5 puntos y el de cola larga es ambiguo (signo según f_A contra r_fb·cob_fb).
  - Resultado: **empate técnico, con ventaja posible de A de ≤1–2 puntos**, sobre todo si B tiene la capa 0 sin cierre.
- **RTX 5070 / RTX 3070.**
  - Valores: e_comp ≈ 16–36 % para 2 llamadas (AgentFloor); f_A ≈ 30–50 % (Reasoning Trap: 36–57 % con distractores); r_fb ≈ 21 %.
  - El término de composición favorece claramente a B (o a abstenerse).
  - A gana solo en la variante "sin composición y sin respaldo", que no es A sino B-sin-respaldo (brazo A4).

**Lista de condiciones necesarias para que A gane** [inferencia]:
1. Modelo Opus 5.5 o Sonnet 5.5 con esfuerzo alto. Haiku 5.5 no tiene cifras de tool use publicadas; Fable 5.1 falló en la prueba de cifras inventadas de Anthropic.
2. Herramientas compuestas de familia (medida, agrupación y filtros cerrados), agregación y conteo en el servidor, `total`/`truncado` explícitos.
3. Abstención estructural del lado del servidor: los estados `ambiguo`, `no_encontrado`, `sin_permiso` y `vacio` con entidades resueltas. Ninguna respuesta numérica sale de texto libre: plantilla desde `structuredContent` más un verificador que exige que cada número citado aparezca en el resultado.
4. Una política explícita de "responder solo si la interpretación en eco cubre todas las restricciones de la pregunta", más un paso de autoverificación con el resultado de la herramienta (evidencia mixta en modelos chicos, mejor en frontera).
5. Resolvedor closed-world y `strict: true` para eliminar herramientas y argumentos inventados.
6. Composición limitada a ≤3 llamadas con dependencia simple (filtrar o intersecar por IDs), nunca ramificación.
7. B evaluado tal como se implementaría para cobertura: capa 0 ampliada con sinónimos y flexión, y respaldo SQL activo. Si B adopta cierre de tokens y elimina el respaldo, desaparecen los términos B1–B6 y A pierde su ventaja.

### Gaps
- Ninguno de los parámetros de la ecuación está medido en Ars Docendi. Las fracciones c_in, c_comp y c_out dependen de un corpus real que no existe.
- No hay mediciones de e_comp con herramientas de familia (solo con APIs genéricas, como Live API-Bench, o con MCP heterogéneos, como MCP-Atlas).

---

## 4. Requisitos de un experimento justo, para que (A) no quede en desventaja

### Takeaway
Hoy el arnés y el dataset favorecen estructuralmente a B: el techo de 4 llamadas, la prohibición de guardar filas en el hilo, un dataset escrito para Text-to-SQL y sin preguntas compuestas, y el saturamiento de Claude (0 falsas en 78 ítems). Una comparación justa necesita:
- herramientas idénticas en ambos brazos;
- un presupuesto de llamadas propio de agente;
- un set ciego con particiones (una herramienta, composición, fuera de catálogo, infactible) y casos adversariales para la capa 0;
- repeticiones (pass^k);
- puntuar como métrica primaria las "respuestas falsas", distinguiendo certificadas de no certificadas.

### Cited Findings
- El arnés del repo hoy:
  - techo de 4 llamadas por turno (`MaximoDeLlamadasPorTurno` = 4);
  - el hilo guarda preguntas y consulta, "nunca filas";
  - el runner de capacidad llama a `GeneradorDeSql` directamente;
  - para comparar arquitecturas hace falta un punto de extensión de estrategia de turno.

  — [README del módulo](backend/src/Modules.Asistente/README.md); [reports/Alternativas a Text to SQL local.md](../../reports/Alternativas%20a%20Text%20to%20SQL%20local.md) (sección "Un banco de pruebas").
- Los datasets se escribieron para medir traducción a SQL ("el corpus se escribió para medir traducción a SQL") y hay un invariante de disjunción entre el dataset y los ejemplos — [backend/eval/datasets/capacidad.json](backend/eval/datasets/capacidad.json) (vía `arquitectura_actual.md` §4, `diseno_superficie_herramientas.md` §6).
- Claude con Text-to-SQL ya da 0 respuestas falsas en capacidad, robustez, diálogo y social (3 sobre-abstenciones) — [backend/eval/lineas-de-base/](backend/eval/lineas-de-base/capacidad.json) [medido]. Es un dataset saturado para el nivel Claude.
- Varianza:
  - LiveMCPBench: corridas repetidas del mismo setup variaron entre 57,9 % y 76,8 % — [arXiv 2508.01780](https://arxiv.org/abs/2508.01780) [resumen].
  - Toolathlon (Sonnet 5.5): pass@3 85,2 % contra pass^3 68,5 % [agregador].
  - BFCL: 48 % de tareas defectuosas según Epoch — [Epoch AI](https://epoch.ai/benchmarks/berkeley-function-calling-leaderboard/review) [resumen] (vía `tasas_de_error_arquitecturas.md` §1).
- Puntuar contra el estado real y no contra el autoinforme del agente — [mcp-vs-cli-bench, agentpatterns.ai](https://agentpatterns.ai/tool-engineering/mcp-cli-cost-ratio-scaffolding-bound/) [practicantes]. Anthropic recomienda evaluar con test sets held-out para no sobreajustar las herramientas — [Writing tools for agents](https://www.anthropic.com/engineering/writing-tools-for-agents).
- Los parsers de tool calls de llama.cpp y vLLM tienen bugs documentados, y en Opus 5.5 / Sonnet 5.5 el `tool_choice` forzado devuelve 400 — (vía `modelos_por_nivel.md` §1; `Alternativas a Text to SQL local/modelos_locales_tool_calling.md`).

### Inferences
**Requisitos** [inferencia]:
1. **Paridad de herramientas.** Las 5–6 herramientas de familia de `diseno_superficie_herramientas.md` §6.2 son la capa 1 de B y la superficie entera de A: misma implementación, mismo ámbito (alinear RLS con las reglas de la API o fijar actores donde coinciden), misma máscara de PII.
2. **Presupuesto propio de agente.** Para A, un techo de 6 llamadas en Claude y 3 en local, con los timeouts ajustados. Con el techo actual de 4, A queda castigado en composición.
3. **Contexto multiturno.** A puede conservar en el hilo los resultados estructurados enmascarados, o al menos la llamada canónica más los IDs devueltos. Prohibirle filas es castigar justo su ventaja en dia-005 («esa materia»). Si por política de PII no se admite, dejarlo documentado como restricción del dominio, no como resultado del experimento.
4. **Optimizaciones de A activadas.**
   - Claude: `strict: true`, `input_examples`, descripciones refinadas con transcripciones de un set de desarrollo separado, resolvedor closed-world y esfuerzo alto.
   - Local: chat template y parser de herramientas correctos, gramática solo en el empaquetado, `tool_choice=auto`, thinking según la tarea (apagado para decidir, encendido para cadenas) y, si B recibe fine-tuning, LoRA con RPO estilo When2Call también para A.
   - En ambos: enrutador social delante (si no, A pierde los 11 ítems sociales por definición).
5. **Igualdad en la redacción.** O plantilla desde datos estructurados en los dos brazos, o LLM en los dos, con el mismo verificador de números.
6. **Set ciego y particionado.** 150–250 preguntas escritas por usuarios del Departamento, sin ver las herramientas, etiquetadas como `una_llamada`, `composicion_2_3`, `solo_sql`, `infactible`, `ambigua` y `sin_permiso`. Más una **batería adversarial de capa 0**: preguntas con términos y slots de las intenciones más restricciones no modeladas, negaciones, períodos, medidas distintas y elipsis tras reescritura (casos B1–B5). Sin ella el experimento no puede ver los modos de falla que favorecen a A.
7. **Brazos.**
   - A-Claude, B-Claude completo, B-Claude sin respaldo y B-Claude sin capa 0.
   - Lo mismo con Qwen3.5-9B en la 3070 y con Qwen3-14B o Qwen3.5-9B en la 5070.
   - Los brazos "sin respaldo" y "sin capa 0" atribuyen la diferencia a la pieza que la causa.
8. **Métricas.**
   - Respuestas falsas totales, y separadas en "certificadas" (capa 0 o 1) y "no certificadas" (respaldo).
   - Riesgo selectivo; sobre-abstenciones; puntaje con penalización 2,0.
   - Pass^k con k ≥ 4 en Claude (el repo mostró determinismo local, pero conviene verificarlo con herramientas).
   - Comparaciones pareadas por ítem (McNemar).
9. **Oráculo.** Comparar las filas de referencia ejecutadas con el mismo actor. Para preguntas compuestas, escribir referencias SQL revisadas, no derivadas de ninguno de los dos brazos.

### Gaps
- No hay corpus real para estimar c_in, c_comp, c_out y c_inf. Sin él, cualquier resultado agregado depende de cómo se arme el set.
- No está decidida la política de PII sobre filas en el hilo, que condiciona el punto 3.

---

## 5. Evaluación honesta tras el steelman: ¿gana A en algún nivel?

### Takeaway
**En los niveles locales (3070 y 5070), no.** La única variante de A que podría bajar las respuestas falsas frente a B es la que renuncia a componer y a responder fuera de cobertura. Esa variante es B sin capa 0 y sin respaldo, y su mérito es de diseño de herramientas y abstención, no del agente ni de MCP.

**En el nivel de Claude (Opus 5.5 / Sonnet 5.5), quizás, por un margen chico**, de ≤1–2 puntos de respuestas falsas [inferencia], y solo si:
- B se implementa con una capa 0 léxica sin cierre de tokens y un respaldo SQL activo;
- el tráfico real tiene una fracción apreciable de preguntas que caben en 2–3 primitivas certificadas;
- A aplica abstención estructural del servidor y redacción desde `structuredContent`.

Con el dataset actual, ganar es imposible: Claude con Text-to-SQL ya tiene 0 falsas en 78 ítems. El hallazgo más útil del steelman no es "A gana". Es que **la capa 0 de B, tal como está escrita, puede emitir respuestas falsas con sello de certificadas**, y que eso se corrige dentro de B: con cierre de tokens, o con una confirmación de cobertura hecha por el LLM o por la `interpretacion` en eco.

### Cited Findings
- Claude Sonnet 5 con Text-to-SQL: capacidad 30/32 (0 incorrectas, 2 sobre-abstenciones), robustez 14/15, diálogo 11/11, social 20/20 — [backend/eval/lineas-de-base/](backend/eval/lineas-de-base/capacidad.json) [medido, repo].
- Qwen3-8B Q4 en la 3070: 26/34 con 7 falsas en capacidad — [docs/architecture/modelo-local.md §3](docs/architecture/modelo-local.md) [medido, repo] (vía `arquitectura_actual.md` §5).
- Composición local: 84 % → 16 % al ramificar (Qwen3-14B) — [AgentFloor](https://arxiv.org/html/2605.00334v1) [resumen]. Abstención local: Qwen3-8B, 36,2 % de alucinación con distractores (56,8 % con thinking) — [Reasoning Trap](https://arxiv.org/pdf/2510.22977) [resumen].
- Frontera: MCP-Atlas Opus 5 85,8 %; CCTU Opus 4.6 65 % de corrección — [Scale](https://labs.scale.com/leaderboard/mcp_atlas); [arXiv 2603.15309](https://arxiv.org/pdf/2603.15309) [agregador/resumen].
- Contra el enrutamiento por LLM en general: los clasificadores por embeddings superan a los LLM en enrutamiento en un asistente desplegado — [arXiv 2608.30738](https://arxiv.org/pdf/2608.30738) [resumen]. A favor del LLM: mayor recall fuera de alcance (85,6 contra 58,1) — [arXiv 2608.20371](https://arxiv.org/html/2608.20371) [resumen]. Hallazgos en tensión: la pregunta es *qué tipo de error* importa (falsos positivos certificados contra falta de cobertura).
- El repo ya descartó clasificar con el LLM (60 % de F1 en triage de 5 clases; 77,4 % en 9 vías), aunque con modelos y prompts anteriores — [docs/product/designs/asistente-conversacional-definicion.md §6](docs/product/designs/asistente-conversacional-definicion.md) (vía `arquitectura_actual.md` §4).

### Inferences
**Veredicto por nivel** [inferencia]:

| Nivel | ¿Gana A en precisión? | Por qué | Qué tomar de A para B |
|---|---|---|---|
| RTX 3070 8 GB | **No** | Composición y abstención del 8B débiles; el único A competitivo es B-sin-respaldo | Herramientas de familia con estados explícitos; ningún respaldo SQL local sin abstención fuerte; cierre de tokens en la capa 0 |
| RTX 5070 12 GB | **No** (empate en el mejor caso con una llamada) | Igual; Qwen3-14B mejora la llamada única (0,971 F1), no la cadena | Ídem; evaluar si el respaldo local debe apagarse y la cola derivarse a abstención |
| Claude API | **Posible, por un margen chico y condicional** | Composición de primitivas certificadas mejor que SQL libre en la cola; el LLM detecta restricciones no modeladas; `isError` con corrección. Pero el respaldo de Claude ya tiene 0 falsas en el dataset | Que la capa 0 no responda sin confirmar cobertura (eco de `interpretacion`); permitir 2–3 llamadas certificadas compuestas en la capa 1 antes de caer al respaldo (un "A acotado" dentro de B) |

- **Síntesis.** Con todas las optimizaciones, la mejor arquitectura para precisión probablemente sea un **B modificado que absorbe lo mejor de A**:
  1. capa 0 con cierre de tokens, o eliminada;
  2. capa 1 con herramientas de familia y, solo en el nivel Claude, composición de hasta 2–3 llamadas certificadas;
  3. abstención estructural del servidor;
  4. redacción desde datos estructurados;
  5. respaldo SQL solo con Claude Opus 5.5, rotulado y con abstención por clase, o directamente "no puedo responder con datos certificados".

  Ese diseño es más preciso que A puro, porque no depende del juicio del modelo para abstenerse, y que B tal como está, porque no certifica comprensiones parciales.
- **Lo que el steelman no logró sostener**:
  - que MCP como protocolo aporte precisión (`outputSchema`, anotaciones, resources y elicitation no tienen mediciones de exactitud);
  - que un agente libre de componer muchas llamadas sea más preciso que SQL en general (Live API-Bench dice lo contrario con APIs genéricas);
  - que un modelo local pueda abstenerse por juicio propio a un nivel cercano a un estado determinista.

### Gaps
- Todas las estimaciones de margen son inferencias. La pregunta solo se cierra con el experimento de §4, sobre todo con la batería adversarial de capa 0 y la partición de composición.
- No hay cifras de tool use de Opus 5.5, Sonnet 5.5 ni Haiku 5.5 en benchmarks públicos (τ², MCP-Atlas, BFCL); el veredicto de Claude se apoya en Opus 5 y Opus 4.x.
- Las cifras de calibración y alucinación de Opus 5 (≈50 % en AA-Omniscience) son de una única fuente secundaria no verificada.
