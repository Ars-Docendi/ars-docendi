## Why

Las tablas de Usuarios y Docentes obligan a buscar los criterios en una barra de filtros separada de las columnas que afectan. Esto dificulta la lectura de listas administrativas y no ofrece la interacción esperada en una tabla similar a Excel, donde cada encabezado permite ordenar y filtrar su propia columna.

La información ya se carga completa y se filtra en memoria en el frontend, y el design system ya soporta encabezados ordenables. Por eso se puede mejorar la interacción sin modificar APIs ni persistencia.

## What Changes

- Reemplazar las barras de filtros superiores de Usuarios y Docentes por controles de filtro asociados a los encabezados de las tablas.
- Permitir ordenar desde cada encabezado aplicable, con orden ascendente, descendente y limpieza del orden manual.
- Incorporar en el menú de cada encabezado opciones de orden, búsqueda textual o selección de valores según el tipo de columna, y limpieza del filtro.
- Conservar los criterios funcionales actuales: búsqueda sin distinguir mayúsculas ni tildes, filtros por pertenencia para roles/asignaciones y combinación de filtros con lógica AND.
- Filtrar Usuarios por Apellido y Nombre, Documento, Legajo, UPN/Email, Roles, Perfil docente y Estado.
- Filtrar Docentes por Apellido y Nombre, Documento, Legajo, Rol, Ámbitos, Asignaciones, Cuenta y Estado.
- Mantener la columna Acciones sin ordenamiento ni filtro.
- Mantener el conteo de resultados, el estado vacío y el acceso restringido existentes.
- Actualizar la documentación de diseño de UX para reflejar el nuevo patrón de filtros por encabezado.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `listar-usuarios`: reemplaza la barra de filtros en dos filas por filtros y ordenamiento asociados a los encabezados de la tabla.
- `listar-docentes`: reemplaza los filtros fijos y opcionales separados por filtros y ordenamiento asociados a los encabezados de la tabla.

## Impact

- **Frontend:** `features/usuarios` y `features/docentes`, incluyendo páginas, tablas, estado de filtros, helpers de ordenamiento y tests.
- **Design system:** se reutiliza `Table.HeaderCell` para ordenamiento; el menú de filtro se resolverá en el frontend sin agregar una dependencia.
- **Documentación:** se actualizará `docs/product/designs/proyecto-docente-design-spec.md` en el patrón de listas filtrables.
- **Backend/API/datos:** sin cambios; los endpoints actuales continúan entregando el listado completo y el filtrado permanece en el cliente.
- **Dependencias y grafo:** sin cambios.
- **Rollback:** revertir los cambios del frontend y de la design spec restaura las barras de filtros actuales; no requiere migración ni rollback de datos.
