## 1. Migración de schema (identity)

- [x] 1.1 Crear `database/identity/013_identity_modelo_academico.sql` con las tablas nuevas `planes` y `materias_plan`, y la modificación de `materias` (claves, FKs, unicidades y banderas según la spec `modelo-academico`).
- [x] 1.2 Agregar la validación previa de la fase 1: abortar si un mismo código de materia tiene nombres distintos entre carreras.
- [x] 1.3 No modificar `identity.carreras`: `planes.carrera_id` la referencia directamente.
- [x] 1.4 Crear un plan placeholder `UNICO` (vigente) por carrera y marcarlo para reemplazo.
- [x] 1.5 Consolidar `materias` a canónica: borrar los datos cargados por carrera y recargarlos desde el listado de Guaraní, uno por código.
- [x] 1.6 `database/identity/016_identity_docente_carrera_directa.sql`: agregar `carrera_id` a `identity.user_roles` también para Docente (ya existía para Coordinador), con `materia_id` + `carrera_id` juntos.
- [x] 1.7 `database/identity/015_identity_planes_activo.sql`: agregar `planes.activo` para que un plan activo no vigente siga siendo válido en el catálogo informativo.
- [x] 1.8 Modificar `identity.materias`: quitar `carrera_id` y reemplazar `UNIQUE(carrera_id, code)` por `UNIQUE(code)`.
- [x] 1.9 Implementar el `Down` de cada migración (revierte su propio paso; el de `013` restaura `carrera_id` en `materias` desde `materias_plan → planes → carreras`).
- [x] 1.10 `enforce_role_scope` por código de rol: `docente` exige `materia_id` y `carrera_id` juntos; `jefe_catedra` exige `materia_id` y deja `carrera_id` en NULL; `coordinador_carrera` exige `carrera_id` y deja `materia_id` en NULL; roles globales no admiten ninguno.

## 2. Entidades y repositorios

- [x] 2.1 Actualizar `IdentityDbContext` y las entidades de identity (`Plan`, `Materia`, `MateriaPlan`; `Carrera` ya existe) con sus mapeos.
- [x] 2.2 `IConsultasIdentity.ListarMateriasAsync` (canónicas) y `ListarMateriasPlanAsync` (catálogo informativo materia–plan, para mostrar y validar combinaciones).
- [x] 2.3 `ConsultasIdentity`: el ámbito de un usuario se lee directo de `user_roles` (materia_id/carrera_id según el rol), sin pasar por un plan.

## 3. Designaciones

- [x] 3.1 `Modules.Designaciones`: `pedidos` y `designaciones` referencian `materia_id` (canónica) + `carrera_id` directos.
- [x] 3.2 `ServicioPedidos`: `AlcancePedido` se construye sincrónicamente desde los campos del pedido, sin lookup de plan.
- [x] 3.3 `ServicioCatalogosDesignaciones` y `ServicioPedidosApi`: el catálogo de materias se arma como pares materia–carrera deduplicados (una materia dictada en dos carreras aparece dos veces), sin duplicar la materia canónica en el selector.
- [x] 3.4 BR-designaciones-009 verificado: el Coordinador ve exactamente los pedidos de su carrera (`RevisionPedidosTests`).

## 4. Administración (API y frontend)

- [x] 4.1 `CatalogosDocentesDto` y `CatalogosUsuariosDto` devuelven materias canónicas y el catálogo informativo materia–plan (pares materia–carrera) para acotar la elección de carrera del Docente.
- [x] 4.2 `MembresiasSelector` y `AsignacionesSelector` piden materia y carrera por separado para el Docente, acotando la carrera a las que de verdad dictan la materia elegida.
- [x] 4.3 DTOs, validación de membresías y mensajes de error según la spec `administracion-membresias-ambito`.

## 5. Datos y generador

- [x] 5.1 Actualizar `infra/scripts/seed-data/sintetico.sql` al nuevo modelo.
- [x] 5.2 `scripts/seed/emitir-sga.js` y el paso CSV → JSON privado emiten `materias`, `planes`/`materias_plan` (informativo) y el rol docente con materia + carrera directos, sin heurística de desambiguación por plan. El SQL resultante queda guardado fuera del repo (carpeta privada del usuario); el paso CSV → JSON no se versiona a propósito.

## 6. Tests

- [x] 6.1 Test de integración de la migración: conteos antes y después (carreras sin cambios; materias = códigos únicos; user_roles, pedidos y designaciones sin pérdida), y el Down de cada migración restaura el paso anterior.
- [x] 6.2 Test de integración de la spec `modelo-academico`: una materia compartida aparece una vez en el catálogo canónico y una vez por carrera en el catálogo informativo materia–plan; los códigos repetidos se rechazan.
- [x] 6.3 `RevisionPedidosTests` y `PedidosApiTests` para BR-designaciones-009 con materia compartida por dos carreras.
- [x] 6.4 `administracion-membresias-ambito`: combinación materia–carrera que no se dicta, y Docente sin carrera.

## 7. Documentación

- [x] 7.1 `docs/architecture/data-model.md` con el modelo nuevo (materias canónicas, planes/materias_plan informativos) y el plan placeholder `UNICO`.
- [x] 7.2 `docs/architecture/api-contracts.md`, `api-contracts-administracion.md` y `api-contracts-designaciones.md` para catálogos, membresías y pedidos.
- [x] 7.3 Cita de BR-designaciones-009 en `docs/business-rules/designaciones.md`.
- [ ] 7.4 Actualizar `openspec/specs/` al archivar el change (sync de specs).

## 8. Planes activos no vigentes

- [x] 8.1 Migración `database/identity/015_identity_planes_activo.sql` (`planes.activo`, default `TRUE`) y su clase C# `PlanesActivo`, con `Down`.
- [x] 8.2 Mapeo de `Plan.Activo` y catálogos de membresías, designaciones y pedidos filtrados por `activo`.
- [x] 8.3 El emisor del SGA carga los planes V y A con `activo = TRUE`; `vigente` sigue según `plan_estado`.

## 9. Verificación

- [x] 9.1 Backend: `dotnet build`/`dotnet test` en verde (162/164; los 2 restantes son una falla de entorno preexistente — WSL/bash no disponible en esta máquina — no relacionada al modelo de datos).
- [x] 9.2 Frontend: `vitest` (261/261), `tsc -b`, `vite build` y `eslint` en verde.
- [x] 9.3 `openspec validate --all --strict`: 47/47 OK.
