# El híbrido cerrado minimiza las respuestas falsas

La técnica ganadora para que el chatbot de Ars Docendi dé la menor cantidad posible de respuestas incorrectas es un **híbrido cerrado de mínimo error (B\*)**. Lo componen siete piezas:

1. intenciones deterministas con una **verificación de cierre**;
2. una **única llamada estructurada** que elige entre 5 o 6 herramientas certificadas o una salida explícita `ninguna`;
3. **resolución de entidades en el servidor**, con estados explícitos;
4. **redacción por plantilla**;
5. **reglas de frontera** duras;
6. un **respaldo Text-to-SQL que se apaga en las placas locales**, o que escala a Claude Opus 5.5 con ZDR y seudonimización si la política de datos de la UNLaM lo admite;
7. **MCP solo como transporte opcional**: ninguna medición muestra que el protocolo cambie la precisión.

En local la ventaja es amplia y estructural. Con la RTX 3070 se estima ≈2–7 % de respuestas falsas para B\* contra ≈15–30 % para un agente MCP con todas las optimizaciones; con la RTX 5070, ≈1–5 % contra ≈10–25 %. Son **estimaciones** armadas a partir de benchmarks heterogéneos, no mediciones. Con la API de Claude la diferencia se reduce a un empate técnico (≈1–4 % contra ≈3–8 %, con intervalos que se solapan). La razón es que un agente que adopta estados del servidor y plantillas termina siendo "B sin capa 0 y sin respaldo".

La ronda adversarial cambió la comparación en cuatro puntos:

- **El enrutador tiene un defecto de cierre.** `ResolutorDeIntenciones` captura preguntas sin comprobar que todos sus términos queden explicados, así que puede emitir respuestas falsas con sello de certificadas.
- **El respaldo local no pasa el umbral.** La cota inferior de Wilson de su precisión (≈62 % con 26/33) queda por debajo del umbral p/(1+p) = 66,7 %.
- **A y B convergen.** Con todas las optimizaciones, A termina siendo casi la misma arquitectura que B.
- **El dataset actual no discrimina en el nivel Claude.** Claude con Text-to-SQL ya tiene 0 respuestas falsas en 78 ítems.

La confirmación queda a cargo de un experimento preregistrado en la rama `feature/asistente-banco-mcp-vs-hibrido`, con un conjunto ciego de al menos 300 preguntas, McNemar exacto por ítem y la decisión de producción tomada sobre la 5070.

> **Convenciones.** Las rutas de código son relativas a la rama `feature/asistente-modelo-local` (HEAD `bc8fc86`), donde vive el módulo del asistente. **[estimado]** marca cálculos o rangos propios; **[vendor]** marca fuentes con interés comercial; **[resumen]** marca cifras tomadas del resumen de un buscador porque el proxy bloqueó la fuente primaria.

## La primera ronda ubicó el error en las decisiones del modelo, no en el protocolo

La primera ronda de investigación ordenó la evidencia según **cuántas decisiones no deterministas se le delegan al modelo en cada turno**. Esa resultó ser la variable que mejor predice las respuestas falsas.

En los benchmarks de llamadas encadenadas, el éxito por tarea cae aproximadamente como p^n. En ComplexFuncBench, GPT-4o pasa de **80,6 % de exactitud por llamada a 60,5 % de éxito por tarea**, y Qwen2.5-7B, de 18,2 % a 5,0 % ([ComplexFuncBench](https://github.com/THUDM/ComplexFuncBench)). Con modelos del tamaño que entra en las placas del proyecto, el colapso empieza en la tercera decisión dependiente. Qwen3-14B logra **92 % con una herramienta, 84 % con una cadena de dos y 16 % cuando tiene que ramificar** según un resultado intermedio; Qwen3-8B logra 76, 64 y 24 % ([AgentFloor](https://arxiv.org/html/2605.00334v1)) [resumen].

El modo de falla más peligroso es silencioso. Cuando a la pregunta le falta un parámetro, Qwen3-8B lo pide solo entre 4 y 21,5 % de las veces y en el resto lo adivina; Claude Haiku 4.5 lo pide 53,5 % de las veces ([olmo-eval PR #449](https://github.com/allenai/olmo-eval/pull/449); [Inspect evals BFCL](https://ukgovernmentbeis.github.io/inspect_evals/evals/assistants/bfcl/index.html)) [resumen]. Un error de parámetro termina en respuesta final incorrecta con probabilidad ≈0,62 ([AgentProp-Bench](https://arxiv.org/pdf/2604.16706)) [resumen]. Un `GET /designaciones?carrera=…` con la carrera adivinada devuelve filas reales y una respuesta perfectamente plausible.

La abstención es la segunda debilidad, y es la que separa a las arquitecturas:

- **Modelos chicos.** Con solo herramientas distractoras disponibles, Qwen3-8B alucina una llamada en **36,2 % de los casos, y en 56,8 % con thinking** ([Reasoning Trap, ACL 2026](https://arxiv.org/pdf/2510.22977)) [resumen].
- **Modelos frontera.** El mejor agente evaluado decide bien cuándo no actuar solo en el **59,5 %** de los pares ([AgentAbstain](https://arxiv.org/pdf/2607.10059)) [resumen].
- **Text-to-SQL.** Las señales estadísticas baratas (self-consistency, log-prob, señales estructurales) tienen un AUROC de 0,61–0,68 para predecir la corrección y "no ofrecen un subconjunto válido de bajo riesgo". El mejor ensemble de jueces contesta el **27 % de las preguntas con 24 % de riesgo selectivo** ([arXiv 2607.06799](https://arxiv.org/html/2607.06799v1)) [resumen].

La literatura propone separar dos tipos de abstención. La **estructural** consiste en que lo que no tiene forma autorizada directamente no se puede expresar. La **estadística** se reserva para un respaldo generativo ([Never the Number](https://arxiv.org/pdf/2608.13926)) [resumen].

La redacción agrega un tercer término de error. Ni siquiera el mejor modelo del leaderboard FaithJudge baja de **6,65 % de alucinación global** ([FaithJudge, EMNLP 2025](https://aclanthology.org/2025.emnlp-industry.54/)) [resumen]. No existe un benchmark que mida cuántos números se reportan mal al narrar filas de SQL o de una API. La mitigación consistente es sacar los números del LLM.

### La superficie correcta es la misma para A y para B

La granularidad de las herramientas pesa más que el protocolo. En MCP-GRANITE, el nivel intermedio de unas 4 herramientas consolidadas dio la mayor completitud en 8 de 9 modelos: **+16,4 % frente al espejo fino de endpoints**. El extremo de una sola herramienta genérica fue el peor, y su tasa de "cero llamadas" subió de 10,2 % a 28,2 % ([MCP-GRANITE](https://arxiv.org/pdf/2609.24161)) [resumen]. Componer sobre APIs genéricas es más difícil que escribir SQL: Live API-Bench convirtió preguntas de BIRD en secuencias de API y la completitud fue de **7 a 47 %** ([Live API-Bench, EACL 2026](https://aclanthology.org/2026.eacl-long.143/)) [resumen].

De ahí sale un catálogo de 5 o 6 herramientas por familia del dominio:

| Herramienta | Qué cubre |
|---|---|
| `listar_catalogo` | Catálogos y listados simples |
| `consultar_designaciones` | Plantel, designaciones de una persona, agregaciones de designaciones |
| `consultar_pedidos` | Pedidos por estado, novedad, prioridad, materia o carrera |
| `buscar_docentes_por_perfil` | Habilidades, formación, certificaciones e intereses del portal |
| `buscar_entidades` | Resolver entidades cuando el tipo es dudoso |
| `mi_alcance` | Ámbito y permisos del actor |

Cada una tiene enums cerrados, una `medida` (`listado | conteo | suma_horas`), un `agrupar_por` cerrado y la agregación resuelta en el backend. Ese catálogo es el patrón `query_metrics` de dbt partido por dominio ([dbt MCP](https://docs.getdbt.com/docs/dbt-ai/about-mcp)) [vendor]. Con él, las 26 preguntas contestables de capacidad se resuelven en una sola llamada. Esa cobertura es **optimista por construcción**, porque el catálogo se diseñó mirando el dataset.

Hay dos hallazgos del repo que valen para las dos arquitecturas:

- **Los endpoints de UI tienen filtros silenciosos.** `GET /api/designaciones/catalogos` excluye a las personas con un pedido vivo en el período activo ([ServicioCatalogosDesignaciones.cs](backend/src/Modules.Designaciones/Services/ServicioCatalogosDesignaciones.cs)). Un agente que pregunte "¿quiénes dictan Bases de Datos?" a ese endpoint recibiría una lista parcial sin ninguna advertencia. Por eso A exige endpoints nuevos y propios del asistente.
- **La resolución de entidades va en el servidor, con estado explícito.** La causa principal de respuestas falsas con SQL válido es el literal mal resuelto, porque "cero filas de un SQL válido son indistinguibles de «no hay»" ([proposal de recuperación de valores](openspec/changes/asistente-recuperacion-de-valores/proposal.md)). Los estados son `ok | ambiguo | no_encontrado | sin_permiso | vacio`, y la consulta no se ejecuta mientras quede una entidad sin resolver. Los LLM recuperan apenas ~27 % de las interpretaciones de una pregunta ambigua ([AMBROSIA](https://arxiv.org/abs/2406.19073)), así que detectar la ambigüedad no puede quedar en manos del modelo.

### Modelos por nivel según la primera ronda

| Nivel | Candidato principal | Alternativa | Evidencia clave | Restricción |
|---|---|---|---|---|
| RTX 3070 8 GB | Qwen3.5-9B Q4_K_M (a medir) | Qwen3-8B Q4_K_M (línea de base medida) | BFCL-V4 66,1 y TAU2 79,1 autorreportados para Qwen3.5-9B; Qwen3-8B Q4 F1 0,919 con ~5 herramientas ([Docker](https://www.docker.com/blog/local-llm-tool-calling-a-practical-evaluation/)) | 1 slot de 16K con Qwen3-8B (medido); nunca bajar de Q4, porque a 3 bits la decisión de llamar colapsa hacia la inacción ([arXiv 2608.06564](https://arxiv.org/html/2608.06564v2)) [resumen] |
| RTX 5070 12 GB | Qwen3-14B Q4_K_M, monousuario | Qwen3.5-9B Q6_K/Q8_0 multi-slot, o Qwen3-8B-AWQ en vLLM (configuración de producción) | Qwen3-14B Q4 F1 0,971; 54,2 tok/s con **pico de 12,0 GB a 4K** ([smeltcore](https://smeltcore.com/recipes/qwen3-14b-on-rtx-5070-q4-k-m-gguf-via-ollama-or-llama-cpp)) [resumen] | MoE con offload descartados: prefill de 2,78 tok/s con los expertos en CPU ([DEV](https://dev.to/upayanghosh/from-oom-to-262k-context-running-qwen3-coder-30b-locally-on-8gb-vram-1ej1)) |
| API de Claude | Sonnet 5.5 (herramientas); Opus 5.5 (SQL de respaldo) | Haiku 5.5 (extracción rápida) | Opus 5.5 87,8 % contra Sonnet 5.5 73,8 % en Text-to-SQL ([AIMultiple](https://aimultiple.com/text-to-sql)) [resumen]; Opus 5 85,8 % en MCP-Atlas ([Scale](https://labs.scale.com/leaderboard/mcp_atlas)) [resumen] | Fable 5.1 exige 30 días de retención (no ZDR); el `tool_choice` forzado devuelve 400 en Opus y Sonnet 5.5; los schemas `strict` se cachean 24 h ([API and data retention](https://platform.claude.com/docs/en/manage-claude/api-and-data-retention)) |

Los anuncios de los modelos 5.5 no publican τ²-bench, BFCL, MCP-Atlas ni BIRD. Toda cifra de tool use para esa generación viene de agregadores o de la generación anterior. El veredicto de la primera ronda fue claro en local: (A) no es confiable en la 3070, es limitado en la 5070 y viable con Claude. En los tres niveles el diseño preferible era (B), con Claude como extractor o respaldo.

## Diecinueve técnicas de optimización, pocas con efecto medido en modelos chicos

El catálogo de técnicas muestra un patrón: lo que más mejora a los modelos de 8–14B es **estructural** (menos herramientas visibles, decisiones de abstención fuera del LLM, cadenas cortas, argumentos validados en el servidor). Esas técnicas son nativas de B y solo se reproducen parcialmente en A. Con Claude, las técnicas propias del proveedor (tool search, `strict`, `input_examples`) llevan a A a niveles altos de selección. Ninguna técnica documentada lleva la abstención de un modelo chico al nivel de un estado determinista.

| Técnica | Efecto medido | Modelos locales 7–14B | Claude | ¿A, B o ambas? |
|---|---|---|---|---|
| Filtrar o recuperar las herramientas visibles (router, tool search, `tools/list` filtrada por permiso) | Opus 4 49→74 %; Opus 4.5 79,5→88,1 % ([Anthropic](https://www.anthropic.com/engineering/advanced-tool-use)) [vendor]; Sonnet 4.6 87,1→93,1 % con shortlist adaptativa ([arXiv 2605.24660](https://arxiv.org/pdf/2605.24660)) [resumen] | Más necesario: 0–49 % con más de 15 herramientas en JSON ([TSCG](https://arxiv.org/pdf/2605.04107)) [resumen] | Tool search solo con más de 10 herramientas o más de 10K tokens | Ambas (B nativo vía router) |
| Granularidad por familia (L3) | +16,4 % de completitud frente al espejo fino; una sola herramienta es lo peor ([MCP-GRANITE](https://arxiv.org/pdf/2609.24161)) [resumen] | Clave | Útil | Ambas |
| Herramientas de nivel tarea con agregación en el servidor | Qwen3-14B 92/84 % con 1–2 llamadas frente a 16 % al ramificar ([AgentFloor](https://arxiv.org/html/2605.00334v1)) [resumen] | Clave | Útil | Ambas |
| Nombres alineados con el preentrenamiento (PA-Tool) | Hasta +17 pts y −80 % de errores de desalineación; la mayor ganancia, en "no hay herramienta adecuada" ([arXiv 2510.07248](https://arxiv.org/html/2510.07248)) [resumen] | Sí (diseñada para chicos; en inglés) | Sin dato | Ambas |
| `input_examples` / few-shot | 72→90 % en parámetros complejos ([Anthropic](https://www.anthropic.com/engineering/advanced-tool-use)) [vendor] | Por prompt, sin dato; en el repo, 8 ejemplos más cambiaron 22 de 34 selecciones | Nativo | Ambas |
| Descripciones detalladas o reescritas automáticamente | +60,89 % de éxito con más de 150 herramientas; +1,4 pts en Gemini-3-pro ([arXiv 2602.20426](https://arxiv.org/html/2602.20426v2)) [resumen]; una descripción editada da ×10 de uso ([EMNLP 2025](https://aclanthology.org/2025.emnlp-main.1060/)) | Sí, con catálogos grandes; frágil | Efecto chico | Ambas |
| Decodificación restringida / `strict` | Validez de schema →100 %, sin mejora semántica ([arXiv 2609.23742](https://arxiv.org/html/2609.23742v1)) [resumen]; puede bajar exactitud en modelos de ≤1,7B ([Constraint Tax](https://www.alphaxiv.org/abs/2605.26128)) [resumen] | Solo la forma; vLLM no implementa `strict` para herramientas | Gratis en precisión | Ambas |
| Thinking apagado al decidir si llamar o abstenerse | Qwen3-8B 56,8→36,2 % de alucinación con distractores ([Reasoning Trap](https://arxiv.org/pdf/2510.22977)) [resumen]; −9 pp en cadenas de 2 ([AgentFloor](https://arxiv.org/html/2605.00334v1)) | Sí, por tarea | En Opus 5.5 no se apaga | Ambas (B lo aplica solo a la decisión) |
| Resolución de entidades en el servidor + IDs + estados | Sin cifras; Anthropic: resolver IDs a nombres "significantly improves precision" ([Anthropic](https://www.anthropic.com/engineering/writing-tools-for-agents)) [vendor] | Sí | Sí | Ambas |
| Errores accionables (`isError`) | Opus 4.6 corrige 65,36 % tras el feedback; o3, 18,57 % ([CCTU](https://arxiv.org/pdf/2603.15309)) [resumen] | Débil (autocorrección pobre) | Sí | A sobre todo; B mapea estados a plantillas |
| Resolvedor closed-world contra el registro de herramientas | Rechazó 322 + 154 alucinaciones reales ([arXiv 2609.19425](https://arxiv.org/pdf/2609.19425)) [resumen, autorreportado] | Sí | Sí | A (B lo logra por gramática) |
| Fine-tuning con irrelevancia + RPO | Alucinación de herramienta 19→1,2 % en 8B sin perder BFCL AST ([When2Call](https://github.com/NVIDIA/When2Call)); TinyAgent 7B 41→83–85 % ([ACL 2024](https://aclanthology.org/2024.emnlp-demo.9.pdf)) | Mayor techo; riesgo de romper la conversación | No aplica | Ambas |
| Programmatic tool calling / code mode | +2,9 y +4,7 pts, −37 % de tokens ([Anthropic](https://www.anthropic.com/engineering/advanced-tool-use)) [vendor] | Sin evidencia | Solo para ≥3 llamadas; **no ZDR** | A (solo Claude) |
| Plantillas desde datos estructurados + verificador de números | Sin benchmark directo; mejor modelo en FaithJudge con 6,65 % de alucinación ([FaithJudge](https://aclanthology.org/2025.emnlp-industry.54/)) [resumen] | Clave | Útil | Ambas |
| `outputSchema` / `structuredContent` | Sin medición de exactitud; habilita verificación determinista ([spec MCP 2026-07-28](https://github.com/modelcontextprotocol/modelcontextprotocol/blob/main/docs/specification/2026-07-28/server/tools.mdx)) | Indirecto | Indirecto | Ambas (B en proceso) |
| Abstención estadística en el respaldo SQL (self-consistency, juez) | AUROC 0,61–0,68; ensemble 0,82 → 27 % de cobertura con 24 % de riesgo ([arXiv 2607.06799](https://arxiv.org/html/2607.06799v1)) [resumen]; en el repo, self-consistency +0,13 pts con p95 de 10,6 a 50 s | Mala señal | Juez de dos proveedores, mejor | B |
| Regla de cierre del enrutador (explicación completa) | Sin publicación; diseño propio tras la ronda adversarial | Sí | Sí | B |
| Elicitation de MCP | Sin medición; rediseñada en 2026-07-28 como Multi Round-Trip Requests ([changelog](https://github.com/modelcontextprotocol/modelcontextprotocol/blob/main/docs/specification/2026-07-28/changelog.mdx)) | Depende del cliente | Depende del cliente | A (B usa el menú de aclaración existente) |
| MCP como transporte | Sin mejora apreciable frente a function calling ([MCPBench](https://arxiv.org/abs/2504.11094)) [resumen]; −9,5 % con servidores MCP genéricos de terceros ([arXiv 2508.12566](https://arxiv.org/pdf/2508.12566)) [resumen] | Neutro (llama-server y vLLM no hablan MCP) | Neutro | Ninguna gana precisión |

La lectura que importa para la decisión está en la última fila: **MCP es transporte**. Las propiedades que bajan el error (estados del servidor, `structuredContent` validable, errores accionables) se pueden implementar igual en proceso. Lo que separa a A de B es otra cosa: quién elige la herramienta, quién completa los slots y quién decide abstenerse.

## La ronda adversarial encontró el defecto en el propio enrutador

La segunda ronda puso a dos abogados a construir el mejor caso para cada arquitectura (`abogado_mcp.md` y `abogado_hibrido.md`), con libertad total de rediseñar endpoints y todas las técnicas encendidas.

### Steelman de A: solo existe en el nivel Claude

El mejor caso para el agente MCP se apoya en tres argumentos, válidos solo con un modelo frontera:

1. **Composición.** Componer 2–3 primitivas certificadas puede equivocarse menos que el SQL libre del respaldo de B. Opus 5 llega a 85,8 % en tareas MCP de 3 a 6 llamadas ([Scale MCP-Atlas](https://labs.scale.com/leaderboard/mcp_atlas)) [resumen].
2. **Detección de restricciones.** Un LLM frontera detecta mejor que una tabla léxica lo que la herramienta no soporta. En CLINC150, el recall fuera de alcance de Claude es **85,6 contra 58,1 de RoBERTa fine-tuneado y 36,4 de TF-IDF** ([arXiv 2608.20371](https://arxiv.org/html/2608.20371)) [resumen].
3. **Corrección en la misma vuelta.** Con errores `isError`, Claude corrige el 65 % ([CCTU](https://arxiv.org/pdf/2603.15309)) [resumen].

Hay además dos ventajas concretas de A:

- **`sin_permiso` explícito.** El 403 de la API distingue "no tenés permiso" de "no hay", mientras que bajo RLS las dos cosas son cero filas.
- **Anáfora al resultado.** En `dia-005` ("esa materia"), A tiene el resultado de la herramienta en contexto y resuelve la referencia sin esfuerzo.

En las placas locales, en cambio, el steelman no encontró un A competitivo. La única variante que podría bajar las falsas (una llamada, sin composición y sin respaldo) es **estructuralmente idéntica a "B sin capa 0 y sin respaldo"**, y su mérito es del diseño de herramientas, no del agente.

### Steelman de B: menos decisiones del modelo, todas verificables

El caso de B no se apoya en un modelo mejor. Se apoya en que **el error de una consulta certificada es ≈0 por construcción** y todo el error restante queda en tres puntos medibles: enrutador, slots y respaldo. En A, cada pregunta suma selección, argumentos, composición, decisión de abstenerse y redacción, todas a cargo del LLM y todas no deterministas.

Incluso en frontera, la inconsistencia entre corridas es grande. Sonnet 5.5 marca **85,2 % pass@3 contra 68,5 % pass^3** en Toolathlon ([BenchLM](https://benchlm.ai/compare/claude-haiku-5-5-vs-claude-sonnet-5-5)) [resumen]. En multiturno, todos los modelos probados caen en promedio **39 %** cuando la instrucción llega subespecificada y repartida en varios turnos ([Laban et al., ICLR 2026](https://arxiv.org/abs/2505.06120v1)) [resumen].

El abogado de B tipificó nueve modos de falla de A con severidad S3, es decir, respuestas incorrectas plausibles y sin señal. Los tres más dañinos son:

- **forzar una herramienta** ante preguntas infactibles: un "mail personal" mandado a `buscar_docentes_por_perfil`;
- **adivinar un parámetro omitido**: el período activo aplicado por defecto sin decirlo;
- **fabricar ante un vacío o una falla** de la herramienta.

### El error se descompone en cuatro términos

Los dos abogados llegaron a la misma descomposición. Con fracciones del tráfico c_in (resuelto por una llamada certificada), c_comp (por 2–3 primitivas), c_out (solo por SQL) y c_inf (infactible):

E_A ≈ c_in·e_sel + c_comp·e_comp + c_out·f_A + c_inf·g_A

E_B ≈ c_in·(q0·FP0 + (1−q0)·e_sel + FN·r_fb) + c_comp·r_fb + c_out·r_fb + c_inf·g_B

Donde:
- e_sel es el error de selección de herramienta;
- e_comp, el error al componer varias llamadas;
- f_A, la tasa con que A fuerza una herramienta parecida en lugar de abstenerse;
- g_A y g_B, la tasa con que cada arquitectura responde una pregunta infactible;
- q0, la fracción que captura la capa 0;
- FP0, su tasa de sobrecaptura;
- FN, los falsos negativos que el enrutador manda al respaldo;
- r_fb, el riesgo selectivo del respaldo.

La diferencia neta entre A y B se reduce a cuatro términos:

1. la capa 0 léxica de B;
2. el respaldo SQL de B;
3. la composición de A;
4. el manejo del multiturno.

**El veredicto depende del signo de esos cuatro términos, no de "MCP contra híbrido".**

### Qué cambió en la comparación después de iterar

| Punto | Después de la ronda 1 | Después de la ronda adversarial |
|---|---|---|
| Fuente principal de error en B | El respaldo SQL (≈21 % de falsas sobre lo contestado con Qwen3-8B) | **Falsos positivos de la capa 0**: respuestas falsas con sello de certificadas, por el defecto de cierre del resolutor |
| Respaldo SQL local | Aceptable con abstención y rótulo "no certificada" | **No justificable con la evidencia disponible**: la cota inferior de Wilson al 95 % de 26/33 es ≈62 %, debajo de 66,7 % (p = 2) |
| A contra B con Claude | B gana | **Convergencia**: A con estados del servidor y plantillas = B sin capa 0 y sin respaldo; margen de ≤1–2 pp en cualquier dirección, y A podría ganar si B conserva la capa 0 ingenua |
| Base empírica en el nivel Claude | Benchmarks públicos | **Dataset saturado**: `claude-sonnet-5` ya tiene 0 falsas en 78 ítems; hace falta un set ciego de ≥300 preguntas |
| Composición multi-llamada | Pasivo | Pasivo en local; en Claude es **un escalón legítimo del respaldo** ("A acotado dentro de B"), antes del SQL libre |
| "Sin permiso" | Lo resuelve la abstención por RLS | A lo obtiene gratis con el 403; **B tiene que consultar `identity.asistente_tiene_permiso` por familia** antes de ejecutar |
| Papel de MCP | Transporte | Confirmado: sin mediciones de exactitud para `outputSchema`, anotaciones, resources ni elicitation |

**El defecto de cierre está en el código, no es una hipótesis de la literatura.** El resolutor acepta una intención así:

```csharp
var candidatas = catalogo.Intenciones
    .Where(i => i.Terminos.IsSubsetOf(terminos) && !i.Excluye.Overlaps(terminos))
    .ToList();
```

Para resolver, alcanza con que cada slot exigido resuelva a un único valor. **No hay ningún control sobre los términos sobrantes** ([ResolutorDeIntenciones.cs](backend/src/Modules.Asistente/Application/Determinista/ResolutorDeIntenciones.cs)). El propio comentario de la clase lo reconoce: la guarda de "no dejar tokens de contenido" que hace viable al enrutador social "no sirve acá".

Con el catálogo vigente ([intenciones.json](backend/src/Modules.Asistente/Recursos/intenciones.json)), "¿Cuántos pedidos rechazados hubo en Ingeniería Industrial el ciclo pasado?" satisface {cuantos, pedido} + `estado = rechazado`, y la carrera y el período se descartan en silencio. "¿Cuántos ayudantes de primera hay en Bases de Datos?" cae en `designaciones-de-un-cargo` y devuelve el conteo global.

Hoy el daño es latente: la tabla dorada da **0 de 39 capturas**, en parte porque «rechazadas» no coincide con el literal `rechazado`. Pero **crece con cada sinónimo que se agrega para subir la cobertura**, que era justamente la recomendación de la ronda 1. Además, el enrutador corre después del reescritor LLM, así que hereda sus errores con sello de certificado ([EnrutadorDeDominio.cs](backend/src/Modules.Asistente/Application/Determinista/EnrutadorDeDominio.cs)).

El respaldo local cayó por una cuenta sencilla. Si responder bien vale +1, responder mal vale −p y abstenerse vale 0, solo conviene responder cuando P(correcto) > p/(1+p). La precisión condicional medida de Qwen3-8B es 26/33 = 78,8 %, pero con n = 33 no se puede afirmar que supere el 66,7 % que exige p = 2, y mucho menos en las clases con más falsas ([modelo-local.md §3](docs/architecture/modelo-local.md)).

Por último, la línea de base de Claude con Text-to-SQL suma **0 respuestas falsas en 78 ítems**: 30/32 en capacidad con 2 sobre-abstenciones, 14/15 en robustez, 11/11 en diálogo y 20/20 en social ([lineas-de-base/capacidad.json](backend/eval/lineas-de-base/capacidad.json)). En ese nivel ninguna arquitectura puede ganar sobre el dataset vigente.

## B* gana con margen en local y empata o gana por poco con Claude

La tabla junta las estimaciones de las dos rondas. Los rangos de respuestas falsas están **[estimado]** sobre una mezcla hipotética de tráfico (60 % de una llamada dentro de cobertura, 10 % componibles, 15 % fuera de cobertura, 10 % infactibles, 5 % ambiguas). Las latencias salen del modelo T ≈ Σ(prefill/pp + tokens de salida/tg) + t_herramientas, contrastado con el p50 medido de la línea de base. **Nada de la 5070 ni de Claude 5.5 está medido en este dominio.**

| Nivel | Arquitectura | Respuestas falsas | Latencia, 1 usuario | Latencia, 4 / 8 en vuelo | Concurrencia | Privacidad |
|---|---|---|---|---|---|---|
| RTX 3070 | C0, Text-to-SQL actual (referencia) | **20,6 % (7/34, medido)** | p50 2,4–2,9 s; p95 6,6 s (medido) | Cola serial | 1 slot de 16K (medido) | Todo local |
| RTX 3070 | A, agente MCP optimizado | ≈15–30 % | ≈8–11 s (4 pasos) | ≈25–30 s (4 slots) o 35–45 s (1 slot) / ≥50 s | 1 slot con Qwen3-8B; 2–4 solo con Qwen3.5-9B | Todo local |
| RTX 3070 | **B\*** | **≈2–7 %** | ≈1–2 s con plantilla; 2–4 s con redacción | ≈3–5 s / 6–9 s | 2–4 slots por el prefijo más chico | Local; escalada opcional seudonimizada |
| RTX 5070 | A | ≈10–25 % | ≈5–7 s | ≈7–10 s / 10–16 s (límite de KV) | ~3–7 agentes en vLLM; Qwen3-14B: 1 slot | Todo local |
| RTX 5070 | **B\*** | **≈1–5 %** | ≈1,5–2,5 s | ≈2–3,5 s / 3–5 s | ~8–12 en vuelo (vLLM, AWQ) | Local; escalada opcional seudonimizada |
| Claude API | C0 (`claude-sonnet-5`, legacy) | **0 en 78 ítems (medido, dataset saturado)** | Sin cifras publicadas | Límites de tasa, no de VRAM | Sin límite local | Pregunta + esquema salen; ZDR necesario |
| Claude API | A | ≈3–8 % | ≈1–3 s + un round-trip por paso | Límites de tasa | Sin límite local | **Las filas con PII entran al contexto**; ZDR obligatorio; PTC no es ZDR |
| Claude API | **B\*** | **≈1–4 %** | ≈1–3 s | Límites de tasa | Sin límite local | El modelo ve la pregunta y el catálogo; las filas se renderizan en el backend; ZDR |

Hay un efecto de privacidad que no se veía en la ronda 1. En el nivel Claude, A tiene que enviarle al proveedor los resultados de las herramientas, con nombres y datos del dominio, para que el agente razone sobre ellos. B\* con plantillas solo necesita mandarle la pregunta y el catálogo de herramientas. Las filas se arman en el backend [estimado]. Fable 5.1 queda fuera de cualquier variante: exige 30 días de retención ([API and data retention](https://platform.claude.com/docs/en/manage-claude/api-and-data-retention)).

### Pros y contras de A, solo por precisión

| A favor de A | En contra de A |
|---|---|
| El 403 explícito distingue "sin permiso" de "no hay" | La abstención depende del juicio del modelo: Qwen3-8B fuerza una herramienta en 36,2 % de los casos con distractores ([Reasoning Trap](https://arxiv.org/pdf/2510.22977)); el mejor agente frontera acierta 59,5 % ([AgentAbstain](https://arxiv.org/pdf/2607.10059)) [resumen] |
| En Claude, compone primitivas certificadas con buena tasa (Opus 5: 85,8 % en MCP-Atlas) | La composición se derrumba en local: 84→16 % al ramificar con Qwen3-14B ([AgentFloor](https://arxiv.org/html/2605.00334v1)) [resumen] |
| Un LLM frontera ve las restricciones que una tabla léxica ignora (85,6 de recall fuera de alcance) | Adivina parámetros omitidos (Qwen3-8B pide el faltante solo 4–21,5 % de las veces) y el error no deja rastro |
| Se corrige en la misma vuelta con `isError` (Opus 4.6: 65 %) | Fabrica ante vacíos o fallas de la herramienta ([arXiv 2609.14758](https://arxiv.org/pdf/2609.14758)) [resumen] |
| Resuelve la anáfora al resultado ("esa materia") con el contexto | Es inconsistente entre corridas (Sonnet 5.5: pass@3 85,2 % contra pass^3 68,5 %) y deriva en multiturno (−39 %) |
| Las mismas herramientas sirven a clientes externos | Reutilizar endpoints de UI da respuestas parcialmente vacías sin aviso (`catalogos` excluye a personas con pedido vivo) |

### Pros y contras de B, solo por precisión

| A favor de B | En contra de B |
|---|---|
| Una sola decisión del modelo por turno: el colapso p^n no aplica | **Falso positivo certificado de la capa 0** si no hay cierre: el peor error posible, porque llega con sello de confianza |
| Abstención estructural: lo que no tiene herramienta no se puede expresar | El respaldo SQL local se equivoca en ≈21 % de lo que contesta y no existe una señal que aísle un subconjunto de bajo riesgo |
| Capa 0 determinista: la misma pregunta da siempre la misma respuesta | El respaldo puede "rescatar" lo que una herramienta rechazó con razón y convertirlo en un falso "no hay" ([Signed Rescue Routing](https://arxiv.org/pdf/2609.07786)) [resumen] |
| Redacción por plantilla: ningún número sale del LLM | Un error de definición en una consulta certificada se replica en todas las respuestas (TD-025: «Categoría 0..6» contra un catálogo 1..6; período por defecto incoherente entre cap-007 y cap-012/014) |
| Estado de conversación como slots, con eco visible | Las paráfrasis que caen en carriles distintos pueden dar respuestas distintas |
| RLS, rol de solo lectura y máscara intactos; cada consulta es un test `BR-*` | Bajo RLS, "sin permiso" es cero filas, salvo que se consulte el permiso de familia antes |

## La técnica ganadora: el híbrido cerrado de mínimo error (B\*)

B\* toma de A lo que la ronda adversarial demostró valioso (estados del servidor, `sin_permiso` explícito y composición acotada en Claude) y corrige los dos defectos de B (la capa 0 sin cierre y el respaldo local sin cota de confianza). Sus componentes son estos:

| Componente | Especificación | Por qué baja el error |
|---|---|---|
| Capas existentes delante | Enrutador social (cero tokens), reconocedor de aclaración y detector de ambigüedad, sin cambios | Los 11 ítems sociales y las ambiguas del fixture no llegan al modelo |
| **Capa 0 con verificación de cierre** | Intenciones reescritas con el vocabulario real (solicitud → pedido, nombramiento → designación, asignatura → materia, ciclo → período, urgente → prioritario). Captura **solo si cada token de contenido queda consumido** por un término de la intención, un slot, un modificador declarado o una palabra vacía. Bloqueo explícito ante negadores, comparativos, temporales y entidades no modeladas. Eco obligatorio de la interpretación. Si no captura, pasa a la capa 1, nunca responde a medias. En los seguimientos no consume la salida del reescritor: el turno se trata como una diferencia de slots sobre la llamada canónica | Elimina FP0, el único error "certificado"; el test de mutación lo vuelve falsable |
| **Capa 1: una sola llamada estructurada** | Salida {herramienta ∈ catálogo ∪ `ninguna`, slots}. En Claude, structured outputs (`output_config.format`) o `tool_choice: auto` + `strict`, porque el forzado devuelve 400 en Opus y Sonnet 5.5. En local, gramática xgrammar/GBNF y thinking apagado en la decisión. Temperatura 0. **Chequeo de anclaje**: cada slot se traza a un fragmento de la pregunta o a un default declarado; un término de dominio sin consumir lleva a `ninguna` | Ataca la selección forzada y el parámetro adivinado sin depender del juicio del modelo |
| **Herramientas certificadas** | Las 5–6 de familia, con enums cerrados, `medida` y `agrupar_por`. SQL por plantilla con `$n` ligados, ejecutado con `EjecutorDeConsulta` (rol de solo lectura, GUC del actor, RLS, tope de 200 filas, máscara). **Chequeo previo de `identity.asistente_tiene_permiso`** por familia, que devuelve `sin_permiso`. Cada default semántico (vigencia, período de pedidos, "posgrado") es una regla `BR-asistente-NNN` con test | Error de consulta ≈0 por construcción; iguala la ventaja del 403 de A |
| **Resolución de entidades en el servidor** | Parámetros "texto o id". Normalización con `unaccent`, minúsculas y sinónimos. Resolución exacta → contención → Damerau-Levenshtein ≤1 sobre el conjunto visible (ARS-165); personas solo por coincidencia exacta. Estados `ok`, `ambiguo` (con candidatos), `no_encontrado` (con sugerencias), `sin_permiso` y `vacio` (solo con todas las entidades resueltas) | Convierte el falso "no hay" en un estado explícito sin revelar la existencia de entidades fuera de alcance |
| **Redacción por plantilla** | Desde el resultado estructurado: `total` separado de las filas, `interpretacion` en eco y la frase "dentro de tu ámbito" en actores acotados; con truncado no se afirman conteos. Si el LLM une texto, `VerificadorDeAfirmaciones` exige que cada número y cada nombre estén en el resultado | Elimina la redacción infiel |
| **Reglas de frontera** | (a) Si la herramienta de la familia devolvió `no_encontrado`, `ambiguo` o `sin_permiso`, el respaldo no corre. (b) Toda respuesta del respaldo se rotula "no certificada" y muestra la interpretación o el SQL. (c) Sin permiso de familia, el respaldo no corre | Impide que el respaldo "rescate" lo que se rechazó con razón |
| **Respaldo en la 3070 y la 5070** | **Apagado (B0)** por defecto, con abstención y sugerencia de preguntas soportadas. Se habilita por clase de pregunta solo si la cota inferior de Wilson al 95 % de su precisión, medida en el set de desarrollo, supera p/(1+p). Variante preferida, si la política de datos lo admite: **escalar a Opus 5.5 con ZDR**, enviando pregunta y esquema seudonimizados (`<DOCENTE_1>`) y nunca filas. Si no se admite, abstención | El respaldo local no pasa hoy el umbral |
| **Respaldo en el nivel Claude** | Dos escalones, los dos rotulados: (i) Opus 5.5 compone **solo herramientas certificadas**, hasta 2–3 llamadas y sin PTC (no es ZDR); (ii) Opus 5.5 genera Text-to-SQL, lo verifica un juez independiente (Sonnet 5.5) y se aplica un umbral por clase calibrado con el evaluador. Si no supera el umbral, abstención | "A acotado" donde Claude sí compone bien; el juez de dos modelos es la única señal publicada con AUROC ≥0,8 |
| **Multiturno** | El hilo guarda la llamada canónica y los IDs devueltos, nunca filas. Los seguimientos son una diferencia de slots con eco. `DetectorDeCambioDeTema` actúa ante pivotes. La anáfora al resultado ("esa materia") se resuelve con el arrastre de consulta adaptado a IDs | Hace visible el arrastre de filtros que en A es silencioso |
| **MCP opcional** | Servidor en el Host sobre `IConsultasCertificadas` (`MapMcp("/api/asistente/mcp")`, token con audiencia propia, `AddAuthorizationFilters`), detrás de la bandera `Asistente__Mcp__Habilitado=false`. El sello "certificado" se reserva a las respuestas de las capas 0 y 1 del backend: un cliente externo que consume las herramientas **es** un A | No cambia la precisión; abre la puerta a un segundo cliente |

### Modelo recomendado por nivel para B\*

| Nivel | Capa 1 (selección y slots) | Respaldo | Notas |
|---|---|---|---|
| RTX 3070 8 GB | **Qwen3-8B Q4_K_M** como primario medido; **Qwen3.5-9B Q4_K_M** se promueve si gana en el set de desarrollo | Apagado, o escalado a Opus 5.5 | Thinking apagado, gramática solo en el empaquetado, nunca debajo de Q4; verificar el aislamiento entre slots de Qwen3.5 |
| RTX 5070 12 GB | **Qwen3-8B-AWQ en vLLM** como primario del experimento (configuración de producción); **Qwen3-14B Q4_K_M** monousuario o **Qwen3.5-9B Q6_K** como candidatos a promover | Apagado, o escalado a Opus 5.5 | vLLM sin `strict` para herramientas: la validez la da xgrammar en structured outputs; KV fp8 frágil en sm_120 |
| Claude API | **Sonnet 5.5** (primario); Haiku 5.5 si iguala en TRF, por latencia | **Opus 5.5** con juez Sonnet 5.5 | ZDR obligatorio; sin PII en `enum` de schemas `strict`; Fable 5.1 excluido |

## El plan de prueba en la rama `feature/asistente-banco-mcp-vs-hibrido`

La rama sale de `origin/feature/asistente-modelo-local` (HEAD `bc8fc86`), que ya trae el proveedor local, las optimizaciones y los 109 cassettes. El experimento confirma o refuta que B\* tiene menos respuestas falsas que el mejor A. El diseño base es el de `diseno_experimento.md`, con cuatro **ajustes que surgen de la ronda adversarial** y que esa nota no incluía:

1. los brazos B, B-strict y B0 implementan la capa 0 **con cierre**;
2. se agrega una ablación **B-ingenuo** (capa 0 tal como está en el código) para medir FP0;
3. se agrega una batería adversarial de capa 0;
4. se etiqueta en D1 una partición `composicion_2_3`, para no castigar a A por construcción.

### Hipótesis preregistradas

| Id | Enunciado | Prueba |
|---|---|---|
| H1a / H1b | En la 3070 / la 5070, con el modelo primario, TRF(B) < TRF(A) | McNemar exacto bilateral sobre los discordantes; Holm sobre {H1a, H1b, H2} |
| H2 | En Claude (Sonnet 5.5), \|TRF(A) − TRF(B)\| ≤ 5 pp | TOST pareado con bootstrap, sobre la media de k = 5 corridas por ítem |
| H3 | U_2(B) ≥ U_2(A), y la cobertura de B no cae más de lo que baja su TRF | Bootstrap pareado (co-requisito de decisión) |
| H4 | A ≡ A-lite: el transporte MCP no cambia la precisión | Discordantes con IC de Clopper-Pearson, límite superior ≤2 % |
| H5 | B-strict tiene menos TRF que B, y el riesgo selectivo del respaldo queda por debajo de p/(1+p) | McNemar y riesgo-cobertura por clase |
| H6 | Cero fugas entre ámbitos y cero inyecciones seguidas en todos los brazos | Conteo; un caso confirmado descalifica el brazo (bloqueante) |
| H7–H9 | B depende menos del modelo (diferencia en diferencias); cambios de veredicto bajo carga ≤2 %; menos arrastre en diálogo para B | Exploratorias (bootstrap, Clopper-Pearson, McNemar por turno) |
| H10 [ajuste] | FP0(B con cierre) < FP0(B-ingenuo) en la batería de mutaciones del enrutador | Conteo pareado; para el cierre se exige FP0 = 0 en D3f |

La **métrica primaria** es la tasa de respuestas falsas (TRF = falsas / N), con desenlaces por ítem extendidos. `Falsa` agrupa `filas_incorrectas`, `respondio_infactible`, `adivino_ambigua`, `redaccion_infiel`, `fuga_de_ambito` y `siguio_inyeccion`. Se agrega `AclaracionCorrecta`. Se reportan además el riesgo selectivo, la cobertura, U_p con p ∈ {0,5; 1; 2} y la fiabilidad "sin falsas a k", con el estimador C(n−f, k)/C(n, k).

### Brazos y modelos

| Brazo | Definición | Qué aísla |
|---|---|---|
| C0 | Text-to-SQL actual (`CarrilSql`, perfil B) | Control y puente con las líneas de base vigentes |
| A | Agente cliente MCP contra un servidor MCP HTTP en el Host que expone 1:1 `GET api/asistente/consultas/{familia}`, con todo encendido: `tools/list` filtrada por permiso y en orden determinista, `structuredContent`, `strict`, `input_examples`, `isError` accionable, resolución en el servidor, herramienta `no_puedo_responder`, cita de la llamada que sostiene la respuesta, enrutador social delante | El agente en su mejor configuración |
| A-lite | Las mismas definiciones, byte-idénticas, como `AIFunction` en proceso | El efecto del transporte (H4) |
| B | B\* con el respaldo `CarrilSql` y las reglas de frontera | El híbrido completo |
| B-strict | B con lint AST + juez + umbral por clase de 0,67, calibrado solo con D0 y D2 | Cuánto aporta la abstención estadística |
| B0 | B\* sin respaldo | Cota inferior de falsas y su costo en cobertura |
| B-ingenuo [ajuste] | B0 con la capa 0 actual, sin cierre; solo D0 y D3 | FP0 del defecto encontrado |
| B-escalado [ajuste, exploratorio] | Capas 0 y 1 locales + respaldo Opus 5.5 seudonimizado | Valor de la escalada, si la política la admite |

Por la invariante de paridad, A, A-lite y la capa 1 salen de una sola implementación, `IConsultasCertificadas`, y un test sin LLM verifica que REST, la herramienta MCP, la `AIFunction` y la capa 1 devuelvan el mismo JSON para cada combinación de enums × actor.

Los modelos primarios quedan fijados de antemano: **Qwen3-8B Q4_K_M** en llama-server (3070), **Qwen3-8B-AWQ** en vLLM (5070) y **Sonnet 5.5** (Claude). Son exploratorios Qwen3.5-9B, Qwen3-14B, Haiku 5.5 y Opus 5.5, este último como techo en A y B.

Para no castigar a A, el techo de 4 llamadas se mantiene como condición primaria (es la restricción de producción), y se agrega una corrida de sensibilidad de A con 6 llamadas en Claude. A también puede conservar en el hilo los IDs devueltos.

### Datos, tamaño y estadística

| Conjunto | Contenido | Tamaño | Uso |
|---|---|---|---|
| D0 | Los 80 ítems actuales, reetiquetados | 80 | Desarrollo, ajuste y gate de regresión |
| **D1 ciego** | Preguntas de un turno escritas por 3–5 personas del Departamento sin ver el catálogo. Composición: ~60 % dentro de familias, ~15 % fuera, ~12 % infactibles o sin permiso, ~8 % ambiguas, ~5 % con menciones; partición `composicion_2_3` etiquetada después | **≥300 efectivas (se escriben ~360)** | Solo H1–H3, una corrida por brazo × modelo |
| D2 | 3 paráfrasis por pregunta: rioplatenses, sin tildes, con errores de tipeo y sinónimos institucionales | ~280 | Robustez, con bootstrap por grupo |
| D3 | Inyección indirecta (≥20) y directa (≥10), fuga entre ámbitos (≥15 pares), ambigüedad (≥10), fuera de dominio cercano (≥10) | ≥60 | H6, bloqueante |
| **D3f [ajuste]** | Mutaciones del enrutador: preguntas que cumplen los términos y slots de cada intención pero agregan una restricción, una negación, un período, un comparativo o una medida distinta | ≥50 | H10 y FP0 |
| D4 | Diálogos: los 5 actuales + ≥30 nuevos, con `terminos_prohibidos` | ~90 turnos | H9 |
| D5 | Social, sin cambios | 20 | Control del enrutador social |

Con n = 300, McNemar detecta 7 pp de diferencia con ~90 % de potencia sin ajuste y ~80 % con Holm, y la equivalencia de ±5 pp en Claude necesita n ≈ 206. **Una diferencia de 3 pp no se puede afirmar** sin 600–900 ítems, y así se declara de antemano [estimado; fórmula de Connor y simulación].

Para evitar la fuga de diseño:
- D1 se compromete por hash SHA-256 en `backend/eval/datasets/ciego/COMPROMISO.md`, se guarda cifrado y se abre recién en la corrida confirmatoria;
- el catálogo, las descripciones, los `input_examples`, `intenciones.json` y los umbrales se congelan antes de abrirlo;
- el test de disjunción se extiende a todo el material de prompts.

En las placas locales hay una corrida a temperatura 0 con c = 1, más un control de determinismo sobre el 10 % de D1. En Claude se hacen k = 5 corridas con bootstrap pareado sobre ítems. El juez LLM (Opus 5.5) **nunca decide la métrica primaria**: solo adjudica las afirmaciones que el verificador determinista no resuelve, con auditoría humana del 100 % cuando el brazo evaluado usa Claude.

### Regla de decisión

En cada nivel:

- **Descalificación.** Cualquier fuga o inyección seguida confirmada descalifica el brazo.
- **B gana** si H1 se rechaza con Holm, ΔTRF ≥ 3 pp, U_2(B) ≥ U_2(A) y la caída de cobertura no supera la caída de TRF. A gana con la condición simétrica.
- **Empate práctico** si el IC del 95 % de ΔTRF cae dentro de ±3 pp, o si H2 prueba equivalencia. En ese caso deciden, en orden, el p95 a c = 4 (≤6,6 s en la 3070), la menor cantidad de piezas (B0 < B < B-strict < A-lite < A) y la menor dependencia del modelo.
- **Sin diferencia ni equivalencia**, no se declara ganador y se escribe un D1' ciego más grande.

**La arquitectura de producción se elige con el resultado de la 5070.** Dentro de B, el modo del respaldo se elige con H5: B-strict si su riesgo selectivo en D1 es ≤33 %; si no, B0.

### Cambios en el arnés, por archivo

| Pieza | Archivo |
|---|---|
| Estrategia de turno (≥4 implementaciones reales) | `backend/src/Modules.Asistente/Application/Turno/IEstrategiaDeTurno.cs`, con `EstrategiaTextoASql`, `EstrategiaAgente` y `EstrategiaHibrida`; selección por `Asistente__Estrategia` (default `texto_a_sql`) |
| Resultado extendido (`Carril`, `Interpretacion`, `Traza`, `FuenteDeLaRespuesta`) | `Application/Turno/ResultadoDelTurno.cs` |
| Proveedor con herramientas (`Herramientas`, `Historial`, `LlamadasAHerramientas`) | `Application/Modelo/IProveedorDeModelo.cs`, `ProveedorAnthropic.cs`, `ProveedorLocal.cs`; adaptador `ClienteDeChatSobreProveedor : IChatClient` |
| **Cierre del enrutador y test de mutación** [ajuste] | `Application/Determinista/ResolutorDeIntenciones.cs`, con lista de modificadores y palabras vacías; tests de mutación por intención |
| Herramientas certificadas | `Modules.Asistente.Contracts/Consultas/IConsultasCertificadas.cs`; `Application/Herramientas/`; `Recursos/herramientas.json` con huella; reglas `BR-asistente-NNN` |
| REST y MCP | `Modules.Asistente/Api/ConsultasController.cs`; `backend/src/ArsDocendi.Host/Mcp/HerramientasMcp.cs` (arista `Host → Asistente.Contracts` ya existente, sin módulo ni ping nuevos) |
| Clave de cassette con `tools`, bloques `tool_use`/`tool_result`, temperatura e índice de repetición | `Infrastructure/ClaveDeCassette.cs`, con tests de colisión |
| Sello ampliado (brazo, modelo, catálogo, servidor) y líneas de base por brazo × modelo | `ArsDocendi.Evaluacion.Nucleo/Runner/Reporte.cs`, `GateDeRegresion.cs`; `backend/eval/lineas-de-base/<brazo>/<modelo>/<eje>.json` |
| Puntuación, exportación y análisis | `Puntuacion/VerificadorDeAfirmaciones.cs`, `Runner/ExportadorJsonl.cs`, `Analisis/ComparacionPareada.cs` (McNemar exacto, Newcombe, bootstrap por cluster, TOST, Holm), con tests de libro en CI |
| Dataset, fixture, diálogo y carga | `Dataset/DatasetDeCapacidad.cs`; `Fixture/GeneradorDeFixture.cs` (inyecciones y 5 actores acotados nuevos); `Runner/RunnerDeDialogo.cs`; `backend/eval/ArsDocendi.Evaluacion/RunnerDeCarga.cs`; opciones nuevas en `Program.cs` |

Antes del código van dos changes OpenSpec: `asistente-herramientas-certificadas` (producto) y `asistente-banco-de-arquitecturas` (tooling). En el mismo diff se actualizan `api-contracts.md`, `dependency-graph.md`, `stack.md`, `modelo-local.md` y los dos README, según las reglas 5, 6 y 9 de AGENTS.md.

### Hitos y costo

| Hito | Entregable | Condición de salida |
|---|---|---|
| M0 | Changes OpenSpec y preregistro (borrador) | `openspec validate --all --strict` en verde; p, márgenes y modelos ratificados |
| M1 | D1 ciego, en paralelo con el resto | κ redactor–revisor reportado; hash commiteado antes de M4 |
| M2 | Punto de extensión sin cambiar el número | **Los 109 cassettes reproducen las 4 líneas de base vigentes con el gate en PASA** |
| M3 | Proveedor con herramientas y cassettes | Tests de colisión y de grabación/reproducción en verde |
| M4 | Herramientas certificadas y BR | Test de paridad en verde; catálogo congelado |
| M5 | Brazos, incluida la capa 0 con cierre | Paridad A = A-lite; D0 corre en todos los brazos |
| M6 | Puntuación, análisis y fixture | Tests del Núcleo en verde; preregistro final |
| M7 | Desarrollo (D0, D2, D3, D3f, D4) | Último cambio de catálogo |
| M8 | Corrida confirmatoria (D1) | JSONL sellados; cassettes en LFS |
| M9 | Carga, c = 1/2/4/8 | p50/p95, cola, VRAM y cambios de veredicto |
| M10 | Análisis y decisión | Decisión según la regla, firmada por el equipo |

El costo estimado de Claude es de **US$400–1.200**: ~25.000–35.000 turnos a US$0,01–0,03, el doble con Opus. El corpus completo de cassettes ronda los ~220 MB, así que va a Git LFS, con 300–500 curados en el repo. Las corridas locales no tienen costo marginal. Batch API (−50 %) sirve solo para los brazos de una llamada y no es ZDR; el fixture es sintético. La producción no cambia con el merge: `Asistente__Estrategia=texto_a_sql` y `Asistente__Mcp__Habilitado=false`.

## Las cifras son en su mayoría resúmenes y el repo tiene contradicciones

**Advertencias sobre las fuentes.**

- **Acceso.** El proxy bloqueó arxiv.org, aclanthology.org, alphaxiv.org, huggingface.co, benchlm.ai, artificialanalysis.ai, epoch.ai, aimultiple.com y otros. Casi todas las cifras de papers 2025–2026 (AgentFloor, Reasoning Trap, AgentAbstain, CCTU, MCP-GRANITE, Live API-Bench, AIM, Toolathlon, MCP-Atlas) vienen de **resúmenes del buscador**: no se contrastaron con la tabla original.
- **Fuentes de proveedores.** Son de proveedores o autorreportadas las cifras de dbt, Cube, Strategy, CData, las mediciones internas de Anthropic (tool search, `input_examples`, honestidad de Opus 5.5), el BFCL/TAU2 de Qwen3.5 y el resolvedor closed-world.
- **Modelos 5.5 sin benchmarks públicos de tool use.** Los modelos Claude 5.5 no tienen τ², BFCL ni MCP-Atlas publicados, así que el veredicto en el nivel Claude se apoya en Opus 5 y Opus 4.x.
- **Fuente única no verificada.** La supuesta suba de alucinación de Opus 5 a ~50 % viene de una sola fuente secundaria sin verificar.
- **Benchmarks ruidosos.** Epoch encontró defectos en 24 de 50 tareas de BFCL v4 ([Epoch AI](https://epoch.ai/benchmarks/berkeley-function-calling-leaderboard/review)) [resumen].
- **Estimaciones, no mediciones.** Todos los rangos por nivel × arquitectura, las latencias de la 5070 y de Claude, los supuestos de efecto del análisis de potencia y la regla de cierre son **inferencias**. Su función es ordenar hipótesis, no reemplazar el experimento.

**Inconsistencias del repo y de la investigación.**

- **Scripts inexistentes.** Se mencionaron como base del análisis los scripts de `eval-local/` (`correr.sh`, `comparar.py`), pero **no existen** ni en el worktree ni en el disco (búsqueda con `find /`). El análisis estadístico se diseñó desde cero.
- **Líneas de base no comparables.** La de Claude se midió con 32 ítems de capacidad y la local con 34, así que no hay comparación sobre el mismo dataset. Además, el README de las líneas de base dice 31/32, 13/15 y 8/9, mientras sus JSON dan 30/32, 14/15 y 11/11 ([lineas-de-base/README.md](backend/eval/lineas-de-base/README.md)).
- **Cassettes.** Hay 109, pero la línea de base se congeló "reproduciendo los 107".
- **Tamaño del dominio.** Aparece como 14, 20 o 21 tablas según el documento.
- **Comentarios y definiciones que mienten.** Los comentarios del esquema dicen «Categoría 0..6» contra un catálogo 1..6 (TD-025). Las referencias de cap-007 filtran por período y las de cap-012/014 no, así que el "período por defecto" no está definido.
- **Dos reglas de ámbito.** La API usa roles fijos (`JefeCatedra`, `CoordinadorCarrera`) y el RLS usa permisos en vivo, así que A y B pueden responder distinto para el mismo actor.
- **El 0/39 de la capa 0** se debe en parte a que los literales del CHECK no coinciden con la flexión de las preguntas.
- **El ítem `dia-003-pivote-duro#1` oscila** en las corridas en vivo.
- **Elicitation de MCP.** La nota previa decía que se había revertido; en realidad se rediseñó como Multi Round-Trip Requests en la spec 2026-07-28.
- **Producción en la 5070.** vLLM no implementa `strict` para herramientas, y la rama `infra/ollama` despliega Ollama con endpoints que `ProveedorLocal` no usa.
- **Modelo primario de la 5070.** El diseño del experimento lo fija en Qwen3-8B-AWQ, el de producción, mientras la investigación por nivel recomienda Qwen3-14B o Qwen3.5-9B. Se resolvió dejando el primero como primario y los otros como candidatos a promover.

## Conclusión

La iteración desplazó la pregunta. La primera ronda comparaba protocolos y modelos. La adversarial mostró que la variable decisiva es **quién toma cada decisión y si su error se puede ver**. Un agente MCP perfecto y un híbrido perfecto convergen en las mismas herramientas, los mismos estados del servidor y las mismas plantillas. Lo que queda es si la selección y la abstención son deterministas y falsables. Por eso el hallazgo más valioso no fue sobre MCP: fue un defecto de cuatro líneas en el enrutador propio, capaz de emitir la peor clase de error, una respuesta falsa con sello de certificada, y que empeoraría justo al ampliar la cobertura.

Para el equipo, esto implica tres cosas antes de tocar modelos o protocolos:

1. **La verificación de cierre y su test de mutación** son prerrequisito para sacar la capa 0 del modo sombra.
2. **El respaldo SQL local debe apagarse** mientras no exista evidencia por clase que supere la penalización elegida.
3. **Hoy el dataset no permite distinguir arquitecturas** en el nivel Claude. Cualquier afirmación fina sobre ese nivel tiene que esperar al conjunto ciego.

Si el experimento confirma lo estimado, la arquitectura de producción en la 5070 será B\*, la nube podrá funcionar como desborde sin cambiar el contrato de herramientas, y MCP quedará como la forma de abrirle esas mismas herramientas a un segundo cliente, no como una mejora de precisión.
