## Why

Una tarea sólo puede ser hija si se crea como tal. Quien descubre después que dos tareas ya cargadas forman parte del mismo trabajo tiene que recrearlas, y "Tareas relacionadas" no sirve porque no crea jerarquía ni aporta al avance.

## What Changes

- Desde el Detalle, la autoridad creadora del padre puede vincular como hija una tarea existente sin padre y no cancelada, y desvincular una hija.
- La tarea vinculada y sus descendientes toman el Proyecto del padre, con confirmación si cambia.
- El avance se recalcula en el padre y en sus ancestros.
- Nuevos endpoints `POST /api/tareas/{id}/hijas` y `DELETE /api/tareas/{id}/hijas/{hijaId}`.

## Capabilities

### New Capabilities

_(ninguna)_

### Modified Capabilities

- `tareas`: suma vincular y desvincular tareas hijas existentes.

## Impact

- **Backend:** `Modules.Tareas`, sin migración: las garantías de jerarquía ya están en la base.
- **Frontend:** `features/tareas` y el artículo "Abrir una tarea" del Centro de ayuda.
