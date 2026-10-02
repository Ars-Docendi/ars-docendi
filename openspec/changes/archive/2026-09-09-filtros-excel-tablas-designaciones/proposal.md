## Why

Las tablas de Designaciones todavía mezclan filtros generales, búsquedas en toolbar y ordenamientos implícitos, mientras que Usuarios y Docentes ya ofrecen una interacción consistente por encabezado, similar a Excel. Extender ese patrón reduce el tiempo para localizar pedidos y períodos sin cambiar la autoridad del backend ni los datos cargados.

## What Changes

- Incorporar filtros y ordenamiento por encabezado en `Mis pedidos`.
- Incorporar filtros por encabezado en la tabla de `Revisión`, conservando como filtros generales los criterios que no representan columnas visibles: Período, Carrera, Prioridad y Sin movimiento.
- Mantener el ordenamiento existente de `Revisión`, con ciclo ascendente, descendente y orden predeterminado por pestaña.
- Incorporar filtros y ordenamiento por encabezado en `Períodos de designación` para Nombre, fechas y Activo.
- Aplicar los filtros antes de la paginación de `Mis pedidos` y antes de calcular filas/contadores de `Revisión`.
- Mantener la columna Acciones sin filtro ni ordenamiento.
- Mantener filtros sin distinguir mayúsculas ni tildes, selección múltiple con OR dentro de una columna y combinación AND entre columnas.
- Mantener las restricciones de permisos, ámbito, acciones de fila y estados Loading/Error/Empty/Success.
- Actualizar la design spec de UX y las specs funcionales delta.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `pedidos-designacion`: agrega filtros y ordenamiento por encabezado a la lista `Mis pedidos`, sin alterar navegación ni acciones permitidas.
- `tablero-revision-tabla`: agrega filtros por encabezado para las columnas visibles y conserva los filtros generales no representados como columnas.
- `gestion-periodos`: agrega filtros y ordenamiento por encabezado a la tabla de períodos.

## Impact

- **Frontend:** componentes y helpers de `frontend/src/features/designaciones`, reutilizando `shared/ui/FiltroEncabezado` y el soporte de ordenamiento de `@ars-docendi/ui`.
- **UX/documentación:** actualización de `docs/product/designs/proyecto-docente-design-spec.md` para documentar el patrón transversal en las tablas de Designaciones.
- **Backend/API/datos:** sin cambios; el filtrado y ordenamiento continúan siendo locales sobre los datos ya autorizados y cargados.
- **Dependencias y grafo:** sin cambios; no se agregan paquetes ni referencias entre módulos.
- **Rollback:** revertir los cambios del frontend y la documentación restaura los filtros actuales de toolbar/lista y el ordenamiento existente; no requiere migraciones ni recuperación de datos.
