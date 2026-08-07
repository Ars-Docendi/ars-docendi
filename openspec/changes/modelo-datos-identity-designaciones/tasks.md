> **Infraestructura de tests: diferida a un change aparte.** El proyecto no tiene
> `backend/tests/`, y casi todo lo verificable acá es comportamiento de PostgreSQL
> (triggers, índices parciales, `NULLS NOT DISTINCT`, `EXCLUDE`, concurrencia), que
> no se testea sin una base real. Se decidió validar primero el schema a mano
> —`--migrate` contra un Postgres limpio— y recién después montar la infra de tests
> con los ~30 tests que quedan marcados abajo. Las tareas de test permanecen sin
> tildar y se migran al change nuevo; no cuentan como completadas acá.

## 1. Enmienda del invariante #4

> Va primero: `openspec/config.yaml` inyecta la regla vieja en cada artefacto que se genere, así que si no se corrige antes, el trabajo posterior la arrastra.

- [x] 1.1 `CLAUDE.md`: enmendar el invariante #4 con la excepción acotada a `identity`/`audit` y el corolario de lectura/escritura
- [x] 1.2 `openspec/config.yaml`: actualizar el contexto de proyecto y la regla de design
- [x] 1.3 `docs/quality/golden-principles.md`: actualizar la sección "Contracts y Shared"
- [x] 1.4 Verificar que no quede ninguna copia de la redacción anterior en docs vivos (los changes archivados quedan como registro histórico y no se tocan). Las descripciones de Shared en `module-anatomy.md` y `stack.md` no enuncian la regla — se actualizan en 11.5

## 2. identity y audit dentro de ArsDocendi.Shared

- [x] 2.1 Agregar a `ArsDocendi.Shared` las dependencias de EF Core / Npgsql
- [x] 2.2 Crear `IdentityDbContext` apuntando a los schemas `identity` y `audit`, con `MigrationsHistoryTable` propio
- [x] 2.3 Implementar `MigradorIdentity : IMigradorModulo` y registrarlo en `AddArsDocendiShared()` para que `--migrate` lo resuelva
- [x] 2.4 Verificar que Shared no incorpore ninguna otra I/O, estado mutable ni lógica de dominio más allá de esta persistencia
- [x] 2.5 Verificar que el grafo de dependencias siga siendo un DAG (Shared suma dependencias de paquete, no de proyecto: el DAG entre proyectos no cambia)

## 3. Infraestructura de migraciones SQL

- [x] 3.1 Configurar los `.sql` de `database/` como recursos embebidos del assembly correspondiente
- [x] 3.2 Escribir el helper que lee un recurso embebido y lo ejecuta vía `migrationBuilder.Sql(...)` (`ArsDocendi.Shared/Persistencia/RecursosSql.cs`; nombres lógicos verificados contra los assemblies compilados)
- [x] 3.3 Verificar que la migración no dependa de rutas del filesystem — confirmado por el deploy de `pr-23`: el runtime nunca tocó el disco, falló por recursos embebidos faltantes
- [x] 3.6 **Corregir el build context de la imagen del backend.** `docker build … backend` dejaba `database/` fuera del contexto, el glob del `.csproj` no matcheaba nada y MSBuild compilaba un assembly sin recursos **sin error**. El deploy de `pr-23` explotó con "0 recursos disponibles". Contexto movido a la raíz (`-f backend/Dockerfile .`) en los 3 workflows + `.dockerignore` nuevo
- [x] 3.7 **Agregar el guard que faltaba**: target `ValidarSqlEmbebido` en ambos `.csproj`, que falla el build si el glob de `.sql` viene vacío. Verificado escondiendo `database/`: el build falla con el mensaje explícito. Este era el defecto real; la ruta mal apuntada era el síntoma
- [ ] 3.4 Test: `--migrate` sobre base limpia crea todos los schemas y termina con exit 0 sin levantar el web server
- [ ] 3.5 Test: reejecutar `--migrate` sobre una base ya migrada es idempotente y termina con exit 0

## 4. Schema identity — personas y cuentas

- [x] 4.1 Crear `database/identity/006_identity_personas.sql`: documento UNIQUE, CUIL, legajo nullable UNIQUE, nombre, apellido, fecha de nacimiento, teléfono, `created_at`, cerrando con `audit.attach`
- [x] 4.2 Agregar `persona_id UUID NULL` con FK a `identity.personas` en `identity.users`
- [ ] 4.3 Test: se persiste una persona sin cuenta y sin legajo (caso Alta)
- [ ] 4.4 Test: el primer login vincula `users.persona_id` a la persona existente sin duplicarla
- [ ] 4.5 Test: documento duplicado es rechazado por la base

## 5. Schema identity — roles, permisos y membresía

- [x] 5.1 Reescribir `002_identity_roles.sql`: `id` UUID, `es_sistema BOOLEAN`, `scope` NOT NULL, seed de los 7 roles de sistema
- [x] 5.2 Escribir el trigger de protección: `code` y `scope` inmutables y `DELETE` denegado cuando `es_sistema = TRUE`; `nombre` y `descripcion` editables (además impide promover un rol común a rol de sistema)
- [x] 5.3 Reescribir `005_identity_user_roles.sql` para el FK a `roles.id` UUID, conservando `enforce_role_scope`, el índice `NULLS NOT DISTINCT` y los índices parciales
- [x] 5.4 Crear `database/identity/007_identity_permisos.sql` con el catálogo cerrado de 20 permisos
- [x] 5.5 Crear `database/identity/008_identity_rol_permisos.sql` con PK compuesta, cerrando con `audit.attach`. Matriz inicial derivada de CLAUDE.md, NO del mock (que asigna aprobación de Decanato al rol Docente) — provisional, pendiente de confirmar con el cliente
- [ ] 5.6 Test: crear un rol nuevo con scope se persiste con `es_sistema = FALSE`
- [ ] 5.7 Test: modificar `code` o `scope` de un rol de sistema es rechazado; editar su `nombre` es aceptado
- [ ] 5.8 Test: borrar un rol de sistema es denegado
- [ ] 5.9 Test: crear un rol sin scope o con scope inválido es rechazado
- [ ] 5.10 Test: `enforce_role_scope` aplica igual a un rol creado por el operador que a uno de sistema
- [ ] 5.11 Test: revocar y volver a otorgar la misma asignación de rol es aceptado
- [ ] 5.12 Test: otorgar dos veces el mismo permiso al mismo rol es rechazado

## 6. Schema designaciones — catálogos y períodos

- [x] 6.1 Crear `database/designaciones/001_designaciones_cargos.sql`: `codigo`, `nombre`, `abreviatura`, `orden`, `activo`, con la lista provisional de 6 y un comentario que referencie la Open Question del cliente
- [x] 6.2 Crear `database/designaciones/002_designaciones_periodos.sql`: ventana de carga, rango de impacto, `activo`
- [x] 6.3 Agregar el índice único parcial que garantiza a lo sumo un período activo
- [ ] 6.4 Test: activar un segundo período sin desactivar el primero es rechazado por la base
- [ ] 6.5 Test: un cargo fuera del catálogo es rechazado en pedidos y designaciones

## 7. Schema designaciones — pedidos

- [x] 7.1 Crear `database/designaciones/003_designaciones_pedidos.sql`: `numero` UNIQUE, `periodo_id`, `persona_id`, `materia_id`, `horas`, novedad, estado, `prioritario`, cargo/dedicación solicitados, justificación, tipo de baja y su detalle, horas de investigación y externas, `etapa_retorno`, `propietario_actual`, `snapshot`
- [x] 7.2 Agregar el índice único parcial `(periodo_id, persona_id) WHERE estado NOT IN ('rechazado','cancelado')` [BR-designaciones-001]
- [x] 7.3 Crear `004_designaciones_pedido_adjuntos.sql` y `005_designaciones_pedido_historial.sql`, ambos cerrando con `audit.attach`
- [x] 7.4 Verificar que ninguna tabla del schema declare `created_by`, `updated_at`, `updated_by` ni `deleted_by` (los únicos hits en `database/` son la firma de `audit.row_history`, que es la función que los reemplaza)
- [ ] 7.5 Test: segundo pedido para el mismo docente y período es rechazado, incluso desde otra cátedra
- [ ] 7.6 Test: dos escrituras concurrentes hacen fallar exactamente una por el índice único
- [ ] 7.7 Test: tras un pedido `rechazado` se puede crear uno nuevo en el mismo período

## 8. Schema designaciones — designaciones vigentes

- [x] 8.1 Crear `database/designaciones/006_designaciones_designaciones.sql`: `persona_id`, `materia_id`, `cargo_id`, `horas`, `vigente_desde`, `vigente_hasta` nullable, `origen_pedido_id` nullable
- [x] 8.2 Agregar la constraint que impide dos designaciones vigentes simultáneas para la misma persona y materia (`EXCLUDE USING gist` sobre `daterange`, más fuerte que "a lo sumo una abierta": también rechaza dos cerradas solapadas)
- [ ] 8.3 Test: una designación cerrada no bloquea la apertura de una nueva sobre la misma materia
- [ ] 8.4 Test: abrir una segunda designación vigente sobre la misma materia es rechazado
- [ ] 8.5 Test: `origen_pedido_id` distingue una designación del circuito de una carga administrativa

## 9. Backend — capa de acceso e integridad

- [x] 9.1 Declarar las entidades de `identity` y `audit` en `IdentityDbContext`, coherentes con el SQL (sin recrear lo definido en SQL crudo): todas con `ExcludeFromMigrations()`
- [x] 9.2 Declarar las entidades de designaciones en `DesignacionesDbContext`
- [x] 9.3 Test: no queda ninguna migración EF pendiente que intente recrear estructuras ya definidas en SQL — `dotnet ef migrations has-pending-model-changes` responde "No changes" para ambos contextos
- [x] 9.4 Exponer desde `ArsDocendi.Shared` la interfaz de consultas de autorización (persona, roles vigentes, rol sobre materia): `IConsultasIdentity`, sólo lectura por diseño
- [ ] 9.5 Implementar repositorios de designaciones respetando Controller → Service → Repository
- [ ] 9.6 Validar en el backend la unicidad de BR-designaciones-001 antes de escribir, y traducir la violación del índice al mismo mensaje
- [ ] 9.7 Implementar el guard de Jefe de Cátedra sobre la materia del pedido, contra `user_roles` vivos
- [ ] 9.8 Implementar el snapshot: congelar cargo, dedicación, materia y horas al enviar a revisión, y no recalcularlo después
- [ ] 9.9 Implementar la persistencia del historial con el rol concreto con el que actuó el actor
- [ ] 9.10 Implementar la materialización de la aprobación sobre designaciones vigentes (Alta abre, Baja cierra, Cambio cierra y abre, Sin novedad no toca) en una única transacción
- [ ] 9.11 Test: un fallo parcial en la materialización de un Cambio revierte el cierre de la designación anterior
- [ ] 9.12 Test: un rol con `es_sistema = FALSE` no puede aceptar, rechazar ni devolver en ninguna etapa
- [ ] 9.13 Test: ningún `Modules.*` escribe sobre `personas`, `roles`, `permisos` ni `rol_permisos`
- [ ] 9.14 Verificar que el `AuditDbConnectionInterceptor` propague `app.current_user_id` en las conexiones del contexto nuevo, de modo que `changed_by` no quede NULL

## 10. Frontend — un pedido por materia

- [ ] 10.1 Reemplazar `PedidoDesignacion.asignaciones` por `materiaId` + `horas` en `features/designaciones/types.ts`
- [ ] 10.2 Adaptar el form de alta/edición: materia editable en Alta, fijada en Cambio, de solo lectura en Baja y Sin novedad; sin acciones de agregar/quitar
- [ ] 10.3 Adaptar el panel de datos actuales para mostrar la transición de la carga horaria de una sola materia
- [ ] 10.4 Actualizar `pedidoValidacion.ts`, `detalleAdapters.ts`, `pedidosSeed.ts` y `pedidosStore.ts`
- [ ] 10.5 Actualizar los tests afectados: `pedidoValidacion.test.ts`, `detalleAdapters.test.ts`, `pedidosApi.test.ts`, `tableroRevisionModelo.test.ts`
- [ ] 10.6 Implementar el mensaje de bloqueo por duplicado sin exponer cátedra, contenido ni autor del pedido bloqueante
- [ ] 10.7 Alinear el mock de `features/roles` al schema real (scope, `es_sistema`) y agregar la advertencia al crear un rol de que no participa del circuito de aprobación
- [ ] 10.8 Alinear el mock de `features/docentes` al catálogo único de cargos
- [ ] 10.9 Verificar que no quede código muerto del modelo de 1..N asignaciones

## 11. Documentación (invariante #6 — mismo PR)

- [x] 11.1 `docs/architecture/data-model.md`: entidades por módulo de identity y designaciones
- [x] 11.2 `docs/architecture/data-model.md`: reapuntar la sección de consideraciones PII de Portal a `identity.personas`
- [x] 11.3 `docs/architecture/data-model.md`: corregir la sección de migraciones (hoy documenta migraciones EF puras)
- [x] 11.4 `docs/architecture/dependency-graph.md`: reflejar que `ArsDocendi.Shared` ahora depende de EF Core / Npgsql, verificando el DAG
- [x] 11.5 `docs/architecture/module-anatomy.md` y `docs/architecture/stack.md`: actualizar la descripción de Shared, que hoy dice solo "utilidades puras transversales"
- [x] 11.6 `docs/architecture/module-anatomy.md`: documentar la disciplina de que los módulos leen identity para autorizar pero no escriben personas, roles ni permisos
- [x] 11.7 `docs/architecture/domains/designaciones.md`: modelo de pedido, designación vigente y trazabilidad
- [x] 11.8 `docs/business-rules/designaciones.md`: BR-001 con su implementación en base de datos; BR-018 atado a `personas.legajo` nullable. El mapping a test queda pendiente del change de infraestructura de tests (TD-004)
- [x] 11.9 `docs/business-rules/designaciones.md`: registrar en Open Questions el catálogo de cargos, el alcance de BR-001, el reintento tras rechazo y la matriz inicial de permisos
- [ ] 11.10 `docs/product/designs/`: design spec del bloqueo por duplicado entre cátedras (invariante #12)
- [x] 11.11 `CLAUDE.md`: actualizar el comentario de `ArsDocendi.Shared` en el árbol de estructura del repositorio
- [x] 11.12 `docs/quality/tech-debt.md`: registrar TD-004 (sin infraestructura de tests) y TD-005 (vocabulario de columnas mixto); actualizar TD-002 con el catálogo único de cargos

## 12. Cierre

- [ ] 12.1 Correr `openspec validate --strict` sobre specs y changes
- [ ] 12.2 Correr el build y la suite completa de backend y frontend
- [ ] 12.3 Verificar `--migrate` sobre una base limpia y sobre una ya migrada
- [ ] 12.4 Correr `/architecture-drift-check` y confirmar que no reporta drift nuevo
- [ ] 12.5 Correr `/evaluate` contra las specs de este change y actualizar `docs/quality/scorecard.md`
