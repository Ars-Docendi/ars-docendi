## Context

El frontend de Designaciones ya tiene el tablero de revisión (`/designaciones/revision`) implementado como Kanban (modelo D): `TableroRevisionPage` → `TableroRevision` → `ColumnaKanban` → `PedidoCard`, con la lógica de dominio en `tableroRevisionModelo.ts` (`construirColumnas`, `avancePedido`, `detallePedido`, `esTuTurno`, `situacionPedido`, `TOTAL_PASOS`) y los filtros en `filtrosTablero.ts`. La data viene del seam `api/` vía React Query; todo es mock frontend-only (sin backend todavía).

El design spec (opción D, `docs/product/designs/screens.pen` + `proyecto-docente-design-spec.md`) define además una **vista Tabla** con switcher Tabla/Tablero que hoy solo existe como mockup `.pen` (frame `ebl4U` / tabla `NivcK`), y un tratamiento específico para las cards de **Rechazados**. Este change baja esos elementos al frontend real sin tocar backend, API ni la lógica de dominio existente.

## Goals / Non-Goals

**Goals:**

- Agregar la vista Tabla como **segunda presentación** de los mismos pedidos/filtros del tablero, reusando toda la lógica de `tableroRevisionModelo.ts` (cero duplicación de reglas).
- Switcher Tabla/Tablero en la toolbar, Tablero por default, selección persistente mientras se está en la superficie.
- Alinear las cards de Rechazados al mockup (distintivo "Rechazado" + motivo citado).
- Ajustes menores de filtros: default "Mis pendientes" + label "Prioritario: Todos".
- Mantener la suite Vitest verde y sumar cobertura de lo nuevo.

**Non-Goals:**

- No se toca backend, API, Contracts, schema ni el grafo de dependencias.
- No se cambian las reglas de dominio (gating por ámbito/etapa, transiciones de estado, avance x/4): se **reusan** tal cual.
- No se rediseña la vista Tablero (Kanban) más allá de las cards de Rechazados y los dos ajustes de filtro.
- No se persiste la vista elegida entre sesiones ni en backend (solo mientras se permanece en la superficie).

## Decisions

### D1 — La Tabla reusa el modelo; aplana las columnas en filas

`TablaRevision` consume el **mismo** resultado de `construirColumnas(pedidos, actor)` que el Kanban y lo aplana a una lista de filas en el orden de las columnas (En revisión → Aceptados → Devueltos → Rechazados). Cada fila usa las mismas funciones puras (`avancePedido`, `detallePedido`, `esTuTurno`, `situacionPedido`) para derivar lo que muestra. Así, Tabla y Tablero quedan garantizadamente consistentes y no se reimplementa ninguna regla.

- **Alternativa descartada**: una función de modelo separada para la Tabla (`construirFilas`) → duplicaría el filtrado/orden y abriría drift entre vistas.

### D2 — Columna Estado = componente compartido `EstadoAvance`

Hoy el mini-stepper + etiqueta viven inline dentro de `PedidoCard`. Se extrae un componente chico reusable (p. ej. `EstadoAvance`/`CeldaEstado`) que, dado un pedido, renderiza: stepper parcial + `En {etapa} · x/4` (en revisión), stepper completo + "Aceptado" (`en_lote`), o dot + "Devuelto"/"Rechazado" (terminales). Lo usa la celda Estado de la Tabla; opcionalmente el Kanban puede migrar a usarlo después (no en este change, para acotar el diff).

- **Alternativa descartada**: copiar el markup del stepper dentro de `TablaRevision` → duplicación visual, drift de tokens (`-500` barras/dots, `-700` texto).

### D3 — Estado del switcher: `useState` en `TableroRevisionPage`

La vista activa (`"tablero" | "tabla"`) se maneja con `useState` en el page, default `"tablero"`. Es suficiente para "persistir mientras se permanece en la superficie" (el page sigue montado). El switcher es un segmented control (iconos lucide `columns-3` / `list`) en la toolbar, consistente con el resto de filtros.

- **Alternativa considerada**: `searchParam` en la URL (`?vista=tabla`) → daría deep-linking y persistiría al recargar, pero agrega acoplamiento al router y no lo pide el spec. Se deja como mejora futura; `useState` cubre el requisito.

### D4 — Card de Rechazados: branch por estado dentro de `PedidoCard`

En `PedidoCard`, **solo** para estado `rechazado`: (a) se renderiza un chip "Rechazado" (tokens `color-status-danger-fg/bg`, icono lucide `x`) en lugar de `NovedadChip`; (b) el motivo se muestra como **cita**: un bloque con borde izquierdo `danger` 2px, texto en itálica entre comillas, con wrap multilínea (no ellipsis). Se agrega un helper `motivoRechazo(pedido)` en el modelo que devuelve el comentario crudo de la acción `rechazar` (sin el prefijo `"Rechazado:"`). Los Devueltos NO cambian (siguen con `NovedadChip` + `"Devuelto: <motivo>"` plano vía `detallePedido`).

- **Alternativa descartada**: meter el branch en `detallePedido` (string) → el motivo citado necesita markup (borde, itálica, wrap), no es expresable como string plano.

### D5 — Filtros: label de prioridad

El label del filtro de prioridad pasa de "Prioridad: Todas" a "Prioritario: Todos" (alineación con el mockup). Cambio localizado, sin tocar la lógica de filtrado.

**Default de vista — evaluado y descartado**: se consideró cambiar el default de `vista` a `"mis-pendientes"` (el mockup muestra ese pill), pero la verificación visual mostró que abre el board casi vacío: `"mis-pendientes"` filtra a `esTuTurno` y los estados terminales (Aceptados/Devueltos/Rechazados) nunca son "tu turno", así que esas columnas quedan en "Sin pedidos". El mockup, en cambio, muestra el board **lleno**. Se mantiene el default **`"completa"`** (board completo, fiel a la apariencia del mockup); el revisor puede pasar a "Mis pendientes" para enfocarse en su turno.

## Risks / Trade-offs

- **[Drift de tokens entre Tabla y card]** Si la celda Estado de la Tabla y el stepper de la card divergen en colores → **Mitigación**: D2 (componente compartido `EstadoAvance`).
- **[La suite Vitest flakea en paralelo por timeout]** en máquinas saturadas → **Mitigación**: confirmar verde con `vitest run --no-file-parallelism`; no subir el umbral de timeout salvo necesidad real.
- **[Revertir el default a "mis-pendientes"]** puede sorprender a quien esperaba "Vista completa" → **Mitigación**: es exactamente lo que pide el mockup/spec; queda documentado en el design spec y es un toggle a un clic.
- **[Crecimiento del diff]** la Tabla + switcher + cards + tests puede pasar las ~400 líneas → **Mitigación**: si supera el presupuesto de PR, partir en chained PRs (Tabla por un lado; cards + filtros por otro), manteniendo este único change.

## Migration Plan

- Cambio **frontend-only**, aislado en `frontend/src/features/designaciones/`. No requiere migración de datos ni de API.
- **Rollback**: revertir el PR restaura el Kanban actual; no hay efectos sobre datos, otras features ni el grafo de dependencias.
- Actualizar `docs/product/designs/proyecto-docente-design-spec.md` (invariante #12) en el mismo PR, indicando que la vista Tabla bajó al frontend real.

## Open Questions

- ¿Se quiere deep-linking de la vista (`?vista=tabla`) más adelante? (fuera de alcance ahora; ver D3).
- ¿El Kanban debería migrar a usar el componente compartido `EstadoAvance` en un change posterior, para unificar el stepper? (no en este change, para acotar el diff).
