## Context

Las tres superficies de Designaciones ya cargan sus datos mediante hooks de React Query y aplican filtros en memoria. `Usuarios` y `Docentes` sirven como referencia: cada página conserva el estado de filtros/orden, los helpers contienen la lógica pura y `FiltroEncabezado` resuelve la interacción visual y accesible. `Table.HeaderCell` ya expone ordenamiento y `aria-sort`.

`Mis pedidos` todavía usa un grid con roles ARIA propios; `Revisión` usa `Table` con ordenamiento por columna y filtros generales; `Períodos` usa `Table` con búsqueda y estado en un toolbar. No se modifican APIs, permisos, contratos, persistencia ni el alcance de los datos devueltos por backend.

## Goals / Non-Goals

**Goals:**

- Unificar la interacción de las tres tablas de Designaciones con el patrón de encabezados de Usuarios/Docentes.
- Mantener filtrado local sobre datos ya autorizados, con filtros textuales normalizados, opciones múltiples y ordenamiento estable.
- Aplicar filtros antes de ordenar y, en `Mis pedidos`, antes de paginar.
- Conservar los filtros generales de Revisión que no representan columnas visibles.
- Mantener navegación, acciones de fila, permisos, estados vacíos y comportamiento por pestañas.
- Cubrir la lógica y la interacción con tests proporcionales al cambio.

**Non-Goals:**

- No agregar filtros ni ordenamiento al backend, a la API o a la base de datos.
- No crear una abstracción genérica de DataTable ni un modelo compartido de columnas.
- No modificar la lista de Roles, las listas del Portal ni las pantallas Aulas/Tareas en construcción.
- No convertir los filtros generales de Período, Carrera, Prioridad o Sin movimiento en columnas ficticias.
- No agregar paginación nueva a Revisión o Períodos.

## Decisions

### 1. Reutilizar el control existente y mantener la lógica específica por feature

Cada tabla compondrá `FiltroEncabezado` dentro de sus encabezados y usará `Table.HeaderCell` para ordenar. El contenido del menú seguirá siendo propio de la feature: pedidos, estados y fechas tienen semánticas distintas y no justifican una interfaz de dominio común.

Se descarta crear una abstracción `DataTable` configurable: agregaría indirección sin una segunda implementación que la necesite. También se descarta agregar un paquete de popovers; el portal, el posicionamiento fuera del scroll, Escape, clic fuera y foco ya están resueltos por `FiltroEncabezado`.

### 2. Estado controlado donde ya existe paginación o filtros de pantalla

- `MisPedidosPage` seguirá siendo dueña de filtros, orden y página. La tabla recibirá el estado y callbacks; los resultados se filtrarán, ordenarán y recién después se paginarán.
- `TablaRevision` recibirá un callback adicional para actualizar los filtros de columna. `TableroRevisionPage` seguirá siendo dueño de los filtros generales y eliminará del toolbar sólo los criterios que pasen a encabezados: Nombre/Docente, Tipo, Legajo y Estado.
- `TablaPeriodos` puede conservar el estado local que ya posee porque no hay paginación ni meta dependiente de resultados filtrados. Su cálculo se extraerá a una función pura para probarlo sin montar la pantalla.

### 3. Migrar Mis pedidos a la tabla nativa del design system

`TablaMisPedidos` pasará del grid de `div` con roles ARIA a `Table.Root`, `Table.Head`, `Table.Body`, `Table.Row` y `Table.Cell`. Así comparte el soporte real de ordenamiento y su `aria-sort`, evitando duplicar semántica de tabla y controles de teclado. La fila seguirá siendo seleccionable con click/Enter/Espacio; los botones Ver, Editar y Eliminar continuarán deteniendo la propagación.

La migración conservará las clases visuales necesarias y no agregará una columna Prioridad que no existe en la tabla actual.

### 4. Criterios por tabla

- **Mis pedidos:** texto en N°, Docente, Legajo, Cátedra y Enviado; selección múltiple en Tipo y Estado; orden en todas esas columnas salvo Acciones. Las fechas se ordenan por el instante subyacente y se muestran con el formato vigente.
- **Revisión:** filtros en Docente, Legajo, Tipo, Inicio, Últ. actualización y Estado; Área sólo cuando la pestaña Todos la muestra. Período, Carrera, Prioridad y Sin movimiento permanecen en `FiltrosLista` porque no son columnas visibles. Los contadores se calculan luego de combinar todos los criterios.
- **Períodos:** texto en Nombre y fechas, selección múltiple en Activo; orden cronológico en las cuatro fechas y orden textual/categórico en Nombre y Activo. Acciones queda fuera.

En todas las tablas, el texto se compara normalizado sin mayúsculas ni diacríticos, los valores múltiples dentro de una columna usan OR y las columnas diferentes usan AND. Los valores de opciones se derivan de los datos ya cargados y autorizados.

### 5. Orden predeterminado y valores vacíos

El ciclo de orden será ascendente -> descendente -> sin orden manual. Al quitarlo, cada pantalla recuperará su orden natural vigente: orden de origen/paginado para Mis pedidos, orden por pestaña de Revisión y orden predeterminado por impacto descendente en Períodos. Los valores vacíos se mantendrán agrupados de forma consistente con los comparadores existentes; los empates usarán un identificador estable cuando el modelo lo permita.

### 6. Verificación y documentación

Se agregarán tests de helpers para predicados, comparadores y ciclo de orden, además de tests de componentes para abrir filtros, limpiar, combinar criterios, aplicar orden después del filtro y mantener acciones/teclado. Se actualizará `docs/product/designs/proyecto-docente-design-spec.md` y se ejecutará la verificación frontend y OpenSpec definida por el repositorio.

## Risks / Trade-offs

- **Migración visual de Mis pedidos** -> conservar clases y verificar click de fila, foco, paginación y botones para evitar regresiones de UX.
- **Menús dentro de tablas con scroll** -> reutilizar el portal y posicionamiento de `FiltroEncabezado`; no renderizar el menú dentro del contenedor desplazable.
- **Filtros duplicados en Revisión** -> eliminar del toolbar sólo Nombre, Tipo, Legajo y Estado; mantener explícitamente Período, Carrera, Prioridad y Sin movimiento.
- **Fechas mostradas como texto** -> filtrar por el valor visible, pero ordenar y comparar fechas usando el ISO/epoch subyacente.
- **Datos crecientes** -> mantener la solución local porque las APIs actuales entregan el listado completo; migrar a filtrado/paginación server-side sólo con evidencia de volumen o rendimiento insuficiente.

## Migration Plan

1. Extender los modelos/helpers de filtros y orden de cada feature, junto con sus tests.
2. Migrar `TablaMisPedidos` a `Table` e integrar encabezados filtrables, preservando la navegación y paginación.
3. Integrar filtros de encabezado en Revisión y ajustar el toolbar para conservar sólo los filtros generales sin columna.
4. Integrar filtros y orden en Períodos y retirar su toolbar duplicado.
5. Actualizar tests, design spec y validar formato, lint, build y OpenSpec.

El rollback consiste en revertir el frontend y la documentación de este change. No hay migraciones de datos, cambios de API ni pasos de recuperación.
