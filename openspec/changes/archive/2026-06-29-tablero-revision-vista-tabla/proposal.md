## Why

El "Tablero de revisión de pedidos" del frontend real ya implementa el modelo D (Kanban de 4 columnas), pero quedó desalineado del design spec / mockup aprobado (`docs/product/designs/screens.pen`, opción D) en puntos visibles para el cliente: (1) falta la **vista Tabla** y su switcher Tabla/Tablero —hoy solo existe como mockup en el `.pen`—, y (2) las cards de pedidos **Rechazados** no muestran el estado ni el motivo como define el diseño. Cerrar la brecha mantiene la UI fiel al spec (invariante #12) y le da al revisor una segunda vista, tabular y más densa, para escanear muchos pedidos de un vistazo.

## What Changes

- **Nueva vista Tabla** del tablero de revisión: tabla plana (sin divisores de grupo), ordenada por estado, con columnas Docente · Asignatura · Novedad · **Estado** (columna combinada estado + avance del circuito) · Prioritario (solo ícono). Reusa los mismos pedidos, filtros y lógica de dominio del Kanban.
- **Switcher Tabla | Tablero** en la toolbar (segmented control), con el Tablero (Kanban) por default y persistencia de la selección elegida.
- **Presentación de Rechazados** (en el Kanban): chip de estado **"Rechazado"** en lugar del chip de novedad + motivo de rechazo destacado como **cita** (en vez del texto plano `"Rechazado: …"`).
- **Label** del filtro "Prioridad: Todas" → **"Prioritario: Todos"**.
- Sin cambios de backend, API, persistencia, ni gating por rol.

## Capabilities

### New Capabilities

- `tablero-revision-tabla`: vista **tabular** del tablero de revisión de pedidos (tabla plana ordenada por estado; columna Estado que **combina estado + avance** del circuito de aprobación; columna de prioridad por ícono) y el **switcher Tabla/Tablero** que alterna entre la vista tabular y el Kanban existente, compartiendo los mismos pedidos y filtros.

### Modified Capabilities

- `aprobacion-pedidos-designacion`: los pedidos **rechazados** presentan su estado y motivo de rechazo de forma **destacada** para el revisor (distintivo de estado "Rechazado" + motivo citado).

## Impact

- **Frontend** (única superficie afectada): `frontend/src/features/designaciones/`
  - **Nuevo**: `components/TablaRevision.tsx` (+ estilos, en `revision.css` o un CSS propio).
  - **Modificados**: `components/PedidoCard.tsx` y `revision.css` (cards Rechazados), `components/filtrosTablero.ts` (default de vista + label), `pages/TableroRevisionPage.tsx` (switcher + estado de vista), posible `components/NovedadChip.tsx` (chip "Rechazado").
  - **Reuso sin cambios de lógica**: `components/tableroRevisionModelo.ts` (`construirColumnas`, `avancePedido`, `detallePedido`, `esTuTurno`, `situacionPedido`, `TOTAL_PASOS`), `components/ColumnaKanban.tsx`, `types.ts`.
- **Tests**: Vitest (hoy 83 verdes). Sumar tests de la vista Tabla, el switcher, el render del chip + cita de Rechazados y el nuevo default. Confirmar con `vitest run --no-file-parallelism` (la suite flakea en paralelo por timeout en máquinas saturadas).
- **Docs**: actualizar `docs/product/designs/proyecto-docente-design-spec.md` (invariante #12) reflejando que la vista Tabla baja al frontend real.
- **Sin impacto** en el grafo de dependencias (DAG), backend, Contracts, API ni schema de base de datos. No introduce normativa institucional nueva (sin `BR-*` nuevos).
- **Rollback**: cambio frontend-only, aislado en la feature `designaciones`; revertir el PR restaura el Kanban actual sin efectos sobre datos ni otras features.
