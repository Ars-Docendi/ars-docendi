## Why

Una revisión visual de las pantallas en uso (pedida por July) encontró inconsistencias y ruido que afectan la claridad del sistema:

- La barra superior muestra un buscador, notificaciones y ayuda que no funcionan, lo que viola el invariante #7 (no fake UI).
- Los encabezados de página apilan hasta cuatro capas que repiten la misma información (breadcrumb, pretitle, título y meta). Además, los nombres no coinciden con el sidebar y mezclan el uso de mayúsculas.
- En el Portal, el mes de un período no se puede elegir hasta que se carga el año.
- En Períodos de designación, el selector de fecha muestra dos íconos y las acciones de cada fila quedan escondidas en un menú de tres puntos.

## What Changes

- **Barra superior**: se eliminan el buscador global, el botón de notificaciones y el de ayuda.
- **Encabezados de página**: se unifica la convención en todas las pantallas:
  - Mayúscula solo en la primera palabra, en sidebar, breadcrumb y título.
  - El título coincide con el label del sidebar, con dos excepciones: "Administración de …" en Usuarios, Docentes y Roles, y "Mis docentes" para el Jefe de Cátedra.
  - El breadcrumb no incluye el grupo del sidebar en ningún módulo: solo pantallas navegables.
  - Se elimina el pretitle, incluido el "Cuatrimestre 2026 · 1C" hardcodeado en Aulas y Tareas.
  - El meta solo muestra datos que no aparecen en otro lugar de la pantalla.
- **Detalle del pedido**: el título pasa a ser solo el tipo de novedad. Se quitan del encabezado el número de pedido y la cátedra, y el meta muestra solo el período.
- **Nuevo/Editar pedido**: pasan a usar `PageHeader` con los títulos "Nuevo pedido" / "Editar pedido". El breadcrumb de edición deja de mostrar el número.
- **Revisión**: el meta deja de repetir el rol del usuario.
- **Período en el Portal**: el mes se puede elegir antes que el año, sin perderse. Se implementa con un nuevo componente `MonthYearPicker` en `@ars-docendi/ui`, con el mismo diseño visual actual.
- **Períodos de designación**:
  - El `DatePicker` muestra un único ícono de calendario dentro del campo (el fix va en `@ars-docendi/ui`).
  - Las acciones Editar/Eliminar pasan a ser botones directos en la fila, en lugar del menú de tres puntos.
- **Confirmación de borrado**: los tres modales de "¿Estás seguro de que querés eliminar…?" (períodos, pedidos y listas del Portal) se unifican en un único componente en `shared/ui`. Todos pasan a mostrar el estado "eliminando" (botón con carga y Cancelar deshabilitado) y el mismo título de error.
- **Dependencia**: `@ars-docendi/ui` pasa de `release/v1.0.2` a `release/v1.0.3`.

No hay cambios breaking de API ni de datos.

## Capabilities

### New Capabilities

- `barra-superior`: contenido de la barra superior del shell. Solo muestra elementos funcionales.
- `encabezado-paginas`: convención de breadcrumb, título y meta en todas las pantallas, con los títulos definidos para cada una.
- `confirmacion-eliminar`: diálogo único de confirmación de borrado, con estado de carga y error, usado por todas las pantallas.
- `campo-periodo-portal`: selección de mes y año en los períodos del Portal (experiencia, educación, proyectos), con el mes independiente del año.

### Modified Capabilities

- `gestion-periodos`: las acciones de cada fila son botones visibles en lugar de un menú, y los campos de fecha del modal muestran un único ícono de calendario.

## Impact

- **Módulos**: solo frontend (`app/shell`, `shared/ui/PageHeader`, `shared/ui/ModalConfirmarEliminar`, y las features `portal`, `designaciones`, `aulas`, `tareas`, `usuarios`, `docentes` y `roles`). No se toca el backend, la API, los Contracts ni el grafo de dependencias.
- **Dependencia externa**: requiere un release `v1.0.3` de `Ars-Docendi/ui-lib`, con el fix del `DatePicker` y el nuevo `MonthYearPicker`, antes de poder bumpear la dependencia.
- **Tests**: hay que actualizar los tests de `Sidebar`, `TablaPeriodos` y `miPortal`, que usan textos o estructuras que cambian.
- **Docs**: design-specs de las pantallas afectadas en `docs/product/designs/` (invariante #12).
- **Rollback**: revertir el PR y volver la dependencia a `release/v1.0.2`. No hay migraciones.
