> **Auditoría del módulo Asistente** — producida el 2026-09-06 sobre `feature/asistente-conversacional`
> por un barrido de 71 agentes (6 lectores de código, 6 barridos de estado del arte, 6 ejes de
> diagnóstico, refutación adversarial por hallazgo). 52 hallazgos propuestos, 29 sobrevivieron.
>
> **Los payloads funcionales de las tres vulnerabilidades están deliberadamente omitidos de este
> archivo**: queda el modo de falla, que es lo que hace falta para arreglarlo. El detalle operativo
> para escribir el test rojo vive en `.claude/skills/refactor-asistente/COLA.md`.
>
> Ejecución: `/refactor-asistente --estado`.

# Informe de estado y deuda — Módulo Asistente

**Rama:** `feature/asistente-conversacional` · **Fecha de medición:** 2026-09-06 · **Todas las cifras están medidas sobre el árbol de trabajo, no estimadas** (salvo donde se marca _estimación_).

---

## 1. Qué es el módulo Asistente

El Asistente es el único módulo del sistema que **no tiene dominio propio**. No hay entidades, no hay agregados, no hay `Domain/` — y eso está escrito y justificado en `backend/src/Modules.Asistente/README.md:26-28`. Lo que hace es traducir una pregunta escrita en español por un docente o un coordinador a una consulta SQL contra los schemas de otros módulos, ejecutarla con un rol de PostgreSQL que no puede escribir nada, y redactar la respuesta. Todo lo que el módulo "sabe" lo lee del catálogo de la base en tiempo de ejecución; nada está copiado a mano.

El turno completo tiene siete pasos y **dos llamadas al modelo** — el resto es determinista. Entra por `Api/AsistenteController.cs:30` (`POST /api/asistente/consultas`), que exige una `Idempotency-Key`, resuelve el actor **solo** desde la sesión (nunca del body, `:48`) y consulta la caché de idempotencia antes de tocar nada. De ahí pasa a `Application/CapaConversacional.cs:48`, que abre el presupuesto punta a punta del turno (un solo `CancellationTokenSource` encadenado al request) y orquesta: resuelve el hilo conversacional en memoria; prueba el **carril social** (saludo, agradecimiento, meta-pregunta "¿qué podés hacer?") que responde a **cero tokens**; resuelve una aclaración pendiente si la hay; detecta si el usuario cambió de tema y suelta el segmento; reescribe la pregunta con las anteriores del hilo (**llamada 1**, y solo si hay historial); consulta el **enrutador determinista en sombra**, que decide y descarta a propósito; detecta ambigüedad contra un índice de entidades traído de la base (cero tokens); y recién entonces delega en el carril SQL.

`Application/CarrilSql.cs:51` es donde está el trabajo caro. Resuelve el perfil del actor contra `identity` (seis flags: ámbito global, ve datos personales, ve la consulta, alcanza designaciones…), le pide a `GeneradorDeSql` que produzca la consulta (**llamada 2**, con el prefijo del esquema cacheado por rol y hasta cuatro ejemplos few-shot elegidos por similitud), pasa el SQL por `ValidadorDeSql` — que tokeniza y rechaza por lista blanca de comienzo (`select`/`with`) y lista negra de funciones — y lo ejecuta a través de `IEjecutorDeConsulta`. La ejecución abre conexión y transacción nuevas, declara `SET TRANSACTION READ ONLY`, fija el actor con `set_config('app.asistente_user_id', …, true)` _transaction-local_ para que las policies RLS resuelvan, envuelve la consulta con `LIMIT tope+1` (fila sonda) y clasifica cada columna del resultado por par `(OID de tabla, attnum)` contra un manifiesto de sensibilidad versionado. Si vuelve vacío y el actor tiene alcance global, se gasta **un** reintento; si el actor es acotado, no —porque un cero puede ser "no hay" o "no podés verlo", y confundirlos es exactamente lo que la política de abstención existe para impedir. Si hay filas, `Enmascarador` es la **frontera de salida**: lo que viaja al proveedor va enmascarado, lo que vuelve al usuario son las filas reales (**llamada 3**, la redacción).

Las capas se reparten así: `Api/` (340 líneas) son tres endpoints y siete DTOs, sin lógica. `Application/` (6.157) es simultáneamente el cerebro y los puertos: contiene la orquestación, el dominio puro (tokenizador, validador, enmascarador, léxico, políticas) y las 17 interfaces que Infrastructure implementa. `Infrastructure/` (4.526) son los adaptadores: la cadena de decoradores del proveedor de modelo, el transporte HTTP con grabación de cassettes, los tres consultores de base, la construcción del prefijo desde `pg_catalog`, cinco cachés perezosos y el registro del turno. `ModuleExtensions.cs` (391) es la raíz de composición y `OpcionesAsistente.cs` (410) toda la configuración. El frontend (`frontend/src/features/asistente/`) es una vista sola —`PanelAsistente`— montada dos veces: como modal desde la barra y como ruta `/asistente`, con la conversación viviendo en el dueño del montaje y no en el panel.

---

## 2. Mapa de la estructura actual

### Backend — `backend/src/Modules.Asistente/` · 11.898 líneas totales, **5.670 de código real**

| Agrupación semántica                          | Archivos                                                                                                                                                                                                                                       |       LOC | Nota                                                                                 |
| --------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------: | ------------------------------------------------------------------------------------ |
| **Api/** — superficie HTTP                    | `AsistenteController` (114), `ModelosAsistente` (192), `PingAsistenteController` (34)                                                                                                                                                          |       340 | 166 de código. Solo importa `Application`, nunca `Infrastructure`                    |
| **Orquestación del turno**                    | `CapaConversacional` (551), `PresupuestoDelTurno`, `ContadorDeLlamadasDelTurno`, `DecisionSombraDelTurno`, `ResultadoDelTurno`, `IRegistroDelTurno`, `IIdempotencia`, `ICuotaDelActor`, `DisponibilidadDelModelo`                              |    ~1.119 | `CapaConversacional` concentra la mitad del grupo. 17 dependencias de constructor    |
| **Carril SQL**                                | `CarrilSql` (404), `GeneradorDeSql` (282), `ValidadorDeSql` (184), `TokenizadorSql` (263), `RedactorDeRespuesta` (146), `IEjecutorDeConsulta` (134), `ResultadoDeConsulta`, `IProveedorDeEsquema`, `ISelectorDeEjemplos`, `IFechaDeReferencia` |    ~1.592 | `TokenizadorSql`+`ValidadorDeSql` (447) son un sub-módulo de seguridad autocontenido |
| **Política de abstención y copy**             | `PoliticaDeAbstencion` (364), `CoberturaDelPortal` (132), `PresentacionPorRol` (123), `RedaccionDeCapacidades` (103), `ICatalogoDeCapacidades`, `Sugerencias`                                                                                  |      ~832 | Mezcla reglas verificables, ~10 constantes de copy y fragmentos de prompt            |
| **Hilo, aclaración y reescritura**            | `HiloConversacional` (148), `ReconocedorDeAclaracion` (141), `ReescritorDePreguntas` (170), `DetectorDeAmbiguedad` (112), `DetectorDeCambioDeTema` (85), `Aclaracion`, `IAlmacenDeHilos`                                                       |      ~733 | Único agregado con estado mutable de la capa                                         |
| **Carril determinista (en sombra)**           | `CatalogoDeIntenciones` (206), `ResolutorDeIntenciones` (147), `ICatalogoDelDominio` (136), `EnrutadorDeDominio` (57) + `LectorDeVocabulario` (175), `CatalogoDelDominioReal` (117)                                                            |   **838** | Construido y **desconectado a propósito**. Decide, anota telemetría y descarta       |
| **Frontera de datos personales**              | `Enmascarador` (136), `ManifiestoDeSensibilidad` (165), `IClasificadorDeSensibilidad` (64), `ClasificacionDeSensibilidad` (47)                                                                                                                 |       412 | Bien aislada                                                                         |
| **Léxico compartido**                         | `NormalizadorLexico` (206), `IIndiceDeEntidades` (77)                                                                                                                                                                                          |       283 | Dependencia transversal de 6 clases + el evaluador                                   |
| **Carril social + puerto del modelo**         | `EnrutadorSocial` (209), `IProveedorDeModelo` (172), `FallasDelProveedor` (24)                                                                                                                                                                 |       405 | Cero tokens el primero; el puerto es la abstracción mejor aplicada                   |
| **Vínculos a pantallas**                      | `BuscadorDeVinculos` (151), `IResolutorDeVinculos` (84)                                                                                                                                                                                        |       235 | Con Null Object; el resolutor real vive en el Host                                   |
| **Infra: cadena de proveedores**              | `ProveedorAnthropic` (374), `ProveedorSimulado` (80), `ProveedorConBreaker` (63), `ProveedorConTechoDeLlamadas` (61), `BreakerDelProveedor` (153), `DisponibilidadDelModeloReal` (30)                                                          |       761 | Decorator chain limpio                                                               |
| **Infra: cassettes y transporte**             | `AlmacenDeCassettes` (293), `ClaveDeCassette` (188), `GrabadorDeCassettes` (135), `ReintentoDeTransporte` (108), `SelloDelCassette` (73)                                                                                                       |       797 | Falla cerrado: sin cassette no sale a la red                                         |
| **Infra: ejecución acotada (invariante #14)** | `ConsultorDeAlcance` (252), `EjecutorDeConsulta` (177), `ConsultorDeCobertura` (85)                                                                                                                                                            |       514 | Los tres materializan la frontera de motor                                           |
| **Infra: prefijo desde `pg_catalog`**         | `RenderizadorDeEsquema` (227), `LectorDeValoresDeCatalogo` (161), `LectorDeCatalogo` (146), `ProveedorDeEsquema` (106)                                                                                                                         |       640 | Deriva de `has_column_privilege`, no de una lista                                    |
| **Infra: cachés y catálogos**                 | `CatalogoDeCapacidades` (255), `CatalogoDeSensibilidad` (154), `SelectorDeEjemplos` (140), `IndiceDeEntidades` (128)                                                                                                                           |      ~677 | Cinco cachés perezosos con el mismo mecanismo copiado                                |
| **Infra: estado efímero**                     | `CuotaEnMemoria` (111), `AlmacenDeHilosEnMemoria` (87), `IdempotenciaEnMemoria` (65)                                                                                                                                                           |       263 | Productivos, no placeholders                                                         |
| **Infra: esquema y registro**                 | `RegistroDelTurno` (151), `MigradorAsistente` (131), `PrivilegiosAsistente` (99), `PurgaDeRegistros` (83), `RegistrosAsistente` (63), `ServicioDePurga` (55)                                                                                   |       582 | Dos filas por turno, deliberadamente no correlacionables                             |
| **Composición y configuración**               | `ModuleExtensions` (391), `OpcionesAsistente` (410)                                                                                                                                                                                            |       801 | 42 registros DI en un método; 35 perillas en una clase plana                         |
| **Recursos embebidos**                        | `ejemplos-sql.json`, `intenciones.json`                                                                                                                                                                                                        |       163 |                                                                                      |
| **`Modules.Asistente.Contracts/`**            | `.csproj` + `README.md`                                                                                                                                                                                                                        | **0 .cs** | Vacío a propósito, registrado como huérfano en el manifiesto de aristas              |

### Frontend — `frontend/src/features/asistente/` · 5.388 líneas

| Grupo                   |       LOC | Detalle                                                                                                                                              |
| ----------------------- | --------: | ---------------------------------------------------------------------------------------------------------------------------------------------------- |
| Hoja de estilos         |   **881** | `asistente.css`, un archivo, 14 secciones, importado solo desde `pages/AsistentePage.tsx:7`                                                          |
| Estado conversacional   |       309 | `useAsistente.ts` (187) + `utils/esperaPareja.ts` (122)                                                                                              |
| Componentes de render   |      ~700 | `Mensaje` (159), `TablaDeResultado` (126), `Conversacion`, `Opciones`, `Sugerencias`, `Razonamiento`, `MarcaSensible`, `AccionesDelMensaje`          |
| Montajes                |      ~324 | `LanzadorAsistente` (132), `PanelAsistente` (129), `AsistentePage` (43), `routes` (20)                                                               |
| Chrome y composer       |      ~456 | `EntradaDePregunta` (128), `AyudaDelAsistente` (99), `EstadoInicial`, `FranjaDeEstado`, `IndicadorDeProceso`, `LineaDeMetricas`, `NuevaConversacion` |
| Hooks de comportamiento |       218 | `useAnclaAlFinal` (128), `useAltoAutomatico` (56), `useVisibleTrasUmbral` (34)                                                                       |
| Contrato + acceso       |       232 | `types.ts` (110), `asistenteApi.ts` (69), `errores.ts` (68), `useAccesoAlAsistente.ts` (55)                                                          |
| **Tests**               | **2.246** | 16 archivos, 3 de los cuales leen `asistente.css` por `node:fs`                                                                                      |

### Tooling y suite

| Artefacto                                   |            LOC | Notas                                                                |
| ------------------------------------------- | -------------: | -------------------------------------------------------------------- |
| `tests/…/Asistente/`                        |     **16.465** | 725 `[Fact]/[Theory]` + 163 `InlineData`; 10.925 de código           |
| `tests/…/Evaluacion/`                       |          2.626 | Prueban el instrumento de medición                                   |
| `tests/…/Infraestructura/`                  |          1.200 | Fixture, dobles, `RaizRepositorio`                                   |
| `tests/…/Portal/RlsPortalAsistenteTests.cs` |            405 | RLS de portal                                                        |
| `tests/…/Cassettes/`                        | 840 (120 JSON) | Salida real del proveedor desde `2d11e7f`                            |
| `src/ArsDocendi.Evaluacion.Nucleo/`         |          3.018 | Tooling en `src/`, con excepción declarada al invariante #1 (ARS-63) |
| `openspec/changes/asistente-*/`             |         ~8.840 | **19 changes, 0 archivados**                                         |

---

## 3. Qué buenas prácticas SÍ sigue

Ser justo acá importa, porque varias de estas cosas son mejores que el promedio de lo que se ve en producción y un refactor mal apuntado las rompe.

**La frontera de motor es real y es falsable.** El invariante #14 no es prosa: `ManifiestoPrivilegiosTests` compara un manifiesto versionado contra `information_schema.column_privileges` en tres direcciones (privilegio efectivo no declarado / declaración sin privilegio / tabla sin clasificar), `PrivilegiosLecturaTests` lee de verdad con los dos roles, y `RlsAlcanceTests` + `Portal/RlsPortalAsistenteTests` verifican RLS sin `FORCE` y visibilidad cero sin actor fijado. Esto es exactamente lo que la literatura de text-to-SQL de 2025-2026 recomienda como capa dura (rol dedicado sin ownership + transacción READ ONLY + RLS + `set_config` transaction-local, no de sesión, precisamente por el pooling), y acá está implementado y probado, no prometido en un prompt.

**El puerto del proveedor es la abstracción mejor aplicada del repo.** `IProveedorDeModelo` no menciona ningún proveedor; el SDK de Anthropic está confinado a `Infrastructure/ProveedorAnthropic.cs` y hay un test de arquitectura que verifica que un solo archivo lo nombre. Sumar un proveedor es una clase más y un brazo del `switch`. La decisión de conservar la temperatura en el puerto aunque el adaptador no la soporte —"sacarla porque un adaptador no la soporta convertiría al puerto en la forma de ese adaptador", `asistente-proveedor-anthropic/design.md:33`— es exactamente el criterio correcto.

**La cadena de decoradores está bien y su orden está argumentado.** `ProveedorConTechoDeLlamadas → ProveedorConBreaker → ProveedorAnthropic|Simulado`, compuesta en `ModuleExtensions.cs:361`, con el motivo escrito en `:130-142`: invertir los dos primeros haría que el breaker contara intentos que el techo iba a rechazar igual. Y `MaxRetries = 0` en el SDK (`ProveedorAnthropic.cs:68`) porque dos reintentadores triplicarían en silencio la cota documentada de 12 requests por turno. Es la misma disciplina que `Microsoft.Extensions.AI` formaliza con `DelegatingChatClient`, alcanzada sin la librería.

**Los cassettes están bien diseñados.** La clave es SHA-256 de cuatro campos del cable (`system`, `messages`, `output_config.effort`, `model`) y deliberadamente **no** del techo de tokens, así que mover esa perilla no invalida 120 cassettes. La grabación intercepta el cable (`DelegatingHandler`) y no el puerto, con el motivo escrito: un decorador de `IProveedorDeModelo` habría grabado la respuesta ya traducida y dejado el parseo del adaptador fuera del cassette. Y falla cerrado: sin cassette y sin re-grabación, no llama a la red. Los 120 son salida real del proveedor desde el commit `2d11e7f`.

**El costo por turno está acotado y medido.** Turno social = 0 llamadas. Turno con aclaración = 0. Turno SQL normal = 3. Techo global impuesto por el decorador, cuota por actor cobrada en un `finally` aunque el turno explote. Eso cubre LLM06 (_Unbounded Consumption_, que subió al 6º puesto del OWASP Top 10 for LLM Applications 2026) como control de arquitectura y no como optimización.

**El enmascarado identifica por estructura, no por nombre.** El par `(OID, attnum)` en vez del alias que eligió la consulta generada, con su límite declarado (se pierde ante cualquier expresión) registrado como TD-009. Es la decisión correcta y está documentada.

**Los guards de arquitectura vienen pareados.** `ArquitecturaAsistenteTests` corre cada detector sobre el código real **y** lo alimenta con una violación sintética, para que una regex rota no pase en verde para siempre. Es el patrón más valioso de la suite y no lo tiene casi nadie.

**Los comentarios explican el porqué, no el qué.** Es el activo intangible más grande del módulo. Casi cada decisión no obvia tiene su párrafo con el modo de falla que evita — y varias veces con el bug real que ya ocurrió (el `"set_config"` entrecomillado que devolvía 26 filas en vez de 138, el "Vosdame 3 materias" del `user-select`, la fecha de referencia que hacía divergir dos tests).

**Cero fake UI.** El botón del asistente no existe si el `GET /capacidades` devuelve 403; el catálogo de ejemplos se valida con `EXPLAIN` contra la base antes de ofrecerse; el SQL solo se muestra si el perfil tiene el permiso. El invariante #7 se cumple.

---

## 4. Diagnóstico contra el estado del arte

### Eje 1 — Pipeline del turno: el gap existe, pero el remedio canónico **no** aplica

**Estado del arte:** Microsoft Agent Framework y LangChain 1.0 implementan el turno como capas con hooks nombrados (`before_model`, `modify_model_request`, `after_model`), con semántica de pila y orden declarado en el registro.

**El repo:** `Application/CapaConversacional.cs:164-350` es un método de **89 líneas de código** con los siete pasos numerados **en comentarios** (`:184, :213, :229, :242, :250, :284, :294`) y 6 puntos de salida. El constructor tiene 17 parámetros y 12 de esas dependencias se usan una sola vez. El archivo (551 líneas) casi duplica el cap soft de ~300 de `golden-principles.md:49`.

**Pero el diagnóstico obvio es falso, y esto es lo instructivo del análisis.** La consecuencia que uno esperaría —"ningún paso se puede testear en aislamiento"— no se sostiene: la lógica de cada paso ya vive en unidades puras separadas con su propio archivo de test en memoria y sin PostgreSQL (`EnrutadorSocialTests` 15 casos, `SeguimientoYAclaracionTests` 22, `AmbiguedadTests` 10, `HiloConversacionalTests` 19, `EnrutadorDeDominioTests` 9). Son ~93 tests que ejercitan los pasos individualmente. Lo que queda en `ResolverAsync` es **composición**, no lógica. Y el orden sí está declarado fuera del código, en `asistente-capa-conversacional/design.md:7-17`, con un árbol paso por paso.

Peor: el `ContextoDelTurno` mutable que exige el patrón de middleware ya fue evaluado y **rechazado por escrito** en `README.md:586-589` ("meterla acá exigiría un objeto de request mutable con el actor adentro, leído por capas que no lo declaran"). Adoptar el patrón canónico revierte una decisión registrada y agrega 10 clases con estado compartido para redistribuir 17 colaboradores en 11 tipos.

**Veredicto:** el archivo excede el cap y eso es real. El remedio proporcional es partirlo (fábricas de resultado y post-procesado a archivos hermanos), no montar un motor de etapas.

### Eje 2 — Puertos y adaptadores: el proveedor está bien, el acceso a datos no tiene gateway

**Estado del arte:** desde Npgsql 7.0 la abstracción primaria es `NpgsqlDataSource` — un objeto thread-safe construido una vez, con su propio pool, registrable por DI, que encapsula toda la configuración de conexión. Y toda guía de least-privilege para agentes insiste en que el preámbulo de seguridad viva en un solo lugar.

**El repo:** **12 `new NpgsqlConnection`** repartidos en 11 archivos. El preámbulo que sostiene la mitad del invariante #14 —`SET TRANSACTION READ ONLY` + `set_config('app.asistente_user_id', …, true)`— está escrito **cuatro veces a mano**: `EjecutorDeConsulta.cs:88-105`, `ConsultorDeAlcance.cs:81-98`, `ConsultorDeCobertura.cs:49-60`, `CatalogoDeCapacidades.cs:170-183`. El nombre del GUC aparece 4 veces en `src`: declarado como constante privada dos veces con el mismo nombre y valor, y hardcodeado como literal otras dos.

**El modo de falla no es ruidoso y es el que el invariante existe para impedir.** Un quinto consultor actor-scoped que se olvide el `set_config` no rompe nada: `identity.asistente_actor()` devuelve `NULL`, la policy da falso, y el asistente responde _"no hay datos"_ en vez de _"no podés verlos"_ — que es precisamente la respuesta falsa que la métrica primaria del proyecto (corrección con abstención) mide. Los 17 `[Fact]` de `ArquitecturaAsistenteTests` guardan la cadena del dueño, la mutación, el DDL, el ping, el SDK y la credencial. **El preámbulo no.**

Y solo **una** de las cuatro conexiones de lectura acota el tiempo: `ConsultorDeAlcance`, `ConsultorDeCobertura` y el `EXPLAIN` de `CatalogoDeCapacidades` corren con el `Command Timeout` por defecto de Npgsql (30 s), o sea el **doble** del valor que el módulo decidió (`TimeoutDeComandoSegundos = 15`). Del lado del servidor no hay ninguna cota: `provision-db.sh` fija `search_path` por rol y nada más.

### Eje 3 — Validación del SQL: lista negra léxica donde el estado del arte pide un AST

**Estado del arte:** el consenso de ingeniería para SQL generado por LLM es parsear a AST, rechazar por **tipo de nodo** (no por palabra clave), re-renderizar el SQL desde el árbol validado, y hacer allowlist de tablas recorriendo CTEs, subqueries y ramas de UNION. La frase que resume la postura: _"no podés escribir 'generá solo SELECT' en un system prompt y llamar a eso un control"_.

**El repo:** `Application/TokenizadorSql.cs` + `ValidadorDeSql.cs` (447 líneas) son un tokenizador propio con lista blanca de comienzo y **lista negra** de funciones. La postura del código es honesta —los comentarios dicen que es la _segunda_ capa y que ante desacuerdo gana el motor— y la lista blanca de comienzo (`select`/`with`) no tiene el modo de falla de una lista negra. Pero el resto sí:

- **`U&"…"` evade el tokenizador.** un identificador escrito con escapes Unicode (`U&"…"`) es `set_config` para el motor y otra cosa para el tokenizador. Verificado contra PG 18: las tres formas de escape pasan hoy el validador real. Y esto **es** el ataque que la clase existe para parar: `TokenizadorSql.cs:30-39` documenta que emitir el contenido de las comillas dobles es "la decisión entera de esta clase" porque sin eso `"set_config"` pasaba y una consulta podía fijarse otro actor (26 filas contra 138 en el prototipo). El contenido se emite verbatim, solo pasado a minúsculas, sin decodificar escapes.
- **`pg_catalog` es alcanzable.** El propio módulo prueba que el rol de solo lectura puede leerlo (`CatalogoDeSensibilidad.cs:31-43` consulta `pg_class`/`pg_namespace`/`pg_attribute` con `CadenaSoloLectura`). El validador no tiene lista de esquemas; el motor no lo bloquea porque `pg_catalog` es legible por todos con independencia de los GRANT y está implícitamente en la ruta de búsqueda (verificado: con `search_path=''`, `SELECT count(*) FROM pg_class` devuelve 577 filas como `asistente_ro_dev`). Eso contradice una política que el módulo declara explícitamente: `PoliticaDeAbstencion.cs:216-223` prohíbe decir "no existe tal columna" porque _"le confirma a quien pregunta cuáles sí existen"_ (D15). La regla se sostiene contra los mensajes de error y se cae contra una consulta directa al catálogo, sin error de por medio.
- **El enmascarado tiene un fail-open.** `ClasificacionDeSensibilidad.Desconocida` se trata como pública, y el argumento que lo acota (`ClasificacionDeSensibilidad.cs:41-44`: "esas columnas solo son legibles con la conexión de datos personales") es **falso** para las tres columnas `sensible-texto` — `designaciones.pedidos.justificacion`, `pedidos.tipo_baja_detalle` y `pedido_historial.comentario` están concedidas también al **rol básico** (`001_asistente_grants.sql:118` y `:122`). Con el rol básico, `SELECT lower(comentario) FROM designaciones.pedido_historial` derrota el enmascarador: `attnum` sale 0, `Desconocida`, y el texto libre viaja al proveedor. Y en `pedido_historial` (8/8 columnas concedidas) también funciona la fila entera colapsada: `to_jsonb(h)`, `json_agg(h)`.

Y hay un riesgo residual que **ninguna** de esas capas cubre, y que conviene decir con todas las letras: el asistente lee campos de texto libre escritos por usuarios (justificativos, comentarios de revisión). Eso es contenido **no confiable** que vuelve al contexto del modelo. Supabase publicó el reconocimiento del límite —"incluso en modo solo-lectura, la inyección de prompt sigue siendo la preocupación número uno"— y demostró que envolver los resultados con advertencias solo _reduce_ el riesgo. La condición de riesgo (la _lethal trifecta_ de Willison: datos privados + contenido no confiable + canal de salida) está completa hoy.

### Eje 4 — Composición y configuración: el patrón sin ninguna de sus garantías

**Estado del arte:** la doc oficial fundamenta el Options pattern en Interface Segregation —"las clases que dependen de configuración dependen SOLO de la configuración que usan"— y la cadena canónica es `AddOptions<T>().Bind(…).ValidateDataAnnotations().Validate(…).ValidateOnStart()`.

**El repo:** `grep -rn "IValidateOptions|ValidateOnStart|ValidateDataAnnotations" backend/src` devuelve **CERO resultados en todo el backend**. `OpcionesAsistente` es una clase plana de **35 propiedades** para ~10 áreas sin relación, consumida entera por **13 archivos**. La única validación es ad-hoc en el composition root: `Requerido()` cubre 6 strings, `ValidarEsfuerzos()` cubre 3. Quedan **22 perillas `int`** que aceptan cero o negativo sin que nada falle. La prueba de que ya duele está en el código: `ServicioDePurga.cs:28` hace `Math.Max(1, PeriodoDePurgaHoras)` porque un cero lo haría girar en vacío — un consumidor se blinda a mano mientras los otros doce no.

Matiz importante que la solución ingenua rompería: en **cuatro** perillas el cero (o el negativo) es apagado deliberado y documentado —`CupoDeLlamadasPorActor`, `FallosParaAbrirElBreaker`, `PresupuestoDelTurnoSegundos`, `TopeDeTurnosDelHistorial`—. Un `[Range(1, …)]` por DataAnnotations rompería cuatro mecanismos de apagado.

Además `AddAsistenteModule` es un método de 270 líneas con **42 registros de servicio** organizado por comentarios-banner. Eso es un olor real pero la refutación mostró que el remedio obvio (extract-method por área) no compra lo que promete: seis archivos de test **ya** ejercitan un área sola contra el `AddAsistenteModule` completo, y el motivo está escrito en `CadenasDeConexionTests.cs:14-18` — se compone la registración de producción "para que sea la registración de producción la que se ejercita, no una reconstrucción".

### Eje 5 — Frontend: organización por feature bien, pero sin frontera declarada

**Estado del arte:** Feature-Sliced Design y `bulletproof-react` coinciden en que la feature declara una API pública (`index.ts`) y que el CSS se coloca junto al componente para que borrar el componente borre su CSS.

**El repo:** `asistente.css` son **881 líneas** en un archivo — el CSS más grande del frontend por margen amplio (le sigue `designaciones/pages/detalle.css` con 630). Y el repo ya demuestra la alternativa: `designaciones/` parte su CSS en 5 archivos colocados. Dos consecuencias medibles: (a) `LanzadorAsistente` no importa la hoja y se estiliza por accidente —el único import está en `pages/AsistentePage.tsx:7`, y un `lazy()` sobre esa ruta deja el modal de la barra sin estilos—; (b) el guard de tokens (`asistente.tokens.test.ts:14`) lee `asistente.css` **por nombre**, así que un `.css` nuevo escapa entero al chequeo de "ningún color a mano".

Ninguna de las nueve features tiene `index.ts`, y `app/shell/TopBar.tsx:8` importa `features/asistente/components/LanzadorAsistente` — el **único** import profundo cross-frontera de todo el frontend (los otros nueve pasan por `<feature>/routes`).

### Eje 6 — Suite de tests: la red de seguridad no se puede correr

**Estado del arte:** collection fixture con contenedor compartido + reset por test (Respawn) o clonado por plantilla. Y carriles separados: unit tests deterministas en cada commit, evals contra golden dataset en cada PR.

**El repo:** `ClasePostgresAislada` implementa `IAsyncLifetime` y xUnit instancia la clase **una vez por caso de test**. Son **534 casos** (470 `[Fact]` + 64 filas de `[InlineData]`) en 45 clases, cada uno con `CREATE DATABASE` + 3 `MigrateAsync` + 2 `CREATE ROLE` + `PrivilegiosAsistente.AplicarAsync` + `RegistrosAsistente.AplicarAsync` + `DROP DATABASE WITH (FORCE)`. Y las 45 clases declaran la **misma** `[Collection]`, que es la unidad de paralelización de xUnit: corren **en serie**. El aislamiento real ya lo da el nombre único de base por clase; la serialización no compra nada.

Encima, 249 de esos casos releen y reejecutan `sintetico.sql` (36 KB) desde **23 copias privadas idénticas** de `SembrarAsync` (más 12 de `EjecutarSeedAsync`). No hay ni un `[Trait]` ni un `xunit.runner.json` en todo el repo, y el CI corre `dotnet test ArsDocendi.slnx` sin filtro: **no existe un carril rápido**. Para saber si el validador sigue rechazando algo hay que esperar 534 provisiones de base en serie. Cuando el ciclo de feedback local es de minutos, la gente deja de correr los tests antes de pushear — y una suite de 16k LOC que solo corre en CI deja de ser la red de seguridad que hace posible refactorizar.

Y hay una asimetría de guard en los cassettes que **ya se degradó una vez en esta rama**: el sello tiene dos mitades y solo la del fixture tiene barrido en CI. El commit `ce1ede8` cambió `RenderizadorDeEsquema.cs` sin tocar cassettes; `2d11e7f` dice textualmente que los 56 cassettes de generación _"no se podían servir… eran peso muerto desde el commit de la tilde"_. La suite quedó verde sobre un corpus irreproducible, y recuperarlo costó una corrida financiada.

### Eje 7 — Documentación: la fuente de verdad declarada no es la vigente

Tres documentos de nivel alto contradicen el código mergeado, y son los primeros que abre alguien que llega:

- `backend/src/Modules.Asistente/README.md:10-28` dice **"Carril SQL construido, sin superficie de usuario"** y lista E4/E5/E7/E8 como pendientes. Las cuatro están construidas — el mismo archivo las documenta más abajo. La sección de endpoints solo lista el ping.
- `docs/product/designs/asistente-conversacional-definicion.md` sigue en `status: draft`, `owner: ""`, `last_updated: 2026-08-23` — 18 changes atrás — y su tabla de no-alcance dice "Aulas, Portal, Tareas: **no existen las tablas**", cuando `asistente-lee-portal` ya concedió seis tablas de portal.
- El **invariante #14 sigue sin acuerdo del equipo**: la tarea 0.1 de `asistente-fundaciones/tasks.md:5` está abierta, y el propio archivo dice en `:3` que _"ninguna tarea posterior arranca antes de que estas dos cierren"_. Se construyeron 19 changes y 11.898 LOC igual, y cinco designs posteriores lo citan como autoridad vinculante.
- **19 changes activos, cero archivados.** El invariante #10 declara el lifecycle y ya está distorsionando cómo se escriben las specs nuevas: `asistente-rediseno-conversacion/design.md:111` tuvo que meter todo como `## ADDED Requirements` porque las specs de `asistente-frontend` no están en `openspec/specs/` y "un delta MODIFIED no tendría base".

---

## 5. Deuda priorizada

Ordenada por relación valor/esfuerzo. **Impacto** describe qué cambia, no cuántas líneas.

| #   | Hallazgo                                                                                                          | Eje         | Impacto                                                                                                                    | Esf. | Riesgo     | Depende de            |
| --- | ----------------------------------------------------------------------------------------------------------------- | ----------- | -------------------------------------------------------------------------------------------------------------------------- | ---- | ---------- | --------------------- |
| 1   | **`U&"…"` evade el tokenizador** — un identificador con escape Unicode pasa el validador y puede fijar otro actor | Guardrails  | Cierra el ataque exacto que la clase existe para parar. Verificado contra PG 18                                            | S    | bajo       | —                     |
| 2   | **XML-doc de `ParseoDesdeCassettesTests` miente** — dice que los cassettes no son reales; lo son desde `2d11e7f`  | Docs        | Un lector concluye que la suite prueba el mecanismo cuando ya prueba el propósito                                          | XS   | nulo       | —                     |
| 3   | **`pg_catalog` alcanzable por la consulta generada** — deniega por prefijo `pg_` + `information_schema`           | Guardrails  | Cierra la enumeración del esquema, que D15 prohíbe pero solo defiende contra errores                                       | S    | bajo       | —                     |
| 4   | **`SembrarAsync` duplicado en 35 clases** (23 + 12 `EjecutarSeedAsync`), 276 invocaciones                         | Tests       | −300 LOC reales; centraliza "contra qué datos corre la suite" y salda TD-007                                               | S    | bajo       | —                     |
| 5   | **Carril rápido con `[Trait]` en la clase base** — hoy no hay forma de correr un subconjunto                      | Tests       | 469 tests puros en segundos sin Docker; el ciclo local vuelve a existir                                                    | S    | bajo       | —                     |
| 6   | **`PreambuloDelActor`: el GUC en un solo lugar + guard** que falle si se nombra fuera                             | Puertos     | Convierte la mitad no-falsable del invariante #14 en un error de build                                                     | S    | bajo       | —                     |
| 7   | **`EscalarAsync`/`EjecutarAsync` reimplementados en 7 subclases**, 2 `Preparar` idénticos                         | Tests       | −115 LOC; hace explícito el `CommandTimeout = 60` del seed                                                                 | S/M  | medio      | 4                     |
| 8   | **Cota de tiempo en el rol + en toda conexión de lectura** — hoy 3 de 4 corren con el default del driver          | Puertos     | El número que gobierna 3 de 4 lecturas lo elige el módulo, no Npgsql                                                       | S    | bajo       | 13                    |
| 9   | **Popover descartable duplicado 4 veces** en 3 capas; crear `shared/hooks/`                                       | Frontend    | −30/−35 LOC; crea el destino que faltaba                                                                                   | S    | bajo       | —                     |
| 10  | **Frontera de la feature: `index.ts` + regla ESLint** — hoy hay un import profundo desde `app/shell`              | Frontend    | La frontera pasa de convención tácita a error de build                                                                     | S    | bajo       | —                     |
| 11  | **`Dispose()` de `ProveedorAnthropic` inalcanzable** — los records marcadores cortan el rastreo del DI            | Puertos     | Cadena de disposición honesta + mecanismo nativo. **Conservar `envolverProveedor`** o se rompe el evaluador                | S    | bajo\*     | —                     |
| 12  | **`CSS`: import faltante en `LanzadorAsistente` + guard de tokens sobre el directorio**                           | Frontend    | El modal deja de estilizarse por accidente; un `.css` nuevo entra solo al guard                                            | S    | bajo       | —                     |
| 13  | **Caché perezoso copiado 5 veces** + carrera real sobre `Dictionary` en `CacheDeCapacidades`                      | Puertos     | −45/−65 LOC y arregla una lectura concurrente sin candado                                                                  | M    | bajo       | —                     |
| 14  | **`Desconocida` es fail-open** — las 3 columnas `sensible-texto` sí las lee el rol básico                         | Guardrails  | Cierra fuga de texto libre al proveedor. **La corrección barata es sacarlas del GRANT**, no la categoría `Opaca`           | M    | bajo       | 3                     |
| 15  | **Validación de opciones (`IValidateOptions`)** — 22 perillas sin chequeo, 4 apagados a preservar                 | Config      | Un ambiente mal configurado deja de fallar en la primera pregunta                                                          | S/M  | medio\*\*  | —                     |
| 16  | **Guard del prefijo del esquema en los cassettes** — la mitad sin barrido, ya se degradó una vez                  | Tests       | El detector de la degradación deja de costar una corrida financiada                                                        | M    | bajo       | 4                     |
| 17  | **Plantilla de PostgreSQL** (`CREATE DATABASE … TEMPLATE`) — 534 migraciones → 1                                  | Tests       | Elimina 533 de 534 provisiones. **Reaplicar `ALTER ROLE … search_path=''` o falso verde en el invariante #14**             | M    | medio-alto | 4                     |
| 18  | **Colección xUnit → `AssemblyFixture`** — 488 tests serializados sin necesidad                                    | Tests       | Paraleliza la mitad lenta del run                                                                                          | M    | medio      | 17                    |
| 19  | **Application importa Npgsql** — `CarrilSql.cs:2` y el SQLSTATE 42501                                             | Núcleo      | Application deja de conocer el motor. **Hay que cubrir `EjecutorDeConsulta` Y `ConsultorDeAlcance`** o vuelve el 500 crudo | S/M  | medio      | —                     |
| 20  | **Gateway de lectura + `NpgsqlDataSource`** — 12 `new NpgsqlConnection`, 3 ternarios de rol                       | Puertos     | 12 puntos de construcción → 2. LOC neto ≈ 0                                                                                | M    | medio      | 6, 8                  |
| 21  | **`ConsultorDeAlcance`: 9 round-trips → 3** para resolver 6 booleanos                                             | Puertos     | Sumar un permiso pasa de un round-trip a una columna. **Precondición: test de `CodigoDeRol` con dos roles**                | M    | medio      | 20                    |
| 22  | **46 tests puros dentro de clases con base** (32 confirmados, 11 en 2 archivos)                                   | Tests       | 11 tests dejan de provisionar una base para asertar sobre texto                                                            | S    | bajo       | —                     |
| 23  | **Grafo de `CarrilSql` armado a mano en 4 sitios**, 3 fechas de referencia distintas                              | Tests       | Sumar una dependencia pasa de 4 archivos a 1                                                                               | S    | bajo       | —                     |
| 24  | **Contracts vacío vs. Application entera pública** — 46 de 47 archivos `public`                                   | Composición | **Ruta A**: `InternalsVisibleTo` + `internal sealed`. 1 línea + cambios mecánicos                                          | S    | bajo       | decisión de equipo    |
| 25  | **Higiene: 3 `<summary>` huérfanos, 2 `<see cref>` rotos, `HuellaDelFixture` en el archivo de opciones**          | Config      | Documentación que dice algo falso del miembro al que quedó pegada                                                          | S    | bajo       | hacer **antes** de 15 |
| 26  | **README del módulo, `definicion.md` en draft, 19 changes sin archivar, invariante #14 sin acuerdo**              | Docs        | Es lo primero que lee alguien que llega, y contradice el código                                                            | M    | nulo       | —                     |

\* Riesgo bajo **solo** si se conserva `envolverProveedor`; sin esa condición el evaluador pierde el `MedidorDeConsumo` y el eje social se auto-rechaza en silencio.
\*\* El riesgo está en la elección `ValidateOnStart` sí/no: con `sí`, un `Asistente:TopeDeFilas` en cero tumbaría también `/api/tareas/ping`, invirtiendo la postura registrada en `ModuleExtensions.cs:76-78`.

**Guardarraíles — cosas que ningún PR de esta lista debe pisar:**

1. La exclusión estructural del evaluador (`eval/ArsDocendi.Evaluacion` fuera de la solución, con 6 guards adentro). El `[Trait]` del #5 **no** es precedente para filtrarlo.
2. `ArsDocendi.Evaluacion.Nucleo` **no** se muda de `backend/src/`: `LectorDeAristas.LeerBackendSrc()` está cableado ahí y sacarlo convertiría la única fila de excepción del manifiesto en "fila sin arista".
3. El orden de migración del fixture (identity → designaciones → portal): la RLS de portal referencia `designaciones.designaciones`.
4. No sumar Polly / `AddStandardResilienceHandler()`: reintroduce reintento **y** breaker en el pipeline HTTP, rompe la cota de 12 requests por turno (pasaría a 36) y pone un segundo corte que `DisponibilidadDelModeloReal` no puede observar.

---

## 6. La verdad sobre el tamaño

**El módulo no tiene 50.000 líneas. Pero la percepción no es un error: es una suma correcta de la cosa equivocada.**

De dónde salen los ~50k:

| Qué se suma                                              |         LOC |
| -------------------------------------------------------- | ----------: |
| `Modules.Asistente/` (backend productivo)                |      11.898 |
| `features/asistente/` (frontend, incluye 2.246 de tests) |       5.388 |
| `tests/…/Asistente/`                                     |      16.465 |
| `tests/…/Evaluacion/`                                    |       2.626 |
| `tests/…/Infraestructura/`                               |       1.200 |
| `Portal/RlsPortalAsistenteTests.cs`                      |         405 |
| `src/ArsDocendi.Evaluacion.Nucleo/` (tooling)            |       3.018 |
| `Cassettes/` (120 JSON)                                  |         840 |
| `openspec/changes/asistente-*/` (19 changes)             |      ~8.840 |
| **Total de lo que el asistente arrastra**                | **~50.680** |

Ahí está el número. Ahora, lo que **es** el asistente:

| Qué                          | LOC totales | **Código real** |
| ---------------------------- | ----------: | --------------: |
| Backend del módulo           |      11.898 | **5.670** (48%) |
| Frontend productivo (TS/TSX) |       2.261 |          ~2.100 |
| CSS                          |         881 |             881 |
| **Lógica del asistente**     |             |     **≈ 8.650** |

El backend del módulo son **5.670 líneas de código y 4.862 de comentario** (ratio 0,86:1). En `Application/` el ratio es **1,02:1** — más comentario que código. Casos extremos: `PoliticaDeAbstencion.cs` 364 líneas con 98 de código; `IEjecutorDeConsulta.cs` 134 con 25. Esos comentarios son el activo más valioso del módulo: explican el porqué, no el qué, y varias veces citan el bug real que la decisión evita.

**Cuánto se puede reducir de verdad, sin perder capacidad.** Sumé los hallazgos confirmados que producen reducción real, no reorganización:

| Movimiento                                            |        LOC |
| ----------------------------------------------------- | ---------: |
| `SembrarAsync` + `EjecutarSeedAsync` + TD-007 (tests) |       −395 |
| Helpers de acceso a datos en tests                    |       −115 |
| Caché perezoso genérico (producción)                  |        −55 |
| Popover compartido (frontend)                         |        −33 |
| Grafo de `CarrilSql` en tests                         |        −32 |
| `ConsultorDeAlcance` en un `SELECT`                   |        −18 |
| Bucle de SQL versionado en tests                      |        −23 |
| CSS muerto + `visually-hidden`                        |        −17 |
| **Total**                                             | **≈ −690** |

Y varios hallazgos de la lista son **LOC positivo a propósito**: la validación de opciones (+70), el guard del prefijo de cassettes (+65), el `PreambuloDelActor` con sus dos tests, la categoría `Opaca`, los tipos de excepción de Infrastructure. El gateway de datos es neutro.

**El neto honesto: el refactor completo mueve entre −400 y −700 líneas sobre ~50.000. Es el 1%.**

Por qué el volumen es irreducible:

- **Los tests son 1,44:1 contra el código productivo del backend, y eso es cobertura, no relleno.** De las 16.465 líneas de `Asistente/`, 10.925 son código y 2.660 son XML-doc donde cada test explica el modo de falla que previene. Hay 725 `[Fact]/[Theory]`. La duplicación real que identifiqué son ~700 líneas de arrange. El resto son casos distintos.
- **El dominio es intrínsecamente grande.** Un asistente que genera y ejecuta SQL sobre datos personales de una institución pública necesita, sin negociación: un validador léxico, un ejecutor acotado, un clasificador de sensibilidad, un enmascarador, un manifiesto de privilegios con su comparador, un catálogo de capacidades, una política de abstención, un hilo conversacional con desambiguación, un puerto de proveedor con breaker y techo, un mecanismo de grabación, y un instrumento de evaluación. Ninguna de esas piezas se puede borrar; se pueden mover.
- **Las 838 líneas del carril determinista en sombra no cambian ninguna respuesta hoy** — y esa es la única masa de código "gratis" que existe. Está declarada, argumentada (`asistente-enrutador-de-dominio/design.md`, D2) y es reversible. Pero es un compromiso con ARS-46, no basura.
- **Los 19 changes de OpenSpec (~8.840 líneas) no son código.** Son el registro de decisiones. Archivarlos —invariante #10— los saca de `openspec/changes/` y mergea las deltas a `openspec/specs/`: reduce el ruido visual sin borrar información. Es el movimiento de percepción con mejor relación costo/beneficio del informe.

**Si el objetivo es que el módulo se sienta más chico, el camino no es borrar líneas: es (a) archivar los 19 changes, (b) subdividir `Application/` en carpetas para que la carpeta plana de 47 archivos deje de leerse como una pila, y (c) arreglar el README, que es lo que le dice a alguien que llega que el módulo está a medio hacer cuando está terminado.**

---

## 7. Arquitectura destino propuesta

Un solo diseño. La regla que lo organiza: **la micro-arquitectura es decisión local del módulo** —la macro (invariantes #1, #2, #4, #14) no se toca— y el criterio de admisión de cada tipo nuevo es el de Ousterhout: _su interfaz tiene que ser más simple que su implementación_. Nada de 45 archivos de 30 líneas.

```
backend/src/Modules.Asistente/
├── ModuleExtensions.cs                 # composición (se queda como está; ver §8 PR-0)
├── Api/                                # sin cambios
│   ├── AsistenteController.cs
│   ├── ModelosAsistente.cs
│   └── PingAsistenteController.cs
├── Configuracion/                      # ← NUEVO
│   ├── OpcionesAsistente.cs            # las 35 perillas siguen juntas (el binding no cambia)
│   ├── ValidadorDeOpcionesAsistente.cs # IValidateOptions con los 3 baldes
│   └── HuellaDelFixture.cs             # sale de OpcionesAsistente: no es una opción
├── Application/
│   ├── Turno/                          # orquestación
│   │   ├── CapaConversacional.cs       # solo ResponderAsync + ResolverAsync
│   │   ├── FabricasDelResultado.cs     # las 5 fábricas privadas que hoy inflan la clase
│   │   ├── CierreDelTurno.cs           # TextoDelRechazo + VinculosAsync
│   │   ├── ResultadoDelTurno.cs, PresupuestoDelTurno.cs
│   │   ├── ContadorDeLlamadasDelTurno.cs, DecisionSombraDelTurno.cs
│   │   └── IRegistroDelTurno.cs, IIdempotencia.cs, ICuotaDelActor.cs
│   ├── CarrilSql/
│   │   ├── CarrilSql.cs, GeneradorDeSql.cs, RedactorDeRespuesta.cs
│   │   ├── ValidadorDeSql.cs, TokenizadorSql.cs     # + decodificación de U&"…"
│   │   ├── IEjecutorDeConsulta.cs, ResultadoDeConsulta.cs
│   │   ├── IPerfilDelActor.cs          # sale de IEjecutorDeConsulta.cs: es otro puerto
│   │   ├── PerfilDelActor.cs           # con métodos, no 6 flags sueltos
│   │   └── FallasDeLaConsulta.cs       # ConsultaSinPrivilegio / ConsultaRechazadaPorElMotor
│   ├── Conversacion/
│   │   ├── HiloConversacional.cs, IAlmacenDeHilos.cs, Aclaracion.cs
│   │   ├── ReconocedorDeAclaracion.cs, DetectorDeAmbiguedad.cs
│   │   ├── DetectorDeCambioDeTema.cs, ReescritorDePreguntas.cs
│   │   └── EnrutadorSocial.cs
│   ├── Abstencion/
│   │   ├── PoliticaDeAbstencion.cs     # solo AlcanzaTodo / ConvieneReintentar / los textos
│   │   ├── ReferenciasSinResolver.cs   # Demostrativos + HayReferenciaSinResolver
│   │   ├── ReglasDeRedaccion.cs        # el fragmento de prompt, con su único consumidor
│   │   ├── CoberturaDelPortal.cs, PresentacionPorRol.cs
│   │   └── RedaccionDeCapacidades.cs, Sugerencias.cs, ICatalogoDeCapacidades.cs
│   ├── Sensibilidad/
│   │   ├── Enmascarador.cs, ClasificacionDeSensibilidad.cs
│   │   ├── IClasificadorDeSensibilidad.cs, ManifiestoDeSensibilidad.cs
│   ├── Determinista/                   # el carril en sombra, agrupado y visible
│   │   ├── EnrutadorDeDominio.cs, ResolutorDeIntenciones.cs
│   │   └── CatalogoDeIntenciones.cs, ICatalogoDelDominio.cs
│   ├── Modelo/
│   │   ├── IProveedorDeModelo.cs, EsfuerzoDelModelo.cs, EsfuerzoConfigurado.cs
│   │   ├── FallasDelProveedor.cs, DisponibilidadDelModelo.cs
│   │   └── IProveedorDeEsquema.cs, ISelectorDeEjemplos.cs, IFechaDeReferencia.cs
│   └── Lexico/
│       ├── NormalizadorLexico.cs       # + Enmarcada(), que hoy está copiada 5 veces
│       ├── IIndiceDeEntidades.cs
│       └── BuscadorDeVinculos.cs, IResolutorDeVinculos.cs
└── Infrastructure/
    ├── Datos/                          # ← NUEVO: la única puerta a las bases
    │   ├── AperturaDeLectura.cs        # NpgsqlDataSource por rol + CommandTimeout
    │   ├── PreambuloDelActor.cs        # ÚNICA declaración de app.asistente_user_id
    │   ├── SesionDeLectura.cs          # conexión + transacción ya preparadas
    │   └── ValorPerezoso.cs / ValorPerezosoPorRol.cs   # el caché, una vez
    ├── Proveedor/                      # cadena + transporte + cassettes (sin cambios)
    ├── Catalogo/                       # lectores de pg_catalog + renderizador
    │   └── InstruccionesDeGeneracion.cs   # el prompt del carril SQL, con su consumidor
    ├── Consultas/                      # EjecutorDeConsulta, ConsultorDeAlcance, ConsultorDeCobertura
    ├── Memoria/                        # cuota, idempotencia, hilos
    └── Esquema/                        # migrador, privilegios, registro, purga
```

**Tipos clave a introducir** (nombres definitivos, español, invariante #13):

| Tipo                                                    | Dónde                    | Qué resuelve                                                                                                                                                                                                                                  |
| ------------------------------------------------------- | ------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `PreambuloDelActor`                                     | `Infrastructure/Datos/`  | Única declaración de `app.asistente_user_id`; dos sobrecargas (con y sin `statement_timeout`). Recibe conexión y transacción desde afuera, como `PrivilegiosAsistente`. **Con guard que falla el build si el GUC se nombra en otro archivo.** |
| `AperturaDeLectura`                                     | `Infrastructure/Datos/`  | Un `NpgsqlDataSource` singleton por rol. `CommandTimeout` decidido, no heredado. Elimina 12 `new NpgsqlConnection` y 3 ternarios de rol.                                                                                                      |
| `ValorPerezoso<T>` / `ValorPerezosoPorRol<T>`           | `Infrastructure/Datos/`  | El caché una vez. Absorbe `CacheDeCapacidades` entera y arregla la lectura sin candado sobre `Dictionary`.                                                                                                                                    |
| `ValidadorDeOpcionesAsistente`                          | `Configuracion/`         | `IValidateOptions` con tres baldes: sin regla (los 4 apagados), estrictamente positivas (17), no negativas (1) + las dos relaciones (`EsperaMaximaMs ≥ EsperaBaseMs`, `TimeoutDeComando × 1000 > TimeoutDeSentencia`).                        |
| `ConsultaSinPrivilegio` / `ConsultaRechazadaPorElMotor` | `Application/CarrilSql/` | Hermanas de `FallasDelProveedor`. Sacan `using Npgsql` de Application.                                                                                                                                                                        |
| `PerfilDelActor` con métodos                            | `Application/CarrilSql/` | Las conjunciones (`EsGlobal && VeDatosPersonales`) viven en el tipo, no en tres archivos. Evita el bug que `PresentacionPorRol.cs:68-71` hoy previene con un comentario.                                                                      |
| `FabricasDelResultado`                                  | `Application/Turno/`     | Las 5 fábricas privadas de `CapaConversacional` a archivo hermano. Baja el archivo de 551 a ~350.                                                                                                                                             |

**Sobre `Modules.Asistente.Contracts`:** la recomendación es **Ruta A** — agregar `[assembly: InternalsVisibleTo("ArsDocendi.Evaluacion.Nucleo")]` junto al que ya existe para `IntegrationTests`, y pasar `Application/` a `internal sealed`. Un archivo tocado y un cambio mecánico de modificador. Cierra el olor real (46 archivos públicos → 0) y deja `IEjecutorDeConsulta` del lado contenido, que es donde el invariante #14 lo quiere. La arista `Evaluacion.Nucleo → Modules.Asistente` sigue registrada como excepción con ticket, porque el evaluador es un arnés y no un consumidor de producción — lectura que el propio `.csproj` del Núcleo sostiene. **Llenar Contracts (Ruta B) obliga a decidir antes si publicar el puerto de ejecución de SQL generado es compatible con el #14; si no lo es, no cierra.** El primer paso de cualquiera de las dos no es código: es cerrar la pregunta que `arquitectura-manifiesto-de-edges/design.md:137` dejó abierta.

**Frontend destino:**

```
frontend/src/features/asistente/
├── index.ts                            # ← API pública: routes + LanzadorAsistente
├── components/
│   ├── <Componente>.tsx + <Componente>.test.tsx    # tests colocados (hoy 8 están en la raíz)
│   ├── panel.css, lanzador.css, conversacion.css, tabla.css, entrada.css, …
├── hooks/, api/, utils/, pages/
└── test/soporte.tsx, test/hojas.ts     # el guard de tokens barre el directorio, no un archivo
```

Más `shared/hooks/useDescartarAlClicAfuera.ts` (el popover, hoy copiado 4 veces) y una regla `no-restricted-imports` en `src/app/**` para que el import profundo rompa el build.

---

## 8. Secuencia de refactor

Cada paso es un PR contra `develop`, compila solo, y no depende del siguiente. El orden importa: **la red de seguridad primero, después la frontera, después el tamaño.**

### Fase 0 — Higiene y verdad documental (sin riesgo, primero)

- **PR-0.1 — Documentación vigente.** Reescribir la sección `## Estado` del README del módulo; sacar `definicion.md` de `status: draft` y enmendar su tabla de no-alcance; corregir el XML-doc de `ParseoDesdeCassettesTests`; los tres `<summary>` huérfanos de `ModuleExtensions.cs:323-338`; los dos `<see cref="Esfuerzo"/>` rotos; subir `ProveedorDeRedaccion` al encabezado; mover `HuellaDelFixture` a su archivo. _Va primero porque los `<summary>` huérfanos migran mal cuando el archivo se parte._
- **PR-0.2 — Archivar los 19 changes.** `/opsx:archive` sobre cada uno, mergeando las deltas a `openspec/specs/`. Saca ~8.840 líneas de `openspec/changes/` y desbloquea que las specs nuevas puedan escribir `MODIFIED` en vez de `ADDED`.
- **PR-0.3 — Cerrar el gate del invariante #14.** No es código: es la tarea 0.1. Presentar la enmienda al equipo y tildar el checkbox, o cancelarla. Mientras siga abierta, cinco designs citan como norma algo que nadie ratificó.

### Fase 1 — Que la suite se pueda correr (habilita todo lo demás)

- **PR-1.1 — `SembrarAsync` a la clase base**, cacheada, con `CancellationToken` opcional. Barre las 23 definiciones y las 12 de `EjecutarSeedAsync`, y de paso las 11 copias de `BuscarRaizRepositorio` (TD-007). Borrado mecánico; una falla se ve como suite roja inmediata.
- **PR-1.2 — Carril rápido.** `[Trait("carril","base")]` en `ClasePostgresAislada` (xUnit hereda traits de la base, así que heredar **es** la marca — verificado que las 45 clases derivan sin excepción). Localmente: `dotnet test --filter 'carril!=base'` sobre 469 tests puros sin Docker.
- **PR-1.3 — Helpers de acceso a datos** (`EscalarAsync`, `EjecutarAsync`, `LeerAsync`) a la clase base, preservando el `CommandTimeout = 60` del camino del seed como parámetro explícito.
- **PR-1.4 — Fábrica única del grafo de `CarrilSql`** en `BancoDelAsistente`, recibiendo `conTecho`, `contador` y `ejecutor` ya construidos. Homogeneizar la fecha de referencia a `2026-08-25` (dejar la de marzo del evaluador, que es deliberada, apuntando a `GeneradorDeFixture.Ancla`).
- **PR-1.5 — Separar los tests puros** de `ManifiestoPrivilegiosTests` y `PrefijoDeEsquemaTests` en clases sin fixture (el patrón ya existe en `CoberturaDelPortalTests`). El compilador verifica el movimiento.

### Fase 2 — Cerrar los agujeros de seguridad (independientes entre sí)

- **PR-2.1 — Decodificar `U&"…"` en el tokenizador.** Test rojo primero, con las tres formas de escape que PG 18 acepta (ver el detalle en la cola de la skill) en una `[Theory]`.
- **PR-2.2 — Denegar el catálogo del motor.** Prefijo `pg_` + `information_schema`, chequeado en palabras **y** en identificadores entrecomillados. Test de integración que ejecute la enumeración con el rol de lectura y verifique el rechazo. (Ninguna tabla ni columna del manifiesto empieza con `pg_` — verificado, sin falsos positivos.)
- **PR-2.3 — Cerrar el fail-open del enmascarado.** Sacar `justificacion`, `tipo_baja_detalle` y `comentario` del GRANT a ambos roles, con un test que afirme la ACL. **Antes: verificar contra el catálogo de preguntas si alguna respuesta las necesita.** Borrar el argumento mitigante falso de `ClasificacionDeSensibilidad.cs:41-44` y de TD-009. Canario obligatorio: sembrar un valor único en `pedido_historial.comentario` y afirmar que no sale del enmascarador con `to_jsonb(h)`, `json_agg(h)` y `lower(comentario)`.
- **PR-2.4 — `PreambuloDelActor` + los dos guards.** El GUC en un solo archivo; guard que falla si se nombra fuera; guard que verifica que toda transacción que fija el actor declara `READ ONLY`. Cuatro call sites, cubiertos por `RlsAlcanceTests`, `EjecucionAcotadaTests`, `PermisoYPersonaPortalTests` y `RlsPortalAsistenteTests`.
- **PR-2.5 — Cota de tiempo en el rol.** `ALTER ROLE … IN DATABASE … SET statement_timeout = '8s'` en `provision-db.sh` (no en el DDL de `PrivilegiosAsistente`, que corre con un rol `NOSUPERUSER` que no puede alterar otro rol), con la comprobación en `verificar-roles-asistente.sh` contando filas en `pg_db_role_setting`.

### Fase 3 — Frontera y capas (después de que la suite corra rápido)

- **PR-3.1 — Traducción de fallas del motor a Infrastructure.** Envolver `EjecutorDeConsulta` **y** `ConsultorDeAlcance` con las excepciones tipadas. Sale `using Npgsql` de Application. Reescribir `EjecucionAcotadaTests.cs:188` preservando el SQLSTATE en la excepción tipada.
- **PR-3.2 — `InternalsVisibleTo` + `internal sealed`** en Application (Ruta A del Contracts). Un archivo + cambio mecánico de modificador.
- **PR-3.3 — Validación de opciones.** `IValidateOptions` con los tres baldes y las dos relaciones, más su archivo de tests. Decidir explícitamente `ValidateOnStart` sí/no en el PR.
- **PR-3.4 — Disposición del proveedor.** Adaptador crudo bajo clave propia, `envolverProveedor` en entrada aparte, `Dispose()` idempotente. Test que afirme disposición exactamente una vez, en las dos variantes.
- **PR-3.5 — `ValorPerezoso<T>`.** Un PR por consumidor, con los tests de `Lecturas` como red. Documentar el cambio semántico del contador (de "veces que se consultó la base" a "veces que hubo que calcular").

### Fase 4 — Reorganización semántica (el movimiento grande, sin cambio de comportamiento)

- **PR-4.1 — Subcarpetas de `Application/`.** Movimiento puro de archivos (el namespace no cambia: mismo assembly). Es el PR que hace que la carpeta plana de 47 archivos deje de leerse como una pila. Verificado por el compilador.
- **PR-4.2 — `Datos/` + `AperturaDeLectura`.** El gateway, migrando un consumidor por PR, con el guard de `new NpgsqlConnection` apretándose solo a medida que la lista de exclusiones se vacía.
- **PR-4.3 — `ConsultorDeAlcance` en un `SELECT`.** **Precondición: test rojo-verde de `CodigoDeRol` con dos roles vigentes distintos** (hoy ninguna aserción cubre ese valor).
- **PR-4.4 — Partir `CapaConversacional`.** Fábricas de resultado y cierre del turno a archivos hermanos. El archivo baja de 551 a ~350, dentro del cap.
- **PR-4.5 — Mover el prompt de generación** de `RenderizadorDeEsquema` a un tipo propio con su consumidor.

### Fase 5 — Frontend (paralelizable con todo lo anterior)

- **PR-5.1** — `import "../asistente.css"` en `LanzadorAsistente` + helper `hojasDelAsistente()` que barra el directorio para el guard de tokens. Borrar `.adoc-asistente-inicio-entrada` (regla muerta) y `.adoc-asistente-quien` (usar `.adoc-sr` de la librería).
- **PR-5.2** — `index.ts` de la feature + regla `no-restricted-imports` en `src/app/**`.
- **PR-5.3** — `shared/hooks/useDescartarAlClicAfuera` consumido por los 4 popovers, con test de las dos ramas (la del clic afuera no está probada en ningún lado del repo).
- **PR-5.4** — Guard de clases CSS huérfanas sobre `sinComentarios`, no sobre el texto crudo.

### Fase 6 — Rendimiento de la suite (el más riesgoso, al final)

- **PR-6.1 — Plantilla de PostgreSQL.** `CREATE DATABASE … TEMPLATE`. **Obligatorio: reaplicar `ALTER ROLE … IN DATABASE "<clon>" SET search_path = ''`** — vive en `pg_db_role_setting`, cuya clave es el OID de la base, y **no se clona**; sin eso los tests que sostienen el invariante #14 pasan a falso verde. Test que falle si el `search_path` efectivo sobre una base clonada no es vacío. Migrar a `Plantilla.Sembrada` **clase por clase**, solo donde el 100% de los casos siembra (6 clases): en las 18 restantes hay ~117 tests que arrancan con base vacía a propósito.
- **PR-6.2 — `AssemblyFixture`.** Sacar el `[Collection]` de las 45 clases. Empezar con `maxParallelThreads` 2-4 y medir; vigilar el DDL concurrente sobre `pg_authid` y que `ClearAllPools()` es process-wide.
- **PR-6.3 — Guard del prefijo del esquema en los cassettes.** Clase propia con base, contra el fixture del evaluador, excluyendo las tres huellas de instrucciones recalculándolas.

**Nota operativa que bloquea la Fase 1:** `backend/global.json` pinea SDK 10.0.201 con `rollForward: latestFeature` y en esta máquina hay 10.0.111, así que `dotnet build` sobre la solución falla localmente. Es probablemente la causa raíz del pre-commit roto, y es un obstáculo directo para el mismísimo ciclo de feedback local que la Fase 1 quiere habilitar. Merece su propio ticket, antes de PR-1.2.
