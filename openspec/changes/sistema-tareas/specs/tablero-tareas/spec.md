## ADDED Requirements

### Requirement: Orden del listado, por columna

El listado SHALL mostrarse ordenado por Fecha de Inicio ascendente (la más próxima primero) cuando el usuario no eligió ninguna columna manualmente. Cada columna del header MUST ser clickeable para ordenar el listado por esa columna, con un ciclo de 3 estados: primer click → ascendente; un segundo click sobre la misma columna → descendente; un tercer click sobre la misma columna → vuelve al orden por defecto (Fecha de Inicio ascendente), sin que ninguna columna quede marcada como activa. Un click sobre una columna distinta a la activa MUST reiniciar el ciclo en ascendente para la nueva columna. La columna y dirección de orden activas MUST indicarse visualmente en el header — mismo mecanismo (`Table.HeaderCell` del design system) que usa la Tabla de revisión de pedidos de Designaciones.

#### Scenario: Orden por defecto

- **WHEN** un usuario abre el listado de tareas por primera vez
- **THEN** las tareas se muestran ordenadas por Fecha de Inicio, de la más próxima a la más lejana

#### Scenario: Click en una columna ordena por ella

- **GIVEN** el listado en su orden por defecto
- **WHEN** un usuario hace click en el header "Título"
- **THEN** el listado se reordena alfabéticamente por Título, ascendente, y el header lo indica visualmente

#### Scenario: Un segundo click sobre la misma columna invierte el orden

- **GIVEN** el listado ordenado por Título ascendente (tras un primer click)
- **WHEN** el usuario hace click en el header "Título" otra vez
- **THEN** el listado se reordena por Título descendente

#### Scenario: Un tercer click sobre la misma columna vuelve al orden por defecto

- **GIVEN** el listado ordenado por Título descendente (tras dos clicks)
- **WHEN** el usuario hace click en el header "Título" una vez más
- **THEN** el listado vuelve a mostrarse ordenado por Fecha de Inicio ascendente, y ningún header queda marcado como activo

### Requirement: Listado único de tareas

El sistema SHALL ofrecer una única pantalla de listado de tareas (`/tareas`), la misma para todos los roles, con una tabla que MUST mostrar las columnas Nro de Tarea, Título, Autor, Responsable, Fecha Inicio, Fecha Fin, Prioridad, % Avance, Estado y Acciones (un botón "Ver" por fila que navega al detalle). La tabla MUST representar explícitamente los estados Loading, Empty, Error y Success.

#### Scenario: El listado muestra Autor y Responsable

- **GIVEN** una tarea creada por Secretaría con Responsable "G. Ruiz"
- **WHEN** se renderiza su fila en el listado
- **THEN** las columnas Autor y Responsable muestran "Secretaría" (o el nombre de quien la creó) y "G. Ruiz" respectivamente

#### Scenario: El listado muestra el porcentaje de avance

- **GIVEN** una tarea con 40% de avance
- **WHEN** se renderiza su fila en el listado
- **THEN** la columna % Avance muestra "40%"

#### Scenario: Cualquier rol ve la misma pantalla inicial

- **GIVEN** un usuario con rol Docente y otro con rol Secretaría
- **WHEN** cada uno abre `/tareas`
- **THEN** ambos ven la misma estructura de listado, con las mismas columnas

#### Scenario: Listado vacío

- **GIVEN** un usuario sin tareas visibles
- **WHEN** abre el listado de tareas
- **THEN** ve un estado vacío sin filas, sin romper la navegación

#### Scenario: Error al cargar el listado

- **WHEN** ocurre un error al obtener las tareas
- **THEN** se muestra un mensaje de error con opción de reintentar

#### Scenario: El botón Ver navega al detalle

- **WHEN** un usuario hace click en el botón "Ver" de una fila
- **THEN** navega a `/tareas/:id` de esa tarea

### Requirement: Semáforo de vencimiento en el listado

En el listado, el **fondo de toda la fila** de una tarea no terminal (Pendiente, En curso o Pausa) SHALL colorearse según el porcentaje del plazo transcurrido, calculado como `(hoy − Fecha Inicio) / (Fecha Fin − Fecha Inicio)`: sin resaltado (fondo normal) por debajo del 50% transcurrido, fondo amarillo entre 50% y 80% transcurrido, fondo rojo desde el 80% transcurrido en adelante (incluida una tarea ya vencida). Las tareas en estado Resuelta o Cancelada MUST NOT mostrar resaltado de semáforo.

#### Scenario: Tarea con menos de la mitad del plazo transcurrido no se resalta

- **GIVEN** una tarea En curso con Fecha Inicio hace 2 días y Fecha Fin dentro de 8 días (20% transcurrido)
- **WHEN** se renderiza su fila en el listado
- **THEN** la fila se muestra con el fondo normal, sin resaltado

#### Scenario: Tarea con más de la mitad del plazo transcurrido resalta la fila en amarillo

- **GIVEN** una tarea En curso con Fecha Inicio hace 6 días y Fecha Fin dentro de 4 días (60% transcurrido)
- **WHEN** se renderiza su fila en el listado
- **THEN** el fondo de toda la fila se muestra en amarillo

#### Scenario: Tarea con 80% o más del plazo transcurrido resalta la fila en rojo

- **GIVEN** una tarea En curso con Fecha Inicio hace 9 días y Fecha Fin dentro de 1 día (90% transcurrido)
- **WHEN** se renderiza su fila
- **THEN** el fondo de toda la fila se muestra en rojo

#### Scenario: Tarea vencida resalta la fila en rojo

- **GIVEN** una tarea Pendiente cuya Fecha Fin ya pasó
- **WHEN** se renderiza su fila
- **THEN** el fondo de toda la fila se muestra en rojo

#### Scenario: Tarea resuelta no muestra semáforo

- **GIVEN** una tarea en estado Resuelta cuya Fecha Fin ya pasó
- **WHEN** se renderiza su fila
- **THEN** la fila se muestra con el fondo normal, sin resaltado

### Requirement: Indicador visual de tareas en Pausa

En el listado, una tarea en estado Pausa SHALL destacarse visualmente en la columna Estado (badge/ícono distintivo), de forma que la autoridad creadora la note al revisar el listado.

#### Scenario: Tarea en pausa se distingue en el listado

- **GIVEN** una tarea en estado Pausa
- **WHEN** su creador abre el listado de tareas
- **THEN** la fila muestra el indicador distintivo de Pausa en la columna Estado

### Requirement: Filtros por columna, en el propio header

El listado SHALL ofrecer un filtro por cada columna de la tabla (Nro de Tarea, Título, Autor, Responsable, Fecha de Inicio, Fecha de Fin, Prioridad, % Avance, Estado), presentado como un ícono en el header de esa columna que abre un desplegable con el control de filtro — mismo componente (`FiltroEncabezado`) y mismo patrón que la Tabla de revisión de pedidos de Designaciones. No hay una fila de filtros generales separada de la tabla: todos los filtros viven en el header de su columna, cerrados por defecto. Los filtros MUST aplicarse sobre las tareas visibles sin recargar la página.

Nro de Tarea, Título, Fecha de Inicio y Fecha de Fin SHALL filtrar por texto libre: el usuario tipea, y el filtro compara contra el valor de la columna sin distinguir mayúsculas/acentos (para las fechas, contra el texto ya formateado dd/mm/aaaa). Autor, Responsable, Prioridad y Estado SHALL filtrar mediante checkboxes que permiten seleccionar varios valores a la vez; las opciones de Autor y Responsable se derivan de los valores presentes en las tareas visibles (no de un catálogo estático), igual que Designaciones deriva las opciones de sus columnas cerradas. % Avance SHALL filtrar por coincidencia exacta mediante un campo numérico.

Un header con un filtro activo MUST indicarlo visualmente (mismo indicador que usa `FiltroEncabezado`), y su menú MUST ofrecer una acción para limpiar ese filtro puntual.

#### Scenario: Filtrar por Título (texto libre)

- **WHEN** un usuario abre el filtro del header "Título" y escribe "aulas"
- **THEN** el listado muestra únicamente las tareas cuyo título contiene "aulas"

#### Scenario: Filtrar por un solo estado

- **WHEN** un usuario abre el filtro del header "Estado" y marca únicamente "Pausa"
- **THEN** el listado muestra únicamente las tareas en estado Pausa

#### Scenario: Filtrar por varios estados a la vez

- **WHEN** un usuario marca "Pendiente" y "En curso" en el filtro de Estado
- **THEN** el listado muestra las tareas en estado Pendiente o En curso, y oculta el resto

#### Scenario: Sin checkboxes marcados no filtra

- **GIVEN** el filtro de una columna de checkboxes (Autor, Responsable, Prioridad o Estado) sin ningún valor marcado
- **WHEN** se renderiza el listado
- **THEN** se muestran tareas de todos los valores de esa columna, sin acotar

#### Scenario: Las opciones de Responsable salen de las tareas visibles

- **GIVEN** un listado donde solo "G. Ruiz" y "M. Díaz" aparecen como Responsable de alguna tarea
- **WHEN** un usuario abre el filtro del header "Responsable"
- **THEN** el desplegable ofrece únicamente esas dos opciones, no el catálogo completo de personas candidatas

#### Scenario: Filtro sin resultados

- **WHEN** un usuario aplica un filtro que ninguna tarea cumple
- **THEN** el listado muestra un estado "Sin resultados" en vez de filas vacías

#### Scenario: Filtrar por Fecha de Inicio como texto

- **GIVEN** tareas con Fecha de Inicio 05/03/2026 y 10/04/2026
- **WHEN** un usuario escribe "03/2026" en el filtro del header "Inicio"
- **THEN** el listado muestra únicamente la tarea con Fecha de Inicio 05/03/2026

#### Scenario: Filtrar por % Avance exacto

- **GIVEN** tareas con 20%, 50% y 90% de avance
- **WHEN** un usuario ingresa 50 en el filtro del header "% Avance"
- **THEN** el listado muestra únicamente la tarea con 50% de avance
