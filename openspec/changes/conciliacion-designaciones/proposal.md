# Proposal

## Why

Hoy, cuando Decanato acepta un pedido, éste pasa a `en_lote` y la designación se escribe en la base en el mismo momento. El circuito institucional real sigue después de ese paso. Secretaría exporta el Excel del lote y lo envía a rectificar. Recién cuando vuelve la rectificación se sabe qué pedidos se aprobaron. Por eso el sistema escribe designaciones que todavía pueden no aprobarse, y no tiene dónde registrar el resultado de la rectificación.

## What Changes

- **BREAKING (comportamiento):** aceptar en Decanato deja el pedido en `en_lote` y **ya no escribe la designación**. `en_lote` deja de ser terminal: es "pendiente de conciliar".
- Dos estados nuevos del pedido:
  - `aprobado`: terminal, ya sincronizado con la base.
  - `no_aprobado`: rechazado en la conciliación, con motivo obligatorio. La pantalla lo muestra como "Rechazado". Se puede editar y, al guardar, vuelve a `en_lote`.
- Pantalla nueva **Conciliación** (`/designaciones/conciliacion`) para los pedidos en lote del período activo.
  - Agrupa por docente y muestra todas sus materias: lo que tiene hoy, cómo queda, la carrera, la novedad y el pedido.
  - Se acepta por docente. Cada cambio se puede editar o rechazar.
  - Pestañas: Por conciliar, Rechazados y Sincronizados.
- **Sincronizar** muestra primero una vista previa (docentes nuevos, altas, cambios, bajas y el detalle por docente). Después aplica en una sola transacción:
  - Alta abre la designación, Baja la cierra y Cambio la cierra y abre otra (o la actualiza si empezó ese mismo día).
  - Cada pedido pasa a `aprobado`.
  - Lo sincronizado ya no se puede editar.
- Dos permisos nuevos del catálogo, asignables por rol. Por defecto los tienen Secretaría y Administrativo:
  - `designaciones.conciliar`: ver la pantalla, rechazar y sincronizar.
  - `designaciones.editar`: editar un pedido `en_lote` o `no_aprobado`, en cualquier campo.
  - No cambian las reglas actuales de edición de borradores y devueltos (BR-designaciones-008).
- **Excel del lote:**
  - Incluye los pedidos `en_lote` y `aprobado` del período, y excluye los `no_aprobado`.
  - La hoja PROPUESTA COMPLETA refleja un Cambio todavía no sincronizado con los valores del pedido.

## Capabilities

### New Capabilities

- `conciliacion-designaciones`: conciliación de los pedidos en lote contra la rectificación. Cubre la pantalla agrupada por docente, el rechazo con motivo, la edición con permiso, la sincronización con vista previa y los permisos `designaciones.conciliar` y `designaciones.editar`.

### Modified Capabilities

- `aprobacion-pedidos-designacion`: la aceptación de Decanato deja el pedido en `en_lote` no terminal y sin materializar. Finalizados y el detalle muestran los estados `aprobado` y `no_aprobado`.
- `persistencia-designaciones`: la materialización pasa de la aceptación de Decanato a la sincronización de la conciliación. Se agregan estados y eventos de historial al modelo.
- `exportacion-lote-designaciones`: el lote incluye `aprobado`, excluye `no_aprobado` y proyecta los Cambios pendientes de sincronizar.
- `pedidos-designacion`: la edición de pedidos `en_lote` y `no_aprobado` se habilita con `designaciones.editar`.

## Impact

- **Módulos:** sólo `Modules.Designaciones`, más el frontend `features/designaciones`. No cambia ningún `*.Contracts` ni el grafo de dependencias.
- **Base de datos:**
  - `designaciones`: CHECKs de estado, acción y etapa, y una columna `pedidos.materializado_en`.
  - `identity`: dos permisos nuevos y su asignación a roles.
  - No se tocan las tablas de personas (`identity.personas`, `identity.users`). La persona de un Alta ya se crea al cargar el pedido.
- **API:**
  - Nuevos: `GET /api/designaciones/conciliacion`, `POST /api/designaciones/conciliacion/sincronizar` y `POST /api/designaciones/pedidos/{id}/no-aprobar`.
  - Se amplía la autorización de `PUT /api/designaciones/pedidos/{id}`.
- **Integridad:** cada evento nuevo queda sellado en la cadena del período (BR-designaciones-019). La forma canónica del sello no cambia.
- **Docs:**
  - `docs/architecture/api-contracts-designaciones.md` y `data-model.md`.
  - El design spec de la pantalla.
  - El artículo del Centro de ayuda sobre designaciones y permisos.
- **Rollback:** las migraciones sólo amplían CHECKs y agregan una columna nullable. Para volver atrás:
  - Se despliega la versión anterior del backend, que vuelve a materializar al aceptar.
  - Antes, los pedidos `aprobado` o `no_aprobado` tienen que resolverse con una migración incremental explícita. Ninguna migración automática los borra.
