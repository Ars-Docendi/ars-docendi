## Why

Probando las grillas con volumen real (July), aparecieron cuatro problemas que afectan sobre todo a usuarios con pantallas chicas y poca experiencia tecnológica:

- La paginación es inconsistente: solo Mis pedidos pagina (9 filas, con "Anterior / Siguiente"); las otras cuatro grillas muestran todo.
- Las columnas que no entran en el ancho quedan **cortadas**: `.adoc-table-wrap` de la ui-lib tiene `overflow: hidden`. Mis pedidos y Revisión lo parchearon cada una por su lado; Períodos, Usuarios y Docentes no.
- Para operar una fila hay que apuntar a un botón chico, y en dos grillas eliminar es una X sin texto, ambigua para este público.

## What Changes

- **Sin paginación**: todas las grillas muestran todas las filas. Se borra el paginador propio de Mis pedidos. Filtrar y ordenar por encabezado siguen siendo la forma de acotar.
- **Scroll dentro de la tabla**: la tabla ocupa el alto disponible, scrollea en vertical y en horizontal dentro de su contenedor, con el **encabezado fijo**. Ninguna columna queda cortada.
- **Click en la fila = acción principal**: abre el detalle si la grilla lo tiene (y se quita el botón "Ver", que repetía lo mismo); si no, abre Editar. La fila también se abre con el teclado. Solo si el usuario tiene permiso para esa acción. Los botones y links de la fila no disparan el click, y seleccionar texto tampoco.
- **"Eliminar" con texto**: la X roja de Mis pedidos y Períodos pasa a ser un botón de texto "Eliminar".
- **Estados más cortos**: "En revisión · Secretaría" pasa a "En Secretaría" (y lo mismo con Coordinación y Decanato) en Mis pedidos, el detalle, el filtro de Revisión y los avisos. Estar en un área ya implica estar en revisión; los nombres coinciden con las pestañas de Revisión.
- **ui-lib v1.0.4**: el scroll y el encabezado fijo se implementan en `Table` de `@ars-docendi/ui`. Dev bumpea la dependencia.

No hay cambios de API ni de datos.

## Capabilities

### New Capabilities

- `grillas-datos`: comportamiento común de las grillas de datos (sin paginación, scroll interno con encabezado fijo, click en la fila, acciones con texto).

### Modified Capabilities

- `pedidos-designacion`: el requirement de filtros de Mis pedidos deja de referirse a la paginación.

## Impact

- **Frontend**: `TablaMisPedidos` / `MisPedidosPage`, `TablaRevision`, `TablaPeriodos`, `TablaUsuarios`, `TablaDocentes`, y el CSS de los parches actuales (`misPedidos.css`, `revision.css`).
- **Dependencia externa**: release `v1.0.4` de `Ars-Docendi/ui-lib` (cambios en `Table` y `components.css`).
- **Sin cambios** en backend, API, Contracts ni en el grafo de dependencias.
- **Docs**: design-spec de Designaciones (patrón transversal de acciones por fila y tablas) y, si aplica, el de administración de usuarios/docentes.
- **Rollback**: revertir el PR y volver la dependencia a `release/v1.0.3`.
