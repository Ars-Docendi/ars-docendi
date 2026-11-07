## Why

El % de avance se carga a mano aunque la tarea ya esté resuelta o aunque su trabajo real esté repartido en tareas hijas, así que el número deja de reflejar el estado de la tarea. El detalle además mostraba "Vencido" en tareas que vencen hoy o mañana.

## What Changes

- Marcar una tarea como **Resuelta** lleva su avance a 100%.
- **BREAKING (regla de producto):** una tarea con hijas no canceladas toma como avance el promedio de sus hijas directas y deja de editarse a mano. Antes el avance del padre era independiente.
- El recálculo sube por toda la jerarquía y queda en el historial. Una tarea cerrada no se recalcula hasta reabrirse.
- `TareaDto` suma `avanceCalculado`, y `PATCH /api/tareas/{id}/avance` responde 422 en esas tareas.
- En el Detalle, "Vencido" aparece sólo si la fecha de fin ya pasó, y el semáforo va después de la fecha.

## Capabilities

### New Capabilities

_(ninguna)_

### Modified Capabilities

- `tareas`: el avance del padre se deriva de sus hijas.
- `flujo-estado-tareas`: Resuelta completa el avance.

## Impact

- **Backend:** `Modules.Tareas`, sin migración: no cambia el schema.
- **Frontend:** `features/tareas` y artículos del Centro de ayuda.
- **Datos existentes:** no se reescriben. Una tarea ya guardada toma la regla nueva en su próximo cambio.
