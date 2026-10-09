# Diseño de la superficie de herramientas para preguntas en lenguaje natural sobre Ars Docendi: maximizar precisión en (A) agente sobre REST/MCP y (B) híbrido intenciones → herramientas certificadas → Text-to-SQL con abstención

Notas de investigación. Las rutas de repo son relativas al worktree `feature/asistente-modelo-local` (`/tmp/claude-0/-home-user-ars-docendi/caaad005-9e8c-59cb-9cfd-725e9fa71d55/scratchpad/asistente`) y se citan con la ruta como "URL". Para el contexto general, ver `research_notes/Alternativas a Text to SQL local/arquitectura_actual.md` y `reports/Alternativas a Text to SQL local.md` (alternativas 2, 4 y 6).

Avisos sobre el acceso a fuentes:
- **Bloqueados por el proxy de egreso**: `engineering.block.xyz` (playbook de Block, también vía web.archive.org), `arxiv.org` y `alphaxiv.org`. De los papers de arXiv solo vi resúmenes y extractos del buscador. Las cifras que vienen de ahí están marcadas como "(extracto)".
- **No investigados en esta ronda**: los posts de GitHub, Stripe, Linear y Cloudflare, y la guía de Microsoft (quedan como gaps).
- **Cifras de segunda mano**: algunas vienen del informe previo (`reports/Alternativas a Text to SQL local.md`) y no las volví a verificar. Están marcadas como "(vía informe previo)".

---

## 1. Granularidad: espejo fino de REST vs. herramientas de nivel tarea vs. una herramienta genérica de "consulta estructurada" (capa semántica como herramienta)

### Takeaway
La evidencia disponible favorece un punto medio. Conviene tener pocas herramientas de nivel tarea, una por familia del dominio, con filtros tipados, enums y una agregación/agrupación cerrada que se resuelva en el backend. Rinde peor tanto el espejo 1:1 de endpoints CRUD como una única herramienta que lo hace todo. Para un modelo chico (Qwen3-8B), el factor que más pesa es que la pregunta típica se resuelva en **una** llamada y que el modelo no tenga que contar, sumar ni filtrar filas por su cuenta.

### Cited Findings
- **Guía de Anthropic: consolidar alrededor de flujos de trabajo.**
  - "More tools don't always lead to better outcomes". Hay que construir herramientas para flujos específicos y de alto impacto, no envoltorios finos de una API. Ejemplos: preferir `search_contacts` a `list_contacts`, y una `schedule_event` que reemplace la cadena `list_users` → `list_events` → `create_event`.
  - Paginación, filtrado y truncado con defaults sensatos. Claude Code limita las respuestas de herramientas a "25,000 tokens by default".

  — [Anthropic, Writing effective tools for agents](https://www.anthropic.com/engineering/writing-tools-for-agents)
- **MCP-GRANITE (preprint, extracto).** Varía la granularidad de la misma lógica de dominio para 9 modelos.
  - El nivel intermedio (L3, unas 4 herramientas consolidadas) dio la mayor completitud en 8 de los 9 modelos: 0,49, que es +16,4 % frente al nivel más fino L4 (8–10 herramientas) y +33,6 % frente a L1.
  - El nivel L1, totalmente consolidado en una sola herramienta, fue el peor: 0,36, −12,9 % frente a L4. Además, la tasa de "cero llamadas a herramienta" subió de 10,2 % a 28,2 %.
  - En una regresión logística que controla por tamaño de modelo, L3 multiplica las odds de completar por 1,36 (IC [1,12; 1,66], p = 0,002) y L1 las reduce 22 % (p = 0,009).

  No pude ver qué modelos ni de qué tamaño. — [MCP-GRANITE, arXiv 2609.24161](https://arxiv.org/pdf/2609.24161)
- **Selección de herramientas con muchas visibles.** La precisión de selección cae de 85 % con 5 herramientas a 45 % con 20. Recuperar entre 3 y 5 candidatas la mantiene por encima de 80 % (vía informe previo). — [RAG-MCP](https://www.alphaxiv.org/abs/2505.03275)
  - Un blog secundario cita otro corte del mismo trabajo: 13,62 % de acierto con el conjunto grande completo, contra 43 % filtrando. Son configuraciones distintas del mismo paper; no lo pude verificar contra el original. — [tianpan.co](https://tianpan.co/blog/2026/04/19/over-tooled-agent-problem)
- **Modelos locales con pocas herramientas** (vía informe previo). Con unas 5 herramientas, Qwen3-8B Q4_K_M obtuvo F1 0,919 y Qwen3-14B 0,971. Los especialistas de function calling rindieron peor: xLAM-2-8B 0,570 y watt-tool-8B 0,484. — [Docker, Local LLM tool calling](https://www.docker.com/blog/local-llm-tool-calling-a-practical-evaluation/)
- **Composición sobre APIs** (vía informe previo). Live API-Bench convirtió preguntas de BIRD en secuencias de API. La completitud fue de 7 a 47 %, y cerca de 50 % con ReAct. Los modelos "a veces explotan mejor el SQL que las API". — [Live API-Bench](https://arxiv.org/pdf/2506.11266)
- **Multiturno con un modelo chico** (vía informe previo). Qwen3-8B obtiene 35,7 % (retail) y 12,0 % (airline) en τ-bench sin thinking. — [Klear-AgentForge](https://arxiv.org/pdf/2511.05951)
- **Capa semántica como herramienta (patrón dbt).** El servidor MCP de dbt trabaja en dos fases:
  - Descubrimiento: `list_metrics` "should be used as a first step", más `get_dimensions` y `get_entities`.
  - Consulta: una sola herramienta, `query_metrics(metrics, group_by, order_by, where, limit)`, con reglas como "any dimension or entity used in order_by must also appear in group_by".
  - `get_metrics_compiled_sql` devuelve el SQL sin ejecutarlo.

  — [dbt docs, about MCP](https://docs.getdbt.com/docs/dbt-ai/about-mcp); [Glama, query_metrics](https://glama.ai/mcp/servers/dbt-labs/dbt-mcp/tools/query_metrics)
- **Cube.** Los servidores MCP comunitarios sobre Cube exponen un descubrimiento (`/meta`) y una consulta (`/load`) con `measures`, `dimensions`, `filters`, `timeDimensions`, `order`, `limit`, etc. El agente "names governed members rather than writing SQL". No pude confirmar el esquema del servidor oficial de Cube. — [Cube.js MCP server (comunitario)](https://github.com/zsembek/Cube.js-MCP-server); [Cube, analytics MCP server](https://cube.dev/articles/analytics-mcp-server)
- **Benchmarks de capa semántica: todos de proveedores.**
  - dbt reporta 100 % dentro del alcance para la capa semántica contra 62,5 % de Text-to-SQL, y 0 % contra 70 % fuera del alcance (vía informe previo). — [dbt Labs 2026](https://docs.getdbt.com/blog/semantic-layer-vs-text-to-sql-2026)
  - Knowi cita +17 a +23 puntos al agregar un documento semántico. — [Knowi](https://www.knowi.com/blog/semantic-layer-for-ai/)
  - CData, sobre 378 prompts: 98,5 % para su enfoque contra 59–75 % para otros. Entre sus modos de falla: un filtro "this quarter" descartado que devolvió todos los deals, y prompts con varios filtros combinados. — [CData whitepaper](https://www.cdata.com/lp/ai-accuracy-whitepaper/)
- **Tool shapes por familia en la industria.** Google MCP Toolbox define herramientas `postgres-sql` con parámetros tipados que se ejecutan como prepared statements, y los parámetros de identidad salen del token, no del modelo (vía informe previo). — [MCP Toolbox](https://mcpservers.org/servers/googleapis/genai-toolbox)
- **Práctica general.** Workato recomienda partir del flujo para decidir qué herramientas hacen falta, resumir datasets grandes antes de devolverlos y usar códigos de error consistentes. Un curso lista como error típico "wrapping each REST endpoint one for one". — [Workato, MCP server tool design](https://docs.workato.com/mcp/mcp-server-tool-design)

### Inferences
- **Recomendación para Ars Docendi (aplica a A y a B por igual).** Cinco o seis herramientas de lectura **por familia del dominio**:
  1. catálogos;
  2. designaciones;
  3. pedidos;
  4. perfil docente (portal);
  5. resolución de entidades;
  6. opcional: "mi alcance".

  Cada una con:
  - filtros opcionales tipados;
  - un `medida` cerrado (`listado | conteo | suma_horas`);
  - un `agrupar_por` cerrado;
  - `orden`.

  Es el nivel "L3" de MCP-GRANITE y el patrón `query_metrics` de dbt, pero partido por dominio y no en una sola herramienta. Una única `consulta_estructurada` sobre todo el dominio equivale a L1 (peor y con más llamadas omitidas). Copiar los ~60 endpoints 1:1 equivale a L4 o peor, y además casi todos son de escritura.
- **Para un 8B, la agregación tiene que ocurrir en el backend.** Si una herramienta devuelve un listado y el modelo cuenta, suma u ordena, se reintroduce el error que la herramienta debía evitar. A eso se suman el truncado a 200 filas y la prohibición de afirmar conteos cuando hay truncado (`openspec/changes/asistente-carril-sql/design.md` D14).
- **Una herramienta con enums cerrados se puede certificar de forma exhaustiva.** Ejemplo: `consultar_designaciones` con `vigencia` (3) × `medida` (3) × `agrupar_por` (5). Son 45 combinaciones de SQL generado por plantilla, todas testeables contra el fixture, y eso calza con la regla 8 del repo (BR-* con test). Un lenguaje de consulta libre (GraphQL u OData) vuelve a poner al modelo a generar código. No encontré evidencia medida a favor de GraphQL/OData como superficie para LLM (ver Gaps).

### Gaps
- No pude leer el PDF de MCP-GRANITE: no sé si incluye modelos de 7–9B ni la distribución de errores por tipo.
- No encontré ningún estudio que compare espejo REST, herramientas de tarea y herramienta semántica única con el **mismo modelo chico y el mismo dominio**.
- No encontré evidencia publicada de precisión de herramientas tipo GraphQL u OData para LLM.
- El playbook de Block (`engineering.block.xyz`) quedó bloqueado. No le atribuyo contenido.

---

## 2. Guías de practicantes: qué reduce errores en concreto

### Takeaway
Las prácticas con respaldo más concreto son:
- consolidar en flujos;
- nombres de parámetros inequívocos, con validación estricta de tipos;
- devolver nombres legibles en lugar de identificadores opacos;
- un formato de salida conciso, o elegible;
- errores accionables con ejemplos de entrada correcta;
- un namespacing coherente;
- evaluar con tareas realistas y mirar las transcripciones.

Todas apuntan a que la herramienta "hable el idioma del usuario" y haga el trabajo determinista.

### Cited Findings
- **Anthropic** — [Writing effective tools for agents](https://www.anthropic.com/engineering/writing-tools-for-agents):
  - Devolver campos de alta señal y "eschew low-level technical identifiers". Resolver UUIDs a lenguaje legible "significantly improves Claude's precision in retrieval tasks by reducing hallucinations".
  - Un enum `response_format` (concise/detailed): en el ejemplo, la respuesta concisa usa unos 72 tokens contra 206.
  - Nombres inequívocos: "instead of a parameter named `user`, try a parameter named `user_id`". Validar entradas y salidas con modelos de datos estrictos.
  - Los errores tienen que "clearly communicate specific and actionable improvements" e incluir ejemplos de entradas bien formadas.
  - Al truncar, orientar al agente hacia búsquedas más acotadas.
  - El namespacing por servicio o recurso tiene efectos "non-trivial" según el modelo: hay que medirlo.
  - Evaluar con tareas realistas y verificables, midiendo exactitud, número de llamadas, tokens y errores. Las llamadas redundantes delatan problemas de paginación; los errores de parámetros repetidos, descripciones poco claras.
- **Workato**: herramientas "simple, composable, and predictable", errores consistentes, agregar herramientas de a una y refinar las descripciones según el uso. — [Workato](https://docs.workato.com/mcp/mcp-server-tool-design)
- **Cube** (fuente de proveedor): los permisos tienen que imponerse "outside the prompt", y hay que poder ver qué definición de métrica produjo cada respuesta. — [Cube](https://cube.dev/articles/analytics-mcp-server)
- **Repo, del lado del modelo chico.** Los cambios mínimos de prompt mueven el resultado:
  - 8 ejemplos más cambiaron la selección de 22 de 34 ítems y costaron 3 aciertos;
  - el glosario en el prefijo bajó capacidad a 25/34 y social a 17/20.

  — [docs/architecture/modelo-local.md §6, §8](docs/architecture/modelo-local.md); [backend/src/Modules.Asistente/README.md](backend/src/Modules.Asistente/README.md)

### Inferences
Aplicación concreta a Ars Docendi:
1. **Nombres de herramientas y parámetros en español de negocio**, alineados con el vocabulario del usuario: `materia`, `carrera`, `cargo`, `estado`, `novedad`, `periodo`.
2. **Los sinónimos se resuelven en el servidor** (auxiliar → ayudante, solicitud → pedido, nombramiento → designación, ciclo → período, urgente → prioritario), no con un glosario en el prompt, que ya empeoró al 8B.
3. **Enums con descripción breve por valor.** Por ejemplo, `estado`: `en_revision_*` agrupado como `en_revision`, `rechazado`, `devuelto`, etc. Los valores son los del CHECK de [database/designaciones/003_designaciones_pedidos.sql](database/designaciones/003_designaciones_pedidos.sql). Hay que corregir antes la inconsistencia "Categoría 0..6" contra el catálogo 1..6 (TD-025), porque un enum que miente es peor que un comentario que miente.
4. **Errores accionables**: `materia_no_encontrada` con sugerencias visibles, `parametro_invalido` con un ejemplo y `sin_permiso`.
5. **Salida concisa por defecto, con la interpretación canónica de los filtros** (ver §3).
6. **Medir con el evaluador del repo**, dejando los datasets fuera del diseño de herramientas (ver §6, sesgo).

### Gaps
- No relevé los posts de diseño MCP de GitHub, Stripe, Linear y Cloudflare, ni la guía de Microsoft. No le atribuyo nada a esas fuentes.

---

## 3. Resolución de entidades y grounding (nombres y materias en español), y cómo evitar que "cero filas" parezca "no hay"

### Takeaway
La causa principal de respuestas falsas con SQL válido es el literal mal copiado o mal resuelto, que da cero filas o la entidad equivocada. La defensa más precisa es que **cada herramienta reciba el texto de la entidad y la resuelva en el servidor, dentro del conjunto visible del actor, antes de consultar**. El resultado de esa resolución tiene que ser un estado explícito: `resuelto`, `ambiguo` (con candidatos), `no_encontrado` (con sugerencias) o `sin_permiso`. La consulta no se ejecuta salvo con todas las entidades resueltas. Ante ambigüedad se pregunta, con el menú de opciones existente o con elicitation de MCP. Detectar ambigüedad es justo lo que los LLM hacen mal, así que esa detección tiene que ser determinista.

### Cited Findings
- **Repo.** El caso motivador es "ingeniería informática" vs "Ingeniería **en** Informática", que daba cero filas con SQL válido.
  - "Zero rows from valid SQL is indistinguishable from 'there is none'". Pesa más para Qwen3-8B, "which copies literals less carefully than Claude".
  - ARS-165 propone resolver por Damerau-Levenshtein en proceso sobre entidades visibles, sin `pg_trgm` ni embeddings. Decisiones:
    - unicidad "over the visible set only", para no filtrar la existencia de entidades fuera de alcance;
    - personas solo por coincidencia exacta;
    - "A wrong hint is worse than no hint".

  — [openspec/changes/asistente-recuperacion-de-valores/proposal.md](openspec/changes/asistente-recuperacion-de-valores/proposal.md); [design.md D3, D5, D6](openspec/changes/asistente-recuperacion-de-valores/design.md)
- **Repo.** El carril determinista no se activa por defecto porque "Enrutar mal hacia la API devuelve cero filas, y «cero filas» es indistinguible de «no hay»". — [openspec/changes/asistente-enrutador-de-dominio/proposal.md](openspec/changes/asistente-enrutador-de-dominio/proposal.md)
- **Repo.** Las menciones `@materia` y `#docente` viajan como `$refN` ligados como `uuid`, y el id nunca llega al modelo. — [backend/src/Modules.Asistente/README.md](backend/src/Modules.Asistente/README.md)
- **CHESS**: un LLM extrae palabras clave de la pregunta y después recupera valores de la base con LSH más similitud semántica (y distancia de edición). Sostiene que sobrecargar el contexto con información redundante degrada el resultado. No obtuve las cifras de la ablación. — [CHESS, arXiv 2405.16755](https://arxiv.org/abs/2405.16755); [Stanford](https://scalingintelligence.stanford.edu/pubs/CHESSpaper)
- **AMBROSIA (NeurIPS 2024).** Ante preguntas ambiguas, el recall de interpretaciones de GPT-4o es de ~27,1 % (0-shot) contra 63,4 % en preguntas no ambiguas, con AllFound de 0,4 %. Llama3-70B obtiene ~30,7 %. "Even the most advanced models struggle to identify and interpret ambiguity". — [AMBROSIA](https://arxiv.org/abs/2406.19073)
- **PRACTIQ**: las preguntas ambiguas y las no contestables "are challenging even for methods leveraging SoTA LLMs". — [PRACTIQ](https://arxiv.org/html/2410.11076v1)
- **Elicitation de MCP (spec 2025-06-18).**
  - El servidor pide datos estructurados al usuario con un JSON Schema durante una herramienta. El cliente tiene que declarar la capacidad.
  - "Servers MUST NOT use elicitation to request sensitive information".
  - El cliente tiene que permitir rechazar o cancelar.
  - No hay un tipo específico para desambiguar: se arma con un enum de candidatos.
  - En 2025-11-25 se agregó un modo URL.
  - Un blog de terceros afirma que la spec 2026-07-28 la reemplaza por respuestas `input_required` con reintento (Multi Round-Trip Requests). No lo verifiqué contra la spec oficial.

  — [MCP spec, elicitation](https://modelcontextprotocol.io/specification/2025-06-18/client/elicitation); [WorkOS](https://workos.com/blog/mcp-elicitation)
- **Anthropic**: nombres legibles en lugar de UUIDs reducen alucinaciones. Conviene preferir herramientas de búsqueda (`search_*`) a listados. — [Anthropic](https://www.anthropic.com/engineering/writing-tools-for-agents)

### Inferences
**Diseño propuesto, común a A y B.**

1. **Parámetros de entidad "texto o id".** `materia: string | materia_id: uuid`, `carrera`, `docente`, `cargo`, `habilidad`. Con id (mención) no hay que resolver. Con texto, el servidor normaliza (`unaccent`, minúsculas, sinónimos) y resuelve contra el conjunto visible:
   - exacto, después contención, después Damerau-Levenshtein ≤ 1 en palabras largas (reglas de ARS-165);
   - personas solo por coincidencia exacta;
   - "Gómez" sin nombre devuelve `ambiguo` con los candidatos visibles.
2. **Salida con estado explícito**:
   ```
   {estado: "ok"|"ambiguo"|"no_encontrado"|"sin_permiso"|"vacio",
    interpretacion: {materia: "Bases de Datos (Ing. en Informática)", vigencia: "vigentes", medida: "listado"},
    alcance: {tipo: "global"|"carrera"|"materia", descripcion: "tus materias"},
    total: n, truncado: bool, filas: [...], candidatos?: [...], sugerencias?: [...]}
   ```
   - `vacio` solo se emite con todas las entidades resueltas y los filtros válidos. En un actor acotado, la redacción dice "no hay … **dentro de tu ámbito**".
   - `no_encontrado` dice "no encontré una materia «X» entre las que podés ver", sin afirmar que no exista. Así se respeta D6 (no filtrar existencia).
   - `interpretacion` hace explícito qué se respondió. Es la versión con herramientas de la "pregunta interpretada" del contrato actual.
3. **La desambiguación es determinista y la decide la herramienta, no el modelo.** cap-020, "Análisis Matemático" que existe en 3 carreras, devuelve `ambiguo` con 3 candidatos (nombre + carrera):
   - en B, va al "menú de aclaración" existente (`ReconocedorDeAclaracion`, opciones bloqueantes);
   - en A, elicitation de MCP con un enum de candidatos, si el cliente la soporta, o una repregunta del agente.

   AMBROSIA indica que no hay que confiar en que el LLM detecte la ambigüedad por su cuenta.
4. **`buscar_entidades(texto, tipos[])`** como herramienta aparte, solo para cadenas (por ejemplo, "¿qué materias tiene la carrera de X?" cuando el modelo duda del tipo). Lo normal es que no se necesite.
5. **Nunca ejecutar con un literal no resuelto.** Con `no_encontrado` o `ambiguo`, la consulta no corre. En B, el respaldo SQL **no** debe ejecutarse para "salvar" una herramienta que devolvió `no_encontrado` o `ambiguo` de una familia cubierta, porque reintroduciría el falso "no hay" (ver §5).

### Gaps
- No hay mediciones públicas de la ganancia de la resolución difusa para nombres en español con modelos de 7–9B. La única medición válida sería el gate de ARS-165 (D9), que todavía no corrió según lo que encontré.
- No sé qué clientes MCP soportan hoy elicitation en modo formulario, ni cómo quedó en la spec 2026-07-28.

---

## 4. Cómo se traduce la autorización por ámbito/RLS a cada arquitectura (quién filtra) y su efecto en la corrección

### Takeaway
- **En A**, filtra el código de aplicación de cada endpoint, con reglas que **no coinciden** con las de RLS. Algunos endpoints de UI aplican además filtros de propósito de pantalla. Si un agente los usa, producen respuestas **silenciosamente incompletas**.
- **En B**, filtra el motor (RLS más el GUC del actor), igual que hoy.
- **A tiene una ventaja de precisión**: un 403 explícito distingue "no tenés permiso" de "no hay", y bajo RLS los dos son cero filas.
- **B la puede igualar** si cada herramienta consulta primero el permiso y el alcance del actor (funciones `identity.asistente_*`) y lo devuelve como estado.

### Cited Findings
- **Repo, RLS del asistente.** Las policies conjuntan permiso y ámbito: `identity.asistente_tiene_permiso('designaciones.ver') AND materia_id IN (SELECT identity.asistente_materias_visibles())`. Los permisos se leen en vivo, sin listas de roles en el código. — [database/designaciones/009_designaciones_rls_asistente.sql](database/designaciones/009_designaciones_rls_asistente.sql); [openspec/changes/asistente-fundaciones/design.md D5–D6](openspec/changes/asistente-fundaciones/design.md)
- **Repo, abstención actual.** Con actor acotado no se reintenta ante resultado vacío, porque "RLS convierte 'no tenés permiso' en cero filas... exactamente la misma firma". `AlcanzaTodo` se decide por dominio (designaciones/portal). — [openspec/changes/asistente-carril-sql/design.md D13](openspec/changes/asistente-carril-sql/design.md); [backend/src/Modules.Asistente/Application/Abstencion/PoliticaDeAbstencion.cs](backend/src/Modules.Asistente/Application/Abstencion/PoliticaDeAbstencion.cs)
- **Repo, el ámbito en la API REST usa otra regla.** `ServicioCatalogosDesignaciones` decide la visibilidad con roles fijos: `actor.EsDeptoWide`, después `RolesCircuito.JefeCatedra` (materias a cargo) y después `RolesCircuito.CoordinadorCarrera` (carreras a cargo); si no, `[]`. — [backend/src/Modules.Designaciones/Services/ServicioCatalogosDesignaciones.cs](backend/src/Modules.Designaciones/Services/ServicioCatalogosDesignaciones.cs)
- **Repo, filtro silencioso de propósito UI en `GET /api/designaciones/catalogos`.**
  - El listado de `Personas` (con sus `DesignacionesVigentes`) excluye a quienes tienen un pedido vivo en el período activo: `.Where(p => !ocupadas.Contains(p.Id) && ...)`, con `ocupadas = ListarPersonasConPedidoVivoAsync(activo.Id)`.
  - Además, cada persona trae `Documento`, que es PII y en el asistente está enmascarado como `sensible-valor`.

  — [ServicioCatalogosDesignaciones.cs](backend/src/Modules.Designaciones/Services/ServicioCatalogosDesignaciones.cs); [backend/src/Modules.Designaciones/Api/ModelosCatalogos.cs](backend/src/Modules.Designaciones/Api/ModelosCatalogos.cs); [database/asistente/manifiesto-sensibilidad.json](database/asistente/manifiesto-sensibilidad.json)
- **Repo, `GET /api/administracion/docentes`.** Combina personas con designaciones vigentes **o** con roles docentes (`porPersona.ContainsKey(p.Id) || RolesDocentes(p.Usuario).Count > 0`), filtradas por las materias visibles del controller. Es otro universo distinto de "designados". — [backend/src/ArsDocendi.Host/Administracion/ServicioDocentes.cs](backend/src/ArsDocendi.Host/Administracion/ServicioDocentes.cs); [backend/src/ArsDocendi.Host/Api/DocentesController.cs](backend/src/ArsDocendi.Host/Api/DocentesController.cs)
- **Repo, `GET /api/designaciones/pedidos`.** Lista por `periodoId` (por defecto el período activo), acotado en el servicio. `GET periodos` exige `PeriodosAdministrar`. — [backend/src/Modules.Designaciones/Api/PedidosController.cs](backend/src/Modules.Designaciones/Api/PedidosController.cs); `research_notes/Alternativas a Text to SQL local/arquitectura_actual.md` §9
- **Repo, truncado.** El tope es de 200 filas con una fila sonda. Con truncado, la redacción "tiene prohibido afirmar conteos" y nunca dice cuántas filas quedaron afuera. — [openspec/changes/asistente-carril-sql/design.md D9, D14](openspec/changes/asistente-carril-sql/design.md)
- **Toolbox y SDK C# de MCP** (vía informe previo):
  - En Toolbox, los parámetros de identidad salen del token y no del modelo. — [Genkit/Toolbox](https://genkit.dev/docs/integrations/toolbox/)
  - El SDK C# de MCP inyecta el `ClaimsPrincipal` en la herramienta, lo saca del esquema y filtra `tools/list` con `[Authorize]`. — [csharp-sdk identity.md](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/identity/identity.md)
- **Cube**: los permisos se imponen "outside the prompt". — [Cube](https://cube.dev/articles/analytics-mcp-server)

### Inferences
- **A con endpoints existentes reutilizados tal cual: riesgo directo de respuestas falsas plausibles.**
  - Con `catalogos`, "¿Quiénes dictan Bases de Datos?" omitiría a todo docente que tenga un pedido vivo en el período activo, y nada le indica al modelo que la lista es parcial.
  - Con `docentes`, "¿quiénes dictan X?" incluiría personas con rol docente sin designación vigente en X, según cómo el `Mapear` construya `Asignaciones`; no lo verifiqué línea por línea.
  - Conclusión: **A exige endpoints nuevos y propios del asistente**, con semántica de negocio explícita (vigencia, universo), y no reutilizar endpoints de UI. Es lo mismo que pide Anthropic: herramientas pensadas para agentes, no envoltorios.
- **Dos fuentes de verdad del ámbito.** Las reglas de A (roles fijos `JefeCatedra` y `CoordinadorCarrera`) y las de B (permiso en vivo más `asistente_materias_visibles()`) pueden divergir cuando Secretaría crea roles en runtime. Para un mismo actor, A y B podrían responder distinto a la misma pregunta. Para una comparación justa de precisión, el evaluador debería fijar actores cuyo ámbito coincida en ambos sistemas, o A debería reutilizar las funciones SQL de alcance.
- **La señal de "sin permiso" favorece a A.** cap-024 (actor `sin_permiso`) en A recibe un 403 de `[Authorize(Policy = DesignacionesVer)]`, que se traduce a `estado: "sin_permiso"`: abstención correcta y explicable. B puede lograr lo mismo si cada herramienta certificada llama antes a `identity.asistente_tiene_permiso(...)`, ya concedida al rol del asistente, y devuelve `sin_permiso` en lugar de ejecutar. Hoy el carril SQL no puede hacerlo porque no sabe qué permiso implica la pregunta. Una herramienta de familia **sí lo sabe**.
- **Truncado y conteos.** En ambas arquitecturas, las herramientas tienen que devolver un `total` calculado con `count(*)` o en el servicio, separado de las filas. Si no, el modelo cuenta sobre una página truncada. `medida = conteo` evita traer filas.
- **Vacío por ámbito.** Cada respuesta debería llevar `alcance`, para que la redacción diga "en tus materias no hay…". Eso convierte el vacío por ámbito en una respuesta correcta y acotada, en vez de una abstención o un falso "no hay".

### Gaps
- No verifiqué si `Mapear` en `ServicioDocentes` restringe `Asignaciones` a las vigentes, ni qué campos PII expone `DocenteAdministracionDto`.
- No hay un test que compare, para el mismo actor, el conjunto visible por la API contra el visible por RLS.

---

## 5. Cómo hacer preciso el Text-to-SQL de respaldo, y cuándo abstenerse

### Takeaway
Con un 8B, la mayor ganancia de precisión probablemente viene de **no generar los literales**: recuperar valores o resolver entidades antes, y reusar consultas verificadas. Después viene la **verificación con abstención**, mediante un juez LLM o un verificador entrenado sobre el esquema propio. Votar por mayoría o self-consistency da ganancias chicas: en el repo midió +0,13 puntos, y su acuerdo es una mala señal para abstenerse. La regla de abstención debería depender de la penalización de la métrica. Con penalización p por respuesta falsa, responder solo conviene si la precisión condicional estimada supera p/(1+p): 67 % con p = 2.

### Cited Findings
- **Ejemplos verificados y few-shot.**
  - En el repo, 20 pares verificados en el prefijo dan 26/12/10/18 contra 24/11/10/17 con 4 por pregunta. Con 28 en el prefijo: 28/12/9/17.
  - Agregar 8 ejemplos cambió la selección de 22 de 34 ítems y costó 3 aciertos.

  — [docs/architecture/modelo-local.md §8](docs/architecture/modelo-local.md); [backend/src/Modules.Asistente/README.md](backend/src/Modules.Asistente/README.md)

  Snowflake Cortex Analyst usa un *Verified Query Repository* y Databricks Genie *trusted assets* (vía informe previo). — [Snowflake](https://docs.snowflake.com/user-guide/snowflake-cortex/cortex-analyst-evaluations); [Databricks](https://docs.databricks.com/aws/en/genie/trusted-assets)
- **Recuperación de valores y schema linking.** CHESS recupera valores con LSH más similitud tras extraer palabras clave. — [CHESS](https://arxiv.org/abs/2405.16755)

  En el repo, el schema linking empeoró a modelos de 7–32B en esquemas institucionales y rompe el prefijo estable. — [docs/architecture/modelo-local.md §6](docs/architecture/modelo-local.md)
- **Majority voting y self-consistency.**
  - OmniSQL-7B mejora en promedio +8,4 puntos sobre su base, de 52,8 a 61,2 en greedy. Con voto por mayoría llega a 88,9 % en Spider test (7B), 88,3 % (14B) y 89,8 % (32B). Las cifras de BIRD dev con voto no aparecieron en el extracto (tabla 5 del paper). — [OmniSQL, arXiv 2503.02240](https://arxiv.org/pdf/2503.02240)
  - CSC-SQL señala que "the most frequently selected output is not always the correct final result". Su paso correctivo suma entre +0,72 y +5,54 puntos sobre self-consistency, y el 7B llega a 69,19 % en BIRD dev. — [CSC-SQL](https://arxiv.org/html/2505.13271v2)
  - En el repo, self-consistency on-prem dio "+0,13 puntos con p95 de 10,6 a 50 s", y la definición lo descarta por "20–30× de costo para comprar ~2 puntos". — [docs/architecture/modelo-local.md §6](docs/architecture/modelo-local.md); [docs/product/designs/asistente-conversacional-definicion.md §6](docs/product/designs/asistente-conversacional-definicion.md)
- **Selección entre candidatos.**
  - CHASE-SQL (ICLR 2025) usa un selector fine-tuneado por comparaciones de a pares: 73,01 % en BIRD dev y 73,0 % en test. Su configuración abierta (Mistral Large como generador y Qwen2.5-Coder fine-tuneado como selector) da 70,33 en dev. — [CHASE-SQL](https://proceedings.iclr.cc/paper_files/paper/2025/hash/974ff7b5bf08dbf9400b5d599a39c77f-Abstract-Conference.html)
  - En una re-evaluación de R³-SQL con candidatos de OmniSQL-7B, el selector de CHASE da 68,38 contra 71,83 de R³-SQL y 70,80 de Contextual-SQL. — [R³-SQL, arXiv 2604.25325](https://arxiv.org/pdf/2604.25325)
- **Predicción de corrección y jueces.** Richardson (2026, extracto):
  - Self-consistency, relevancia de esquema y ejecutabilidad dan AUROC de ~0,61–0,68; los log-probs, 0,67.
  - Un juez LLM, de 0,72 (GPT-4o-mini) a 0,78 (Claude). Un ensamble de dos proveedores, 0,82, que permite por ejemplo "answering 27% of questions at 24% selective risk", mientras que "self-consistency offers no valid low-risk subset".
  - Los verificadores fine-tuneados dan ~0,77–0,79 en distribución y caen a ~0,66 en esquemas no vistos.

  — [What Predicts Correctness in Text-to-SQL?, arXiv 2607.06799](https://arxiv.org/abs/2607.06799)
- **Clasificador selectivo.** Somov y Tutubalina (AAAI 2025) abstienen bajo un umbral de incertidumbre. Detectan mejor las preguntas irrelevantes que las consultas mal generadas, y T5 está mejor calibrado que GPT-4 o Llama 3 en ICL. — [Confidence Estimation for Error Detection in Text-to-SQL](https://arxiv.org/abs/2501.09527)
- **TrustSQL** (vía informe previo): con penalizaciones fuertes, ningún método superó de forma consistente a abstenerse siempre. — [TrustSQL](https://arxiv.org/html/2403.15879v4)
- **Ambigüedad.** Los LLM recuperan ~27–31 % de las interpretaciones de preguntas ambiguas. — [AMBROSIA](https://arxiv.org/abs/2406.19073)
- **Repo, piezas ya existentes.**
  - Validador léxico (no AST);
  - `RepararConsultaFallida` (una ronda ante error del motor, con el error saneado);
  - reintento ante vacío solo con actor global;
  - salida JSON con `es_contestable`, `categoria` y `motivo`;
  - menciones `$refN`;
  - un especialista medido localmente: XiYan-7B Q4 obtuvo 14/34 y "se abstiene de más"; Arctic-R1 7B, 23/34.

  — [docs/architecture/modelo-local.md §3](docs/architecture/modelo-local.md); [backend/src/Modules.Asistente/Application/CarrilSql/ValidadorDeSql.cs](backend/src/Modules.Asistente/Application/CarrilSql/ValidadorDeSql.cs)
- **Penalización de la métrica**: 0,5, 1,0 o 2,0 por respuesta falsa (vía informe previo). — [backend/eval/README.md](backend/eval/README.md)

### Inferences
**Técnicas para el respaldo de B, ordenadas por impacto esperado en precisión con un 8B (inferido, no medido).**

1. **Literales resueltos antes de generar.** Activar la recuperación de valores (ARS-165) y reutilizar la misma resolución de entidades que las herramientas (§3), pasando ids como `$refN`. Ataca el error dominante documentado.
2. **Certificadas como ejemplos.** Los SQL de las herramientas certificadas y sus variantes sirven de few-shot verificados en el prefijo. Así el respaldo "aprende" de las mismas consultas que las herramientas, sin solaparse con el dataset de evaluación, que es un invariante del repo.
3. **Validación estructural por AST.** Con `pg_query` o similar, más reglas semánticas del dominio "tipo lint". Por ejemplo:
   - toda consulta sobre `designaciones.designaciones` explicita `vigente_hasta`;
   - no comparar texto de usuario con `=` sin `unaccent`;
   - no agregar sobre `pedidos` sin decidir el período.

   El fallo de una regla se repara una vez; si persiste, abstención.
4. **Ejecución guiada.** Errores del motor: una reparación, como hoy. Vacío con actor global: reintento, como hoy. Vacío con actor acotado: abstención con mensaje de ámbito.
5. **Juez de verificación** (LLM o verificador fine-tuneado sobre el esquema propio, que siempre está "en distribución"), con abstención por umbral. Las señales de Richardson sugieren que el juez es mejor señal que el acuerdo entre muestras. El umbral se calibra con el evaluador del repo para la penalización vigente.
6. **Self-consistency y selección de candidatos**: ganancia baja y costosa; mala señal de abstención. Solo valdría como insumo del juez (generar 2–3 candidatos y que el juez elija o se abstenga).
7. **Aclaración determinista.** El detector de ambigüedad existente antes del respaldo, no el LLM (AMBROSIA).

**Regla de abstención.** Si responder bien vale +1, responder mal vale −p y abstenerse vale 0, conviene responder solo cuando P(correcto) > p/(1+p):

| Penalización p | Umbral de P(correcto) |
| --- | --- |
| 0,5 | 33 % |
| 1 | 50 % |
| 2 | 67 % |

La precisión condicional actual de Qwen3-8B al responder en capacidad es de ~26/(26+7) ≈ 79 %. Eso supera el umbral en promedio, pero no en las clases con más falsas. La abstención debería decidirse **por clase de pregunta**, según la familia que detectó el enrutador y la categoría declarada, con tasas medidas en el evaluador, y no con un umbral global. El repo descartó "umbral agregado como gate", pero eso se refería a la puerta de la CI, no a esto.

**Reglas de frontera del híbrido** (precisión):
- **(a)** Si una herramienta de la familia correcta devolvió `no_encontrado`, `ambiguo` o `sin_permiso`, el respaldo **no** se ejecuta.
- **(b)** Una respuesta del respaldo se rotula como "no certificada" y muestra la interpretación.
- **(c)** Sin permiso de familia (`asistente_tiene_permiso`), el respaldo tampoco corre para esa familia.

### Gaps
- No obtuve las cifras de OmniSQL-7B con voto en BIRD dev (tabla 5): el "+3 puntos" que menciona la consigna queda **sin verificar**.
- No hay cifras de la ablación de recuperación de valores de CHESS.
- No hay mediciones de jueces LLM chicos (7–9B) como verificadores de SQL en PostgreSQL ni en español.
- Todas las cifras de BIRD y Spider son en inglés y sobre SQLite.

---

## 6. Catálogo concreto por arquitectura y cobertura sobre los datasets del repo

### Takeaway
Las 80 preguntas evaluables se agrupan en ocho familias. Con cinco o seis herramientas de nivel tarea que resuelven entidades en el servidor, **las 26 preguntas contestables de capacidad se responden en una sola llamada**, en A y en B, y las 2 ambiguas se convierten en una aclaración determinista. Si A se limita a los endpoints existentes, la mayoría de las preguntas exige varias llamadas, cálculo hecho por el modelo, o no tiene respuesta. Hay dos casos con riesgo de falsas silenciosas. B agrega al respaldo SQL solo lo que queda fuera de las herramientas, y en estos datasets, si las herramientas se diseñan a partir de ellos, eso es casi nada. Diseñar las herramientas mirando el dataset **sesga la medición**: hace falta un conjunto ciego.

### Cited Findings
- **Datasets.**
  - Capacidad: 34 ítems, entre ellos 6 `no_contestable` y 2 `ambigua`; 3 ítems con actor acotado (`carrera`/`materia`) y 1 `sin_permiso`; 2 con menciones `@`/`#`.
  - Robustez: 15 paráfrasis que heredan la referencia de su ítem de origen.
  - Diálogo: 5 diálogos, 11 turnos.
  - Social: 20 ítems (11 `social` que deben resolverse con "cero tokens", 3 `no_contestable` y 6 `negativo` que el enrutador no debe capturar).

  — [backend/eval/datasets/capacidad.json](backend/eval/datasets/capacidad.json); [robustez.json](backend/eval/datasets/robustez.json); [dialogo.json](backend/eval/datasets/dialogo.json); [social.json](backend/eval/datasets/social.json)
- **Invariante**: "este dataset y el catálogo de ejemplos del módulo son disjuntos — si se solaparan, la métrica mediría cuán bien el sistema reproduce ejemplos que ya vio". — [capacidad.json](backend/eval/datasets/capacidad.json)
- **Intenciones actuales.** Son 5, con términos como "pedido", "plantel" y "cuantos", y la tabla dorada da **0 de 39** capturas. Los datasets usan "solicitudes", "nombramientos", "asignatura" y "dictan". — [backend/src/Modules.Asistente/Recursos/intenciones.json](backend/src/Modules.Asistente/Recursos/intenciones.json); `arquitectura_actual.md` §4
- **Catálogos cerrados en la base.** `estado` tiene 8 valores (de `borrador` a `cancelado`), `novedad` 4, `tipo_baja` 3 y la dedicación "Categoría 0..6". La vigencia es `vigente_hasta IS NULL`. — [database/designaciones/003_designaciones_pedidos.sql](database/designaciones/003_designaciones_pedidos.sql); [006_designaciones_designaciones.sql](database/designaciones/006_designaciones_designaciones.sql)
- **Endpoints REST de lectura disponibles hoy**:
  - `GET api/designaciones/pedidos?periodoId` (período activo por defecto) y `{id}`;
  - `GET api/designaciones/catalogos`: períodos, materias visibles, personas "no ocupadas" con designaciones vigentes, cargos, dedicaciones y novedades;
  - `GET api/administracion/docentes?busqueda&materiaId&rol&activo`;
  - `GET api/designaciones/periodos` (con `PeriodosAdministrar`);
  - el portal propio (`api/portal/perfil`), sin acceso a perfiles ajenos.

  — [PedidosController.cs](backend/src/Modules.Designaciones/Api/PedidosController.cs); [ModelosCatalogos.cs](backend/src/Modules.Designaciones/Api/ModelosCatalogos.cs); [DocentesController.cs](backend/src/ArsDocendi.Host/Api/DocentesController.cs); `arquitectura_actual.md` §9

### Inferences

#### 6.1 Familias del dataset
Clasificación propia, a partir de las consultas de referencia.

| Familia | Ítems de capacidad | Robustez / diálogo / social |
|---|---|---|
| F1 Catálogos y listados simples | cap-001 carreras, 002 cargos por jerarquía, 011 ciclos, 015 materias de una carrera (4) | rob-001/003/004/006/007/010/013; dia-003 t2, dia-004 t1; soc-011/013/015/016 |
| F2 Plantel vigente de una materia | cap-004, 009 (actor materia, "la asignatura que dirijo"), 033 (@materia, con cargo) (3) | rob-005 (typo), rob-011; dia-002 t2, dia-003 t1, dia-005 t2 |
| F3 Designaciones de una persona | cap-034 (#Suárez) (1) | — |
| F4 Agregaciones y estados de designaciones | cap-003 (top por materia), 005 (conteo vigentes), 006 (conteo ayudante1), 010 (por cargo), 013 (cerradas, listado), 022 (suma horas) (6) | rob-002/008/014; dia-001 t1–t2, dia-005 t1; soc-012 |
| F5 Pedidos | cap-007 (ciclo en curso), 008 (actor carrera), 012 (rechazadas), 014 (conteo bajas), 016 (urgentes), 023 (actor materia), 024 (sin permiso) (7) | rob-009/012/015; dia-004 t2–t3; soc-014 |
| F6 Perfil docente (portal ajeno) | cap-025 (habilidad), 026 (posgrado terminado), 027 (certificación vence 2026), 028 (interés en dictar), 029 (conteo con certificación) (5) | — |
| F7 Fuera de dominio o denegado | cap-017 sueldo, 018 notas, 019 aula, 030 mail de contacto (tabla denegada), 031 ruta del CV (denegada), 032 proyectos (denegada) (6) | soc-008/009/010 |
| F8 Ambiguas | cap-020 (Análisis Matemático en 3 carreras), 021 (Gómez) (2) | dia-002 t1 |
| Social y meta | — | soc-001…007, 017…020 (11) |

#### 6.2 Catálogo propuesto para A (agente sobre endpoints nuevos expuestos por MCP)

Son endpoints REST nuevos y propios del asistente, por ejemplo bajo `api/consultas/...`. Requisitos:
- no reutilizar los de UI (ver §4);
- aplicar el ámbito con la misma regla que RLS;
- respuestas sin PII, o con la máscara del manifiesto;
- la forma de salida de §3.

Herramientas:
1. `listar_catalogo(tipo: carreras|materias|cargos|periodos|estados_pedido|novedades, carrera?: texto)`. Los cargos van ordenados por `orden` (jerarquía) y los períodos por fecha. Cubre F1.
2. `consultar_designaciones(materia?|materia_id?, carrera?, docente?|docente_id?, cargo?: enum con sinónimos, vigencia: vigentes|cerradas|todas = vigentes, medida: listado|conteo|suma_horas, agrupar_por?: materia|cargo|carrera|docente, orden?, limite?)`. Cubre F2, F3 y F4.
3. `consultar_pedidos(periodo: activo|todos|texto = todos, estado?: enum[], novedad?: enum[], prioritario?: bool, materia?, carrera?, docente?, medida: listado|conteo, agrupar_por?: estado|novedad|materia)`. Cubre F5. Hay que fijar por especificación qué significa "período por defecto": las referencias de cap-012/014 no filtran período, y cap-007 sí.
4. `buscar_docentes_por_perfil(habilidad?, interes?, nivel_educativo?: enum{grado, posgrado→[Especialización, Maestría, Doctorado], …}, educacion_terminada?: bool, certificacion_vence_desde?/hasta?: fecha, con_certificacion?: bool, medida: listado|conteo)`. Cubre F6. "Posgrado" queda como definición certificada en el enum, no inferida por el modelo.
5. `buscar_entidades(texto, tipos[])`: solo para cadenas o dudas de tipo.
6. `mi_alcance()`: tipo de ámbito, materias y carreras visibles, y permisos de dominio. Sirve para "la asignatura que dirijo", aunque no es estrictamente necesaria, porque sin filtro el ámbito ya acota.

Fuera del agente quedan el enrutador social determinista y el catálogo de capacidades (`GET /api/asistente/capacidades`). Sin ellos, los 11 ítems `social` fallan por definición: exigen cero tokens.

#### 6.3 Catálogo propuesto para B (híbrido)

- **Capa 0, determinista y sin tokens.**
  - Los existentes: enrutador social, reconocedor de aclaración y detector de ambigüedad.
  - El catálogo de intenciones **reescrito con el vocabulario real**: "solicitud(es)" = pedido, "nombramiento(s)" = designación, "asignatura" = materia, "dictan" = designados vigentes, "ciclo" = período, "urgente" = prioritario.
  - Slots resueltos con §3.
  - Candidatas razonables: `catalogo-de-X`, `materias-de-una-carrera`, `plantel-de-una-materia`, `pedidos-en-un-estado`, `pedidos-de-una-novedad` (listado o conteo), `conteo-de-designaciones-por-vigencia` y `designaciones-de-un-cargo`.
  - Cada intención apunta a **la misma** herramienta certificada de la capa 1 con parámetros fijos, de modo que una sola implementación sirve a las dos capas.
- **Capa 1: las mismas 4 o 5 herramientas de A (1–4 y 6), implementadas como SQL certificado.**
  - SQL fijo o por plantilla con enums cerrados, `$n` ligados y ejecución con `EjecutorDeConsulta`: rol de solo lectura, actor en el GUC, RLS, tope y máscara.
  - Antes de ejecutar, chequeo de permiso de familia con `identity.asistente_tiene_permiso`.
  - El LLM elige entre las herramientas y una salida explícita `ninguna`.
- **Capa 2: el respaldo Text-to-SQL** con las técnicas y las reglas de frontera de §5.

#### 6.4 Cobertura y número de llamadas
Estimación propia, no medida.

**Capacidad (34 ítems):**

| Arquitectura | 1 llamada | Varias llamadas o cálculo del LLM | Sin respuesta posible | Ambiguas (2) | No contestables (6) |
|---|---|---|---|---|---|
| A con endpoints **existentes** (espejo) | ~4–5: cap-002, 011 y 007 desde `catalogos`/`pedidos`, con orden o filtro hecho por el LLM; cap-008/023 solo período activo | ~10: cap-004/033/034 vía `catalogos` (**omite personas con pedido vivo**: falsa silenciosa), cap-003/005/006/010/022 contando sobre el volcado, cap-012/014/016 período por período (y `periodos` exige `PeriodosAdministrar`), cap-015 cruzando materias y carreras | ~8: cap-013 (cerradas: no hay endpoint), cap-025–029 (portal ajeno), cap-001 (carreras; depende de permisos de administración), cap-009 parcial | El agente tiene que detectarlas solo (AMBROSIA: mal) | 0 llamadas; abstención a criterio del modelo |
| A con catálogo **6.2** | 26 de 26 contestables (cap-009/008/023 sin filtro, acotadas por ámbito; cap-024 → `sin_permiso` explícito) | 0 necesarias (`buscar_entidades` solo si el modelo duda) | 0 | 1 llamada → `ambiguo` con candidatos → aclaración | 0 llamadas; sin señal determinista; riesgo de forzar una herramienta parecida (cap-030 → perfil) |
| B (6.3) | Capa 0: ~10–14 ítems de F1/F4/F5 con slots completos (0 llamadas de selección). Capa 1: el resto de las 26 en 1 llamada de selección | 0 | 0 en este dataset. El respaldo solo cubre la cola **fuera** del dataset | Capa 0: el detector de ambigüedad y `ambiguo` de la herramienta → menú | `ninguna` → respaldo con `es_contestable=false`/categoría, o rechazo por plantilla (hoy Claude acierta 8/8 abstenciones; Qwen responde algunas) |

**Robustez (15).**
- Los errores de tipeo en slots (rob-005 "Dattos") los resuelve la resolución difusa del servidor, igual en A y en B.
- Los errores fuera de slots (rob-004 "carogs", rob-006 "cilcos") los absorbe el LLM en la selección de herramienta. Para la capa 0 de B hace falta tolerancia a errores en los términos disparadores; si no, caen a la capa 1 sin perder precisión.
- rob-008 "auxiliares de primera" requiere sinónimo en el enum de cargo.
- rob-012 "solicitudes urgentes" → `prioritario`.

**Diálogo (11 turnos).** Los seguimientos se convierten en una **diferencia de parámetros** sobre la llamada anterior:
- dia-001 t2: `vigencia = cerradas`.
- dia-004 t3: `+ estado = rechazado`.

Eso es más simple y verificable que reescribir SQL, siempre que el hilo guarde la llamada canónica (nunca filas, regla actual).

dia-005 t2 ("esa materia", que solo aparece en la respuesta) se comporta distinto en cada arquitectura:
- **A**: el agente tiene el resultado de la herramienta en contexto y lo resuelve con naturalidad, pero eso implica guardar filas en el hilo, cosa que hoy se evita a propósito.
- **B**: necesita el "arrastre de consulta" existente (`openspec/changes/asistente-arrastre-de-consulta/`) adaptado a llamadas canónicas.

dia-002 (aclaración) funciona igual en las dos.

**Social (20).**
- En A, solo si conserva el enrutador social delante del agente. Si no, los 11 ítems `social` fallan.
- En B, ya está resuelto.
- Los 6 `negativo` exigen que ni el enrutador social ni la capa 0 capturen de más.

#### 6.5 Lectura de precisión, A vs. B, con catálogos equivalentes

- **Dentro de la cobertura de las herramientas, las dos arquitecturas son prácticamente iguales**: misma selección, misma extracción de argumentos, misma resolución en el servidor. Las diferencias reales son tres:
  1. **B** dispara la capa 0 sin modelo cuando hay intención con slots completos, lo que deja un error de selección ≈ 0 si las reglas son precisas.
  2. **A** obtiene `sin_permiso` explícito gratis por el 403. B lo obtiene si cada herramienta consulta el permiso primero.
  3. **A** permite encadenar y recuperarse de un `no_encontrado` en la misma vuelta, pero para un 8B encadenar es más fuente de error que de rescate (τ-bench, Live API-Bench).
- **Fuera de cobertura, ganan precisión de formas opuestas**:
  - **A** no tiene respaldo: se abstiene, salvo que el agente "fuerce" una herramienta parecida, que es su modo de falla típico.
  - **B** responde la cola con el carril SQL, cuya precisión condicional hoy es de ~79 % con Qwen3-8B. Para no perder precisión, el respaldo tiene que estar sujeto al umbral p/(1+p) por clase y a las reglas de frontera de §5. Con un respaldo así acotado, B ≥ A en precisión y B > A en cobertura.
- **Sesgo de evaluación.** Si los catálogos 6.2 y 6.3 se derivan de estos 80 ítems, la cobertura de 26/26 es optimista por construcción, igual que el invariante de disjunción para los ejemplos. Hacen falta:
  - preguntas nuevas, ciegas, escritas por usuarios del Departamento (ARS-65 y el hueco 1 de la definición);
  - una partición "dentro" / "fuera de catálogo", para medir por separado precisión de herramientas y precisión del respaldo, como hace dbt (dentro vs. fuera del alcance).

### Gaps
- Las cifras de cobertura de la tabla 6.4 son estimaciones manuales. No verifiqué qué permiso exige `GET api/administracion/catalogos` (carreras) ni el universo exacto de `DocenteAdministracionDto.Asignaciones`.
- No hay medición de cuántas preguntas reales caerían fuera de un catálogo de 5–6 herramientas: no hay corpus de tráfico real (`arquitectura_actual.md` §8).
- No hay medición de cuántas veces un 8B "fuerza" una herramienta para una pregunta fuera de dominio. Habría que medir los 6 `no_contestable` de capacidad y los 3 de social con el catálogo 6.2 frente a `ninguna`.
