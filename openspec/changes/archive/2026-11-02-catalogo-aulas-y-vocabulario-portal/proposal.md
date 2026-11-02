## Por qué

El relevamiento de requisitos encontró pendientes concretos en Aulas y Portal:

- El aula asignada a una reserva es texto libre: no hay catálogo de aulas y laboratorios, nada impide asignar la misma aula dos veces en el mismo horario y el permiso `aulas.gestionar` está sembrado pero nadie lo usa.
- El vocabulario de habilidades e intereses es una lista fija del frontend. Cada término nuevo queda sugerido para siempre porque nadie puede incorporarlo, renombrarlo ni fusionarlo.
- El docente no ve en Mi Portal sus designaciones vigentes ni sus horas.
- Ni Aulas ni Portal tienen reglas de negocio `BR-*` registradas.

## Qué cambia

**Backend**

- Aulas: catálogo `aulas.aulas` (nombre, tipo, capacidad, equipamiento, activa) administrado con `aulas.gestionar`. La asignación elige un aula activa del catálogo y la base impide dos reservas aprobadas superpuestas en la misma aula. Superar la capacidad no bloquea.
- Portal: `GET /api/portal/vocabulario` y una administración del vocabulario (incorporar, renombrar, fusionar) con el permiso nuevo `portal.vocabulario.administrar`.
- Designaciones: `GET /api/designaciones/vigentes/mias` para la persona autenticada.

**Frontend**

- Pantalla "Catálogo de aulas" y selector de aula con advertencia de capacidad en el detalle de la solicitud.
- El selector de habilidades e intereses lee el vocabulario de la API; pantalla "Vocabulario de experticia".
- Sección "Designaciones vigentes" en Mi Portal.

**Documentación**

- `docs/business-rules/aulas.md` y `docs/business-rules/portal.md`.

**Fuera de alcance**: catálogo de comisiones (depende de la integración con Guaraní) y períodos de exámenes.

## Capabilities

### New Capabilities

- `catalogo-aulas`: alta, edición, activación y baja de aulas y laboratorios.
- `vocabulario-experticia`: curaduría del vocabulario compartido de habilidades e intereses.
- `designaciones-vigentes-propias`: el docente consulta sus designaciones vigentes y horas en Mi Portal.

### Modified Capabilities

- `reserva-aulas`: la asignación usa el catálogo, sin superposición y con advertencia de capacidad.
- `experticia-docente`: el vocabulario proviene de la base y una variante fusionada resuelve a su término canónico.

## Impacto

- Módulos: `Modules.Aulas`, `Modules.Portal`, `Modules.Designaciones`, `ArsDocendi.Shared` (permiso nuevo), frontend `features/aulas`, `features/portal`, `shared/perfil`.
- Grafo de dependencias: sin aristas nuevas. La sección de Mi Portal consume el endpoint de Designaciones desde el frontend, sin que `Modules.Portal` referencie Designaciones.
- Migraciones: `aulas` (catálogo, `aula_id`, exclusión de superposición), `portal` (vocabulario base) e `identity` (permiso y descripción).
- Rollback: revertir el PR antes de desplegar. Después de aplicar la migración, la recuperación es explícita desde respaldo; no hay reversión automática de DDL.
