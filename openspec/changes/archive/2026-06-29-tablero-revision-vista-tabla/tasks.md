## 1. Modelo (reuso, sin cambiar reglas)

- [x] 1.1 Agregar helper `motivoRechazo(pedido)` en `tableroRevisionModelo.ts` que devuelva el comentario crudo de la acción `rechazar` (sin el prefijo `"Rechazado:"`), reusando `comentarioDe`.
- [x] 1.2 Test en `tableroRevisionModelo.test.ts` para `motivoRechazo` (con motivo y sin motivo).

## 2. Componente compartido Estado/avance (D2)

- [x] 2.1 Test (red): `EstadoAvance` renderiza por estado → en revisión (mini-stepper parcial + `En {etapa} · x/4`), `en_lote` (stepper completo + "Aceptado"), `devuelto` ("Devuelto"), `rechazado` ("Rechazado"), con los tokens de color correctos (`-500` barras/dots, `-700` texto).
- [x] 2.2 Extraer `EstadoAvance.tsx` (componente puro a partir del markup del stepper hoy inline en `PedidoCard`) + estilos; verde.

## 3. Cards de Rechazados en el Tablero (D4)

- [x] 3.1 Test (red): `PedidoCard` para estado `rechazado` muestra un chip "Rechazado" (no el `NovedadChip`) y el motivo como cita destacada; un pedido `devuelto` sigue mostrando `NovedadChip` + `"Devuelto: <motivo>"` plano.
- [x] 3.2 Implementar en `PedidoCard.tsx` el branch para `rechazado`: chip "Rechazado" (tokens `color-status-danger-fg/bg`, icono lucide `x`) + cita del motivo (`motivoRechazo`), con estilos de blockquote (borde izq `danger` 2px, itálica, comillas, wrap) en `revision.css`.
- [x] 3.3 Verde + verificar que Devueltos y los demás estados no cambiaron.

## 4. Vista Tabla (D1)

- [x] 4.1 Test (red): `TablaRevision` aplana las columnas de `construirColumnas` a filas ordenadas por estado (En revisión → Aceptados → Devueltos → Rechazados), con columnas Docente, Asignatura, Novedad, Estado y Prioritario; cubre el estado Empty.
- [x] 4.2 Implementar `TablaRevision.tsx` (reusa `construirColumnas`, `EstadoAvance`, `detallePedido`, `situacionPedido`, `NovedadChip`) + estilos de la tabla en `revision.css` (header `bg-canvas` + borde `strong`, tabla plana, columna Asignatura filler).
- [x] 4.3 Columna Prioritario solo-ícono: bandera roja (lucide) si `prioritario`, celda vacía si no.

## 5. Switcher Tabla / Tablero (D3)

- [x] 5.1 Test (red): `TableroRevisionPage` abre en la vista Tablero por default; al elegir "Tabla" en el switcher muestra `TablaRevision` con los mismos pedidos y filtros activos.
- [x] 5.2 Implementar estado de vista (`useState<"tablero" | "tabla">("tablero")`) + segmented control en la toolbar (iconos lucide `columns-3` para Tablero y `list` para Tabla), consistente con los demás filtros.

## 6. Ajustes de filtros (D5)

- [x] 6.1 Default de `vista`: se evaluó `"mis-pendientes"` (mockup) pero la verificación visual mostró que deja el board vacío (los terminales no son "tu turno"); se mantiene `"completa"` en `filtrosTablero.ts` + test del default cubierto en `TableroRevisionPage.test.tsx`.
- [x] 6.2 Cambiar el label del filtro de prioridad de "Prioridad: Todas" a "Prioritario: Todos".

## 7. Docs, verificación y cierre

- [x] 7.1 Actualizar `docs/product/designs/proyecto-docente-design-spec.md` (invariante #12): la vista Tabla + switcher bajaron al frontend real; remover la nota de "pendiente".
- [x] 7.2 `pnpm --filter frontend lint` y `pnpm --filter frontend build` verdes.
- [x] 7.3 `pnpm --filter frontend exec vitest run --no-file-parallelism` verde (suite completa; confirma sin el flake de paralelismo).
- [x] 7.4 `pnpm exec openspec validate --strict tablero-revision-vista-tabla` verde.
- [x] 7.5 Spot-check visual (Coordinador, captura headless): switcher Tablero/Tabla ✓, columna Estado combinada en la Tabla ✓, card de Rechazados (chip + cita) ✓, board lleno por default ✓. Reveló que "mis-pendientes" vacía el board → se mantuvo "completa" (ver 6.1).
- [x] 7.6 Correr `/evaluate` contra spec + grading-criteria → composite **4.45** (Func 4 · Code 5 · UX 5 · Orig 4 · Doc 4); fila agregada al scorecard.
