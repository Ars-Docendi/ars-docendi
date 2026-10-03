# El módulo Asistente — mapa, decisiones y lo que no se toca

Medido sobre el árbol el 2026-09-06, rama `feature/asistente-conversacional`.
Todo número de acá sale de un comando, no de la memoria. Si no coincide con el árbol,
gana el árbol: verificá antes de citar.

## 1. Qué es

Es el **único módulo sin dominio propio**. No hay entidades, no hay agregados, no hay
`Domain/`, y eso está argumentado en `backend/src/Modules.Asistente/README.md:26-28`.
Lo que hace: traduce una pregunta escrita en español a una consulta SQL contra los schemas
de **otros** módulos, la ejecuta con un rol de PostgreSQL que no puede escribir nada, y
redacta la respuesta. Todo lo que "sabe" del esquema lo lee del catálogo en tiempo de
ejecución; nada está copiado a mano.

Por eso tiene su propio invariante — **#14, "frontera de motor para consulta generada"**.
No puede cumplir el #1 (cross-module solo vía `Contracts`) porque pasar por Contracts
significaría que el modelo genere llamadas a métodos en vez de SQL, que es otro sistema.
La frontera la sostiene el motor: rol sin GRANT de mutación, GRANT enumerados columna por
columna contra un manifiesto versionado, policies RLS, y tests que fallan si cualquiera de
esas condiciones se degrada.

## 2. El turno, paso a paso — este es el mapa

Siete pasos, **hasta tres llamadas al modelo**. El resto es determinista.

1. **Entrada** — `Api/AsistenteController.cs:30` (`POST /api/asistente/consultas`).
   Exige `Idempotency-Key`. Resuelve el actor **solo desde la sesión, nunca del body**
   (`:48`). Consulta la caché de idempotencia antes de tocar nada.
2. **Orquestación** — `Application/CapaConversacional.cs:48`. Abre el presupuesto punta a
   punta del turno (un `CancellationTokenSource` encadenado al request) y corre, en orden:
   - resuelve el hilo conversacional en memoria;
   - **carril social** (saludo, agradecimiento, "¿qué podés hacer?") → **cero tokens**;
   - resuelve una aclaración pendiente si la hay → **cero tokens**;
   - detecta cambio de tema y suelta el segmento;
   - **reescribe la pregunta** con las anteriores del hilo → **llamada 1**, y solo si hay historial;
   - consulta el **enrutador determinista en sombra**: decide, anota telemetría y **descarta
     a propósito**;
   - detecta ambigüedad contra un índice de entidades traído de la base → cero tokens;
   - delega en el carril SQL.
3. **Carril SQL** — `Application/CarrilSql.cs:51`. Resuelve el perfil del actor contra
   `identity` (seis flags: ámbito global, ve datos personales, ve la consulta, alcanza
   designaciones…), le pide la consulta a `GeneradorDeSql` (**llamada 2**, con el prefijo del
   esquema cacheado por rol y hasta cuatro ejemplos few-shot por similitud), la pasa por
   `ValidadorDeSql` (tokeniza, lista **blanca** de comienzo `select`/`with`, lista **negra**
   de funciones) y la ejecuta vía `IEjecutorDeConsulta`.
4. **Ejecución acotada** — `Infrastructure/EjecutorDeConsulta.cs`. Conexión y transacción
   nuevas, `SET TRANSACTION READ ONLY`, `set_config('app.asistente_user_id', …, true)`
   **transaction-local** (no de sesión — por el pooling) para que resuelvan las policies RLS,
   envuelve con `LIMIT tope+1` (fila sonda), y clasifica cada columna del resultado por par
   **(OID de tabla, attnum)** contra un manifiesto de sensibilidad versionado.
5. **Reintento con criterio** — si vuelve vacío y el actor tiene ámbito global, se gasta
   **un** reintento. Si el actor es acotado, **no**: un cero puede ser "no hay" o "no podés
   verlo", y confundirlos es exactamente lo que la política de abstención existe para impedir.
6. **Enmascarado** — `Application/Enmascarador.cs` es la **frontera de salida**: lo que
   viaja al proveedor va enmascarado; lo que vuelve al usuario son las filas reales.
7. **Redacción** — **llamada 3**.

**Costo por turno, acotado y medido:** social = 0 llamadas · aclaración = 0 · SQL normal = 3.
Techo global impuesto por decorador; cuota por actor cobrada en un `finally` aunque el turno
explote. Cota documentada: `MaximoDeLlamadasPorTurno (4) × MaximoDeIntentosDeTransporte (3)
= 12 requests HTTP por turno`. **Ese 12 es un número escrito y defendido. Cualquier cosa que
lo mueva es una regresión, no una mejora.**

## 3. Dónde vive cada cosa

| Carpeta                        |       LOC | Qué es                                                                                                                                                                                                      |
| ------------------------------ | --------: | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Api/`                         |       340 | Tres endpoints y siete DTOs, sin lógica. Solo importa `Application`                                                                                                                                         |
| `Application/`                 |     6.157 | Cerebro **y** puertos: orquestación, dominio puro (tokenizador, validador, enmascarador, léxico, políticas) y las 17 interfaces que Infrastructure implementa. **47 archivos en una carpeta plana**         |
| `Infrastructure/`              |     4.526 | Adaptadores: cadena de decoradores del proveedor, transporte HTTP con grabación de cassettes, tres consultores de base, prefijo desde `pg_catalog`, cinco cachés perezosos, registro del turno. 33 archivos |
| `ModuleExtensions.cs`          |       391 | Raíz de composición. `AddAsistenteModule` son ~272 líneas con **40 registros DI**                                                                                                                           |
| `OpcionesAsistente.cs`         |       410 | 35 perillas planas, 22 de ellas `int`                                                                                                                                                                       |
| `Modules.Asistente.Contracts/` | **0 .cs** | Vacío a propósito, registrado como `"estado": "huerfano"` en `backend/manifiesto-de-aristas.json:58-60`                                                                                                     |

Frontend: `frontend/src/features/asistente/` — una sola vista (`PanelAsistente`) montada dos
veces, modal desde la barra y ruta `/asistente`. `asistente.css` son **881 líneas**, el CSS
más grande del frontend (le sigue `designaciones/pages/detalle.css` con 630).

Suite: `backend/tests/ArsDocendi.IntegrationTests/Asistente/` — 16.465 LOC, 725
`[Fact]/[Theory]`, **534 casos** (470 `[Fact]` + 64 filas de `[InlineData]`).
Evaluador: `backend/src/ArsDocendi.Evaluacion.Nucleo/` (en la solución) +
`backend/eval/ArsDocendi.Evaluacion` (**fuera** de la solución, a propósito).

## 4. Lo que YA está bien — no es candidato a "mejorar"

- **La frontera de motor es falsable.** `ManifiestoPrivilegiosTests` compara el manifiesto
  versionado contra `information_schema.column_privileges` en tres direcciones;
  `PrivilegiosLecturaTests` lee de verdad con los dos roles; `RlsAlcanceTests` y
  `Portal/RlsPortalAsistenteTests` verifican RLS sin `FORCE` y visibilidad cero sin actor.
- **El puerto del proveedor es la mejor abstracción del repo.** `IProveedorDeModelo` no
  menciona ningún proveedor; el SDK de Anthropic está confinado a
  `Infrastructure/ProveedorAnthropic.cs` y un test de arquitectura verifica que un solo
  archivo lo nombre. Sumar un proveedor = una clase + un brazo del `switch`. La temperatura
  se conserva en el puerto aunque el adaptador no la soporte, con el motivo escrito en
  `openspec/changes/asistente-proveedor-anthropic/design.md:37`: _"sacarla porque un
  adaptador no la soporta convertiría al puerto en la forma de ese adaptador"_.
- **La cadena de decoradores y su orden.** `ProveedorConTechoDeLlamadas → ProveedorConBreaker
→ ProveedorAnthropic|Simulado`, compuesta en `ModuleExtensions.cs:361`, con el motivo en
  `:130-142`: invertir los dos primeros haría que el breaker contara intentos que el techo iba
  a rechazar igual. Y `MaxRetries = 0` en el SDK (`ProveedorAnthropic.cs:68`) porque dos
  reintentadores triplicarían en silencio la cota de 12.
- **Los cassettes.** Clave = SHA-256 de cuatro campos del cable (`system`, `messages`,
  `output_config.effort`, `model`) y deliberadamente **no** del techo de tokens, así que mover
  esa perilla no invalida 120 cassettes. La grabación intercepta el cable
  (`DelegatingHandler`), no el puerto — un decorador de `IProveedorDeModelo` habría grabado la
  respuesta ya traducida. **Falla cerrado**: sin cassette y sin re-grabación, no sale a la red.
  Los 120 son salida real del proveedor desde el commit `2d11e7f`.
- **El enmascarado identifica por estructura**: par `(OID, attnum)` en vez del alias que
  eligió la consulta generada, con su límite declarado (se pierde ante cualquier expresión)
  registrado como TD-009.
- **Los guards de arquitectura vienen pareados.** `ArquitecturaAsistenteTests` corre cada
  detector sobre el código real **y** lo alimenta con una violación sintética
  (`:51` el detector, `:69` el par), para que una regex rota no pase en verde para siempre.
  **Todo guard nuevo tiene que venir pareado igual.**
- **Cero fake UI.** El botón no existe si `GET /capacidades` devuelve 403; el catálogo de
  ejemplos se valida con `EXPLAIN` contra la base antes de ofrecerse; el SQL solo se muestra
  si el perfil tiene el permiso.

## 5. Los nueve remedios ya evaluados y rechazados

Con evidencia y con el archivo donde vive la decisión. Si los proponés, traé información
nueva. Esta lista existe para que no vuelvan cada seis meses.

1. **Pipeline de middlewares / motor de etapas con `ContextoDelTurno` mutable.**
   El diagnóstico superficial es correcto (`CapaConversacional.cs:164-350` son 89 líneas de
   código con los siete pasos en comentarios y 6 puntos de salida), pero la consecuencia
   esperada es falsa: cada paso ya vive en una unidad pura con su propio archivo de test
   (`EnrutadorSocialTests` 15, `SeguimientoYAclaracionTests` 22, `HiloConversacionalTests` 19,
   `AmbiguedadTests` 10, `EnrutadorDeDominioTests` 9 = **75 atributos**; tres de esos cinco
   archivos son puros, dos derivan de `ClasePostgresAislada`). Lo que queda en `ResolverAsync`
   es **composición, no lógica**, y el orden ya está declarado fuera del código en
   `openspec/changes/asistente-capa-conversacional/design.md:7-17`. Peor: el objeto de request
   mutable que el patrón exige **ya fue rechazado por escrito** en `README.md:586-589`
   ("exigiría un objeto de request mutable con el actor adentro, leído por capas que no lo
   declaran"). El remedio proporcional es **partir el archivo** (renglón E4), no montar un motor.
2. **Polly / `AddStandardResilienceHandler()`.** Tres razones verificadas:
   (a) `BreakerDelProveedor` **no es un breaker de HTTP** — es estado del _puerto_, consultado
   **antes** de que el turno arranque, para elegir el texto que ve el usuario; un handler de
   resiliencia pondría un segundo corte en el pipeline HTTP que `DisponibilidadDelModeloReal`
   no puede observar; (b) reintroduce reintento **y** breaker: la cota de 12 pasaría a **36**
   sin que nada falle; (c) `ReintentoDeTransporte` usa **jitter completo** (uniforme entre cero
   y el backoff), distinto de la fórmula decorrelacionada de `UseJitter`, y su lista de
   reintentables es corta y documentada contra un caso real (el 400 de límite de gasto).
   _(Lo que sí es un defecto real y chico ahí: `ReintentoDeTransporte` es la única pieza que
   duerme contra el reloj de la máquina; todo lo demás recibe `TimeProvider`. Inyectárselo
   tiene cuatro call sites: `ModuleExtensions.cs:300` + tres tests.)_
3. **Extract-method sobre `AddAsistenteModule` por área.** Es un método de ~272 líneas con 40
   registros y eso es un olor real, pero **siete** archivos de test ya ejercitan un área sola
   contra el `AddAsistenteModule` completo, y el motivo está escrito en
   `CadenasDeConexionTests.cs:14-18`: se compone la registración de producción "para que sea la
   registración de producción la que se ejercita, no una reconstrucción".
4. **`[Range(1, …)]` por DataAnnotations sobre las perillas `int`.** En **cuatro** el cero es
   apagado deliberado y documentado: `CupoDeLlamadasPorActor`, `FallosParaAbrirElBreaker`,
   `PresupuestoDelTurnoSegundos`, `TopeDeTurnosDelHistorial`. Un `[Range]` uniforme rompe
   cuatro mecanismos de apagado. Va como `IValidateOptions` con tres baldes (renglón D3).
5. **Borrar el carril determinista en sombra** (`CatalogoDeIntenciones`, `ResolutorDeIntenciones`,
   `ICatalogoDelDominio`, `EnrutadorDeDominio`, `LectorDeVocabulario`, `CatalogoDelDominioReal`
   — 838 LOC). Está **desconectado a propósito**: decide, anota telemetría y descarta. Es un
   compromiso registrado con ARS-46, argumentado en
   `openspec/changes/asistente-enrutador-de-dominio/design.md`. Es reversible, pero se revierte
   por decisión del equipo, no de paso en un refactor.
6. **Mover `NormalizadorLexico` a `ArsDocendi.Shared`.** No pasa el invariante #4: lleva el
   diccionario de sinónimos del dominio del asistente (profesor→docente, tramite→pedido,
   catedra→materia) y las palabras vacías del español. Shared es el kernel del que dependen
   los 4 módulos; esto lo consume exactamente uno.
7. **Mudar `ArsDocendi.Evaluacion.Nucleo` a `tools/`.** `LectorDeAristas.LeerBackendSrc()` está
   cableado a `backend/src/`; moverlo convierte la única fila de excepción del manifiesto en
   "fila sin arista" y rompe el test del invariante #2. La crítica de ubicación es legítima,
   ya está registrada con ticket, y mover no compra nada funcional.
8. **Meter el evaluador en la solución, o usar `[Trait]` para filtrarlo.** La exclusión es
   **estructural** (fuera del `.slnx`, con seis guards adentro) porque el criterio es "qué
   cuesta dinero". El `[Trait]` del renglón C2 es otro mecanismo con otro motivo: ahí el costo
   del olvido es esperar de más; acá es una factura.
9. **Borrar comentarios, XML-docs o tests "que son mucho".** Ver §6.

## 6. Por qué el volumen es irreducible

- **Los tests son 1,44:1 contra el código productivo y eso es cobertura, no relleno.** De las
  16.465 líneas, 10.925 son código y 2.660 son XML-doc donde cada test explica el modo de
  falla que previene. La duplicación real identificada son ~700 líneas de _arrange_.
- **El dominio es intrínsecamente grande.** Un asistente que genera y ejecuta SQL sobre datos
  personales de una institución pública necesita, sin negociación: validador léxico, ejecutor
  acotado, clasificador de sensibilidad, enmascarador, manifiesto de privilegios con su
  comparador, catálogo de capacidades, política de abstención, hilo conversacional con
  desambiguación, puerto de proveedor con breaker y techo, mecanismo de grabación, e
  instrumento de evaluación. Ninguna pieza se puede borrar; se pueden mover.
- **Los 19 changes (~8.840 líneas) no son código.** Son el registro de decisiones. Archivarlos
  los saca de `openspec/changes/` y mergea las deltas a `openspec/specs/`: reduce ruido visual
  sin borrar información.

## 7. El riesgo residual que ninguna capa cubre

El asistente lee campos de texto libre escritos por usuarios (justificativos, comentarios de
revisión). Eso es contenido **no confiable** que vuelve al contexto del modelo. La condición
de riesgo de inyección indirecta está completa hoy: datos privados + contenido no confiable +
canal de salida. Los renglones A1, A2 y A3 la **reducen**; ninguno la cierra. Si alguien pide
"resolver la inyección de prompt", la respuesta honesta es que no se resuelve con un guard: se
acota sacando el texto libre del corpus (A3) y se declara.
