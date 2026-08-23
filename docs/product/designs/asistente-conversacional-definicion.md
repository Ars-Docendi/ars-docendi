---
status: draft # draft | review | approved
owner: ""
feature: "" # link al spec en openspec/specs/<capability>/spec.md cuando exista el change
last_updated: 2026-08-23
---

# Asistente conversacional — definición

Documento de definición del módulo **Asistente**: objetivo, casos de uso, requisitos y
arquitectura objetivo. Es la fuente de los tickets del proyecto de trabajo y el insumo del
change OpenSpec que el invariante #5 exige antes de tocar código.

## Qué es y qué no es este documento

**Es** el resultado de una sesión de definición en cuatro fases, hecha sobre el paquete de
handoff de una investigación previa (`handoff-chatbot/`, 8 archivos) que auditó un POC de
chatbot text-to-SQL y su integración al sistema.

**No es** una validación con usuarios. Los casos de uso de la sección 2 son **hipótesis
derivadas del esquema y de los roles**, no relevamiento. Nadie del Departamento los confirmó
todavía. El alcance no está congelado hasta que eso ocurra.

**El POC previo queda como aprendizaje, no como base de código.** Lo que se conserva son sus
decisiones verificadas, sus datasets y sus hallazgos; el sistema se construye de cero dentro
de ars-docendi.

### Convención de etiquetas

| Etiqueta       | Significado                                                                                  |
| -------------- | -------------------------------------------------------------------------------------------- |
| `[VERIFICADO]` | Comprobado contra el repositorio, la base o la documentación del proveedor durante la sesión |
| `[HIPÓTESIS]`  | Derivado por juicio, pendiente de validar con el Departamento                                |
| `[PENDIENTE]`  | Hueco explícito, no rellenado                                                                |

---

## 1 · Objetivo

> Que un empleado del Departamento de Ingeniería Informática obtenga, **conversando en
> español**, información que está en la base de ars-docendi y que su rol tiene derecho a ver —
> sin depender de que otra persona se la busque, y sin depender de que la interfaz tenga una
> vista construida para esa pregunta en particular.
>
> El asistente debe sostener una conversación clara: reconocer qué es y qué no es una pregunta
> del sistema, explicar sus propias capacidades, decir qué puede y qué no puede responder, y
> hacerlo según buenas prácticas de un chatbot moderno.

**Criterio de alcance**: si los datos están en la base y el usuario tiene permiso, el asistente
debería poder responder.

### 1.1 Naturaleza y contexto

| Dimensión                 | Definición                                                                                                                      |
| ------------------------- | ------------------------------------------------------------------------------------------------------------------------------- |
| Tipo                      | Producto de uso real en producción, no experimento                                                                              |
| Usuarios                  | ~1 a 30, personal del Departamento                                                                                              |
| Naturaleza                | **Módulo de asistencia. No es core.** Se apaga entero sin afectar al Host                                                       |
| Alternativa que reemplaza | Consultar por mail o preguntándole a otra persona — el sistema aún no está en producción, así que no hay proceso digital previo |
| Disponibilidad            | Baja aceptable. Su caída no cascadea                                                                                            |

### 1.2 Métrica de éxito

**Corrección con abstención.** El sistema nunca afirma algo falso; ante duda se abstiene y lo
dice. Saber callarse vale más que un punto de accuracy.

La adopción **no** es criterio en la etapa inicial: no hay línea de base contra la cual comparar
y no la va a haber, porque el proceso anterior nunca se midió.

> **Advertencia para el informe académico** `[VERIFICADO]`
> Los números de este proyecto no son comparables con los públicos de otros sistemas
> text-to-SQL. Sistemas en producción reportan aceptación de primer intento del orden del
> 40–48%; una métrica de coincidencia de result set contra una referencia, sobre un fixture
> propio, mide otra cosa. Presentarlos como si compitieran es un riesgo de credibilidad.

### 1.3 Usuarios

`[VERIFICADO]` — matriz de permisos real, `database/identity/008` + `010`.

| Rol                   | `designaciones.ver` | Ámbito  |
| --------------------- | ------------------- | ------- |
| `docente`             | **NO**              | materia |
| `jefe_catedra`        | Sí                  | materia |
| `coordinador_carrera` | Sí                  | carrera |
| `secretaria`          | Sí                  | global  |
| `decanato`            | Sí                  | global  |
| `administrativo`      | Sí                  | global  |
| `sys_admin`           | Sí                  | global  |

Los usuarios del asistente son los **seis roles no-`docente`**. La decisión es **provisional** y
coincide exactamente con la matriz del propio sistema: lo que RLS abriría es subconjunto de lo
que la API REST ya concede.

**No se implementa como lista de roles.** `[VERIFICADO]` La matriz está comentada en la
migración como _«PROVISIONAL — PENDIENTE DE CONFIRMACIÓN CON EL CLIENTE»_ y es editable en
runtime desde `/membresia-roles` sin migración; además el catálogo de roles **no es cerrado**
(Secretaría puede crear roles propios). Una lista negra `code != 'docente'` **falla abierta**:
cualquier rol nuevo pasaría por default. La exclusión se implementa como **chequeo de permiso
leído en vivo**.

---

## 2 · Casos de uso

`[HIPÓTESIS]` — ninguna familia fue validada con un usuario del Departamento.

| Familia                                        | Ejemplos                                                                                                                                           | Carril                                                 |
| ---------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------ |
| **F1 · Estado y avance del trámite**           | «¿En qué estado está el pedido de Andrea Cáceres?» · «¿Qué pedidos esperan revisión de coordinación?» · «¿Qué prioritarios siguen sin resolverse?» | SQL, API para el detalle                               |
| **F2 · Motivo y trazabilidad de una decisión** | «¿Por qué se devolvió el pedido de Pérez?» · «¿Quién lo rechazó y con qué justificativo?»                                                          | SQL                                                    |
| **F3 · Cobertura de cátedra y plantel**        | «¿Qué docentes dictan Algoritmos en Informática?» · «¿Qué materias no tienen ningún docente designado?»                                            | **SQL exclusivo**                                      |
| **F4 · Carga horaria y situación del docente** | «¿Cuántas horas tiene asignadas Pérez?» · «¿En qué materias está designado Sandoval?»                                                              | SQL                                                    |
| **F5 · Histórico y vigencia**                  | «¿Desde cuándo está vigente la designación de Frías?» · «¿Qué altas y bajas hubo este período?»                                                    | SQL                                                    |
| **F6 · Catálogo y parametrización**            | «¿Cuál es el período activo?» · «¿Cuántas carreras activas hay?»                                                                                   | API donde alcance, SQL para agregar                    |
| **F7 · Contacto e identificación (PII)**       | «Dame los teléfonos de los docentes de Lenguajes y Compiladores»                                                                                   | SQL con enmascaramiento                                |
| **F8 · Conversación, capacidades y borde**     | Saludo · «¿qué podés responder?» · desambiguación · seguimiento · modo degradado                                                                   | Determinista, **0 tokens**                             |
| **F9 · Primera persona**                       | «¿Cuáles son mis pedidos?»                                                                                                                         | SQL — requiere `users.persona_id`, que es **nullable** |

**F3 es el núcleo del valor.** `[VERIFICADO]` `DesignacionesController` tiene **solo `ping`**: no
existe ningún endpoint de designaciones vigentes. El asistente no duplica la API, cubre lo que
la API no tiene.

### 2.1 No-alcance

| #   | Fuera                                        | Motivo                                                                          |
| --- | -------------------------------------------- | ------------------------------------------------------------------------------- |
| 1   | Acciones, cambios de estado, escritura       | Solo lectura, decisión firme                                                    |
| 2   | Cualquier dato fuera de la base del sistema  | Sin APIs externas, planillas ni Intraconsulta                                   |
| 3   | Normativa y reglamento (`BR-*`)              | No está en la base                                                              |
| 4   | Aulas, Portal, Tareas                        | **No existen las tablas** — `[TEMPORAL]`, el backend es WIP                     |
| 5   | Alumnos, inscripciones, cupos                | No existen en el esquema                                                        |
| 6   | «¿Quién tiene horas disponibles?»            | No hay columna de techo de horas                                                |
| 7   | «¿Cuántos faltan para completar la cátedra?» | No existe el tamaño objetivo de cátedra                                         |
| 8   | `audit.change_log`                           | Guarda filas enteras en JSON: fuga por la ventana de atrás                      |
| 9   | `designaciones.idempotencia_comandos`        | `response_body JSONB` con respuestas HTTP completas                             |
| 10  | Rol `docente`                                | Provisional                                                                     |
| 11  | Escape a un agente humano                    | No existe el canal — una salida que no existe es una promesa falsa              |
| 12  | Persistencia del hilo conversacional         | Postergado. El hilo vive en memoria; se pierde en cada redeploy y eso se acepta |

---

## 3 · Requisitos

### 3.1 Funcionales

| ID    | Requisito                                                                                             |
| ----- | ----------------------------------------------------------------------------------------------------- |
| RF-01 | Consulta en español, respuesta redactada en español                                                   |
| RF-02 | Enrutado determinista a tres carriles: sin datos · API · SQL                                          |
| RF-03 | El carril social y meta resuelve con **0 tokens**                                                     |
| RF-04 | Catálogo de capacidades **por actor**, derivado de los GRANT efectivos y nunca del payload del prompt |
| RF-05 | Rechazo cooperativo con `sugerencias`, campo distinto de `opciones`                                   |
| RF-06 | Desambiguación por consulta a la base, sin LLM                                                        |
| RF-07 | Reconocer la respuesta a una aclaración: etiqueta → token distintivo → ordinal                        |
| RF-08 | Seguimiento conversacional con reescritura a pregunta autocontenida                                   |
| RF-09 | El cambio de tema **fuerza** historial vacío; no se le pide al modelo que lo ignore                   |
| RF-10 | Mostrar `pregunta_interpretada` cuando difiere del mensaje del usuario                                |
| RF-11 | Transparencia **media**: exponer `razonamiento`, sin explicación paso a paso                          |
| RF-12 | Enmascarar datos sensibles hacia el LLM; valores reales al cliente                                    |
| RF-13 | Autorización por **permiso leído en runtime**, dentro del predicado RLS                               |
| RF-14 | Cuatro estados: respondida · no contestable · necesita aclaración · servicio degradado                |
| RF-15 | Feedback progresivo con estados honestos y umbral de aparición                                        |
| RF-16 | Registro operativo y analítico **desvinculados entre sí**                                             |
| RF-17 | Política de abstención (sección 3.3)                                                                  |
| RF-18 | La fecha de referencia es un parámetro del turno                                                      |
| RF-19 | El modo degradado resuelve por los carriles deterministas                                             |
| RF-20 | Cuota por actor medida en **llamadas al LLM**, no en requests HTTP                                    |

### 3.2 No funcionales

| ID     | Requisito                                                                                                            |
| ------ | -------------------------------------------------------------------------------------------------------------------- |
| RNF-01 | Métrica primaria: corrección con abstención, con score penalizado                                                    |
| RNF-02 | Gate de regresión con **lock por ítem**, no umbral agregado                                                          |
| RNF-03 | Los reportes de evaluación se sellan con hash de prefijo, dataset y seed                                             |
| RNF-04 | El eval aborta si la API no responde y **no deja reporte en disco**                                                  |
| RNF-05 | Solo lectura **mecánica**: rol sin GRANT de mutación + invariante escrito + test                                     |
| RNF-06 | Deny by default con manifiesto versionado; el test falla en **tres** direcciones                                     |
| RNF-07 | PII controlada por GRANT de columna; segundo rol para el carril con PII                                              |
| RNF-08 | Sin reloj en la SQL: el validador lo rechaza                                                                         |
| RNF-09 | Techo total por turno: **30 s**, medido punta a punta y no por etapa                                                 |
| RNF-10 | Techo de llamadas al LLM por turno (**4**), global y no por capa                                                     |
| RNF-11 | Reintento con backoff y jitter; `400` excluido del reintento                                                         |
| RNF-12 | Techo de gasto configurado en la consola del proveedor, por ambiente                                                 |
| RNF-13 | El proveedor de LLM es reemplazable detrás de una interfaz                                                           |
| RNF-14 | Prefijo del prompt estable: nada que mute el system prompt por turno                                                 |
| RNF-15 | Sin clave real en ambientes efímeros de PR: cliente simulado determinista                                            |
| RNF-16 | El proyecto de evaluación se excluye **estructuralmente** del CI, más un guard                                       |
| RNF-17 | `aria-live` solo sobre los mensajes, `role="log"`, `role="status"`, gestión de foco                                  |
| RNF-18 | Sin etiquetas internas ni errores crudos visibles al usuario                                                         |
| RNF-19 | Retención 90 días con purga verificada por test; **sin `audit.attach`**                                              |
| RNF-20 | El módulo se apaga entero sin afectar al Host                                                                        |
| RNF-21 | Español; código en español según el invariante #13                                                                   |
| RNF-22 | Disponibilidad baja aceptable; la caída del asistente no cascadea                                                    |
| RNF-23 | El validador emite el contenido de identificadores entrecomillados y lo chequea contra funciones prohibidas          |
| RNF-24 | Cuatro ejes de evaluación: capacidad · robustez de fraseo · diálogo con chequeo negativo de arrastre · social y meta |

### 3.3 Política de abstención

| #   | Cuándo                                                                   | Qué devuelve                                          |
| --- | ------------------------------------------------------------------------ | ----------------------------------------------------- |
| 1   | El esquema no cubre la pregunta                                          | No contestable **+ sugerencias**                      |
| 2   | Choque de valores (apellido compartido, materia repetida entre carreras) | Necesita aclaración + opciones                        |
| 3   | Resultado vacío **y actor no global**                                    | «No encontré nada en tu alcance» — **nunca «no hay»** |
| 4   | El resultado se truncó                                                   | Sin afirmar conteo                                    |
| 5   | El validador rechaza la SQL                                              | No contestable, sin reintento ciego                   |
| 6   | Proveedor caído o cuota agotada                                          | Servicio degradado                                    |
| 7   | El dato existe pero falta el permiso                                     | «No tenés acceso» — **nunca «no hay»**                |

**Restricción dura**: nunca declarar cuántas filas quedaron afuera. «Ves 3 de 124» es un canal
de inferencia sobre datos que el usuario no puede ver.

### 3.4 Registro de consultas y retención

Dos registros que **no se cruzan**, ambos con retención de 90 días y purga automática
verificada por test.

| Registro      | Guarda                                                                                                      | No guarda                                  |
| ------------- | ----------------------------------------------------------------------------------------------------------- | ------------------------------------------ |
| **Operativo** | `actor_id`, timestamp, carril, estado del resultado, llamadas al LLM, tokens, latencia, reintento, truncado | El texto de la pregunta, la SQL, las filas |
| **Analítico** | Texto de la pregunta, categoría, estado del resultado, fecha **redondeada al día**                          | `actor_id`, hora exacta, la SQL, las filas |

**Por qué la fecha redondeada**: con ~30 usuarios, un timestamp preciso permitiría reidentificar
al autor cruzando ambos registros con un join.

**Nunca, en ninguno de los dos**: las filas devueltas. Ni por defecto ni detrás de un flag.
La SQL generada solo en logs de diagnóstico efímeros — un `WHERE` puede llevar un DNI.

**No aplicar `audit.attach()` a estas tablas.** Es el patrón de las migraciones del repo; si
alguien lo agrega por consistencia, el texto de cada pregunta termina también en
`audit.change_log`, que guarda la fila entera en JSON y no tiene política de retención. Debe
quedar declarado explícito en la migración, con el motivo escrito.

Ambas tablas las escribe la **conexión dueña**, no el rol de solo lectura.

---

## 4 · Arquitectura objetivo

### 4.1 Ubicación

`Modules.Asistente` + `Modules.Asistente.Contracts`, módulo normal registrado por el Host con
`AddAsistenteModule()`. Rutas `/api/asistente/*`, frontend `features/asistente`.

Nombre en español por el invariante #13, consistente con Designaciones, Aulas, Portal y Tareas.

**Nota**: el `.Contracts` nace vacío — el asistente consume Contracts ajenos, pero nadie lo
consume a él. `[PENDIENTE]` decidir con el equipo si se crea por convención o se documenta la
excepción.

**Ubicaciones descartadas**: `ArsDocendi.Shared` (el invariante #4 le prohíbe I/O, red y estado
mutable, y el asistente es las tres cosas) · dentro del Host (el único lugar que escapa a los
tres guards de los tests de arquitectura: el sitio menos restringido para el componente que más
restricción necesita) · servicio aparte (contra la topología documentada; sumaría un contenedor
por cada ambiente efímero de PR).

### 4.2 El turno

```
POST /api/asistente/consultas
  │
  ├─ [Authorize(asistente.consultar)]
  ├─ Cuota por actor                          → sin cupo: servicio_degradado, sin llamar al proveedor
  ├─ Resolver hilo (memoria, TTL 2 h)
  │
  ├─ IntentRouter  ─────────────────────────► CARRIL SIN DATOS · 0 tokens
  │    saludo · agradecimiento · meta
  │    (se saltea si hay aclaración pendiente)
  │
  ├─ ClarificationReplyMatcher
  ├─ QuestionRewriter (solo si hay historial) + PivotDetector
  │
  ├─ EnrutadorDeDominio ───────────────────► CARRIL API · vía Modules.<X>.Contracts + DI
  │
  ├─ AmbiguityDetector (consulta la base) ──► necesita_aclaracion · 0 tokens
  │
  └─ CARRIL SQL
       generación (call 1, temp 0.0, prefijo cacheado)
         → SqlValidator
         → ejecución envuelta: LIMIT 201, READ ONLY, GUC del actor
         → Enmascarador
         → redacción (call 2, temp 0.3)
```

**El enrutador de dominio es determinista.** Clasificar la intención con una llamada al LLM está
descartado con evidencia: 60% de F1 para triage de cinco clases, 77,4% para nueve vías. Un
clasificador que falla una de cada cuatro veces, cuesta una llamada y corta el flujo es peor que
una tabla.

Tres reglas lo hacen viable:

1. **Catálogo cerrado de intenciones**, cada una con patrón sobre texto normalizado, slots
   exigidos y endpoint destino.
2. **Los slots se resuelven contra la base**, con el mismo índice de entidades que usa el
   detector de ambigüedad. Nada hardcodeado.
3. **Enruta a la API solo si todos los slots resuelven a un valor único.**

**El default es SQL, nunca API.** Enrutar mal hacia la API devuelve cero filas, y «cero filas» es
indistinguible de «no hay» — exactamente la mentira que RF-17 prohíbe. Fallar hacia el carril
más caro es fallar hacia el carril que puede responder.

**Honestidad de costo**: el carril API cuesta 0 tokens **en el primer turno**. En un seguimiento
paga el reescritor, porque enrutar «¿y el de Pérez?» sin resolver la anáfora es imposible.

### 4.3 Acceso a datos

Tres conexiones que nunca se cruzan:

| Conexión        | Rol                           | Uso                                                                                     |
| --------------- | ----------------------------- | --------------------------------------------------------------------------------------- |
| Dueña           | rol dueño                     | Migraciones del módulo y escritura de los dos registros                                 |
| Lectura básica  | `asistente_ro_<ambiente>`     | SQL generada, sin PII                                                                   |
| Lectura con PII | `asistente_ro_pii_<ambiente>` | SQL generada cuando el actor tiene el permiso de datos de docentes **y** alcance global |

**Dos roles de lectura y no uno** porque es lo que mantiene la defensa en el motor: un usuario
sin el permiso **no puede leer la columna**, no «no se la mostramos».

**La condición para la conexión con PII exige alcance global además del permiso**, y no es
redundancia. `Politicas.DocentesVer` la pasa quien tenga `usuarios.ver` **o** quien esté en el rol
`jefe_catedra`, pero los tres endpoints de docentes **acotan los datos por separado**, en el
controller. La política es la puerta; el acotamiento es otra cosa que se aplica después. Un
asistente que mirara solo la política heredaría la puerta y no el acotamiento, y como
`identity.personas` no tiene RLS, un jefe de cátedra podría leer documento y teléfono de todo el
padrón — algo que la interfaz le niega.

**Riesgo residual aceptado y registrado**: un actor de ámbito de materia o carrera sigue pudiendo
listar nombre, apellido y legajo de todo el padrón, porque esas columnas se conceden al rol básico
y `personas` no tiene RLS. Se aceptó porque son datos que ya circulan en cualquier listado de
cátedra, y porque agregar una policy sobre `identity` que consulte `designaciones` invertiría la
dirección del grafo de dependencias: es una discusión que merece su propio PR y no el mismo donde
se pide la excepción arquitectónica.

**Tipos distintos por cadena** — `CadenaDueña`, `CadenaSoloLectura`, `CadenaSoloLecturaPii` — para
que pedir la equivocada no compile. Hoy el sistema tiene una sola cadena que comparten los dos
`DbContext`; introducir tres `string` sueltos convertiría el error en un fallo silencioso que
solo se nota cuando alguien logra escribir.

**Rol por ambiente**, con contraseña propia, creado y destruido con la base. Los roles de
Postgres son objetos de **cluster**: un rol único sería el mismo principal, y la misma
contraseña, para producción y para cada ambiente efímero de PR que corre código arbitrario.

**Reparto del DDL** — lo fuerza que el provisioning corra en el paso 1 sobre una base vacía y
las migraciones en el paso 3:

| Pieza                                                                 | Dónde                                     | Por qué                                                                      |
| --------------------------------------------------------------------- | ----------------------------------------- | ---------------------------------------------------------------------------- |
| `CREATE ROLE`, `GRANT CONNECT`, `search_path`                         | Script de provisioning                    | El rol debe existir antes que todo                                           |
| `GRANT USAGE`, `GRANT SELECT (columnas)`, `CREATE EXTENSION unaccent` | Migración del módulo                      | En el paso 1 no hay tablas: `GRANT ON ALL TABLES` **otorga cero y no falla** |
| Policies RLS sobre tablas de `designaciones`                          | Migración del módulo dueño de esas tablas | El dueño del bounded context escribe su DDL                                  |

**Manifiesto deny-by-default**, versionado: toda tabla de los schemas expuestos figura como
`concedida(columnas)` o `denegada-explícita(motivo)`. El test falla en **tres** direcciones —
privilegio fuera del manifiesto · privilegio declarado que desapareció · **tabla en el schema
sin clasificar**.

La tercera es la que importa a futuro. `[VERIFICADO]` En el POC, al sincronizar con la PR #23
apareció `designaciones.idempotencia_comandos`, cuyo `response_body JSONB` guarda la respuesta
HTTP completa de cada comando —con datos de personas adentro—. El `GRANT ON ALL TABLES` **se la
habría dado sola**. Como el backend sigue creciendo, van a seguir apareciendo tablas así: el
manifiesto es el mecanismo que hace que la próxima rompa el CI en vez de abrir un agujero.

### 4.4 Autorización: RLS con el permiso de dominio adentro

**Por qué RLS y no autorización en la aplicación**: en el resto del sistema, la consulta la
escribimos nosotros y el conjunto de consultas posibles es finito y conocido en compilación. En
el asistente el conjunto es infinito y desconocido hasta el runtime. RLS es la única capa que se
evalúa **junto con cada fila**, sin importar qué código produjo la SQL.

Cuatro funciones `SECURITY DEFINER`:

```
identity.asistente_actor()             -- lee el GUC del turno
identity.asistente_es_global()
identity.asistente_materias_visibles()
identity.asistente_tiene_permiso(code) -- user_roles → rol_permisos → permisos, en vivo
```

Predicado de cada policy:

```sql
identity.asistente_tiene_permiso('designaciones.ver')
AND ( <predicado de ámbito: global | carrera | materia> )
```

**Esa conjunción es el punto central.** RLS decide **qué filas**, no si el usuario tiene derecho a
la tabla. Sin el permiso adentro del predicado, un rol con ámbito de materia pero sin permiso de
designaciones recibiría pedidos, historial y justificativos de rechazo que la API REST le niega
con 403 — el asistente no ampliaría un permiso, **crearía acceso donde no hay ninguno**. Un
`[Authorize]` en el endpoint no cubre el hueco: cuando la SQL ya está corriendo, el `[Authorize]`
es pasado.

Leer el permiso en vivo es además lo que mantiene al asistente sincronizado con el sistema: si
Secretaría cambia la matriz desde `/membresia-roles`, el asistente la sigue sin desplegar nada.

**Propagación del actor**: conexión y transacción nuevas por cada ejecución,
`SET TRANSACTION READ ONLY`, y `set_config('app.asistente_user_id', <id>, true)` — el tercer
parámetro es **transaction-local**, así que el GUC muere en el `COMMIT` y no sobrevive al pool.

**La fuente del id es `ICurrentUser.UserId`**, que ya expone `identity.users.id`. **Nunca el claim
`oid` de Azure AD**: el GUC espera el id local y meter el object id **rompe en silencio**.

`ENABLE`, **no `FORCE`**. Las policies son `TO asistente_ro*` y el backend conecta como el rol
dueño; en Postgres, RLS ya aplica a todo rol no propietario con `ENABLE` a secas. `FORCE` existe
para someter al propietario y aplicarlo tiraría el backend entero.

### 4.5 Enmascaramiento

Va **entre la ejecución de la SQL y la llamada de redacción**. El manifiesto clasifica cada
columna en tres categorías:

| Categoría        | Qué pasa                                                                 |
| ---------------- | ------------------------------------------------------------------------ |
| `publica`        | Viaja al modelo tal cual                                                 |
| `sensible-valor` | Al modelo va un marcador estable; el valor real va al cliente en `filas` |
| `sensible-texto` | **No viaja al modelo en absoluto**; va directo al cliente                |

La tercera resuelve el texto libre —`pedido_historial.comentario`, los justificativos de
rechazo—, donde redactar con reglas sería frágil y no mandarlo es simple.

**Consecuencia de diseño**: con columnas sensibles, la narración deja de ser el vehículo del
dato. El modelo redacta el marco («encontré 4 docentes») y la interfaz renderiza la tabla.

**El enmascaramiento es asimétrico y no cierra la entrada.** La pregunta cruda del usuario viaja
al proveedor. Si alguien tipea un DNI en la pregunta, llega al modelo igual. Protege el camino
de vuelta, no el de ida.

### 4.6 Contrato de API

| Endpoint                         | Qué                                                       |
| -------------------------------- | --------------------------------------------------------- |
| `POST /api/asistente/consultas`  | El turno                                                  |
| `GET /api/asistente/capacidades` | Catálogo por actor, derivado de los GRANT efectivos       |
| `GET /api/asistente/ping`        | `[AllowAnonymous]`, sin base ni proveedor — invariante #3 |

Respuesta: `estado` (los cuatro) · `respuesta` · `pregunta_interpretada?` · `razonamiento?` ·
`opciones[]?` · `sugerencias[]?` · `filas[]?` · `columnas[]` con marca de sensibilidad · `sql?`
detrás de permiso · `metricas{}`.

**`Idempotency-Key` obligatorio** en el POST — un doble submit cuesta 2 o 3 llamadas al LLM. Se
resuelve **en memoria con TTL corto**: alcanza para el doble clic y no persiste nada. No se reusa
ni se copia `designaciones.idempotencia_comandos`, que guarda el `response_body` completo.

**Feedback progresivo sin SSE**: respuesta completa, indicador en el cliente con umbral de
aparición y `role="status"`. No se inventan etapas. Etapas reales exigirían SSE, que cambia el
contrato y agrega infraestructura difícil de justificar en un módulo no-core. Queda como mejora
si se mide que hace falta.

### 4.7 Frontend

`features/asistente`, con **dos montajes**: ruta propia `/asistente` y lanzador global. Una ruta a
la que hay que navegar no resuelve el problema de descubrimiento.

El lanzador tiene lugar natural: hoy existe un botón **«Ayuda»** en la barra superior, `disabled`,
con `title="Próximamente"`. Activarlo **elimina un fake UI existente** en vez de agregar superficie.

No se porta el HTML del POC: su defecto de accesibilidad está verificado ahí. Se resuelve con el
patrón correcto en React — región viva **solo sobre los mensajes**, `role="log"`, la línea de
métricas **fuera** de la región viva, foco en el campo de entrada al llegar la respuesta.

### 4.8 Evaluación y CI

Proyecto de evaluación **fuera del archivo de solución**, más un guard **dentro** que falle si
vuelve a entrar. Un filtro en el YAML no sobrevive a un merge descuidado, y el síntoma sería una
factura, no un test rojo.

Ambientes efímeros: **cliente simulado determinista**, con default seguro en el compose. Sin clave
real, nunca — el workflow de despliegue de esos ambientes hace checkout del head del PR y ejecuta
un script que viene del propio PR, en un job con los secrets del environment.

Cuatro ejes con reportes separados, sellados con hash de prefijo, dataset y seed. Gate por **lock
por ítem**. Preflight que aborta con código ≠ 0 si la API no responde, **sin dejar reporte en
disco** — sin crédito, el eval no falla: miente.

---

## 5 · Trazabilidad de los fixes del handoff

| Fix      | Qué resolvía                                          | Dónde aterriza                                                                                    |
| -------- | ----------------------------------------------------- | ------------------------------------------------------------------------------------------------- |
| F-01     | Bypass del validador por identificador entrecomillado | RNF-23 · §4.2                                                                                     |
| F-02     | Reportes que describen datasets inexistentes          | RNF-03, RNF-04 · §4.8                                                                             |
| F-03     | Vacío por RLS confundido con inexistencia             | RF-17 casos 3 y 7                                                                                 |
| F-04     | Sin techo de gasto ni cuota                           | RF-20, RNF-12 · §4.2                                                                              |
| F-05     | El eval no mide arrastre de contexto                  | RNF-24 · §4.8                                                                                     |
| F-06     | Cero reintento de transporte                          | RNF-10, RNF-11                                                                                    |
| F-07     | El reescritor arrastra siempre                        | RF-08 · §4.2                                                                                      |
| F-08     | Un saludo cuesta una llamada completa                 | RF-02, RF-03 · §4.2                                                                               |
| F-09     | `answerable=false` es terminal y hostil               | RF-05 · §4.6                                                                                      |
| F-10     | El cambio de tema no se detecta                       | RF-09 · §4.2                                                                                      |
| F-11     | El eval no detecta regresiones                        | RNF-02 · §4.8                                                                                     |
| F-12     | No hay eje de evaluación social                       | RNF-24 · §4.8                                                                                     |
| F-13     | No hay modo degradado                                 | RF-14, RF-19 · §4.2                                                                               |
| F-14     | Accesibilidad del stream                              | RNF-17 · §4.7                                                                                     |
| F-15     | Etiquetas internas y errores crudos al usuario        | RNF-18 · §4.6, §4.7                                                                               |
| F-16     | `razonamiento` se paga y se tira                      | RF-11 · §4.6                                                                                      |
| F-17     | La métrica no premia abstenerse                       | RNF-01                                                                                            |
| **F-18** | Documentación del POC desactualizada                  | **Huérfano declarado.** El POC no es la base de código                                            |
| F-19     | No hay descubrimiento de capacidades                  | RF-04 · §4.6                                                                                      |
| **F-20** | Costo por llamada repetida                            | **Huérfano declarado.** Optimización opcional; con cache de prefijo compartido el margen es menor |

---

## 6 · Decisiones y descartes que se respetan

Del análisis previo, con su motivo. **No volver a proponerlos sin evidencia nueva.**

| Descarte                              | Motivo                                                                                                                                               |
| ------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------- |
| Clasificar la intención con el LLM    | 60% de F1 en triage de 5 clases; 77,4% en 9 vías. Peor que una tabla, y cuesta una llamada                                                           |
| Small talk generativo                 | Agrega llamada, temperatura y alucinación en el turno donde no hay nada que averiguar; y vuelve no determinista el eje que necesita afirmar 0 tokens |
| Generar las sugerencias con el LLM    | Tercera llamada por turno, y puede sugerir preguntas sin respuesta                                                                                   |
| **Semantic caching sobre respuestas** | **Letal con RLS**: la misma pregunta de dos actores tiene respuestas distintas por diseño. Un cache compartido es un canal de fuga entre usuarios    |
| Schema pruning / selector de esquema  | Rompe la estabilidad del prefijo cacheado: cada pregunta pagaría escritura en vez de lectura sobre el bloque más grande del prompt                   |
| Descomposición multi-agente           | Factor ~50× de costo por pregunta                                                                                                                    |
| Self-consistency                      | 20–30× de costo para comprar ~2 puntos                                                                                                               |
| Score de confianza numérico           | El modelo reporta 95%+ sin importar si acertó; no calibra, persuade                                                                                  |
| `FORCE ROW LEVEL SECURITY`            | Sometería al rol dueño a policies que no lo contemplan: caída total del backend                                                                      |
| Rate limiting por IP                  | Todo el tráfico entra por un túnel; un departamento tras NAT compartiría cupo                                                                        |
| Clave de API real en ambientes de PR  | El workflow ejecuta un script que viene del propio PR con los secrets del environment                                                                |
| Rechazar toda SQL con comillas dobles | Falsos positivos con alias legítimos. La solución correcta es emitir el contenido y chequear funciones                                               |
| Umbral agregado de accuracy como gate | Tres ítems que se rompen y tres que se arreglan dan delta cero y pasan el umbral                                                                     |
| Escape a un agente humano             | No existe el canal; una salida que no existe es una promesa falsa                                                                                    |

---

## 7 · Enmienda de arquitectura requerida

El asistente ejecuta SQL generada por un modelo directamente contra los schemas `identity` y
`designaciones`. No pasa por repositorios ni por Contracts. De los cinco pasos del checklist para
agregar un edge, **«implementar vía Contracts + DI» es el único que no puede cumplir** — y no por
pereza: pasar por Contracts significaría que el modelo genere llamadas a métodos en vez de SQL,
que es otro sistema.

**No existe lectura del texto actual de la frontera de identity bajo la cual esto no viole su
letra.** La salida es enmendar la regla explícitamente, no reinterpretarla: una regla
reinterpretada deja de restringir a nadie, y `/architecture-drift-check` se queda sin nada contra
qué evaluar.

Texto propuesto:

> **Invariante #14 — Frontera de motor para consulta generada.**
> Un módulo puede consultar schemas ajenos sin pasar por `Contracts` **únicamente** si la frontera
> está sostenida por el motor de base de datos y es falsable: rol de Postgres sin GRANT de
> mutación, GRANT enumerados columna por columna contra un manifiesto versionado, policies RLS que
> conjunten el permiso de dominio, y tests que fallen si cualquiera de esas condiciones se degrada.
> Hoy aplica a `Modules.Asistente` y solo a él.

Acompañan: filas nuevas en el registro de edges (`Modules.Asistente → Modules.Designaciones.Contracts`
y hacia la superficie de administración, para el carril API), los dos diagramas actualizados, y el
motivo documentado en el PR.

**El argumento**: el asistente cambia el _mecanismo_ de la frontera sin cambiar su _propósito_. El
propósito de «solo vía Contracts» es que un módulo no alcance lo interno de otro por caminos no
declarados ni verificables. Acá el camino está declarado en un manifiesto y verificado por el
motor. Efecto que ninguna convención consigue: «logs sin PII», que `data-model.md` exige y hoy es
disciplina, pasa a ser un **hecho mecánico**.

**Nota de honestidad**: ser un módulo no-core hace _más_ difícil defender esta enmienda, no menos.
«Es solo un módulo de asistencia» es exactamente el argumento con el que se puede rechazar la
excepción. Y un módulo no-core que abriera un agujero de autorización sería el peor intercambio
posible — por eso el permiso de dominio dentro del predicado RLS no es recomendado, es innegociable.

---

## 8 · Huecos abiertos

| #   | Hueco                                                  | Qué bloquea                          | Cómo se cierra                                                                                                                                    |
| --- | ------------------------------------------------------ | ------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | **Validación de los casos de uso** con el Departamento | Congelar el alcance                  | Conversación con un usuario real. Prevista, sin fecha                                                                                             |
| 2   | **Autenticación de producción (Azure AD)**             | Que el asistente llegue a producción | Tiene dueño, no tiene fecha. **Fuera del camino crítico**: se construye listo para producción y habilitarla es un flag                            |
| 3   | **Marco institucional de datos personales**            | Nada, por ahora                      | Cubierto con el default conservador de §3.4, revisable si aparece una política                                                                    |
| 4   | **Valor del techo de gasto**                           | Producción                           | Parámetro operativo; debe existir antes de desplegar                                                                                              |
| 5   | **PR #23 sin integrar**                                | Todo lo que asume el modelo de datos | Su cuerpo dice _«En prueba, no integrar a develop todavia»_. Confirmar con el equipo                                                              |
| 6   | **`.Contracts` vacío**                                 | Nada                                 | Consulta chica al equipo                                                                                                                          |
| 7   | **`identity.personas` sin RLS**                        | Nada; riesgo residual acotado        | Mitigado exigiendo alcance global para la conexión con PII (§4.3). El cierre completo es una policy propia, con la inversión del grafo a discutir |

---

## 9 · Referencias

- Handoff de la investigación previa: `handoff-chatbot/` — 8 archivos, con la evidencia de cada hallazgo
- [`docs/architecture/module-anatomy.md`](../../architecture/module-anatomy.md)
- [`docs/architecture/dependency-graph.md`](../../architecture/dependency-graph.md)
- [`docs/architecture/data-model.md`](../../architecture/data-model.md)
- [`docs/quality/golden-principles.md`](../../quality/golden-principles.md)
- [`docs/product/design-principles.md`](../design-principles.md)
