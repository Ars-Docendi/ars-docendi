## Context

El frontend de Designaciones y el de administración funcionan enteramente sobre mocks. Los 4 `DbContext` del backend no declaran entidades. Lo único persistido es `identity` + `audit`, escritos a mano en `database/*.sql` con triggers plpgsql.

El diseño se derivó de lo que las pantallas ya ejercitan, no de un modelo especulativo. Eso permitió detectar cinco contradicciones latentes entre el código mock, las specs vigentes en `openspec/specs/` y los dos changes en vuelo (`admin-docentes`, `roles-membresia`), que este documento resuelve una por una.

Restricciones heredadas que el diseño respeta sin discusión:

- **`audit` es la fuente única de metadata de auditoría.** `data-model.md` prohíbe denormalizar `created_by` / `updated_at` / `updated_by` / `deleted_by`. Solo `created_at` está tolerado. Toda tabla de negocio termina su archivo SQL con `SELECT audit.attach(...)`.
- **Soft delete es per-tabla y opcional.** Solo participan las tablas que declaran `deleted_at`, y sus unique indexes deben ser parciales (`WHERE deleted_at IS NULL`).
- **FKs cross-schema: evitarlas.** Referencia blanda por ID + validación vía la interfaz pública del otro módulo.
- **Idioma del código: español** (invariante #13), salvo símbolos del framework.

## Goals / Non-Goals

**Goals:**

- Schema `identity` completo: personas separadas de cuentas, roles creables con scope, permisos y su membresía.
- Schema `designaciones` completo: períodos, pedidos, adjuntos, historial, designaciones vigentes y catálogo de cargos.
- Trazabilidad de punta a punta: de una designación vigente al pedido que la originó, de ahí a su historial de trámite, y de cualquier fila a `audit.change_log`.
- Un dueño explícito para `identity` y `audit`, que hoy no pertenecen a ningún módulo.
- Un mecanismo de migración que soporte el plpgsql que EF Core no sabe generar.

**Non-Goals:**

- Schemas `portal`, `aulas` y `tareas`. Sus `types.ts` están vacíos: no hay pantalla que valide un modelo. Modelarlos ahora sería especulación (YAGNI).
- Integración real con la API Guaraní. El catálogo de materias sigue siendo local; Guaraní se ataca en otro change.
- Permisos con scope por carrera/materia. `roles-membresia` ya lo declaró non-goal ("solo permiso global por ahora") y se respeta.
- Migrar el frontend a HTTP real. Este change define el schema y su capa de persistencia; el reemplazo de los mocks por React Query es trabajo posterior.
- Jerarquía de cargos ("solo se puede subir"). La spec vigente la deja fuera de alcance explícitamente (tema C). El catálogo `cargos` lleva una columna `orden` que la habilitará después, sin usarla ahora.

## Decisions

### D1 — `identity.personas` separada de `identity.users`

**Opción elegida**: dos tablas. `personas` es la entidad canónica de un ser humano (documento, CUIL, legajo, nombre, apellido, fecha de nacimiento, teléfono) y existe con o sin cuenta. `users` queda acotada a autenticación (azure_oid, upn, display_name) y lleva `persona_id UUID NULL`.

**Alternativas descartadas**:

- _`portal.docentes` como entidad canónica_ — es lo que insinuaba `data-model.md`. Obliga a Designaciones a soft-referenciar cross-schema sin integridad referencial, y a que un Alta cree un docente en Portal desde Designaciones, acoplando dos módulos.
- _Snapshot embebido en el pedido, sin entidad de persona_ — hace imposible preguntar "todos los pedidos del docente X" con integridad, y duplica PII en cada fila.

**Por qué**: el caso que rompe cualquier alternativa es el pedido de novedad "Alta". Refiere a un docente que **nunca se logueó** (no está en `users`, que solo contiene principals de Azure AD vistos por el sistema) y que **todavía no tiene legajo** (BR-designaciones-018 exime al Alta de tenerlo). Con `personas` separada, un Alta es `INSERT INTO personas` con `legajo NULL`, y el primer login rellena `users.persona_id`. Además `admin-docentes` D10 ya lo había anticipado: _"en producción, ambas features llamarán al mismo endpoint `/api/identity/personas`"_.

**Consecuencia sobre PII**: `data-model.md` dice hoy que "el módulo Portal maneja datos personales de docentes". Con esta decisión la PII (documento, CUIL, teléfono, fecha de nacimiento) vive en `identity.personas`. La sección de consideraciones PII de ese documento debe reapuntar; los requisitos (cifrado at-rest/in-transit, logs sin PII, backups cifrados) no cambian, solo su sujeto.

---

### D2 — `designaciones.designaciones` como entidad de estado vigente, y snapshot en el pedido

**Opción elegida**: existen las dos cosas. Una tabla `designaciones` con `(persona_id, materia_id, cargo_id, horas, vigente_desde, vigente_hasta NULL, origen_pedido_id NULL)` que representa **qué es cierto hoy**, y una columna `snapshot` en `pedidos` que congela **qué era cierto cuando se envió**.

**Alternativas descartadas**:

- _Solo pedidos, estado derivado (event sourcing liviano)_ — toda lectura de "cargo actual del docente" se vuelve un fold sobre el historial de pedidos aprobados. Peor: los datos previos al sistema no tienen pedido de origen, así que el bootstrap inicial no tendría cómo representarse, y la futura importación desde Guaraní tampoco.
- _Designación vigente sin snapshot_ — un pedido aprobado en diciembre mostraría el cargo de diciembre en vez del de agosto. El trámite reescribiría su propio pasado. `audit.change_log` no lo salva: audita el pedido, no la designación que el pedido _leyó_.

**Por qué**: pedido y designación quieren propiedades opuestas y por eso no pueden ser la misma fila.

|                        | Pedido (trámite)                 | Designación (estado)    |
| ---------------------- | -------------------------------- | ----------------------- |
| Naturaleza             | Inmutable una vez enviado        | Mutable, tiene vigencia |
| Verdad que sostiene    | "Esto decía cuando se firmó"     | "Esto es cierto hoy"    |
| Vínculo con la persona | FK viva **+** snapshot congelado | FK viva                 |
| Vida                   | Termina en un estado terminal    | Vive hasta una Baja     |

La FK da integridad y consultabilidad; el snapshot da valor probatorio. Aprobar un pedido se vuelve una transacción legible: **Alta** inserta filas, **Baja** cierra `vigente_hasta`, **Cambio** cierra y reabre.

**`origen_pedido_id NULL` no es un detalle**: la pantalla `/docentes` (change `admin-docentes`) escribe designaciones a mano, y el circuito de aprobación escribe las mismas filas. Son dos caminos de escritura hacia una sola tabla. `NULL` significa "carga administrativa directa"; un valor significa "producto de un trámite aprobado". Sin esa columna, la trazabilidad no puede distinguirlos.

---

### D3 — Un pedido cubre exactamente una materia

**Opción elegida**: `pedidos.materia_id` único (la cátedra) + `horas` como columna del pedido. Se elimina la lista `asignaciones: AsignacionMateria[]` de 1..N y su tabla intermedia.

**Alternativas descartadas**:

- _Cátedra dueña explícita con N asignaciones de detalle_ — el pedido lleva `catedra_materia_id` para el ruteo y las asignaciones quedan como detalle. Conserva la UI actual pero mantiene dos conceptos de "materia del pedido" conviviendo.
- _Cátedra dueña + `carrera_id` desnormalizado_ — mismo esquema, con la carrera copiada para filtrar sin join. Choca con la regla de `data-model.md` que solo tolera `created_at` como denormalización.

**Por qué**: `identity.roles` ya comprometió que `jefe_catedra` tiene `scope = 'materia'` — o sea, **cátedra = materia**. Con N asignaciones, un pedido podía abarcar materias de dos carreras distintas y dos Coordinadores competían por él, sin que BR-designaciones-009 tuviera forma de resolverlo. No era hipotético: el catálogo mock incluye materias comunes (Análisis Matemático, Álgebra) que en UNLaM se dictan en varias carreras.

Con una sola materia, `carrera` se deriva de `materias.carrera_id` (un único Coordinador, determinista) y el guard de BR-013 pasa a ser una consulta directa:

```sql
EXISTS (SELECT 1 FROM identity.user_roles ur
         JOIN identity.roles r ON r.id = ur.role_id
        WHERE ur.user_id    = :actor
          AND r.code        = 'jefe_catedra'
          AND ur.materia_id = :materia_del_pedido
          AND ur.deleted_at IS NULL)
```

**Costo asumido y explícito**: contradice ~6 escenarios de la spec vigente `pedidos-designacion` ("Alta con múltiples materias", "Cambio permite agregar, quitar y cambiar materias", "No se puede dejar un pedido sin materias", etc.) y la UI que ya los implementa. La delta spec de este change los retira.

---

### D4 — BR-designaciones-001 literal, con la base de datos como autoridad

**Opción elegida**: un pedido por docente por período, **sin importar la cátedra**, implementado como índice único parcial **y** validación en el backend.

```sql
CREATE UNIQUE INDEX pedidos_uno_por_docente_periodo
    ON designaciones.pedidos (periodo_id, persona_id)
 WHERE estado NOT IN ('rechazado', 'cancelado');
```

**Alternativa descartada**: reformular BR-001 como "por docente **por cátedra** por período" (agregando `materia_id` al índice). Habría evitado que un Jefe de Cátedra bloquee a otro, pero enmienda una regla de negocio cuya fuente normativa el equipo no controla.

**Por qué las dos capas y no una**: no es redundancia, cumplen funciones distintas y solo una es correcta bajo concurrencia. El índice es la autoridad: dos requests simultáneos del mismo Jefe de Cátedra, uno falla siempre. El backend valida antes para producir el mensaje que exige la spec ("ya existe un pedido para ese docente en el período") y atrapa la violación del índice como fallback. Es el mismo patrón de defensa en profundidad que la spec vigente ya aplica a la dedicación solicitada.

**Dos detalles del `WHERE`**: los borradores se borran físicamente (la spec dice "el pedido deja de existir"), así que no hace falta contemplar soft-delete; y excluir los terminales permite volver a presentar tras un rechazo.

**Consecuencia que hay que asumir de frente**: combinada con D3, esta regla implica que un docente recibe **a lo sumo un trámite por período, en total**. Si dicta en dos cátedras, el primer Jefe de Cátedra que cargue bloquea al segundo. En el mock, 6 de los 9 docentes tienen 2 materias, así que el caso se va a dar. Efecto lateral bueno: `horas_investigacion` y `horas_externas` —que la spec define como del docente y no de la materia— dejan de ser ambiguas, porque hay a lo sumo un pedido vivo por docente y período.

---

### D5 — `roles` creables con `es_sistema`, permisos cerrados

**Opción elegida**: `identity.roles` pasa a `UUID`, gana `es_sistema BOOLEAN` y admite filas nuevas. Los 7 originales quedan protegidos por trigger: `code` y `scope` inmutables, `DELETE` denegado; `nombre` y `descripcion` sí editables (la pantalla `/roles` necesita "Editar rol"). `scope` sigue siendo `NOT NULL` para todos, así que el trigger `enforce_role_scope` de `user_roles` sigue funcionando sin cambios.

**Alternativas descartadas**:

- _Mantener el catálogo cerrado y hacer editables solo los permisos_ — la más segura, pero obliga a recortar `/roles` (quitar "crear rol" y la herencia D3 de `roles-membresia`).
- _Separar `roles` (circuito, 7 fijos) de `grupos` (permisos, creables)_ — cada concepto haría una sola cosa, pero agrega dos tablas de asignación a usuario y un modelo mental que hay que enseñarle al operador. Más superficie de la que el proyecto necesita.

**Por qué**: resuelve la contradicción entre lo que ya está en la base (7 roles, SMALLINT, con scope, `code = 'coordinador_carrera'`) y lo que la UI de `/roles` promete (6 roles, UUID, creables, sin scope, `nombre = "Coordinador"`). `code` sigue siendo `NOT NULL` para todos; un rol custom se lo genera como slug.

**Límite que la UI debe comunicar**: la máquina de estados mapea etapa → rol por `code` contra los 7 de sistema. **Un rol custom nunca participa del circuito de aprobación.** Si `/roles` no lo dice, un operador va a crear "Coordinador Suplente" esperando que apruebe pedidos, y no va a pasar nada. Es un requisito de UI, no de schema, pero nace de esta decisión.

**Por qué los permisos sí van cerrados**: un permiso es un `code` que algún check del backend lee. Un permiso creado desde la UI sin código que lo consuma no hace nada — sería exactamente lo que el invariante #7 prohíbe (fake UI). `identity.permisos` es catálogo fijo de 20; `identity.rol_permisos` es lo editable.

---

### D6 — `pedido_historial` es tabla de dominio, no una vista sobre `audit.change_log`

**Opción elegida**: tabla propia con `(pedido_id, accion, rol_id, etapa, comentario, fecha)`, que además hace `audit.attach`.

**Alternativa descartada**: derivar el historial de `audit.change_log`, que ya captura cada UPDATE con `old_row`/`new_row`/`changed_by`.

**Por qué** — cuatro razones, todas verificables contra el código actual:

1. **`por_rol` no es derivable.** `change_log` guarda `changed_by` (UUID de usuario), pero un usuario puede tener varios roles: en el mock, Gustavo Ruiz tiene `["Docente", "Jefe de Cátedra"]`. ¿Con cuál actuó? El log no lo sabe.
2. **El comentario es dato de negocio.** BR-designaciones-005 exige justificativo en el rechazo y comentario en la devolución, y la UI del detalle los muestra. No es metadata de auditoría.
3. **`changed_by` es nullable.** `data-model.md` advierte que queda `NULL` si el claim no parsea como UUID. Un registro con valor probatorio no puede tolerar eso.
4. **`change_log` está pensado para purgarse.** Tiene un índice BRIN sobre `changed_at` justamente para cortes de retención/partición. El historial de un trámite no se purga nunca.

Que el historial también haga `attach` no es contradictorio: responden preguntas distintas. El historial dice _qué pasó en el trámite_; el `change_log` dice _quién tocó qué fila_. Que alguien edite el historial a mano tiene que dejar rastro.

---

### D7 — `identity` y `audit` viven dentro de `ArsDocendi.Shared`, y el invariante #4 se enmienda

**Opción elegida**: los schemas `identity` y `audit` viven **dentro** de `ArsDocendi.Shared`, que ya hospeda `ICurrentUser`, `AuditDbConnectionInterceptor` e `IMigradorModulo`. Se agregan `IdentityDbContext`, `MigradorIdentity : IMigradorModulo` y la interfaz de consultas de autorización. **El invariante #4 se enmienda en el mismo PR** para admitir explícitamente esta I/O y solo esta.

**Alternativas descartadas**:

- _Un proyecto hermano `ArsDocendi.Shared.Identity`_ — dejaba el invariante #4 literalmente intacto al costo de un `.csproj` más. Se descartó: la separación era puramente nominal (todos los módulos referencian ambos proyectos igual) y compraba una pureza de papel, no una frontera real.
- _Un quinto módulo `Modules.Identity`_ — respetaría los invariantes al pie, pero convierte la autenticación en un módulo de negocio del que dependen los otros cuatro, y obliga a revisar el DAG entero.
- _Absorberlo en `Modules.Portal`_ — haría que Designaciones consulte a Portal en cada request para autorizar, y que auth deje de ser transversal.

**Por qué**: la auditoría transversal ya vive en Shared (`AuditDbConnectionInterceptor` setea `app.current_user_id` en cada conexión del pool). Poner el schema que esa infraestructura audita en otro proyecto partía un concern en dos por razones formales. Todos los módulos dependen de identity para autorizar; Shared es exactamente la capa de la que todos dependen.

**La enmienda al invariante #4** es un carve-out con cerco explícito, no un cheque en blanco. El texto nuevo admite **únicamente** la persistencia de `identity` y `audit`, y prohíbe cualquier otra I/O, estado mutable o lógica de dominio en Shared. Sin ese cerco, "Shared puede hacer I/O" se convierte en el vertedero del monolito. Se actualiza en cuatro lugares que enuncian la regla: `CLAUDE.md` (invariante #4), `openspec/config.yaml` (contexto de proyecto y regla de design, que se inyectan en la generación de artefactos) y `docs/quality/golden-principles.md`.

**Riesgo que introduce**: todos los módulos referencian Shared, así que **todos van a poder leer y escribir `identity` sin pasar por Contracts**. El invariante #1 no lo cubre porque no es una relación cross-module. La disciplina queda escrita como corolario del invariante #4 enmendado: los módulos **leen** identity para autorizar; escribir `personas`, `roles`, `permisos` o `rol_permisos` es exclusivo de la superficie de administración. A futuro conviene un test de arquitectura que falle si un `Modules.*` escribe sobre entidades de identity.

---

### D8 — Los `.sql` como fuente autorizada, aplicados por migraciones EF

**Opción elegida**: el DDL se autora en archivos `.sql` versionados bajo `database/`, se embeben como recurso en el assembly, y las migraciones EF los ejecutan con `migrationBuilder.Sql(...)`. El arranque `--migrate` que ya existe no cambia.

**Alternativas descartadas**:

- _Migraciones EF puras_ — es lo que documenta hoy `data-model.md`, y no alcanza.
- _Un runner de SQL aparte (DbUp/Evolve)_ — agrega una dependencia y un segundo mecanismo de migración conviviendo con `IMigradorModulo`.
- _Leer los `.sql` del filesystem en runtime_ — crea una dependencia de rutas en el deploy que la imagen de contenedor no garantiza.

**Por qué**: buena parte de lo diseñado EF Core no sabe generarlo.

| Construcción                                           | ¿EF la genera?   |
| ------------------------------------------------------ | ---------------- |
| `enforce_role_scope`, `log_change`, `attach` (plpgsql) | No               |
| `NULLS NOT DISTINCT` en `user_roles`                   | No               |
| `EXCLUDE` sobre solapamiento de vigencia               | No               |
| `SELECT audit.attach('schema.tabla')` por tabla        | No               |
| Índices parciales (`WHERE ...`)                        | Sí (`HasFilter`) |

Embeber como recurso da lo mejor de ambos: el SQL se escribe legible y diffeable, y se aplica determinísticamente por el mismo camino que el resto. `data-model.md` hay que corregirlo: hoy documenta migraciones EF puras.

---

### D9 — `cargos` como catálogo único, con `orden`

**Opción elegida**: una tabla `cargos` con `code`, `nombre`, `orden` e `is_active`, que reemplaza los tres vocabularios que hoy conviven en el código.

```
designaciones/types.ts    4:  Titular · Adjunto · JTP · Ayudante
docentes/mock             6:  Profesor Titular · Profesor Asociado ·
                              Profesor Adjunto · JTP ·
                              Ayudante de 1ª · Ayudante de 2ª
admin-docentes D6 (texto) 7:  …+ "Docente Autorizado"
                              (ni siquiera está en el array del código)
```

**Por qué `orden`**: la spec vigente deja fuera de alcance la jerarquía de cargos ("solo se puede subir", tema C) pero la dedicación **sí** tiene jerarquía hoy y se resuelve por índice en un array del frontend. Una columna `orden` permite implementar el tema C después sin migración. No se usa en este change.

**Cuál de las tres listas es la correcta es Open Question para el cliente** — es nomenclatura institucional del convenio colectivo, no una decisión de diseño.

## Risks / Trade-offs

- **D3 + D4 juntas bloquean al segundo Jefe de Cátedra.** Un docente que dicta en dos cátedras solo admite un trámite por período; el primero en cargar gana. → Mitigación: la UI tiene que explicar el bloqueo **sin filtrar datos de una cátedra ajena** (el segundo JC no puede ver el pedido que lo bloquea). Requiere design spec (invariante #12). Si el cliente confirma que la regla debía ser por cátedra, es agregar `materia_id` al índice único.
- **D3 rompe UI ya implementada y ~6 escenarios de spec vigente.** → Mitigación: la delta spec de este change los retira explícitamente, y las tasks incluyen la migración del frontend y sus tests. No queda código muerto ni escenarios huérfanos.
- **D7 abre `identity` a escritura desde cualquier módulo.** → Mitigación: la disciplina queda escrita como corolario del invariante #4 enmendado y en `module-anatomy.md`, y se verifica en `/pr-review`. A futuro, un test de arquitectura que falle si un `Modules.*` escribe sobre entidades de identity.
- **La enmienda al invariante #4 puede erosionarse.** Una vez que Shared admite I/O, la presión para meterle "una cosita más" es permanente. → Mitigación: el texto enmendado enumera la excepción y prohíbe explícitamente todo lo demás. `/pr-review` y `/architecture-drift-check` deben tratar cualquier I/O nueva en Shared como violación, no como precedente.
- **D1 mueve la PII de `portal` a `identity`.** La sección de consideraciones PII de `data-model.md` apunta al módulo equivocado tras este change. → Mitigación: actualizarla en el mismo PR (invariante #6). Los requisitos técnicos no cambian, solo su sujeto.
- **D5 permite roles que no hacen nada en el circuito.** Un rol custom nunca aprueba pedidos. → Mitigación: la UI de `/roles` debe declararlo al crear. Sin eso, es fake UI de hecho aunque no de forma.
- **D8 depende de que los `.sql` embebidos se apliquen en orden estable.** Un archivo mal numerado rompe el arranque `--migrate`. → Mitigación: el orden lo fija la migración EF que los invoca, no el nombre del archivo; y `--migrate` es idempotente (`Database.Migrate()`).
- **El catálogo de cargos queda con una lista provisional** hasta que responda el cliente. → Mitigación: `cargos` es una tabla, no un enum; corregir la lista es un `INSERT`/`UPDATE`, no una migración de schema.

## Migration Plan

No hay datos productivos: `identity` nunca se desplegó a producción y los otros schemas están vacíos. Eso hace que todo el trabajo sea reescritura de archivos, no migración de datos.

1. Enmendar el invariante #4 en `CLAUDE.md`, `openspec/config.yaml` y `docs/quality/golden-principles.md`. Agregar `IdentityDbContext` y `MigradorIdentity` a `ArsDocendi.Shared`, registrarlos en la DI. Verificar que el DAG siga sin ciclos.
2. Reescribir `002_identity_roles.sql` (UUID + `es_sistema` + trigger de protección) y `005_identity_user_roles.sql` (FK a UUID). Agregar `personas`, `permisos`, `rol_permisos` y `users.persona_id`.
3. Agregar `database/designaciones/*.sql` completo, cada archivo terminando en `SELECT audit.attach(...)`.
4. Migraciones EF que embeben y ejecutan los `.sql`. Validar con `--migrate` sobre una base limpia.
5. Migrar el frontend a un pedido por materia (D3) y alinear los mocks de docentes y roles.
6. Actualizar `data-model.md`, `dependency-graph.md`, `module-anatomy.md`, `docs/business-rules/designaciones.md` y el design spec — mismo PR (invariante #6).

**Rollback**: revertir el PR y recrear la base con `--migrate`. El único punto irreversible sería aplicar el cambio de `identity.roles` sobre una base con datos reales; mientras no haya deploy productivo de identity, no aplica.

## Open Questions

Ninguna bloquea el schema: las tres son ajustes de una línea sobre lo diseñado.

1. **Catálogo de cargos (D9)** — ¿4, 6 o 7 valores? Es nomenclatura del convenio colectivo; la define el cliente.
2. **BR-designaciones-001 (D4)** — su fuente normativa figura como _"pendiente de confirmación con el cliente"_ en `docs/business-rules/designaciones.md`. Se implementa la versión literal. Aflojarla a "por cátedra" es agregar `materia_id` al índice único.
3. **Rechazo y reintento (D4)** — el índice excluye `rechazado`, o sea que hoy se puede volver a presentar tras un rechazo. Si un rechazo debe cerrar el período para ese docente, se saca `'rechazado'` del `WHERE`.
4. **`portal.docentes` tras D1** — con `personas` como entidad canónica, ¿qué le queda a Portal? Presumiblemente áreas de experticia y disponibilidad horaria (lo que `docs/product/` describe como autogestión del docente). Se resuelve cuando Portal tenga frontend.
