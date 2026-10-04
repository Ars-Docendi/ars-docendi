## Context

Estado actual en `database/identity`:

- `identity.carreras (id, code, name, is_active)`.
- `identity.materias (id, code, name, carrera_id NOT NULL, is_active)` con `UNIQUE(carrera_id, code)`. Una materia compartida por cuatro carreras son cuatro filas con el mismo `code`.
- `identity.user_roles (user_id, role_id, materia_id, carrera_id, …)` con trigger `enforce_role_scope` (Docente exige materia, Jefe de Cátedra exige materia, Coordinador exige carrera) y `UNIQUE NULLS NOT DISTINCT (user_id, role_id, materia_id, carrera_id) WHERE deleted_at IS NULL`.

Consumidores de `materia.CarreraId` en Designaciones:

- `ServicioPedidos.cs`: el Coordinador solo crea pedidos sobre materias de sus carreras (`actor.CarrerasACargo.Contains(pedido.CarreraId)`).
- `ServicioCatalogosDesignaciones.cs`: el catálogo de materias que ve un Coordinador se filtra por carrera.
- `ServicioPedidosApi.cs`: el DTO de pedido expone `carreraId` y nombre de carrera.

Designaciones y pedidos referencian `identity.materias(id)` por `materia_id` (`designaciones.pedidos`, `designaciones.designaciones`), con la carrera tomada hoy de `materias.carrera_id`.

Datos reales: la carga del SGA tiene ~575 filas de materia (una por par materia+carrera) y ~1109 asignaciones de rol docente, sin información de plan (solo `propuesta_codigo`). Al consolidar materias por código, 44 pares (materia, carrera) quedan bajo dos planes "activos no vigentes" de la misma carrera — ambigüedad de plan sin ningún criterio normativo para preferir uno, pero sin impacto en la asignación porque esta nunca necesita saber el plan, solo la carrera.

## Goals / Non-Goals

**Goals:**

- Una materia canónica por código, compartida entre carreras.
- Pertenencia materia↔plan explícita, con vigencia, como catálogo informativo.
- Que Designaciones y roles sigan funcionando con su alcance por carrera, sin ambigüedad cuando una materia pertenece a varias.
- Migración sin pérdida de filas ni asignaciones, reversible para los datos existentes.

**Non-Goals:**

- Versiones de plan, revisiones de materia, catálogos de estado, responsables académicas (change posterior).
- Correlatividades.
- Cambiar el circuito de aprobación de pedidos (BR-designaciones-005..017).
- Resolver la carrera de una asignación a través de un plan: ninguna regla de negocio implementada necesita esa granularidad, y la importación real agrega ambigüedad de plan sin agregar valor (ver el caso de los 44 pares en "Context").

## Decisions

**D1. `materias` como entidad canónica, con `code` único global.** El código de 5 dígitos identifica la materia; el mismo código en varias carreras es la misma materia. Alternativa descartada: mantener una fila por carrera (estado actual): duplica opciones en la UI y no permite expresar "dictada en varias carreras". Riesgo: si Guaraní reusa un código para dos materias distintas, la unicidad global falla; la migración valida que cada código tenga un único nombre antes de consolidar, y aborta si no.

**D2. `planes` por carrera, con `vigente`/`activo`; `materias_plan` como pertenencia (`plan_id`, `materia_id`, `activo`), única por par. Catálogo informativo: ninguna FK de negocio depende de él.** Una materia puede estar en varios planes, y un mismo materia puede estar en planes de carreras distintas. `planes`/`materias_plan` informan qué materias dicta cada carrera y con qué vigencia, y se consultan para validar que una combinación `(materia_id, carrera_id)` se dicte de verdad — pero `user_roles`, `pedidos` y `designaciones` nunca referencian `materias_plan` por FK. Alternativa descartada: columna `carrera_id` en `materias` (vuelve al problema de la fila por carrera).

**D3. `user_roles`, `pedidos` y `designaciones` llevan `materia_id` + `carrera_id` directos, elegidos juntos.**

- **Docente:** `materia_id` + `carrera_id` juntos. Una materia compartida entre carreras exige elegir una; si dicta en más de una, son dos membresías (o dos designaciones/pedidos), una por carrera.
- **Jefe de Cátedra:** solo `materia_id` (la materia canónica). Es responsable de la materia en todas sus carreras, así que una sola membresía alcanza. Para autorizar un pedido se compara la materia del pedido con la del jefe, sin importar la carrera.
- **Coordinador de Carrera:** solo `carrera_id`.
- **Pedidos y designaciones:** `materia_id` + `carrera_id` directos, igual que el Docente — el Coordinador competente (BR-designaciones-009) es el de esa carrera.

Alternativa descartada: resolver la carrera a través de una pertenencia `materia_plan` (el pedido/rol apuntaría a un `materia_plan_id`, y la carrera saldría de `materia_plan → plan → carrera`). Se descartó porque ninguna regla de negocio necesita esa granularidad de plan, y agrega una indirección que la importación real convierte en ambigüedad pura (los 44 pares del "Context"): dos planes "activos no vigentes" de la misma carrera no tienen ningún criterio normativo para elegir uno, cuando el par (materia, carrera) ya identifica la asignación sin ambigüedad.

Como `materia` es el scope de dos roles, la validación de `enforce_role_scope` se decide por código de rol y no solo por el campo `scope` del rol.

**D4. Migración en varios pasos, sin eliminar datos del modelo nuevo entre pasos.**

1. Crear `planes` y `materias_plan`, poblados desde `carreras` y `materias`: `planes.carrera_id` referencia `carreras.id`; un plan placeholder por carrera (`vigente = true`, código `UNICO`, marcado para reemplazo cuando el SGA entregue planes reales); un `materias_plan` por cada fila de `materias` existente (1:1, por eso no hay pérdida).
2. Modificar `identity.materias`: quitar `carrera_id`, reemplazar `UNIQUE(carrera_id, code)` por `UNIQUE(code)`. Antes de eso se borran los datos cargados (las filas que creamos nosotros), sin tocar la estructura de la tabla; los datos se recargan desde el listado de Guaraní.
3. Agregar `carrera_id` a `identity.user_roles` (ya existía, solo se habilita también para Docente), a `designaciones.pedidos` y a `designaciones.designaciones`, con backfill desde la carrera única que tenía la materia antes de consolidarla, y FKs simples a `identity.carreras`.
4. Planes activos no vigentes: agregar `planes.activo` (default `TRUE`) para que un plan "A" (activo, no vigente) siga siendo válido en el catálogo informativo aunque no sea el plan vigente de su carrera.

Trade-off aceptado: el plan placeholder no tiene información real de versión ni de vigencia; los planes reales se cargan después y reasignan `materias_plan`.

## Risks / Trade-offs

- **[Código con nombres distintos entre carreras]** → la migración falla antes de consolidar; se resuelve con el SGA antes de correr.
- **[Plan placeholder mal leído como real]** → se marca explícitamente en `planes` (código `UNICO`) y en `data-model.md`; el primer plan real reemplaza la pertenencia sin cambiar `materia_plan` para materias que no cambian.
- **[Designaciones y roles remapeados]** → test de integración que compara conteos antes y después (pedidos, designaciones, user_roles) y que un Coordinador ve exactamente los mismos pedidos que antes de la migración.
- **[Costo de tocar Designaciones]** → el cambio cruza `Shared/identity` y `Modules.Designaciones`; ambos ya dependían de identity, así que el grafo no gana aristas nuevas.

## Migration Plan

1. `database/identity/013_identity_modelo_academico.sql`: crea `planes`/`materias_plan`, consolida `materias` a canónica (con validación previa de códigos y nombres).
2. `database/designaciones/014_designaciones_materia_plan.sql` y `015_designaciones_materia_canonica.sql`: ajustes de `pedidos`/`designaciones` durante la consolidación de `materias`.
3. `database/identity/014_identity_materias_plan_unicidad.sql` y `015_identity_planes_activo.sql`: unicidad de `materias_plan` y soporte de planes activos no vigentes.
4. `database/identity/016_identity_docente_carrera_directa.sql` y `database/designaciones/017_designaciones_carrera_directa.sql`: agregan `carrera_id` a `user_roles` (Docente), `pedidos` y `designaciones`, con backfill y FKs a `identity.carreras`.
5. Tests de integración con conteos antes/después y verificación de alcance del Coordinador.
6. `sintetico.sql` y el generador de `sga.sql` (`scripts/seed/emitir-sga.js`) al nuevo modelo: materia + carrera directos, sin heurística de desambiguación por plan.

**Rollback:** el `Down` de cada migración revierte su propio paso; el de `013` vuelve a agregar `carrera_id` a `materias` y restaura la unicidad por carrera, tomando la carrera de `materias_plan → planes → carreras`. Es exacto para los datos existentes; no reconstruye materias compartidas creadas después de migrar.

## Open Questions

1. ¿Los códigos de plan del SGA se pueden obtener para reemplazar el placeholder `UNICO`, o el placeholder queda hasta un change posterior?
2. ¿Hay códigos de materia que se repiten con nombres distintos entre carreras? La migración lo detecta, pero conviene conocerlos antes.
