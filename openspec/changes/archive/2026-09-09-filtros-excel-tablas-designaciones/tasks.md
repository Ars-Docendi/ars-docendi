## 1. Helpers y comportamiento común

- [x] 1.1 Extender `frontend/src/features/designaciones/components/filtrosMisPedidos.ts` con el estado de orden, comparadores por número/texto/fecha/estado y la composición filtros -> orden; verificar con tests de coincidencia sin tildes, valores vacíos, ciclo asc/desc/sin orden y aplicación antes de paginar.
- [x] 1.2 Extender `frontend/src/features/designaciones/components/filtrosTablero.ts` y `tableroRevisionModelo.ts` para representar los filtros de encabezado, incluido Área cuando corresponde, sin duplicar Período, Carrera, Prioridad ni Sin movimiento; verificar con tests de filtros combinados y contadores por pestaña.
- [x] 1.3 Crear el helper puro de filtros/orden de Períodos a partir de `TablaPeriodos.tsx`; verificar búsqueda normalizada, selección Activo/Inactivo, orden cronológico por ISO y ciclo de orden.

## 2. Tabla Mis pedidos

- [x] 2.1 Migrar `TablaMisPedidos` del grid ARIA a los subcomponentes `Table` del design system, conservando clases visuales, click de fila, Enter/Espacio y propagación detenida de Ver/Editar/Eliminar; verificar roles nativos y navegación con tests de componente.
- [x] 2.2 Integrar `FiltroEncabezado` y `Table.HeaderCell` en cada columna de datos de `TablaMisPedidos`, dejando Acciones sin control; verificar apertura por teclado, indicador activo, limpieza y `aria-sort`.
- [x] 2.3 Pasar filtros/orden controlados desde `MisPedidosPage`, aplicar filtros y orden antes de `slice` y reiniciar la página cuando cambie el resultado; verificar filtros combinados, orden de filas visibles, paginación y estado vacío.

## 3. Tabla de Revisión

- [x] 3.1 Agregar callbacks de filtros de columna a `TablaRevision` y conectar menús para Docente, Legajo, Tipo, Inicio, Últ. actualización, Estado y Área sólo en Todos; verificar que abrir el menú no dispare el ordenamiento.
- [x] 3.2 Ajustar `TableroRevisionPage` para retirar del `FiltrosLista` Nombre, Tipo, Legajo y Estado, conservando Período, Carrera, Prioridad y Sin movimiento; verificar que no haya controles duplicados y que el ámbito autorizado permanezca intacto.
- [x] 3.3 Verificar la interacción de Revisión con tests de componente: filtros antes de contadores, cambio de pestaña, Área sólo en Todos, limpieza individual, filtros generales conservados y orden predeterminado al quitar el orden manual.

## 4. Tabla de Períodos

- [x] 4.1 Integrar filtros y orden por encabezado en `TablaPeriodos` para Nombre, cuatro fechas y Activo, eliminando el toolbar duplicado y preservando Acciones; verificar los escenarios de filtros, fechas cronológicas y orden accesible.
- [x] 4.2 Agregar o actualizar tests de `TablaPeriodos` para limpieza individual, selección de estado, `aria-sort`, acciones sin filtro/orden y menú operable con teclado.

## 5. Documentación

- [x] 5.1 Actualizar `docs/product/designs/proyecto-docente-design-spec.md` para documentar el patrón de filtros y ordenamiento por encabezado en Mis pedidos, Revisión y Períodos, manteniendo los filtros generales sin columna visible; verificar que no describa toolbars eliminados ni controles duplicados.

## 6. Verificación final

- [x] 6.1 Ejecutar `pnpm --filter frontend test:run` y corregir regresiones de tablas, helpers y accesibilidad.
- [x] 6.2 Ejecutar `pnpm --filter frontend lint`, `pnpm --filter frontend build`, `pnpm format:check` y `pnpm exec openspec validate --all --strict`; dejar constancia de cualquier check que el entorno no permita completar.
