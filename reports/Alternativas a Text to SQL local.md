# Certificar consultas antes de liberar herramientas

La alternativa con mejor relación entre riesgo, costo y beneficio para el Asistente de Ars Docendi con un modelo local no es un MCP sobre los endpoints REST. Es un **enrutador híbrido cuyo carril principal sean consultas SQL certificadas y parametrizadas, expuestas al modelo como 6 a 10 herramientas de nivel tarea**. Esas herramientas corren en proceso, sobre el mismo `EjecutorDeConsulta`, con RLS, rol de solo lectura y enmascaramiento. Detrás queda el Text-to-SQL actual como respaldo rotulado y con abstención. El motivo central es que esta opción conserva intacta la frontera de seguridad que el equipo ya construyó y verifica en CI (invariante #14: rol sin mutación, GRANT por columna, RLS con permiso de dominio, actor en un GUC). Además le quita al modelo de 8B la tarea en la que más se equivoca: escribir joins y copiar literales. Hoy esa tarea deja **7 respuestas falsas sobre 34 ítems de capacidad** en la RTX 3070 ([modelo-local.md §3](docs/architecture/modelo-local.md)). Exponer los endpoints REST vía MCP es la opción más débil de las seis. Los endpoints de lectura son pocos. No existe endpoint para designaciones vigentes ni para portales ajenos. La evidencia académica indica que componer varias llamadas a API es *más* difícil que escribir SQL ([Live API-Bench, EACL 2026](https://arxiv.org/abs/2506.11266)). MCP, por su parte, no le aporta nada al modelo local: llama-server y vLLM solo emiten tool calls al estilo OpenAI. El hardware refuerza la elección. El costo dominante del turno actual es un prefijo de unos 12.000 tokens. Reemplazarlo por definiciones de herramientas de unos 2 a 3k tokens libera KV cache, y eso habilita 2 a 4 conversaciones simultáneas en 8 GB, donde hoy entra una sola (estimado, a medir). Ninguna de estas ventajas está medida todavía sobre el dominio de UNLaM. El informe cierra con un banco de pruebas de bajo costo sobre el evaluador existente (`backend/eval`), con Claude como techo y Qwen como objetivo, para medirlas antes de reescribir nada.

## La arquitectura actual pone la seguridad en el motor y la fragilidad en el modelo

El turno actual tiene dos partes. La primera es una capa conversacional casi toda determinista: saludos, aclaraciones, cambio de tema y ambigüedad, con cero tokens. La segunda es un carril SQL con **dos llamadas al modelo como base**: generar el SQL (temperatura 0, prefijo cacheado) y redactar la respuesta (temperatura 0,3). Hay una tercera llamada opcional para reescribir los seguimientos. El techo duro es de **4 llamadas por turno y 12 requests HTTP** ([README del módulo](backend/src/Modules.Asistente/README.md)). El prefijo de generación mide unos **12.000 tokens**: instrucciones, valores de catálogos cerrados y el esquema renderizado desde `information_schema.column_privileges` más los `COMMENT ON`. Ese prefijo es estable byte a byte y "es el costo dominante" ([modelo-local.md §1](docs/architecture/modelo-local.md)). El modelo escribe SQL libre contra **21 tablas base** de `identity`, `designaciones` y `portal`, sin vistas ni capa semántica ([manifiesto-privilegios.json](database/asistente/manifiesto-privilegios.json)).

La seguridad no depende del modelo. Se apoya en cuatro capas:

1. **Dos roles de solo lectura** (básico y con PII), con GRANT columna por columna contra un manifiesto que falla en CI en tres direcciones ([design fundaciones D3–D4](openspec/changes/asistente-fundaciones/design.md)).
2. **Policies RLS** que conjuntan el permiso de dominio leído en vivo con el ámbito del actor (global, carrera o materia) ([009_designaciones_rls_asistente.sql](database/designaciones/009_designaciones_rls_asistente.sql)).
3. **Un validador léxico** (lista blanca de comienzo y listas negras de funciones y palabras clave).
4. **Una transacción `READ ONLY`** con timeouts y `LIMIT tope+1` ([ValidadorDeSql.cs](backend/src/Modules.Asistente/Application/CarrilSql/ValidadorDeSql.cs); [EjecutorDeConsulta.cs](backend/src/Modules.Asistente/Infrastructure/EjecutorDeConsulta.cs)).

Sobre la salida se aplica un **enmascaramiento por (OID, attnum)** con 151 columnas clasificadas ([manifiesto-sensibilidad.json](database/asistente/manifiesto-sensibilidad.json)) y una **política de abstención de siete casos**. Esa política distingue "cero filas por RLS" de "no hay" ([PoliticaDeAbstencion.cs](backend/src/Modules.Asistente/Application/Abstencion/PoliticaDeAbstencion.cs)). El modelo de amenaza es explícito: el LLM es un usuario de base de datos no confiable con privilegios mínimos.

Las debilidades documentadas se concentran en lo que el modelo escribe, no en lo que puede leer:

- **Respuestas falsas.** Qwen3-8B Q4_K_M, con el perfil optimizado y medido el 2026-10-03, acierta **26/34 en capacidad, 12/15 en robustez, 10/11 en diálogo y 18/20 en social, con 7 respuestas falsas en capacidad**, p50 de 2,4 a 2,9 s y 7,4 GiB de VRAM ([modelo-local.md §3, §8](docs/architecture/modelo-local.md)). El modo de falla más difícil de detectar es "una consulta válida con un literal mal copiado", porque cero filas de un SQL válido son indistinguibles de "no hay" ([proposal recuperación de valores](openspec/changes/asistente-recuperacion-de-valores/proposal.md)).
- **Fragilidad ante cambios del prompt.** Agregar ocho ejemplos cambió la selección de 22 de 34 ítems y costó tres aciertos. El glosario empeoró capacidad y social ([README del módulo](backend/src/Modules.Asistente/README.md)).
- **Fugas de texto libre.** Una expresión sobre una columna sensible (`lower(comentario)`) deja vacío el par (OID, attnum) y se trata como pública (TD-009, abierto) ([tech-debt.md](docs/quality/tech-debt.md)).
- **Inyección indirecta.** La auditoría declara completa la *lethal trifecta* ([Auditoría Eje 3](docs/quality/auditoria-asistente.md)).

El equipo descartó por escrito que el modelo "genere llamadas a métodos" de `Contracts` como sustituto del SQL, con el argumento de que "es otro sistema" ([design fundaciones D1](openspec/changes/asistente-fundaciones/design.md)). También descartó **clasificar la intención con el LLM** ("60% de F1 en triage de 5 clases; 77,4% en 9 vías") con la consigna "no volver a proponerlo sin evidencia nueva" ([definición §6](docs/product/designs/asistente-conversacional-definicion.md)). Ambos descartes alcanzan a cualquier alternativa basada en herramientas: elegir una herramienta es, en la práctica, clasificar la intención con el LLM. Por eso este informe trata la precisión de selección de herramientas como la primera hipótesis a medir, no como un supuesto. Existe además un carril determinista hacia la API con 5 intenciones, que corre en modo sombra: su tabla dorada da **0 de 39 capturas** sobre los datasets actuales ([README del módulo](backend/src/Modules.Asistente/README.md); [intenciones.json](backend/src/Modules.Asistente/Recursos/intenciones.json)).

La siguiente tabla resume qué conserva y qué reemplaza cada alternativa frente a las piezas existentes:

| Pieza actual | (1) SQL libre | (2) REST vía MCP | (3) AIFunction sobre Contracts | (4) SQL certificado como tools | (5) Capa semántica | (6) Híbrido |
|---|---|---|---|---|---|---|
| RLS + roles de solo lectura | Conserva | **Pierde** (autoriza el código de la API) | **Pierde** (autoriza el servicio del módulo) | Conserva | Conserva si las vistas usan `security_invoker` | Conserva en (4) y (1) |
| Validador léxico | Conserva | Innecesario | Innecesario | Innecesario (SQL fijo) | Conserva si el modelo escribe SQL | Solo en el respaldo |
| Enmascaramiento (OID, attnum) | Conserva | **Hay que rehacerlo** sobre los DTO | **Hay que rehacerlo** sobre los DTO | Conserva (columnas directas) | Hay que clasificar las columnas de las vistas | Conserva |
| Abstención de 7 casos | Conserva | Hay que rediseñarla | Hay que rediseñarla | Conserva y suma "entidad no encontrada" | Conserva y suma "fuera de cobertura" | Conserva |
| Catálogo determinista (intenciones) | Sombra | Destino natural | Destino natural | Destino natural | Parcial | Primer filtro |
| Prefijo de 12k | Lo usa | Lo reemplaza por tools | Lo reemplaza por tools | Lo reemplaza por tools | Lo reduce (vistas) | 12k solo en el respaldo |
| Evaluador de 4 ejes | Sin cambios | Requiere un runner nuevo | Requiere un runner nuevo | Requiere un runner nuevo (mismas filas) | Sin cambios | Requiere un runner nuevo |

## La evidencia externa favorece acotar, pero nadie comparó los paradigmas con un modelo chico

Ningún estudio compara cabeza a cabeza los cinco paradigmas con el mismo modelo pequeño y el mismo dominio. Lo que hay son piezas que, juntas, apuntan en una dirección consistente.

**Text-to-SQL con modelos de 7–9B tiene un piso de error alto aun en benchmarks limpios.** Los mejores 7B especializados llegan a **68,9 % (Arctic-Text2SQL-R1-7B) y 63,9 % (OmniSQL-7B)** de exactitud de ejecución en BIRD dev. Un coder genérico de 7B ronda el **50,9 %** ([Arctic-Text2SQL-R1](https://arxiv.org/pdf/2505.20315); [OmniSQL](https://arxiv.org/html/2503.02240v1)). En esquemas empresariales la caída es mayor que la distancia entre modelo chico y modelo frontera. En Spider 2.0, el mejor agente bajó a **21,3 %** contra 73 % en BIRD con el mismo tipo de modelo ([Spider 2.0, ICLR 2025](https://proceedings.iclr.cc/paper_files/paper/2025/file/46c10f6c8ea5aa6f267bcdabcb123f97-Paper-Conference.pdf)). En BEAVER, frameworks agénticos con GPT-5.2 alcanzan solo **10,8 %** ([BEAVER](https://arxiv.org/pdf/2409.02038)). El esquema de Ars Docendi (21 tablas comentadas) está mucho más cerca de BIRD que de BEAVER, y eso matiza el pesimismo. Aun así, los propios especialistas, entrenados sobre SQLite, "pierden mucho en PostgreSQL" ([modelo-local.md §3](docs/architecture/modelo-local.md)). La única cifra histórica sobre español indica una caída media de **6,1 puntos** respecto del inglés ([MultiSpider, AAAI 2023](https://ojs.aaai.org/index.php/AAAI/article/view/26499)). Es una fuente antigua.

**Las herramientas funcionan bien cuando hay pocas y cuando una llamada alcanza.** En la evaluación independiente de Docker (21 modelos, 3.570 casos, escenario con unas 5 herramientas), **Qwen3-8B Q4_K_M obtuvo F1 0,919 y Qwen3-14B Q4_K_M 0,971**. Los especialistas de function calling rindieron mal (xLAM-2-8B 0,570, watt-tool-8B 0,484) ([Docker](https://www.docker.com/blog/local-llm-tool-calling-a-practical-evaluation/)). La precisión de selección cae fuerte con más herramientas visibles: de **85 % con 5 a 45 % con 20**. Recuperar de 3 a 5 candidatas la mantiene por encima del 80 % ([RAG-MCP](https://www.alphaxiv.org/abs/2505.03275)). En el extremo opuesto, Live API-Bench tomó las preguntas de BIRD y las convirtió en secuencias de API ejecutables. La completitud fue de **7 a 47 %, y llegó a cerca de 50 % con ReAct**. Los autores observan que los modelos "a veces explotan mejor el SQL que las API" ([Live API-Bench](https://arxiv.org/pdf/2506.11266)). En tool use multiturno, Qwen3-8B queda en **35,7 % (retail) y 12,0 % (airline) en τ-bench sin thinking** ([Klear-AgentForge](https://arxiv.org/pdf/2511.05951)). La lectura conjunta es clara: las herramientas valen la pena si son de grano grueso y resuelven la pregunta típica en una o dos llamadas, con la agregación hecha en el backend. Un mapeo 1:1 de endpoints CRUD reproduce el problema de Live API-Bench.

**Las capas semánticas y las consultas curadas mejoran la precisión dentro de su cobertura y fallan "ruidosamente" fuera de ella.** En el benchmark ACME Insurance (11 preguntas por 20 corridas), dbt reporta **100 % dentro del alcance para la capa semántica contra 62,5 % de Text-to-SQL, y 0 % contra 70 % fuera del alcance** ([dbt Labs 2026](https://docs.getdbt.com/blog/semantic-layer-vs-text-to-sql-2026)). Cube reporta entre **+17 y +23 puntos** con modelos frontera al agregar un documento semántico ([arXiv 2604.25149](https://arxiv.org/pdf/2604.25149)). Ambas fuentes son de proveedores y los conjuntos son diminutos: indican una dirección, no una magnitud. Lo que sí es robusto es el patrón de falla. Text-to-SQL devuelve un número plausible y equivocado; la capa curada devuelve un error. En TrustSQL, bajo penalizaciones fuertes a los errores, **ningún método superó de forma consistente a abstenerse siempre** ([TrustSQL](https://arxiv.org/html/2403.15879v4)). Esto encaja con la métrica primaria del proyecto, "corrección con abstención", y con su penalización configurable de 0,5, 1,0 o 2,0 por respuesta falsa ([backend/eval/README.md](backend/eval/README.md)).

**La industria convergió en "acotar, enriquecer, verificar", no en SQL desnudo.** Los nombres cambian según el proveedor, pero el patrón es el mismo: respuestas curadas por humanos, que el sistema prefiere y que además funcionan como conjunto de evaluación.

- Snowflake Cortex Analyst combina un modelo semántico con un *Verified Query Repository* ([Snowflake Docs](https://docs.snowflake.com/user-guide/snowflake-cortex/cortex-analyst-evaluations)).
- Databricks Genie usa *trusted assets*: SQL parametrizado más funciones de catálogo ([Databricks Docs](https://docs.databricks.com/aws/en/genie/trusted-assets)).
- Grab expone consultas SQL subidas por analistas como API para el agente ([Grab Engineering](https://engineering.grab.com/transforming-the-analytics-landscape-with-RAG-powered-LLM)).
- Google MCP Toolbox define herramientas `postgres-sql` con parámetros tipados que se ejecutan como prepared statements. Los parámetros de identidad se completan desde el token y no desde el modelo ([MCP Toolbox](https://mcpservers.org/servers/googleapis/genai-toolbox); [Genkit docs](https://genkit.dev/docs/integrations/toolbox/)).

Los despliegues amplios de SQL libre muestran precisiones modestas: LinkedIn, **53 % "correcto o casi"** por revisión experta ([arXiv 2507.14372](https://arxiv.org/abs/2507.14372v1)). Las cifras altas, como el 93 % de Swiggy o el 100 % de Vercel, vienen de dominios acotados y curados o de modelos frontera ([InfoQ](https://www.infoq.com/news/2026/01/swiggy-hermes-conversational-ai); [The New Stack](https://thenewstack.io/the-key-to-agentic-success-let-unix-bash-lead-the-way/)). El caso de Vercel, que *redujo* herramientas y dio más libertad al modelo, no se traslada a un 8B: funcionó con Claude Opus 4.5 sobre una capa Cube bien documentada.

**En seguridad, las herramientas eliminan una clase de ataque y dejan otra.** P2SQL mostró que la inyección de prompt se convierte en inyección SQL y que las reglas escritas en el prompt se evaden ([P2SQL, ICSE 2025](https://arxiv.org/pdf/2308.01990)). Ars Docendi ya mitiga esto en el motor, no en el prompt. Las herramientas con argumentos tipados y SQL fijo eliminan la clase entera. La inyección indirecta, en cambio, sigue presente en todas las alternativas: texto libre de usuarios (`pedido_historial.comentario`, justificativos) que vuelve al contexto. El envenenamiento de herramientas medido en MCPTox (ASR promedio de **36,5 %**, máximo de 72,8 %) ([MCPTox, AAAI 2026](https://arxiv.org/html/2508.14925v1)) afecta casi solo a servidores MCP de terceros. Un servidor propio compilado en el monolito no hereda ese riesgo.

## Seis alternativas medidas contra el sistema que ya existe

La tabla resume la comparación. La evidencia de precisión distingue lo medido en el repo, la evidencia externa en dominios análogos y lo inferido. Las latencias y los efectos de VRAM de las alternativas 2 a 6 son **estimaciones** derivadas del modelo `T_turno ≈ Σ(TTFT + tokens_salida / tok/s) + Σ t_herramienta` y de las mediciones del perfil B en la 3070.

| Criterio | (1) Text-to-SQL libre (base) | (2) Tools sobre REST vía MCP | (3) AIFunction in-process sobre Contracts | (4) SQL certificado como tools | (5) Capa semántica / vistas | (6) Híbrido: intenciones → tools → SQL |
|---|---|---|---|---|---|---|
| **Evidencia de precisión** | **Medida**: 26/34, 7 falsas (Qwen3-8B). Claude ≈30/32 (otro dataset) | Externa: 7–47 % en composición (Live API-Bench); F1 0,92 con una sola llamada | Igual que (2) en lo que toca al modelo | Externa: patrón Genie/Cortex/Toolbox; sin cifras cabeza a cabeza. Tarea del modelo: elegir tool y extraer argumentos | Externa (vendor): 100 % dentro y 0 % fuera de cobertura | Hereda (4) en lo cubierto y (1) en el resto; sin medición |
| **Cobertura** | La más amplia: 21 tablas, cola larga | **Baja**: no hay endpoint de designaciones vigentes, de portal ajeno ni de agregaciones; `GET periodos` exige `PeriodosAdministrar` | Media: depende de queries nuevas en Contracts | Media-alta: lo que se cure (familias plantel, nombramientos, pedidos, períodos, portal) | Alta dentro del modelado | **Total** (con respaldo) |
| **Seguridad y autorización** | Motor: RLS, roles, READ ONLY. Validador léxico; TD-009; P2SQL mitigado en el motor | `[Authorize]` y ámbito en servicios; **sin RLS ni máscara**; superficie OAuth/MCP extra | Ámbito en servicios con `ClaimsPrincipal`; sin SQL; máscara a rehacer | **Conserva todo el motor**; SQL fijo con bind; elimina P2SQL; el actor nunca es argumento | Motor, si las vistas son `security_invoker`; el modelo sigue escribiendo SQL | Conserva el motor; el respaldo arrastra los riesgos de (1) |
| **VRAM y concurrencia** | Prefijo de 12k: **1 slot de 16k en 8 GB (medido)**; ~8 en vuelo en 12 GB con vLLM (estimado) | Prefijo de tools de ~2–3k (estimado), pero contexto creciente con resultados | Igual que (2) | Igual que (2), con resultados más compactos | Prefijo menor que 12k si las vistas reemplazan tablas | 0 tokens en intenciones; tools chicas; 12k solo en el respaldo |
| **Latencia por turno** | **p50 2,4–2,9 s, p95 6,6 s (medido, 3070)** | 3–6 llamadas: ~2–3× la base (estimado) | Igual que (2), sin el hop MCP (ms) | 2 llamadas (elegir y redactar), o 1 con plantillas: ≈ base o menos (estimado) | ≈ base | Intenciones ~0 s de modelo; tools ≈ base; respaldo = base + 1 llamada |
| **Esfuerzo** | Nulo | **Alto**: endpoints nuevos, servidor MCP, autenticación, máscara, runner | Medio-alto: queries en Contracts, edges a aprobar (ARS-46) | **Medio**: reutiliza el ejecutor, los manifiestos y el preámbulo del actor | Medio-alto: vistas, manifiestos y catálogo de métricas | Medio-alto, pero incremental |
| **Mantenibilidad** | Frágil ante prompt y esquema; regrabar cassettes | Spec MCP en movimiento (2 revisiones rompientes en ~8 meses) | Acoplamiento a contratos de 3 módulos | SQL versionado y testeable con BR-*; cada query es un test | Otro artefacto a mantener; riesgo de vistas que "mienten" | Más piezas, cada una simple y testeable |

### (1) Text-to-SQL libre: la línea de base que hay que batir

Es el sistema actual con el perfil B: 20 ejemplos en el prefijo, esquema compacto, plantillas de redacción, caché de SQL y especulación por n-gramas. Su ventaja estructural es que el modelo nunca puede leer lo que el rol no ve, y que la autorización la impone un motor verificado en CI.

- **Pros.** Cubre la cola larga sin escribir código nuevo. La seguridad es falsable y está probada. Es el único carril que responde hoy designaciones vigentes y portales ajenos. Tiene línea de base, cassettes y gate por ítem. El prefijo estable aprovecha al máximo el prefix caching.
- **Contras.** **Una de cada cinco preguntas de capacidad recibe una afirmación falsa** con el modelo local. Es frágil ante cambios mínimos del prompt. El prefijo de 12k limita la concurrencia a 1 slot en 8 GB. El validador es léxico, no AST. TD-009 y la inyección indirecta siguen abiertos. Además, cada migración de esquema exige reiniciar y regrabar.

### (2) Tool calling sobre los endpoints REST, expuestos por MCP

El backend publicaría un servidor MCP (SDK C# 2.2.0, estable y alineado con la spec 2026-07-28, que es *stateless*) y el host del chat lo consumiría con `IChatClient` más `UseFunctionInvocation()` ([csharp-sdk releases](https://github.com/modelcontextprotocol/csharp-sdk/releases); [ChatWithTools](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/samples/ChatWithTools/Program.cs)). El SDK inyecta el `ClaimsPrincipal` en cada tool, lo excluye del esquema y filtra `tools/list` con `[Authorize]` ([identity.md](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/identity/identity.md)). La spec prohíbe el *token passthrough*: el host tendría que usar un token emitido para el recurso MCP ([security_best_practices](https://github.com/modelcontextprotocol/modelcontextprotocol/blob/main/docs/docs/2026-07-28/tutorials/security/security_best_practices.mdx)).

- **Pros.** Hereda la autorización por permiso y el acotamiento por ámbito que ya aplican `PedidosController` y `DocentesController`. Elimina el SQL generado. Abre la puerta a otros clientes (Claude Desktop, Copilot, otros sistemas UNLaM). Ofrece un punto único de auditoría (`AddCallToolFilter`).
- **Contras.** **La cobertura es la menor de las seis.** Fuera del asistente hay unos 60 endpoints, en su mayoría de escritura. Faltan designaciones vigentes, agregaciones, historial filtrado y portales ajenos, y varios ítems del dataset (cap-003, 005, 006, 010, 022, 025–029) no tienen endpoint ([PedidosController.cs](backend/src/Modules.Designaciones/Api/PedidosController.cs); [DocentesController.cs](backend/src/ArsDocendi.Host/Api/DocentesController.cs)). Sin la máscara por (OID, attnum), los DTO podrían llevar PII al proveedor; no se verificó qué exponen. Las preguntas compuestas empujan a encadenar llamadas, que es el punto débil documentado de los 8B. MCP agrega OAuth, discovery y una spec que cambió de forma rompiente dos veces en ocho meses. Y **no le aporta nada al modelo local**: llama-server y vLLM no hablan MCP. Los convertidores OpenAPI→MCP para .NET (MCPify, AutoMcp) están en versiones 0.0.x o beta y producen justamente el mapeo 1:1 que Anthropic desaconseja ([Anthropic, Writing effective tools](https://www.anthropic.com/engineering/writing-tools-for-agents)).

### (3) Function calling in-process con `AIFunction` sobre Contracts o funciones de consulta curadas

Es la misma idea que (2) sin el hop MCP. Son métodos C# registrados como `AIFunction` que reciben el `ClaimsPrincipal` del request y llaman a `Modules.X.Contracts`. Como `McpClientTool` hereda de `AIFunction`, migrar más adelante a MCP es casi gratis ([McpClientTool.cs](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/src/ModelContextProtocol.Core/Client/McpClientTool.cs)).

- **Pros.** No hay token que propagar ni superficie OAuth. Las reglas de negocio que viven en código (estados del pedido, ventanas de período) se reutilizan sin reescribirlas en SQL. Respeta la regla 1 del repo (comunicación vía Contracts). Es lo que recomienda la literatura práctica para un único cliente first-party ([Prefect](https://www.prefect.io/resources/mcp-vs-function-calling)).
- **Contras.** Reabre la decisión D1 y requiere aprobar edges `Asistente → Designaciones/Portal.Contracts` (ARS-46). Esa aprobación está pendiente y la regla 7 del repo pide no crear abstracciones especulativas. La autorización pasa del motor al código de cada servicio, que hoy acota de forma desigual: `PeriodosController` exige permiso de administración incluso para leer. Hay que rehacer el enmascaramiento sobre los DTO. Faltan las queries que el SQL hoy resuelve, como el plantel vigente: "No existe ningún endpoint equivalente" ([manifiesto-privilegios.json](database/asistente/manifiesto-privilegios.json)). Para las agregaciones, cada función nueva en Contracts amplía la superficie pública de un módulo ajeno.

### (4) Consultas SQL certificadas y parametrizadas expuestas como herramientas

Es el término medio de MCP Toolbox, Genie y Grab, implementado en proceso y sin la dependencia de Toolbox. Cada herramienta es una consulta escrita y revisada por el equipo, versionada, con parámetros tipados ligados como `$n`. Se ejecuta con el `EjecutorDeConsulta` existente: misma conexión de rol de solo lectura, mismo `set_config` del actor, mismo timeout y mismo tope de filas. El modelo solo elige la herramienta y extrae argumentos (texto de materia, apellido, estado, período), y esos argumentos se resuelven contra entidades visibles al actor con la recuperación de valores ya propuesta (ARS-165, Damerau-Levenshtein en proceso). Las semillas ya existen: los 20 pares de `ejemplos-sql.json` y los destinos de las 5 intenciones. Hay que respetar el invariante de que el dataset de evaluación y el catálogo de ejemplos sean disjuntos ([backend/eval/README.md](backend/eval/README.md)).

- **Pros.**
  - **Conserva entera la frontera del invariante #14** sin pedir edges nuevos: sigue siendo "consulta contra el motor", ahora con SQL fijo.
  - El enmascaramiento por (OID, attnum) funciona sin cambios, y una consulta revisada puede evitar por construcción las expresiones que hoy producen TD-009.
  - La política de abstención se conserva y se enriquece: "no encontré la materia X entre las que podés ver" es distinguible de "no hay registros".
  - Desaparecen P2SQL, los literales mal copiados y los joins inventados.
  - Cada herramienta es un test con regla BR-* y fuente, en línea con la regla 8.
  - El prefijo baja de unos 12k a unos 2–3k tokens (estimado).
  - Los resultados son "Trusted" en el sentido de Genie: la misma pregunta da la misma consulta.
- **Contras.**
  - La cobertura se limita a lo curado, así que hace falta un respaldo.
  - Elegir la herramienta es clasificar la intención con el LLM, una práctica que el repo descartó (60 % de F1): hay que medirla en este dominio antes de comprometerse.
  - Si se crea una herramienta por pregunta, el catálogo explota (con 20 herramientas visibles, la selección cae al 45 % en RAG-MCP). La disciplina es de 6 a 10 herramientas de nivel tarea con filtros opcionales.
  - Las consultas requieren mantenimiento cuando cambia el esquema, aunque ese mantenimiento es explícito y lo detecta CI, a diferencia del prompt.

### (5) Capa semántica

En su forma pesada (dbt Semantic Layer, Cube, Wren AI MDL) agrega un runtime y un lenguaje nuevos, en conflicto con la regla 7 del repo. La forma "pobre" pero coherente con el stack son **vistas curadas en PostgreSQL** con nombres de negocio en español y métricas definidas (por ejemplo `plantel_vigente`, `pedidos_con_estado_legible`), sobre las que el modelo sigue escribiendo SQL.

- **Pros.** La evidencia de proveedores indica la mayor ganancia relativa en las preguntas modeladas, y el beneficio debería ser mayor para un 8B que para un modelo frontera, porque transfiere joins y semántica a artefactos deterministas (inferencia, no medida). Reduce el prefijo. Corrige la deuda TD-005 (nombres mixtos inglés/español) sin migrar tablas. Ataca comentarios que "mienten" al modelo, como TD-025.
- **Contras.** El modelo **sigue escribiendo SQL**, así que siguen presentes P2SQL (mitigado), los literales y la fragilidad del prompt. Las vistas de PostgreSQL, por defecto, verifican permisos como su dueño. Dado que las policies usan `ENABLE` y no `FORCE`, y que el backend conecta como dueño, una vista sin `security_invoker = true` podría saltear RLS ([PostgreSQL, CREATE VIEW](https://www.postgresql.org/docs/current/sql-createview.html)). Es un riesgo a verificar con un test. Cada columna de vista necesita clasificación en ambos manifiestos, y las columnas calculadas reproducen el problema de TD-009. Fuera de lo modelado, el beneficio es nulo. El repo ya descartó el *schema pruning* por romper el prefijo estable: una vista estable no lo rompe, pero un selector dinámico de vistas sí.

### (6) Enrutador híbrido: intenciones deterministas → herramientas → SQL de respaldo

Encadena lo que ya existe y lo que propone (4). Primero, el `EnrutadorDeDominio` determinista (0 tokens) resuelve intenciones con slots completos. Después, una llamada con las 6 a 10 herramientas certificadas y una salida explícita `ninguna`. Por último, el carril SQL actual como respaldo, con abstención y con la respuesta rotulada. La política vigente del repo ("el default es SQL, nunca API", porque enrutar mal hacia la API devuelve cero filas indistinguibles de "no hay") se conserva por diseño: las herramientas de (4) se ejecutan bajo el mismo RLS y resuelven entidades antes de consultar, así que un enrutamiento equivocado produce "entidad no encontrada" o un respaldo, no un falso "no hay".

- **Pros.** Es el patrón al que convergió la industria (verified queries con respaldo generativo). Cobertura total. El costo de GPU baja de forma desigual pero real: las preguntas frecuentes no pagan el prefijo de 12k. Cada carril se mide por separado en el evaluador. Es incremental: se puede encender por intención o por herramienta, con banderas, como las optimizaciones opt-in actuales.
- **Contras.** Tiene más piezas y más modos de falla en las fronteras: herramienta equivocada con resultado plausible, o respaldo que contesta lo que la herramienta se abstuvo de contestar. Una llamada extra en el camino de respaldo añade aproximadamente 1 a 1,5 s en la 3070 (estimado). El respaldo hereda todos los riesgos de (1). El techo de 4 llamadas por turno obliga a diseñar con cuidado: elegir herramienta, respaldo SQL y redacción ya suman 3 sin reescritura.

## El prefijo de 12k, no el modelo, decide cuántos usuarios entran en la placa

### Lo que está medido y lo que está estimado

**No existe una RTX 3070 de 12 GB oficial.** La 3070 es de 8 GB. Las Ampere de 12 GB son la 3060 y la 3080, y en Blackwell la 5070 ([Wikipedia RTX 50](https://en.wikipedia.org/wiki/GeForce_RTX_50_series); [AnandTech](https://www.anandtech.com/show/17204)). El repo trata la 3070 como PC de prueba para una persona y la **RTX 5070 de 12 GB como objetivo de producción para 2 a 30 usuarios** ([modelo-local.md](docs/architecture/modelo-local.md)). Este informe cubre ambos presupuestos.

La aritmética de memoria cierra con una medición del propio repo, y eso le da confianza. Qwen3-8B tiene 36 capas, 8 cabezas KV y head_dim 128, lo que da **144 KiB por token en FP16** y unos 76,5 KiB en q8_0 ([Raschka](https://sebastianraschka.com/llm-architecture-gallery/kv-cache-calculations)). En la 3070, dos slots de 13.312 tokens "no entraron (falló al reservar 1.989 MiB de KV)". El cálculo da 26.624 tokens × 76,5 KiB = **1.989 MiB exactos** ([modelo-local.md §8](docs/architecture/modelo-local.md)).

| Escenario | Pesos | KV disponible | Contextos que entran | Estado |
|---|---|---|---|---|
| 3070 8 GB, Qwen3-8B Q4_K_M, llama-server, KV q8_0, escritorio en la GPU (~1,3 GB) | ~5 GB | ~1,2 GiB | **1 slot de 16.384**; 2 de 13.312 no entran; pico de 7,3 GiB | **Medido** |
| Igual, GPU sin monitor | ~5 GB | ~2,4 GiB | ~32k tokens: 2 slots de 16k | Estimado |
| Igual, carril de herramientas (prefijo ~3k, contexto de ~6–8k por conversación) | ~5 GB | 1,2–2,4 GiB | **2 slots con escritorio, 3–4 sin monitor** | Estimado |
| 3070, Qwen3.5-9B Q4_K_M (híbrido, 8 de 32 capas con atención completa) | ~5,8 GB | KV ~0,98 GB a 32k | 32k entra; varios slots plausibles | Terceros ([insiderllm](https://insiderllm.com/guides/qwen-3-5-9b-setup-guide/)); no medido |
| 5070 12 GB, vLLM Qwen3-8B-AWQ, KV FP8, APC | ~5,9 GB | ~3,5–4 GiB (~50–58k tokens) | SQL: **~8–12 en vuelo**; agente con contexto creciente: ~3–7 | Estimado (repo y notas) |
| 5070, llama-server Qwen3-8B Q4_K_M | ~5 GB | — | 4 slots de 16k; 8 slots (~9,6 GiB) no entran | Estimado (repo) |
| 5070, Qwen3-14B Q4_K_M, llama-server | ~9 GB | ~1,5–2 GB | ~1 slot de 16k | Estimado; el repo declara "inviable" la AWQ en vLLM |

De la tabla salen dos lecturas que contradicen intuiciones habituales. La primera es que **el carril de herramientas, aunque hace más llamadas, ocupa menos memoria por conversación** que el carril SQL: se ahorran unos 9k tokens de prefijo que en llama-server se duplican por slot. En 8 GB eso es la diferencia entre 1 y 2 a 4 usuarios simultáneos. La segunda es que **la GPU no debe manejar el monitor**: el escritorio consume 1,3 GB, que equivalen a más de un contexto de 16k ([modelo-local.md §4](docs/architecture/modelo-local.md)).

La literatura de serving advierte que un agente multiplica la ocupación de la GPU por turno y que la caché LRU desaloja el KV justo durante la pausa de la herramienta ([KVFlow](https://arxiv.org/abs/2507.07400); [Continuum](https://arxiv.org/abs/2511.02230)). Las herramientas de (4) se ejecutan en milisegundos contra PostgreSQL local, así que ese efecto es menor que en agentes con herramientas lentas.

### Concurrencia y latencia por alternativa

Con 30 usuarios a un turno por minuto, la carga es de unos **0,5 turnos/s**. La 5070 con vLLM y 8 en vuelo estaría dentro del agregado estimado de **250–350 tok/s** ([modelo-local.md §5](docs/architecture/modelo-local.md)). La 3070 con un slot y un p50 de 2,4–2,9 s procesa en serie; si el turno promedio dura unos 3–4 s, eso da **15–20 turnos por minuto** antes de que crezca la cola (estimado). Alcanza para uso real de 10 a 30 personas, cuya simultaneidad suele ser mucho menor que un turno por minuto, pero no para picos.

Las palancas en orden de impacto (estimado) son cuatro:

1. Sacar preguntas frecuentes del carril de 12k, es decir, las alternativas (4) y (6).
2. Redactar con plantillas cuando el resultado es un valor o una lista corta (ya existe `RedaccionConPlantillas`).
3. Liberar la GPU del monitor.
4. Solo después, cambiar de motor o de modelo.

En la 3070, la decodificación medida va de **58,3 a 67,6 tok/s con especulación por n-gramas** ([modelo-local.md §8](docs/architecture/modelo-local.md)). Una llamada de selección de herramienta, con unos 50 a 100 tokens de salida, cuesta aproximadamente 1–1,5 s más el prefill incremental (estimado). Por eso un turno de (4), con elección y plantilla, debería quedar en el rango de la base o por debajo, y un agente de 4 o 5 pasos duplicaría o triplicaría la latencia.

### Motores

En **8 GB, llama-server es la única opción realista**. El supuesto del repo de que llama-server "guarda N copias" del prefijo describe el modo particionado. Las builds recientes tienen KV unificado (`--kv-unified`, activo con slots automáticos) y caché de prompts en RAM del host (`--cache-ram`, `--cache-idle-slots`) ([manpage llama-server](https://manpages.debian.org/unstable/llama.cpp-tools/llama-server.1.en.html); [PR #16391](https://github.com/ggml-org/llama.cpp/pull/16391)). No hay evidencia de que deduplique el prefijo entre slots *activos*, así que hay que medirlo. Además, el repo exige `--cache-ram 0` cuando se activa `RedaccionSinEnmascarar`, por el issue #27148 de KV entre slots. Esa tensión debe resolverse antes de apoyarse en la caché del host.

En **12 GB Blackwell (sm_120)**, vLLM sigue siendo la elección del repo porque su prefix caching comparte bloques entre requests. El soporte es frágil:

- wheels sin kernels sm_120 ([vLLM #35432](https://github.com/vllm-project/vllm/issues/35432));
- KV FP8 que autoselecciona FlashInfer y falla, con el workaround de forzar `TRITON_ATTN` ([vLLM #60262](https://github.com/vllm-project/vllm/issues/60262));
- KV FP8 que corrompe la salida en modelos híbridos como Qwen3.5 ([vLLM #37554](https://github.com/vllm-project/vllm/issues/37554));
- **ausencia de strict mode** para herramientas: la validez de los argumentos depende del modelo y del parser ([vLLM docs](https://docs.vllm.ai/en/v0.20.2/features/tool_calling/)).

Para Qwen3 se usa `--enable-auto-tool-choice --tool-call-parser hermes`. En llama.cpp hacen falta `--jinja` y una build reciente: sin ella, los delimitadores `<tool_call>` se filtran como texto. Con cuantizaciones por debajo de Q4, las tool calls salen malformadas ([netclaw](https://netclaw.dev/troubleshooting/llama-cpp/)). Ollama queda descartado en el repo por un bug de VRAM en RTX 50 sobre Windows y por menor control de slots. A concurrencia 1, llama-server y vLLM rinden parecido; la ventaja de vLLM aparece con 4 o más requests simultáneos y solo si el KV entra ([Red Hat](https://developers.redhat.com/articles/2025/09/30/vllm-or-llamacpp-choosing-right-llm-inference-engine-your-use-case), cifras de un resumen secundario).

### Modelos para tool calling

| Presupuesto | Recomendación | Evidencia | Advertencias |
|---|---|---|---|
| 8 GB | **Qwen3-8B Q4_K_M** (base probada) | Repo: medido en SQL. Docker: F1 0,919 en tools. BFCL-v3 ≈66,3 ([LoopTool](https://arxiv.org/pdf/2511.09148)) | Atención completa en todas las capas: el KV limita los slots |
| 8 GB | **Qwen3.5-9B Q4_K_M** (candidato a medir) | Oficial: BFCL-V4 66,1, TAU2 79,1 ([model card](https://huggingface.co/Qwen/Qwen3.5-9B)); KV mucho menor | Cifras autorreportadas. Bugs de parsing en llama.cpp (#22684, tool call dentro de `reasoning_content`). Fuga de estado recurrente entre slots en HIP (#29092). Prefix caching frágil en híbridos según el repo. **Probar el aislamiento entre slots antes de producción** |
| 8 GB | Qwen3-4B-Instruct-2507 (si se prioriza concurrencia) | BFCL-v3 sin thinking 57,6 (oficial) | Mismo KV por token que el 8B: el ahorro está en los pesos |
| 12 GB | **Qwen3.5-9B a Q6/Q8 con más slots**, o Qwen3-8B AWQ en vLLM | Igual que arriba | Bugs de FP8 KV en híbridos con vLLM |
| 12 GB | Qwen3-14B Q4_K_M si la concurrencia es baja | Docker: F1 0,971, el mejor local | Deja ~1 slot de 16k; inviable en vLLM AWQ según el repo |
| Excluidos | xLAM-2-8B, watt-tool-8B, gpt-oss-20b | xLAM 0,570 y watt 0,484 en Docker; xLAM con licencia CC-BY-NC; gpt-oss-20b ~14,9 GB a 8k ([smeltcore](https://smeltcore.com/recipes/gpt-oss-20b-on-rtx-3080-ti-mxfp4-chat-in-12-gb-via-llama-cpp-expert-offload/)) | — |

En todos los casos conviene **thinking apagado por defecto**. En el repo, el razonamiento en la segunda generación no sumó aciertos y subió el p95 de 7,8 a 9,6 s. Con thinking, Qwen3-8B mejora en τ-bench (airline de 12,0 a 25,0), pero a costa de tokens que compiten por la misma GPU ([Klear-AgentForge](https://arxiv.org/pdf/2511.05951)). Los nombres y las descripciones de las herramientas deben ir en español coherente, cortos y "convencionales". Alinear los esquemas con patrones de preentrenamiento mejoró hasta 17 % a modelos chicos ([ACL 2026](https://aclanthology.org/2026.acl-long.948/)). El español es de los idiomas que menos pierden (unos 4,6 puntos en τ-Multilingual) ([arXiv 2609.35820](https://arxiv.org/pdf/2609.35820)).

## Un banco de pruebas de bajo costo, con Claude como techo y Qwen como objetivo

El evaluador existente tiene las propiedades que hacen falta para una comparación rigurosa:

- comparación por **conjunto de filas** contra una consulta de referencia ejecutada en vivo con el mismo actor;
- puntuación con tres penalizaciones (0,5, 1,0 y 2,0);
- *preflight* que rechaza proveedores caídos o sin crédito;
- sello con hashes de prefijo, dataset y fixture;
- **gate por ítem** en lugar de umbral agregado;
- cuatro ejes que no se promedian ([backend/eval/README.md](backend/eval/README.md)).

El plan lo reutiliza sin debilitarlo.

**Adaptación mínima del arnés.** Hoy el runner de capacidad llama directamente a `GeneradorDeSql`. Para comparar alternativas hace falta un punto de extensión: una estrategia de turno que devuelva un resultado común (filas antes de redactar, estado contestó/se abstuvo/falló, y una "interpretación"). La puntuación por filas queda igual. En el eje de diálogo, los `terminos_prohibidos` se buscan hoy en la pregunta interpretada; para el carril de herramientas, la superficie equivalente son **los argumentos de la herramienta**, donde también se haría visible el arrastre. El eje social no cambia, porque el enrutador social precede a cualquier carril, y su verificación de cero tokens sigue valiendo. El bucle de herramientas debe pasar por `IProveedorDeModelo` y no por un `FunctionInvokingChatClient` desnudo. El README del evaluador lo explica: "medir sobre un proveedor desnudo mediría otro sistema". Así aplican la compuerta, el techo de 4 llamadas, el breaker y los cassettes, que grabarían también los bloques `tool_use`.

**Brazos del experimento:**

| Brazo | Qué se mide | Prioridad |
|---|---|---|
| A1 | Text-to-SQL actual, perfil B (control) | Obligatorio |
| A4 | 6–10 herramientas certificadas + abstención, sin respaldo | Obligatorio |
| A6 | Intenciones → A4 → A1 de respaldo | Obligatorio |
| A5-lite | A1 con el prefijo apuntando a 3–5 vistas curadas `security_invoker` | Opcional, barato |
| A3 | Igual a A4, pero las herramientas llaman a Contracts o servicios donde existe la regla en código | Solo para las familias sin SQL equivalente |
| A2 | Igual a A3 vía MCP | **Una sola corrida de latencia y paridad**: MCP no cambia la precisión del modelo |

Cada brazo se corre con tres modelos: **Claude (`claude-sonnet-5`, el default del repo) como techo**, **Qwen3-8B Q4_K_M en la 3070** (objetivo medido) y **Qwen3.5-9B Q4_K_M en la 3070**. Si hay acceso a la 5070, se repite con Qwen3-8B AWQ en vLLM. Restar Claude menos local, por brazo, mide algo que hoy no se conoce: **cuánto depende cada alternativa de la capacidad del modelo**. La hipótesis a falsar es que (4) y (6) cierran la brecha local–Claude que (1) deja abierta.

**Datasets.** Se mantienen los 80 ítems y se agregan cuatro cosas:

1. Una etiqueta de **cobertura esperada** por ítem (intención, herramienta o solo SQL), que convierte el 0/39 actual en un número accionable.
2. **2 a 4 paráfrasis por pregunta** cubierta por herramientas, al estilo de los benchmarks de Genie ([Databricks Docs](https://docs.databricks.com/aws/en/genie/benchmarks)), para llegar a unos 150–200 ítems.
3. Un subconjunto **adversarial de inyección indirecta**, con instrucciones sembradas en `pedido_historial.comentario` y justificativos del fixture. Hoy no hay pruebas documentadas de este tipo.
4. Ítems de **fuga entre actores**: el mismo pedido consultado por un actor global y por uno acotado, verificando conteos y máscaras.

Las descripciones y los ejemplos de las herramientas deben quedar disjuntos del dataset, con el mismo test que hoy protege `ejemplos-sql.json`.

**Métricas por brazo y modelo:**

- puntaje con penalizaciones de 0,5, 1,0 y 2,0;
- **respuestas falsas** como métrica primaria;
- abstenciones sobre lo factible;
- precisión de selección de herramienta y de argumentos, por separado;
- tasa de respaldo;
- llamadas y tokens de entrada y salida por turno;
- p50 y p95;
- en la 3070 y la 5070, una **prueba de carga con c = 1, 2, 4 y 8** (el piloto pendiente del repo), midiendo espera en cola, VRAM pico y tok/s agregados.

Como el intervalo de confianza de un dataset de este tamaño es de varios puntos, las comparaciones deben ser **pareadas por ítem** (mismo ítem, brazo A contra brazo B), en línea con el gate por ítem del repo. No conviene comparar agregados.

**Criterios de decisión propuestos**, a ratificar por el equipo, porque la penalización es una decisión de producto aún abierta:

1. Promover (6) solo si, con el modelo local y penalización 2,0, **las respuestas falsas bajan respecto de A1 sin aumentar las abstenciones sobre lo factible en más de lo que bajan las falsas**.
2. Exigir que el p95 en la 3070 no supere el actual (6,6 s).
3. Exigir cero regresiones en los ítems de fuga y de inyección.
4. Considerar A2 (MCP) únicamente si aparece un segundo cliente.

**Costo.** El repo estima con Claude **"del orden de un centavo de dólar por turno"** ([tech-debt.md TD-008](docs/quality/tech-debt.md)). A partir de ahí, la cuenta es esta (estimada): una corrida de 80 ítems de A1 cuesta cerca de US$1; A4 y A6, con 2 a 3 llamadas pero prefijos más chicos, del mismo orden. Tres brazos, por tres repeticiones de Claude para medir varianza, más el dataset ampliado a unos 200 ítems, dan **del orden de US$20 a 40 en total**. Las corridas locales son deterministas (tres repeticiones sin cambios en el repo) y no tienen costo marginal. Las corridas de Claude deben grabar cassettes, que se commitean y vuelven gratuitas las reproducciones. La clave se configura como `Asistente__Proveedor=anthropic` y `Asistente__ClaveDelProveedor`, no como `ANTHROPIC_API_KEY`, que no está en este contenedor. Solo viajan al proveedor datos del fixture sintético.

## Recomendación y plan por fases

La recomendación es **construir (6) con (4) como carril principal, en proceso y sin MCP**:

- dejar (1) como respaldo rotulado;
- usar (3) solo para las preguntas cuya regla vive en código y no en datos;
- usar vistas de (5) solo donde simplifiquen una consulta certificada;
- postergar (2) hasta que exista un segundo cliente.

La decisión se condiciona al resultado del banco de pruebas, no al revés.

| Fase | Duración (estimada) | Entregable | Criterio de salida |
|---|---|---|---|
| 0. Medir bien la base | 1–2 semanas | Correr Claude sobre el dataset vigente de 34 ítems; reconciliar las líneas de base (ver inconsistencias); punto de extensión de estrategia de turno en el arnés; etiquetas de cobertura; ítems de inyección y fuga; change OpenSpec (regla 5) | Línea de base de Claude y Qwen sobre el mismo dataset, sellada |
| 1. Herramientas certificadas | 2–4 semanas | 6–10 herramientas SQL parametrizadas (plantel por materia, designaciones de un docente, pedidos por estado/persona/novedad, períodos, habilidades/certificaciones del portal), ejecutadas con `EjecutorDeConsulta`; soporte de tool calls en `IProveedorDeModelo` (local y Anthropic); recuperación de valores ARS-165 para los argumentos | A4 contra A1 pareado: menos falsas con Qwen3-8B, p95 ≤ 6,6 s |
| 2. Híbrido y hardware | 2–3 semanas | Enrutador de intenciones fuera de modo sombra hacia A4; respaldo SQL rotulado; prueba de carga c = 1/2/4/8 en la 3070 (y en la 5070 si está disponible); Qwen3.5-9B con prueba de aislamiento entre slots | A6 cumple los criterios; concurrencia medida |
| 3. Opcional | Según demanda | Exponer las mismas herramientas con `[McpServerTool]` en el Host, con `ClaimsPrincipal`, `[Authorize]` y token con audiencia propia; desborde a Anthropic en picos si la política de datos lo admite | Hay un segundo cliente o una política aprobada |

## Riesgos, preguntas abiertas y advertencias sobre fuentes y documentación

**Riesgos principales:**

| Riesgo | Por qué importa | Mitigación |
|---|---|---|
| La selección de herramientas reproduce el 60 % de F1 que el repo descartó | Sería un falso positivo plausible: herramienta equivocada con filas reales | Medir la selección por separado; mantener 6–10 herramientas visibles y una salida `ninguna`; dejar el enrutamiento determinista primero |
| Abstención mal calibrada en las fronteras | El respaldo contesta lo que la herramienta rechazó con razón | Abstención de herramienta "dura" para entidades no visibles; el respaldo no se invoca después de un rechazo por ámbito |
| Vistas sin `security_invoker` | Podrían saltear RLS, porque las policies no son `FORCE` | Test de motor obligatorio y entrada en el manifiesto |
| Fuga entre slots en modelos híbridos | Un caso reportado en HIP filtró texto de otro prompt | Test de aislamiento; `--cache-ram 0` con datos sin máscara |
| Inyección indirecta | Sigue presente en todas las alternativas | No devolver `sensible-texto`; sin canales de salida (sin fetch de URLs ni imágenes remotas); sin herramientas de escritura |
| Bucles de agente | El techo de 4 llamadas se agota | Diseñar para 1 o 2 llamadas; límite duro y respuesta de degradación |
| Fragilidad de sm_120 | Bugs de vLLM con FP8 KV y kernels | Tener llama-server como plan B y fijar versiones |

**Preguntas abiertas para el equipo:**

1. ¿Cuál es la penalización de producto por una respuesta falsa (0,5, 1,0 o 2,0)?
2. ¿Se aprueban edges hacia Contracts (ARS-46) o el asistente se queda dentro de la frontera de motor?
3. ¿Qué preguntas hacen los usuarios reales? No hay corpus real (ARS-65 y el hueco 1 de la definición siguen abiertos).
4. ¿El hardware de producción es una 5070 de 12 GB, o la consigna de "3070 de 12 GB" se refiere a otra placa (3060 o 3080 de 12 GB)?
5. ¿Se admite desbordar a Anthropic en picos, sabiendo que la pregunta cruda viaja sin máscara?
6. ¿La GPU de producción quedará sin monitor?

**Advertencias sobre las fuentes.**

- El proxy de la investigación bloqueó arxiv.org, huggingface.co, docs.getdbt.com, cube.dev, learn.microsoft.com, OWASP y los blogs de Uber, LinkedIn, Vercel y Grab. Muchas cifras salen de **resúmenes de buscador** sobre la fuente primaria: las de dbt y Cube, BEAVER, Live API-Bench (hay discrepancia entre la v1, con 0–7 %, y la v2, con 7–47 %), Red Hat, Uber, Swiggy y Vercel, entre otras.
- Son de **proveedores**: Snowflake con su "90 %+", dbt, Cube, AtScale y Databricks Genie One con 84,5 % sobre 28 preguntas.
- Son **autorreportadas**: BFCL y TAU2 de Qwen3.5, que además usa una corrección propia en airline.
- La existencia de un "Gemma 4 12B" no se verificó en una fuente primaria.
- La evaluación de Docker es de junio de 2025, en una MacBook M4 Max y sobre un escenario de e-commerce.
- La spec MCP y el SDK C# sí se verificaron leyendo el código fuente en GitHub.

Antes de citar cualquiera de estos números en una decisión formal, conviene verificarlo en el PDF o la página original.

**Inconsistencias internas del repo** que el plan de la fase 0 debería resolver:

- La línea de base de Claude da en el README **31/32, 13/15, 8/9 y 20/20**, pero sus JSON dan **30/32, 14/15, 11/11 y 20/20** ([lineas-de-base/README.md](backend/eval/lineas-de-base/README.md)).
- Claude se midió sobre **32 ítems de capacidad y el modelo local sobre 34**: hoy no existe comparación local contra Claude sobre el mismo dataset.
- Hay **109 cassettes**, pero la línea de base se congeló "reproduciendo los 107".
- El tamaño del dominio aparece como "catorce tablas, poco más de cien columnas", como "20 tablas" en las líneas de base y como **21 tablas y 151 columnas** en los manifiestos.
- El README dice que `Modules.Asistente.Contracts` no tiene ningún `.cs`, pero hoy contiene dos.
- La auditoría habla de "dos llamadas al modelo" y numera tres.
- La rama `infra/ollama` despliega Ollama con `qwen3:4b` en Debian 13 y solo expone `/api/chat` y `/api/generate`. En cambio, `modelo-local.md` descarta Ollama, elige vLLM sobre Ubuntu 24.04, y `ProveedorLocal` llama a `/chat/completions`, que esa configuración de Traefik no deja pasar ([ollama-compartido.md, origin/infra/ollama](docs/operations/ollama-compartido.md)).
- La premisa de que llama-server "guarda N copias" del prefijo está parcialmente desactualizada por el KV unificado.
- Los comentarios del esquema dicen «Categoría 0» a «6», cuando el catálogo va de 1 a 6 (TD-025): le mienten al modelo de cualquier alternativa que lea el esquema.

## Conclusión

La discusión "Text-to-SQL contra MCP" está mal planteada para Ars Docendi. MCP es transporte, y lo que realmente se decide es **quién escribe la consulta**. Hoy la escribe un modelo de 8B que copia literales con poco cuidado. La alternativa es que la escriba el equipo, una vez, revisada y bajo el mismo RLS. El valor del sistema actual está en su frontera de motor y su evaluador, no en que el modelo genere SQL. Por eso la evolución con mejor relación costo-riesgo es la que mantiene esa frontera y achica la libertad del modelo. Exponer endpoints REST, en cambio, cambiaría una seguridad verificada en CI por una autorización dispersa en controladores, y además con menos cobertura. Hay también una consecuencia de hardware que no era obvia: sacar las preguntas frecuentes del prefijo de 12k es, a la vez, la mejora de precisión más probable y la mayor ganancia de concurrencia disponible en 8 GB, antes de tocar el motor o el modelo.

Lo que todavía no se sabe es si un 8B elige bien entre 6 y 10 herramientas en español rioplatense dentro de este dominio. La evidencia externa (F1 de 0,92 con pocas herramientas) contradice el 60 % que el repo registró al clasificar intenciones con el LLM. Esa contradicción es la "evidencia nueva" que la propia definición exige antes de reabrir un descarte, y se puede resolver con una o dos semanas de trabajo y unas decenas de dólares de API, usando un arnés que ya existe y está bien diseñado.
