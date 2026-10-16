# Casos de industria y productos: chatbots en lenguaje natural sobre datos operativos (Text-to-SQL, tools/MCP, capa semántica, híbridos), 2024–2026

> Nota metodológica: varios blogs primarios (uber.com, linkedin.com, vercel.com, zenml.io, engineering.grab.com, docs.databricks.com, googleapis.github.io) estuvieron bloqueados por el proxy de egreso en esta sesión. Para esos casos los datos provienen de resúmenes de búsqueda que citan el post original o de agregadores secundarios (ZenML LLMOps Database, Wren AI, InfoQ, Analytics Vidhya). Se marca explícitamente cuando un número es secundario o es una afirmación de marketing del propio vendor. Fecha de corte de la investigación: octubre 2026.

## 1. Casos de ingeniería: qué construyeron, qué precisión lograron, qué falló y por qué cambiaron de enfoque

### Takeaway
Ninguna empresa grande reporta Text-to-SQL "libre" sobre el esquema completo como solución suficiente: todas convergieron en (a) acotar el dominio (workspaces/charters/intents), (b) recuperar metadatos enriquecidos + consultas de ejemplo, y (c) poner al humano en el loop o exponer consultas curadas como APIs/tools. Las precisiones medidas en producción son modestas (LinkedIn 53% "correcto o casi" por revisión experta; Pinterest 20%→40% aceptación al primer intento) salvo cuando el dominio está muy acotado y curado (Swiggy 54%→93%, Vercel 80%→100% con capa semántica bien documentada).

### Cited Findings

**Uber – QueryGPT (post del blog de ingeniería, 2024)**
- Arquitectura evolucionó desde una versión de Hackdayz (RAG simple sobre ejemplos SQL y esquemas) a un pipeline con agentes: *Intent agent* mapea la pregunta a "workspaces" de negocio (sistema o custom), *Table agent* propone tablas que el usuario puede aceptar o editar, y *Column prune agent* poda columnas irrelevantes — [Uber Engineering, QueryGPT](https://www.uber.com/en-us/blog/query-gpt/) (contenido obtenido vía resumen de búsqueda; acceso directo bloqueado).
- Qué falló: la precisión cayó a medida que se onboardeaban más tablas y SQL de ejemplo; la búsqueda por similitud sobre el prompt no devolvía esquemas relevantes, de ahí el paso intermedio de clasificación de intención — [Uber Engineering](https://uber.com/blog/query-gpt).
- El Table agent surgió porque usuarios reportaron que las tablas elegidas eran incorrectas y pidieron elegirlas ellos (human-in-the-loop como salvaguarda principal) — [Uber Engineering](https://www.uber.com/en-AT/blog/query-gpt).
- Column prune agent: aun con GPT-4 Turbo (128K tokens) algunas requests fallaban por tablas con muchísimas columnas — [Uber Engineering](https://uber.com/blog/query-gpt).
- Evaluación: golden set de preguntas reales logueadas con intent, tablas y SQL de referencia verificados; se mide si el intent es correcto, solapamiento de tablas y si la query ejecuta — [slides QueryGPT en Scribd (secundario)](https://www.scribd.com/document/797618430/QueryGPT).
- Resultados reportados (productividad, no exactitud SQL): ~1,2 M queries interactivas/mes, Operaciones 36%; autoría de query de ~10 min a ~3 min; release limitada a 300 DAU, 78% reporta ahorro de tiempo significativo — [ZenML (secundario)](https://www.zenml.io/llmops-database/natural-language-to-sql-query-generation-at-scale); [Wren AI (secundario, vendor)](https://getwren.ai/post/how-uber-is-saving-140-000-hours-each-month-using-text-to-sql-and-how-you-can-harness-the-same-power).
- No se encontraron métricas publicadas de precision/recall de los agentes de intent/tablas — [resumen de búsqueda sobre Uber](https://www.zenml.io/llmops-database/natural-language-to-sql-query-generation-at-scale).

**LinkedIn – SQL Bot (blog dic. 2024; paper arXiv jul. 2025; Trino Summit dic. 2024)**
- Construye un knowledge graph indexando metadatos de la base, logs históricos de queries, wikis y código; un agente Text-to-SQL recupera y rankea contexto del grafo, escribe la query y autocorrige alucinaciones y errores de sintaxis — [arXiv 2507.14372](https://arxiv.org/abs/2507.14372v1).
- Precisión medida: revisión experta encontró 53% de respuestas "correctas o casi correctas" en un benchmark interno; >300 usuarios semanales — [arXiv 2507.14372](https://arxiv.org/abs/2507.14372v1).
- Ablaciones (Trino Summit 2024): agregar queries de ejemplo +~16% recall de tablas; re-rankers +~9%; agente de autocorrección +~2% recall y −~14% alucinaciones — [LinkedIn @ Trino Summit 2024 (PDF)](https://trino.io/assets/blog/trino-summit-2024/trino-summit-2024-linkedin-ai.pdf).
- Contradicción: circula la cifra "~95% de usuarios califica la precisión como 'Passes' o superior" atribuida al post de LinkedIn de 2024; no pudo verificarse en esta sesión (blog bloqueado) y no coincide con el 53% del paper (métrica distinta: satisfacción de usuario vs. revisión experta) — [LinkedIn Engineering blog (no accesible)](https://www.linkedin.com/blog/engineering/ai/practical-text-to-sql-for-data-analytics); [arXiv 2507.14372](https://arxiv.org/abs/2507.14372v1).

**Pinterest – Text-to-SQL en Querybook (2024)**
- Dos iteraciones: primero generador SQL con LLM integrado en Querybook (el usuario elige tablas), luego selección de tablas vía RAG (embeddings de resúmenes de tablas y queries históricas) — [ZenML (secundario)](https://www.zenml.io/llmops-database/text-to-sql-system-with-rag-enhanced-table-selection).
- Aceptación al primer intento pasó de 20% a >40% a medida que maduró; 35% de mejora en velocidad de completar tareas de escritura SQL — [ZenML (secundario)](https://www.zenml.io/llmops-database/text-to-sql-system-with-rag-enhanced-table-selection); Wren AI advierte que el dato no controla por diferencias entre tareas — [Wren AI](https://getwren.ai/post/deep-dive-into-how-pinterest-built-its-text-to-sql-solution).
- Evolución posterior ("Analytics Agent", >100k tablas, >2.500 usuarios analíticos) mide por separado precisión de descubrimiento de tablas y de generación SQL — [devday.kr (secundario, coreano)](https://devday.kr/article/unified-context-intent-embeddings-for-scalable-text-to-sql).

**Swiggy – Hermes (India; V1 2024 → V3 fines 2025/ene. 2026)**
- V1 en Slack con GPT-3.5; V2 RAG con GPT-4o y "charters" por unidad de negocio con su propio knowledge base (métricas, tablas, columnas, SQL de referencia) — [Analytics Vidhya, ago. 2024](https://www.analyticsvidhya.com/blog/2024/08/swiggy-hermes/); [ZenML](https://www.zenml.io/llmops-database/text-to-sql-solution-for-data-democratization-in-food-delivery-operations).
- Lección: el volumen de datos y diferencias entre negocios (Food Marketplace vs. Instamart) hicieron inviable una solución única — [ZenML](https://www.zenml.io/llmops-database/text-to-sql-solution-for-data-democratization-in-food-delivery-operations).
- V3: recuperación vectorial, memoria conversacional, orquestación agéntica y explicabilidad; precisión de query 54% → 93% (cifra de resumen secundario; metodología no verificada) — [InfoQ, ene. 2026](https://www.infoq.com/news/2026/01/swiggy-hermes-conversational-ai); [ZenML](https://www.zenml.io/llmops-database/evolution-of-hermes-v3-building-a-conversational-ai-data-analyst).
- Uso: cientos de usuarios, varios miles de queries, turnaround promedio < 2 min — [ZenML](https://www.zenml.io/llmops-database/text-to-sql-solution-for-data-democratization-in-food-delivery-operations).

**Delivery Hero / Woowa Brothers – QueryAnswerBird (2024)**
- Encuesta interna: más de la mitad del personal tenía barreras para escribir SQL (tiempo, lógica de negocio, dudas sobre los datos) — [ZenML](https://www.zenml.io/llmops-database/building-queryanswerbird-an-ai-data-analyst-with-text-to-sql-and-rag).
- Stack GPT-4 + RAG + LangChain en Slack: genera SQL, lo interpreta, valida sintaxis y explora tablas; clave: enriquecer metadatos (propósito de tabla, descripciones de columnas, valores clave, keywords, servicios, preguntas de ejemplo), basado en un paper NeurIPS 2023 — [Delivery Hero Tech, parte 1](https://deliveryhero.jobs/blog/introducing-the-ai-data-analyst-queryanswerbird-part-1-utilization-of-rag-and-text-to-sql/).
- Dos rondas de beta (analistas/ingenieros, luego PMs) revelaron brecha entre Text-to-SQL inicial y necesidades reales → se agregó "data discovery" — [Delivery Hero Tech, parte 2](https://tech.deliveryhero.com/introducing-the-ai-data-analyst-queryanswerbird-part-2-data-discovery).

**Grab – Data-Arks / Report Summarizer (2024)**
- En lugar de Text-to-SQL libre, los usuarios suben consultas SQL o scripts Python a Data-Arks, que se exponen como APIs que un agente LLM puede llamar; el Report Summarizer llama a esas APIs y el LLM resume insights — [Grab Engineering](https://engineering.grab.com/transforming-the-analytics-landscape-with-RAG-powered-LLM) (contenido vía resumen de búsqueda; fetch bloqueado).

**Vercel – d0 ("We removed 80% of our agent's tools", ~dic. 2025/ene. 2026)**
- d0 v1: ~18 tools especializadas + prompt engineering pesado + manejo cuidadoso de contexto → ~80% de éxito, frágil y costoso — [Vercel blog](https://vercel.com/blog/we-removed-80-percent-of-our-agents-tools) (vía resumen); [ZenML](https://www.zenml.io/llmops-database/simplifying-text-to-sql-agents-by-removing-80-of-tools).
- d0 v2: un agente "file system" con una tool de ejecución bash que navega la capa semántica Cube (archivos YAML) con utilidades Unix, sobre Snowflake, con Claude Opus 4.5 → 100% éxito, 3,5x más rápido, ~37–40% menos tokens, ~40–42% menos pasos (las cifras difieren entre resúmenes secundarios) — [AI Engineer Guide](https://aiengineerguide.com/til/vercel-agent-tools-cut-improved-accuracy/); [The New Stack](https://thenewstack.io/the-key-to-agentic-success-let-unix-bash-lead-the-way/).
- Se publicó como open source el paquete npm `bash-tool` — [resumen de búsqueda sobre el post de Vercel](https://vercel.com/blog/we-removed-80-percent-of-our-agents-tools).

**Ramp – MCP sobre API de desarrollador (2025)**
- Ramp construyó un servidor MCP para consultar en lenguaje natural datos de gasto a través de su API de desarrollador (tools sobre API, no SQL directo) — [Evidently AI, "7 agentic AI examples"](https://www.evidentlyai.com/blog/agentic-ai-examples).

**Anthropic – guía de diseño de tools (sep. 2025)**
- Recomienda consolidar tools (p. ej., un `get_customer` que devuelve datos estructurados en vez de una tool por campo), reducir la cantidad de tools en contexto, y evaluar no sólo accuracy sino runtime, número de llamadas, tokens y errores de tools; usar transcripts para ver por qué el agente llama o no una tool — [Anthropic Engineering, "Writing effective tools for agents"](https://www.anthropic.com/engineering/writing-tools-for-agents).

### Inferences
- El patrón común es "acotar → enriquecer contexto → verificar": workspaces (Uber), charters (Swiggy), knowledge graph (LinkedIn), metadatos enriquecidos (Woowa). Para Ars Docendi, el catálogo determinista de intents ya cumple el rol de "intent agent"/"workspace" de Uber; la pieza que suele faltar es el corpus de SQL de referencia por intent y un golden set.
- Hay dos lecciones que parecen opuestas pero no lo son: Grab/Ramp reemplazan SQL libre por consultas/APIs curadas (más control), mientras Vercel reduce tools y da más libertad al modelo. La diferencia es que Vercel tenía una capa semántica Cube bien documentada y un modelo frontera (Opus 4.5); con un modelo local de ~8B esa libertad es mucho más riesgosa.
- Las cifras altas (93%, 100%) vienen de dominios acotados, con contexto curado y/o modelos frontera; las cifras de despliegues amplios (53%, 40%) son el piso realista para SQL libre sobre muchas tablas.

### Gaps
- No se pudo acceder a los posts primarios de Uber, LinkedIn, Vercel, Grab ni ZenML; números como "95% Passes" (LinkedIn) y el tamaño del set de evaluación de Vercel (se rumorea un set pequeño) no se verificaron.
- No se encontraron write-ups públicos de Airbnb, Wix, Instacart (sólo una oferta laboral de "Agentic Analytics" — [startup.jobs](https://startup.jobs/senior-software-engineer-i-agentic-analytics-instacart-8007962)), Shopify ni Salesforce sobre bots de datos internos en esta sesión.
- Mercado Libre u otras empresas LatAm: no se halló ningún caso publicado de Text-to-SQL/agente de datos.
- Ningún caso detalla cómo manejaron PII/permisos a nivel fila salvo de forma genérica (LinkedIn y Uber no publican el mecanismo en lo recuperado).

## 2. Productos comerciales: cómo están arquitecturados

### Takeaway
Todos los productos de los hyperscalers/BI convergieron en "capa semántica + consultas verificadas/curadas + benchmarks", no en Text-to-SQL desnudo: Snowflake (semantic model YAML + Verified Query Repository), Databricks Genie (trusted assets: SQL parametrizado y funciones UC), Looker (LookML), Fabric/Power BI (few-shot examples + verified answers). Las cifras de precisión publicadas son internas del vendor.

### Cited Findings
- **Snowflake Cortex Analyst**: Snowflake afirma "90%+ SQL accuracy on real-world use cases" gracias a modelos semánticos, medido en un benchmark interno no público (marketing/medición interna, no reproducible) — [Snowflake Engineering, "Cortex Analyst: Behind the Scenes"](https://www.snowflake.com/en/blog/engineering/snowflake-cortex-analyst-behind-the-scenes/).
- Cortex Analyst Evaluations: ejecuta el SQL generado y compara resultados contra las verified queries; crea una copia temporal de la semantic view sin esas queries; una verified query sirve como guía en runtime o como ground truth, no ambas; reporta accuracy, regresiones y latencia — [Snowflake Docs, Cortex Analyst evaluations](https://docs.snowflake.com/user-guide/snowflake-cortex/cortex-analyst-evaluations).
- Cifras de terceros contradictorias: AtScale (vendor de capa semántica) reporta 100% en su NLQ benchmark vs. 54% promedio — [AtScale](https://atscale.com/blog/semantic-layer-cortex-analyst-accuracy/); Atlan cita 57%→78% en BIRD al agregar modelo semántico — [Atlan](https://atlan.com/know/snowflake/cortex-analyst-vs-text-to-sql/); Querio ~45%→~90% — [Querio](https://querio.ai/articles/querio-vs-snowflake-cortex-analyst). Ninguna verificable de forma independiente.
- **Databricks AI/BI Genie – trusted assets**: consultas SQL de ejemplo parametrizadas (la respuesta muestra los valores de argumentos y el usuario puede cambiarlos) y funciones registradas en Unity Catalog para preguntas que una query estática/parametrizada no cubre; una respuesta se marca "Trusted" sólo si se usa el texto exacto del asset; si no, el ejemplo sólo guía la generación; se recomienda un schema dedicado para las funciones — [Databricks Docs, Trusted assets](https://docs.databricks.com/aws/en/genie/trusted-assets) (vía resumen de búsqueda).
- Genie benchmarks: hasta 500 preguntas por space, con SQL de referencia; "Good" si los valores de filas coinciden (sin importar orden/nombres de columnas); 2–4 parafraseos por pregunta; las funciones UC pueden servir de gold standard; Genie no aprende de los benchmarks — [Databricks Docs, Benchmarks](https://docs.databricks.com/aws/en/genie/benchmarks); [Databricks blog, benchmarks and Ask for Review](https://www.databricks.com/blog/building-confidence-your-genie-space-benchmarks-and-ask-review).
- Databricks reportó 84,5% en 28 preguntas empresariales para un agente "Genie One" con ontología vs. 52,4% y 25% para coding agents (interno, muestra pequeña, sin verificación independiente) — [Pebblous (secundario)](https://blog.pebblous.ai/blog/databricks-genie-one-governed-data/en/).
- **Looker / Conversational Analytics**: Google afirma que su capa semántica LookML reduce errores de datos en consultas NL de gen AI "hasta dos tercios" vs. tablas no gobernadas (test interno, metodología no publicada) — [Google Cloud blog](https://cloud.google.com/blog/products/business-intelligence/how-lookers-semantic-layer-enhances-gen-ai-trustworthiness); [Valtech](https://www.valtech.com/blog/unlock-retail-insights-with-googles-conversational-analytics-api/).
- Guía de Google: etiquetas que reflejen el lenguaje del negocio, sinónimos y descripciones, ocultar campos internos (IDs) — [Google, Conversational Analytics in Looker (PDF)](https://services.google.com/fh/files/misc/conversational_analytics_in_looker.pdf).
- **Microsoft Fabric data agent**: few-shot examples pregunta→query; el agente recupera típicamente los 4 más relevantes; SDK con función de evaluación de ejemplos; vista "Run Steps" muestra qué ejemplos influyeron — [Microsoft Learn, data agent example queries](https://learn.microsoft.com/en-us/fabric/data-science/data-agent-example-queries); [Fabric blog](https://blog.fabric.microsoft.com/en/blog/creator-improvements-in-the-data-agent).
- **Power BI Copilot – verified answers**: visuales aprobados por humanos disparados por frases; viven en el modelo semántico (misma respuesta en todos los reportes); Microsoft advierte que la salida de Copilot es no determinista — [Microsoft Learn, verified answers](https://learn.microsoft.com/power-bi/create-reports/copilot-prepare-data-ai-verified-answers).

### Inferences
- "Verified queries" (Snowflake), "trusted assets" (Databricks) y "verified answers" (Power BI) son el mismo patrón con distinto nombre: respuestas curadas por humanos que el sistema prefiere y que además funcionan como set de evaluación. Esto equivale a la combinación "catálogo determinista de intents + SQL de referencia" que ya tiene Ars Docendi.
- Todos los vendors venden la capa semántica como el principal factor de precisión, pero las cifras (90%+, 2/3 menos errores) son internas; deben tratarse como dirección, no magnitud.

### Gaps
- No se recuperó documentación de AWS Bedrock Knowledge Bases (structured data), ThoughtSpot Spotter, BigQuery data agents ni Salesforce en esta sesión.
- No hay benchmarks independientes que comparen productos con la misma metodología.

## 3. El "término medio": consultas SQL parametrizadas/certificadas expuestas como tools (MCP Toolbox, Genie trusted functions, Cortex verified queries)

### Takeaway
Existe evidencia de producto (Google MCP Toolbox, Genie, Grab Data-Arks, Vanna 2.0) de que el término medio —SQL parametrizado definido por humanos, con parámetros ligados (bind) y parámetros de identidad inyectados desde el token, expuesto como tools— es el patrón preferido cuando importan seguridad y consistencia; su costo es cobertura limitada, que se complementa con SQL generado como fallback.

### Cited Findings
- **Google MCP Toolbox for Databases**: tools en `tools.yaml` con `kind: postgres-sql`, `source`, `description`, `parameters` tipados y `statement` con placeholders `$1`; se ejecutan como prepared statements (posicionales), descritos como seguros contra SQL injection y la opción recomendada — [mcpservers.org (README del repo)](https://mcpservers.org/servers/googleapis/genai-toolbox); [ScyllaDB docs, MCP Toolbox](https://docs.scylladb.com/stable/get-started/build-with-ai/integrations/mcp-toolbox.md).
- `templateParameters` permiten variar identificadores (tablas/columnas) pero se interpolan antes de preparar y son vulnerables a inyección; usar parámetros básicos siempre que sea posible — [ScyllaDB docs](https://docs.scylladb.com/stable/get-started/build-with-ai/integrations/mcp-toolbox.md).
- `authRequired` a nivel tool: el servidor valida tokens OIDC contra el auth service configurado; tokens inválidos o ausentes se rechazan. Parámetros con `authServices` + `field: sub` se completan desde el claim del token, no desde el input del usuario/LLM (p. ej., `WHERE user_id = $1`), lo que impide pedir filas de otro usuario — [DBHub, review de MCP Toolbox](https://glama.ai/mcp/servers/@bytebase/dbhub/blob/9340818f0eaf64599a654180fa5de0518080a780/docs/blog/postgres-mcp-server-review-mcp-toolbox.mdx); [Genkit docs, authenticatedParams](https://genkit.dev/docs/integrations/toolbox/).
- Limitación reportada: el soporte de auth services era sólo Google Sign-In (OIDC), con Keycloak/Entra pendientes (puede estar desactualizado) — [DBHub review](https://glama.ai/mcp/servers/@bytebase/dbhub/blob/9340818f0eaf64599a654180fa5de0518080a780/docs/blog/postgres-mcp-server-review-mcp-toolbox.mdx).
- **Databricks Genie**: SQL parametrizado como trusted asset y funciones UC para lo que no cubre el SQL estático; sello "Trusted" sólo con texto exacto — [Databricks Docs](https://docs.databricks.com/aws/en/genie/trusted-assets).
- **Grab Data-Arks**: SQL/Python subidos por usuarios expuestos como APIs para el agente — [Grab Engineering](https://engineering.grab.com/transforming-the-analytics-landscape-with-RAG-powered-LLM).
- **Vanna 2.0**: reescritura como framework de agente "user-aware": el contexto de usuario llega a tools, SQL y logs de auditoría; control de acceso por grupos a nivel tool; el filtrado por fila se implementa en las tools (p. ej., `RunSqlTool` aplica filtros según identidad) — [Vanna releases](https://github.com/vanna-ai/vanna/releases); [Vanna, "Why we built Vanna 2.0"](https://vanna.ai/docs/why-we-built-this).
- **Riesgo del extremo opuesto (SQL libre vía MCP)**: el servidor Postgres MCP de referencia de Anthropic envolvía la query en `BEGIN TRANSACTION READ ONLY` y ejecutaba `client.query(sql)`; como node-postgres acepta múltiples sentencias, `COMMIT; DROP SCHEMA public CASCADE;` escapaba de la transacción read-only; además `COMMIT; SET statement_timeout TO 1;` persistía en conexiones del pool afectando a otros usuarios. Fix (fork Zed v0.1.4, abr. 2025): prepared statement único + destruir la conexión tras cada llamada. ~21.000 descargas semanales en npm pese a estar archivado (archivado 29/05/2025 según timeline; "deprecado desde 10/07/2025" según key points — fechas inconsistentes en el propio artículo). Recomienda usuario Postgres con privilegios restringidos, aunque eso no resuelve la fuga de estado de sesión — [Datadog Security Labs, 21/08/2025](https://securitylabs.datadoghq.com/articles/mcp-vulnerability-case-study-SQL-injection-in-the-postgresql-mcp-server/).
- Patrón de guardrails genérico: validar el SQL generado en código (una sola sentencia, read-only, sólo tablas aprobadas, límite de filas) y ejecutar con rol read-only que sólo ve tablas modeladas, nunca PII cruda — [bigdatadwbi, sep. 2026](https://www.bigdatadwbi.com/2026/09/building-ai-analytics-assistant-text-to.html).

### Inferences
- Pros del término medio (inferidos de las fuentes): seguridad por construcción (bind parameters, identidad inyectada desde el token y no desde el LLM), resultados consistentes/auditables ("Trusted"), y la tarea del LLM se reduce a elegir tool + extraer parámetros, algo que un modelo local de 7–8B hace mucho mejor que generar SQL con joins.
- Contras: cobertura limitada a lo que alguien curó; explosión de tools si se crea una por pregunta (lo que Vercel y Anthropic desaconsejan); mantenimiento cuando cambia el esquema; y para preguntas fuera de catálogo se necesita un fallback (SQL generado guiado por ejemplos, o abstención).
- Para Ars Docendi: exponer endpoints existentes del backend (que ya aplican `IConsultasIdentity` y autorización por ámbito) como tools MCP hereda las reglas de autorización del backend, equivalente a `authServices`/`authRequired` de Toolbox; un Text-to-SQL libre sobre PostgreSQL necesitaría replicar esas reglas con RLS/vistas por rol. Un servidor MCP que ejecute SQL arbitrario no debería basarse sólo en transacciones read-only (lección Datadog).

### Gaps
- No se encontró una comparación cuantitativa publicada (misma carga, mismo modelo) entre "tools parametrizadas" y "SQL libre" en precisión.
- No se verificó en la documentación oficial (bloqueada) la sintaxis vigente de `authServices`/`authRequired` ni qué proveedores OIDC soporta hoy MCP Toolbox.

## 4. Lecciones recurrentes: consultas curadas, capa semántica, few-shot, abstención, feedback humano, evaluación

### Takeaway
Las prácticas que se repiten en todos los casos son: (1) consultas de ejemplo/verificadas recuperadas como few-shot y reutilizadas como set de evaluación; (2) metadatos/capa semántica en lenguaje del negocio; (3) humano en el loop para tablas o aprobación; (4) benchmarks con comparación por resultado de ejecución y seguimiento de regresiones. La abstención explícita está poco documentada públicamente.

### Cited Findings
- Queries de ejemplo son la palanca individual más fuerte medida públicamente para recuperación de tablas (+~16% recall en LinkedIn) — [LinkedIn @ Trino Summit 2024](https://trino.io/assets/blog/trino-summit-2024/trino-summit-2024-linkedin-ai.pdf).
- Fabric recupera ~4 few-shot por pregunta; recomienda ejemplos diversos, sin solapamientos ni contradicciones, que reflejen preguntas reales — [Microsoft Learn](https://learn.microsoft.com/en-us/fabric/data-science/data-agent-example-queries).
- Evaluación por ejecución: Snowflake compara resultados contra verified queries y reporta regresiones — [Snowflake Docs](https://docs.snowflake.com/user-guide/snowflake-cortex/cortex-analyst-evaluations); Databricks compara filas ignorando orden/nombres y recomienda 2–4 parafraseos — [Databricks Docs](https://docs.databricks.com/aws/en/genie/benchmarks).
- Uber: golden set de preguntas reales con intent, tablas y SQL verificados — [Scribd QueryGPT (secundario)](https://www.scribd.com/document/797618430/QueryGPT).
- Humano en el loop: Uber Table agent (usuario acepta/edita tablas) — [Uber](https://uber.com/blog/query-gpt); Woowa y Delivery Hero con betas por rol — [Delivery Hero Tech](https://tech.deliveryhero.com/introducing-the-ai-data-analyst-queryanswerbird-part-2-data-discovery); Databricks "Ask for Review" — [Databricks blog](https://www.databricks.com/blog/building-confidence-your-genie-space-benchmarks-and-ask-review).
- Benchmarks públicos son ruidosos: una auditoría reporta 52,8% (BIRD) y 66,1% (Spider 2.0-Snow) de errores de anotación — [rmarcus.info, "Text-to-SQL Benchmarks are Broken"](https://rmarcus.info/dbscholar/papers/598).
- Modelos chicos locales: Arctic-Text2SQL-R1-7B (Qwen2.5-Coder-7B + GRPO) reporta BIRD-dev 68,9%, Spider-test 88,8%, pero Spider2.0-DK 15,6% y EHRSQL 36,7% — [Hugging Face model card / arXiv 2505.20315](https://huggingface.co/papers/2505.20315); OmniSQL-7B 87,9% Spider-test (greedy) — [arXiv 2503.02240](https://arxiv.org/pdf/2503.02240).

### Inferences
- La brecha entre Spider (~88%) y Spider 2.0/EHRSQL (15–37%) para modelos de 7B sugiere que un 8B local rinde bien en esquemas simples y mal en esquemas empresariales reales; esto refuerza tools/consultas curadas para lo frecuente y modelo frontera (API Anthropic) o abstención para lo complejo.
- Las verified queries cumplen doble función (few-shot en runtime + ground truth en evaluación), pero Snowflake advierte que no deben usarse ambas a la vez en una misma evaluación (data leakage).

### Gaps
- Pocas fuentes públicas describen mecanismos explícitos de abstención ("no sé"/"fuera de alcance") y su tasa; no se encontraron métricas.
- Manejo de PII: sólo guías genéricas (rol read-only, tablas modeladas, identidad inyectada); ninguna empresa publica tasas de incidentes o auditorías.

## 5. Frameworks open source reutilizables (estado 2024–2026)

### Takeaway
El ecosistema OSS es volátil: Vanna (el más popular, RAG + "training" con SQL) se reescribió como framework de agentes user-aware en 2.0 y su repo fue archivado el 29/03/2026; Dataherald parece inactivo desde jul. 2024; Wren AI apuesta por capa semántica (MDL); Google MCP Toolbox es la opción más alineada con "SQL parametrizado como tools" y está activamente mantenida.

### Cited Findings
- **Vanna**: 2.0 = reescritura agéntica con permisos por usuario/grupo, auditoría, rate limiting, componente web `<vanna-chat>` y adaptador legacy para 0.x; repo archivado (read-only) el 29/03/2026 — [Vanna releases / repo](https://github.com/vanna-ai/vanna/releases); [Querio](https://querio.ai/articles/wren-ai-vs-vanna-ai-open-source-text-to-sql-compared).
- **Wren AI**: define términos de negocio en una capa semántica (MDL) antes de generar SQL; errores típicos por falta de cobertura semántica (vs. Vanna: contexto/training desactualizado); requiere más trabajo de modelado — [Querio](https://querio.ai/articles/wren-ai-vs-vanna-ai-open-source-text-to-sql-compared); [Wren AI blog (vendor)](https://getwren.ai/post/why-the-semantic-layer-is-essential-for-reliable-text-to-sql-and-how-wren-ai-brings-it-to-life).
- **Dataherald**: agente que usa un "Context Store" de lógica de negocio; afirma superar al SQL agent de LangChain en su propio benchmark; último push jul. 2024 (dormido, sin aviso formal de archivo) — [Dataherald docs](https://dataherald.readthedocs.io/en/stable/text_to_sql_engine.html); [ecosyste.ms](https://awesome.ecosyste.ms/projects/github.com%2Fdataherald%2Fdataherald).
- **DB-GPT**: framework de apps "data-native" con RAG, Text2SQL y múltiples backends; DB-GPT-Hub (fine-tuning Text2SQL) último push 02/07/2025 — [Jimmy Song (secundario)](https://jimmysong.io/ai/db-gpt); [gittrend](https://gittrend.io/repo/eosphoros-ai/DB-GPT-Hub).
- **Google MCP Toolbox for Databases** (`googleapis/genai-toolbox`, renombrado `mcp-toolbox`): servidor MCP con tools SQL parametrizadas en YAML, auth OIDC, integraciones con ADK/Genkit/LangChain — [mcpservers.org](https://mcpservers.org/servers/googleapis/genai-toolbox); [ADK docs](https://adk.dev/integrations/mcp-toolbox-for-databases).
- **Servidores Postgres MCP**: el de referencia de Anthropic está archivado y vulnerable en npm; fork de Zed parcheado — [Datadog Security Labs](https://securitylabs.datadoghq.com/articles/mcp-vulnerability-case-study-SQL-injection-in-the-postgresql-mcp-server/); panorama de alternativas (DBHub, etc.) — [DBHub, "State of Postgres MCP servers 2025"](https://glama.ai/mcp/servers/@bytebase/dbhub/blob/bdc906c1dffce4a916c264cc883c47e73a3557f1/docs/blog/state-of-postgres-mcp-servers-2025.mdx).
- **Modelos locales especializados**: Arctic-Text2SQL-R1-7B (Snowflake, open weights) y OmniSQL-7B — [arXiv 2505.20315](https://arxiv.org/abs/2505.20315); [arXiv 2503.02240](https://arxiv.org/pdf/2503.02240).
- Alternativa no-LLM: LexaQuery (Univ. de Plovdiv) usa NLP basado en reglas para traducir preguntas de estudiantes a SQL, más rápido que alternativas neuronales con precisión "satisfactoria" en dominio acotado — [Az-buki](https://press.azbuki.bg/en/?p=224533).

### Inferences
- Para un stack .NET + PostgreSQL con endpoints ya autorizados, reutilizar Vanna/Wren implica adoptar un runtime Python adicional y una capa de datos paralela; MCP Toolbox (Go, binario único, YAML) o un servidor MCP propio delgado sobre los endpoints existentes son menos invasivos. La caída de Vanna/Dataherald sugiere no depender de frameworks Text-to-SQL de startups para un sistema institucional de largo plazo.

### Gaps
- No se verificaron estado y actividad actuales de Wren AI, Chat2DB y MindsDB (no recuperados en esta sesión).
- Supabase MCP server: no se recuperaron fuentes sobre su modelo de seguridad (p. ej., advertencias de prompt injection publicadas en 2025).

## 6. Casos universitarios / sector público / LatAm

### Takeaway
No se encontró ningún caso documentado de chatbot LLM conectado a SIU Guaraní ni de una universidad argentina que use LLM para consultas administrativas sobre datos institucionales; la literatura disponible son prototipos académicos pequeños, mayormente orientados a estudiantes.

### Cited Findings
- SIU Guaraní: sistema de gestión académica integral usado por universidades nacionales argentinas (consorcio SIU; >270 implementaciones según una fuente) — [RedCLARA (PDF)](https://documentas.redclara.net/bitstream/10786/835/1/11-3_Sistema_gestion_academica_SIU_guarani.pdf).
- UTN Regional La Plata: asistente virtual para ingresantes 24/7; no indica integración con sistema académico — [Revista Ingenio, UTN FRLP](https://ingenio.frlp.utn.edu.ar/index.php/ingenio/article/download/79/149/964).
- CHIMLE (LACCEI 2026, Chile): prototipo LLM + herramientas SQL para "interacción segura" con datos académicos estructurados, consultas multi-turno sobre rendimiento estudiantil y estadísticas agregadas; implementado en Colab — [LACCEI 2026](https://laccei.org/LACCEI2026-Chile/meta/FP800.html).
- Universidad Autónoma de Sinaloa: asistente académico con LLM + RAG sobre información institucional validada — [revistas.uas.edu.mx](https://revistas.uas.edu.mx/index.php/IJISTA/article/download/1825/1430/13785).
- MyAdvisor (Univ. de Cape Town, 2025): combina SQL generado + búsqueda semántica; probado con 10 estudiantes, satisfacción 4,6/5 (muestra pequeña, autoreporte) — [UCT honours project](https://projects.cs.uct.ac.za/honsproj/cgi-bin/view/2025/allies_flanegan_howard.zip/myAdvisor_allies_howard_flanegan/chatbot.html).
- LexaQuery (Plovdiv): enfoque por reglas sobre SQL, elegido por costo/latencia frente a LLMs — [Az-buki](https://press.azbuki.bg/en/?p=224533).

### Inferences
- Ars Docendi estaría en terreno poco documentado en Argentina; los antecedentes útiles son los patrones industriales (secciones 1–3), no casos universitarios. El enfoque LexaQuery (reglas) valida que el catálogo determinista de intents es una base legítima, no un parche.

### Gaps
- No se encontraron casos de Mercado Libre, Globant, ni organismos públicos argentinos/LatAm con chatbots sobre datos operativos.
- No se halló información sobre iniciativas del consorcio SIU con IA generativa.
