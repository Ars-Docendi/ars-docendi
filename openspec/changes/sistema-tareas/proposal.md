## Why

El módulo Tareas (`docs/architecture/domains/tareas.md`) está definido a nivel de dominio pero sin implementación: hoy `frontend/src/features/tareas` es solo un placeholder ("Módulo en construcción"). El departamento necesita una forma digital de que las autoridades (Secretaría, Decanato, Administrativos) asignen tareas de coordinación interna a los usuarios y hagan seguimiento de su vencimiento, reemplazando el seguimiento informal actual.

## What Changes

- Nueva pantalla de listado de tareas, única para todos los roles, con columnas Nro, Título, Autor, Responsable, Fecha Inicio, Fecha Fin, Prioridad, % Avance y Estado. El **fondo de toda la fila** se colorea con el semáforo de vencimiento: sin resaltado por debajo del 50% del plazo transcurrido, amarillo entre 50% y 80%, rojo desde el 80% (incluida vencida).
- Filtros del listado: uno por cada columna de la tabla (Nro de Tarea, Título, Autor, Responsable, Fecha de Inicio, Fecha de Fin, Prioridad, % Avance y Estado). Nro de Tarea, Responsable y Título visibles por defecto; el resto se agrega bajo demanda. Los filtros de fecha son "hasta esta fecha" (inclusive); el de % Avance es coincidencia exacta (campo de bajo valor de negocio, incluido solo por consistencia con "un filtro por columna"). El filtro Responsable es un campo de búsqueda: se tipea texto y se elige de una lista de resultados, no un `<select>` tradicional. El usuario puede guardar la combinación de filtros activos con un nombre y volver a aplicarla después.
- Botón "Nueva Tarea" en el listado, visible **solo** para Secretaría Académica, Decanato y Administrativos, que abre un formulario de creación (Título, Descripción, Fecha Inicio, Fecha Fin, Prioridad, Responsable de un único usuario elegido con el mismo combobox buscable que el filtro).
- Nueva pantalla de Detalle de Tarea (click en fila del listado): encabezado, descripción, fechas, % de avance, Responsable, Autor, campo Solución (al resolverse), comentarios internos (hilo simple) e historial/auditoría de cambios.
- Estados de una tarea: Pendiente, En curso, Pausa (consulta), Resuelta y Cancelada. El estado Pausa lo marca el Responsable cuando tiene una consulta sobre la tarea, y debe resaltarse visualmente en el listado que ve la autoridad creadora. Pasar a Resuelta exige completar el campo Solución con el detalle de cómo se resolvió.
- Porcentaje de avance (0-100) que el Responsable actualiza manualmente, visible como columna en el listado y en el Detalle; independiente del Estado (no se sincronizan automáticamente).
- Permisos de edición a nivel de lógica de UI (no un sistema de roles nuevo): la autoridad creadora edita todos los campos y es la única que puede Cancelar una tarea; el Responsable puede cambiar libremente el Estado entre Pendiente / En curso / Pausa / Resuelta, actualizar el % de avance y completar la Solución, pero no puede cancelarla ni editar los demás campos. Se reutiliza el patrón `useActorContexto` ya existente en Designaciones para derivar el rol actual.
- Implementación frontend-first con mock store en memoria (mismo patrón que `features/docentes`, `features/roles`, `features/usuarios`), sin backend real más allá del `GET /api/tareas/ping` ya existente.

## Capabilities

### New Capabilities

- `tareas`: entidad Tarea, formulario de creación (con visibilidad del botón restringida por rol), pantalla de detalle (descripción, fechas, % de avance, Responsable, Autor, Solución, comentarios internos, historial/auditoría) y edición del % de avance por el Responsable.
- `tablero-tareas`: pantalla de listado inicial de tareas con columnas Autor/Responsable/% Avance incluidas, semáforo de vencimiento sobre el fondo de la fila, un filtro por cada columna (con Nro de Tarea/Responsable/Título visibles por defecto) y configuraciones de filtro guardadas, e indicador visual de tareas en Pausa (consulta) para la autoridad creadora.
- `flujo-estado-tareas`: estados (Pendiente / En curso / Pausa / Resuelta / Cancelada), transición Pausa iniciada por el Responsable (con comentario obligatorio), transición a Resuelta (con Solución obligatoria), y permisos de quién puede cambiar qué (el Responsable mueve libremente entre Pendiente/En curso/Pausa/Resuelta; Cancelar y editar campos quedan reservados a la autoridad creadora).

### Modified Capabilities

_(ninguna — el módulo Tareas no tiene specs previas; no se modifica comportamiento de otros módulos)_

## Impact

- **Código afectado**: `frontend/src/features/tareas/**` (hoy placeholder) — nuevas pages, components, hooks, mock store y types. Reutiliza primitivos de `shared/ui` (`PageHeader`, `FiltrosLista`, `AuditLog`, `Breadcrumbs`, `Button`, `InlineAlert`) ya usados por Designaciones. `shared/ui/FiltrosLista.tsx` se extiende (aditivo, retrocompatible) con los tipos de campo `"fecha"` (Fecha de Inicio/Fecha de Fin), `"numero"` (% Avance), `"buscable"` (Responsable: combobox con búsqueda por texto) y `"multiSelect"` (Estado: checkboxes en un desplegable, elegir varios estados a la vez) — afecta un componente compartido, pero sin cambiar el comportamiento de los consumidores existentes (Designaciones, Docentes, Usuarios, Roles).
- **Dependencias cross-module**: ninguna nueva. El dominio ya declara dependencia hacia `Modules.Portal.Contracts` para conocer al Responsable, pero como este change es frontend-first con mock store, no se toca el backend ni el grafo de dependencias todavía.
- **Fuera de alcance**: backend real de `Modules.Tareas` (más allá del ping existente), sistema de permisos configurable (pertenece al change `roles-membresia` de otro equipo, que no se toca), parametrización del umbral del semáforo, asignación múltiple de usuarios, adjuntos de archivos, notificaciones.
- **Rollback**: el módulo queda aislado en `frontend/src/features/tareas` sin backend ni migraciones; revertir es eliminar el feature y su entrada de rutas, sin efectos en otros módulos.
