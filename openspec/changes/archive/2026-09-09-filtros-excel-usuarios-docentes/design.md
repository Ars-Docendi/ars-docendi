## Context

Las páginas `/usuarios` y `/docentes` reciben el listado completo desde sus hooks de React Query. Actualmente cada página conserva un estado de filtros, aplica un `useMemo` sobre los datos y pasa las filas resultantes a su tabla. Los filtros visuales viven en `FiltrosUsuarios` y `FiltrosDocentes`.

El design system ya expone `Table.HeaderCell` con `sort`, `onSortChange` y `aria-sort`. No expone un menú de filtros ni un popover. Ver la motivación y el alcance en `proposal.md`, y los contratos de comportamiento en las specs delta.

## Goals / Non-Goals

**Goals:**

- Reemplazar la barra superior de filtros por menús asociados a los encabezados de Usuarios y Docentes.
- Mantener los predicados actuales de búsqueda y extenderlos sólo para representar los filtros de las columnas nuevas.
- Compartir la interacción visual y accesible del menú sin compartir lógica de dominio entre Usuarios y Docentes.
- Aplicar filtros y orden sobre el conjunto ya autorizado y cargado en el cliente.
- Mantener el conteo de resultados, la navegación a edición y las restricciones de la vista de Jefe de Cátedra.

**Non-Goals:**

- No agregar filtrado, ordenamiento, paginación ni búsqueda en los endpoints.
- No cambiar DTOs, permisos, contratos, consultas de identidad ni datos persistidos.
- No convertir `Table` en una tabla genérica ni agregar un paquete de componentes.
- No ordenar por columnas cuyo valor visible sea un resumen de colección, como Roles, Ámbitos o Asignaciones.
- No modificar el patrón de filtros de otras pantallas que usan `FiltrosLista`.

## Decisions

### Estado y lógica permanecen en cada feature

Cada `IndexPage` seguirá siendo dueño del estado serializable de filtros y orden, y calculará las filas visibles a partir de los datos recibidos. La tabla recibirá los valores actuales y callbacks para modificar ese estado.

La lógica pura de cada feature se separará en helpers pequeños para:

- aplicar filtros a Usuarios o Docentes;
- comparar textos normalizados y legajos numéricos;
- ordenar las filas visibles;
- construir las opciones de filtros a partir de datos ya autorizados.

Esto permite que el `PageHeader` siga mostrando el conteo correcto sin callbacks de conteo inversos desde la tabla y permite probar la lógica sin montar la página completa.

### Control visual compartido, predicados específicos

Se agregará en `shared/ui` un control pequeño para el encabezado filtrable. El control resolverá únicamente:

- botón de filtro y estado activo;
- apertura/cierre del menú;
- cierre con Escape y al hacer clic fuera;
- retorno del foco al botón disparador;
- posicionamiento del menú y estilos comunes;
- campos nativos de texto, checkbox y acciones del menú.

Usuarios y Docentes decidirán qué tipo de campo, opciones y callbacks recibe cada columna. No se compartirá una interfaz de dominio ni se mezclará la interpretación de roles, membresías o asignaciones.

Se reutilizarán controles nativos y los componentes ya instalados de UI. No se agregará una dependencia de popover, tabla o filtros.

### Ordenamiento separado del botón de filtro

Los encabezados simples usarán el soporte existente de `Table.HeaderCell` para el ordenamiento y su `aria-sort`. El botón de filtro será un control adyacente dentro del encabezado y detendrá la propagación de su evento para no disparar el orden al abrir el menú.

El ciclo de orden será ascendente, descendente y sin orden manual. Al volver a sin orden manual se conservará el orden predeterminado recibido por la API: Apellido y Nombre.

Los comparadores usarán texto normalizado para nombres y etiquetas, comparación numérica para legajos cuando ambos valores sean numéricos y un desempate estable por identificador. Los filtros se aplicarán antes del ordenamiento.

### Opciones y semántica de filtros

- Usuarios usará coincidencia textual para Apellido y Nombre, Documento, Legajo y UPN/Email; selección de valores para Roles, Perfil docente y Estado.
- Docentes usará coincidencia textual para Apellido y Nombre, Documento y Legajo; selección de valores para Rol, Ámbitos, Cuenta y Estado; y búsqueda combinada por código/nombre de materia o cargo en Asignaciones.
- Las opciones categóricas se derivarán del listado visible y de catálogos que ya respetan el ámbito autorizado. Nunca se mostrarán opciones de datos que la API no haya entregado.
- Un filtro de columna se aplicará inmediatamente al cambiarlo. Los filtros de columnas distintas se combinarán con AND; varias selecciones dentro de una misma columna se combinarán con OR.
- Cada menú tendrá una acción explícita para limpiar sólo su propia columna. El indicador del encabezado permanecerá activo mientras el filtro tenga un valor no vacío.

### Menús dentro de una tabla con scroll

La tabla conservará su scroll horizontal. Para evitar que un menú abierto quede recortado por el contenedor desplazable, el control calculará su posición a partir del rectángulo del botón y renderizará el menú en una capa fuera del contenido de la tabla, ajustándolo a los límites del viewport.

El menú usará una superficie semántica con campos etiquetados, controles de teclado y foco manejado explícitamente. Abrirlo no debe activar botones de acciones ni cambiar el orden.

### Eliminación de la barra existente

Cuando la tabla adopte los menús de encabezado, `FiltrosUsuarios` y `FiltrosDocentes` dejarán de ser consumidos y se eliminarán para no conservar dos caminos de filtrado. `FiltrosLista` permanece sin cambios porque otras superficies lo usan.

## Risks / Trade-offs

- **[Menús recortados o mal posicionados en viewport pequeño]** → Renderizar fuera del contenedor de la tabla, ajustar contra el viewport y conservar el scroll horizontal.
- **[Sobrecarga visual en tablas anchas]** → Usar controles compactos, mostrar indicador sólo cuando hay filtro activo y mantener la columna Acciones sin controles.
- **[Ambigüedad en columnas con colecciones]** → Permitir filtro por pertenencia en Rol, Ámbitos y Asignaciones, pero dejar su ordenamiento fuera de alcance.
- **[Volumen creciente de datos]** → Mantener esta primera versión local porque los endpoints actuales entregan listados completos; migrar a filtros/paginación del servidor sólo si el volumen medido vuelve insuficiente la carga completa.
- **[Regresión de accesibilidad]** → Cubrir teclado, `aria-sort`, nombre accesible, cierre con Escape y foco en tests de componentes.

## Migration Plan

1. Añadir el control visual compartido y los helpers puros de Usuarios y Docentes.
2. Migrar cada página para conservar sus datos remotos, reemplazar el estado de filtros y pasar el estado a la tabla.
3. Reemplazar los encabezados y quitar las barras de filtro específicas de ambas features.
4. Actualizar tests de componentes y helpers, y actualizar la design spec de UX.
5. Ejecutar las verificaciones frontend y OpenSpec proporcionales al cambio.

El rollback consiste en revertir los cambios frontend y la actualización de la design spec. No hay migración de base, cambios de API ni pasos de recuperación de datos.
