## Why

El frontend de Designaciones y el de administración (usuarios, docentes, roles, membresía) están completos y funcionando **enteramente sobre mocks en memoria** (`features/*/mock/mockStore.ts`, `pedidosStore.ts` hidratado desde `localStorage`). Los cuatro `DbContext` del backend existen pero no declaran una sola entidad: los schemas `designaciones`, `aulas`, `portal` y `tareas` están vacíos. Lo único que hay en la base es `identity` + `audit`, escritos a mano en `database/*.sql`.

Sin modelo de datos no hay backend, y sin backend el sistema no puede quedar en producción. Este change define el schema de las dos superficies que el frontend ya ejercita —identity y designaciones— derivándolo de lo que las pantallas realmente necesitan, y resuelve cinco contradicciones que hoy están latentes entre el código, las specs vigentes y los changes en vuelo.

`portal`, `aulas` y `tareas` quedan **fuera de alcance a propósito**: sus `types.ts` están vacíos (una línea cada uno) y no hay pantalla que valide un modelo. Diseñarlos ahora sería especulación.

## What Changes

### Identidad de la persona, separada de la cuenta

- Nueva tabla `identity.personas`: la entidad canónica de una persona (documento, CUIL, legajo, nombre, apellido, fecha de nacimiento, teléfono). Existe **con o sin cuenta de Azure AD**.
- `identity.users` pasa a referenciar `persona_id` (nullable) y queda acotada a autenticación (azure_oid, upn, display_name).
- Habilita el caso que hoy no se puede modelar: un pedido de novedad "Alta" refiere a un docente que nunca se logueó y todavía no tiene legajo (BR-designaciones-018). `legajo` es nullable en `personas`.
- Cumple lo que el change `admin-docentes` ya prometió en su D10: _"en producción, ambas features llamarán al mismo endpoint `/api/identity/personas`"_.

### Roles creables con scope, y permisos

- **BREAKING** — `identity.roles` pasa de `SMALLINT` a `UUID` y deja de ser un catálogo cerrado: admite filas nuevas creadas por Secretaría. Se agrega `es_sistema BOOLEAN`; los 7 roles originales quedan protegidos por trigger (`code` y `scope` inmutables, `DELETE` denegado). Reescribe `002_identity_roles.sql` y el FK de `005_identity_user_roles.sql`. No hay datos productivos: es reescribir archivos, no migrar.
- Un rol con `es_sistema = FALSE` **nunca participa del circuito de aprobación**: la máquina de estados mapea etapa → rol por `code` contra los 7 de sistema. La UI de `/roles` debe comunicarlo.
- Nuevas tablas `identity.permisos` (catálogo cerrado de 20, `code` + nombre + descripción) y `identity.rol_permisos` (editable). Los permisos **no** son creables desde la UI: un permiso sin un check que lo lea en el código sería fake UI (invariante #7).
- Resuelve la contradicción entre `identity.roles` (7 fijos, SMALLINT, con `scope`) y el mock de `/roles` (6 creables, UUID, sin `scope`, nombres distintos).

### Schema de designaciones

- Nuevas tablas: `periodos`, `pedidos`, `pedido_adjuntos`, `pedido_historial`, `designaciones` y el catálogo `cargos`.
- `designaciones.designaciones` es **la entidad que faltaba**: el estado vigente `(persona, materia, cargo, horas)` con `vigente_desde` / `vigente_hasta`. Hoy la inventa el mock (`DocenteExistente.materiasActuales`, `cargoActual`, `dedicacionActual`). Aprobar un pedido escribe acá: Alta inserta, Baja cierra `vigente_hasta`, Cambio cierra y reabre.
- `origen_pedido_id` (nullable) distingue una designación producida por el circuito de aprobación de una cargada a mano desde `/docentes`. La pantalla `/docentes` y el circuito escriben la misma tabla por dos caminos; la trazabilidad exige poder diferenciarlos.
- `pedidos.snapshot` congela cargo, dedicación y horas vigentes **al momento de enviar**. Un trámite aprobado en Decanato tres meses después tiene que seguir diciendo qué cargo tenía el docente el día que se cargó, o el documento miente.
- `pedido_historial` es tabla de dominio, **no** derivada de `audit.change_log`: `por_rol` no es derivable (un usuario puede tener varios roles), el comentario/justificativo es dato de negocio que exige BR-designaciones-005, y `change_log` está pensado para purgarse.
- Nuevo catálogo `cargos` que unifica los tres vocabularios incompatibles que hoy conviven (4 valores en `designaciones/types.ts`, 6 en `docentes/mock/mockStore.ts`, 7 mencionados en el texto de `admin-docentes` D6). **Cuál lista es la correcta queda como Open Question para el cliente.**

### Un pedido cubre exactamente una materia

- **BREAKING** — El pedido pasa a llevar un único `materia_id` (la cátedra) en vez de una lista `asignaciones: AsignacionMateria[]` de 1..N. Se elimina la tabla intermedia y las horas suben al pedido.
- Resuelve una ambigüedad de ruteo real: con N materias, un pedido podía abarcar materias de dos carreras distintas y dos Coordinadores competían por él (BR-designaciones-009). Con una sola materia, `carrera` se deriva de `materias.carrera_id` y el guard de BR-013 es una consulta directa contra `user_roles.materia_id`.
- Contradice ~6 escenarios de la spec vigente `pedidos-designacion` y la UI ya implementada. El costo está aceptado y explicitado abajo.
- BR-designaciones-001 se mantiene **literal**: un pedido por docente por período, sin importar la cátedra. Se implementa como índice único parcial en Postgres (autoridad, sobrevive a la concurrencia) **más** validación en el backend (mensaje de error del spec). Consecuencia asumida: si un docente dicta en dos cátedras, el primer Jefe de Cátedra que cargue bloquea al segundo.

### Ownership e infraestructura de migraciones

- Los schemas `identity` y `audit` pasan a vivir **dentro de `ArsDocendi.Shared`**, que ya hospeda `ICurrentUser`, `AuditDbConnectionInterceptor` e `IMigradorModulo`. Hoy no tienen dueño: hay 4 módulos y 6 schemas, y nadie los migra.
- **BREAKING (gobernanza)** — El invariante #4 se enmienda en el mismo PR. Hoy dice _"`ArsDocendi.Shared`: solo utilidades puras, sin I/O ni estado mutable"_, y un `DbContext` es I/O. El texto nuevo admite **únicamente** la persistencia de `identity` y `audit` y prohíbe explícitamente cualquier otra I/O, estado mutable o lógica de dominio en Shared — un carve-out con cerco, no un cheque en blanco. Se actualiza en los cuatro lugares que enuncian la regla: `CLAUDE.md`, `openspec/config.yaml` (contexto de proyecto **y** regla de design, que se inyectan en la generación de artefactos) y `docs/quality/golden-principles.md`.
- Los archivos `database/*.sql` quedan como **fuente autorizada** del DDL, embebidos como recurso en el assembly y aplicados por migraciones EF vía `migrationBuilder.Sql(...)`. EF Core no puede generar el plpgsql (`enforce_role_scope`, `log_change`, `attach`), ni `NULLS NOT DISTINCT`, ni las constraints `EXCLUDE` de vigencia.

## Capabilities

### New Capabilities

- `persistencia-identity`: schema `identity` — personas, users, roles con `es_sistema` y scope, permisos, user_roles, rol_permisos. Incluye el ownership del schema en `ArsDocendi.Shared` y las reglas de protección del catálogo de sistema.
- `persistencia-designaciones`: schema `designaciones` — periodos, pedidos, adjuntos, historial, designaciones vigentes y el catálogo de cargos. Incluye BR-designaciones-001 como índice único parcial y el snapshot del pedido.
- `migraciones-sql-versionado`: los `.sql` de `database/` como fuente autorizada, embebidos y aplicados por migraciones EF; contrato de `IMigradorModulo` para identity/audit.

### Modified Capabilities

- `pedidos-designacion`: un pedido pasa a cubrir **exactamente una materia**. Se eliminan los requirements y escenarios que asumen una lista de 1..N asignaciones (agregar/quitar/cambiar materia, "Alta con múltiples materias", "Cambio precarga el listado editable"). La materia y sus horas pasan a ser campos únicos del pedido. **BREAKING.**

## Impact

### Base de datos

- `database/identity/002_identity_roles.sql` y `005_identity_user_roles.sql` se reescriben (SMALLINT → UUID, `es_sistema`, trigger de protección).
- Nuevos: `database/identity/00X_identity_personas.sql`, `00X_identity_permisos.sql`, `00X_identity_rol_permisos.sql`; `database/designaciones/*.sql` completo.
- `identity.users` gana `persona_id`. `audit.change_log` no cambia.

### Backend

- `ArsDocendi.Shared` gana `IdentityDbContext` (schemas `identity` + `audit`), `MigradorIdentity : IMigradorModulo` y la interfaz de consultas de autorización. No se crean proyectos nuevos.
- `Modules.Designaciones`: primeras entidades reales en `DesignacionesDbContext`, repositorios y servicios del circuito.
- Cross-module: ninguno de los otros 3 módulos cambia. **Riesgo nuevo a vigilar**: todos los módulos referencian Shared, así que todos podrán leer y escribir identity sin pasar por Contracts. La disciplina —leer para autorizar, escribir solo desde la superficie de administración— no la cubre el invariante #1 porque no es cross-module; queda escrita como corolario del invariante #4 enmendado.

### Frontend

- `features/designaciones/types.ts`: `PedidoDesignacion.asignaciones` se reemplaza por `materiaId` + `horas`. Impacta el form de alta/edición, el panel de datos actuales, `pedidoValidacion.ts`, `detalleAdapters.ts` y sus tests.
- `features/docentes` y `features/roles`: los mocks se alinean al schema real (roles con `scope` y `es_sistema`, cargos desde un catálogo único).

### Gobernanza (mismo PR)

- `CLAUDE.md`: invariante #4 enmendado.
- `openspec/config.yaml`: el contexto de proyecto y la regla de design que inyecta en cada artefacto generado. Si no se actualiza, toda propuesta futura arrastra la regla vieja.
- `docs/quality/golden-principles.md`: sección "Contracts y Shared".

### Documentación (invariante #6 — mismo PR)

- `docs/architecture/data-model.md`: entidades por módulo, ownership de identity/audit en Shared, y la corrección de la sección de migraciones (hoy documenta migraciones EF puras).
- `docs/architecture/dependency-graph.md`: `ArsDocendi.Shared` pasa a tener dependencia de EF Core / Npgsql. Verificar que el DAG siga sin ciclos.
- `docs/architecture/module-anatomy.md` y `docs/architecture/stack.md`: la descripción de Shared como "utilidades puras transversales" queda incompleta.
- `docs/business-rules/designaciones.md`: BR-001 gana su implementación en base de datos; BR-018 se ata a `personas.legajo` nullable.
- `docs/product/designs/`: invariante #12 — la UI necesita explicarle al segundo Jefe de Cátedra que otro ya cargó un trámite sobre ese docente, **sin filtrarle datos de una cátedra que no le corresponde ver**.

### Rollback

Todo el trabajo es aditivo sobre una base sin datos productivos. Rollback = revertir el PR y recrear la base con `--migrate`. El único punto irreversible sería aplicar el cambio de `identity.roles` sobre una base con datos reales; mientras no haya deploy productivo de identity, no aplica.

## Open Questions

Tres cosas necesitan al cliente, no al equipo. Ninguna bloquea el schema: las tres son ajustes de una línea sobre lo diseñado.

1. **Catálogo de cargos** — ¿4, 6 o 7 valores? Hoy hay tres listas incompatibles en el código.
2. **BR-designaciones-001** — su fuente normativa figura como _"pendiente de confirmación con el cliente"_. Se implementa la versión literal (un pedido por docente por período). Aflojarla a "por cátedra" es cambiar una columna del índice único.
3. **Rechazo y reintento** — el índice único excluye `rechazado` y `cancelado`, o sea que hoy se puede volver a presentar tras un rechazo. Si un rechazo debe cerrar el período para ese docente, se saca `'rechazado'` del `WHERE`.
