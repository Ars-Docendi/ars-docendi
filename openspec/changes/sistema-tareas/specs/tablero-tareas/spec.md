## ADDED Requirements

### Requirement: Orden del listado, por columna

El listado SHALL mostrarse ordenado por Fecha de Inicio ascendente (la más próxima primero) por defecto. Cada columna del header MUST ser clickeable para ordenar el listado por esa columna; un click sobre la columna ya activa MUST alternar entre ascendente y descendente, y un click sobre una columna distinta MUST pasar a ordenar por esa columna en orden ascendente. La columna y dirección de orden activas MUST indicarse visualmente en el header.

#### Scenario: Orden por defecto

- **WHEN** un usuario abre el listado de tareas por primera vez
- **THEN** las tareas se muestran ordenadas por Fecha de Inicio, de la más próxima a la más lejana

#### Scenario: Click en una columna ordena por ella

- **GIVEN** el listado ordenado por Fecha de Inicio
- **WHEN** un usuario hace click en el header "Título"
- **THEN** el listado se reordena alfabéticamente por Título, ascendente, y el header lo indica visualmente

#### Scenario: Un segundo click sobre la misma columna invierte el orden

- **GIVEN** el listado ordenado por Título ascendente (tras un primer click)
- **WHEN** el usuario hace click en el header "Título" otra vez
- **THEN** el listado se reordena por Título descendente

### Requirement: Listado único de tareas

El sistema SHALL ofrecer una única pantalla de listado de tareas (`/tareas`), la misma para todos los roles, con una tabla que MUST mostrar las columnas Nro de Tarea, Título, Autor, Responsable, Fecha Inicio, Fecha Fin, Prioridad, % Avance y Estado. La tabla MUST representar explícitamente los estados Loading, Empty, Error y Success.

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

### Requirement: Filtros del listado cubren todas las columnas

El listado SHALL ofrecer un filtro por cada columna de la tabla: Nro de Tarea, Título, Autor, Responsable, Fecha de Inicio, Fecha de Fin, Prioridad, % Avance y Estado, reutilizando el componente `FiltrosLista` ya usado en Designaciones. Nro de Tarea, Responsable y Título MUST estar siempre visibles por defecto; Autor, Fecha de Inicio, Fecha de Fin, Prioridad, % Avance y Estado MUST estar ocultos por defecto y agregarse mediante "+ Añadir filtro", de forma que no todos los filtros estén presentes al inicio. Los filtros MUST aplicarse sobre las tareas visibles sin recargar la página.

Los filtros de Fecha de Inicio y Fecha de Fin SHALL filtrar por "hasta esta fecha" (inclusive): al ingresar una fecha, el listado muestra solo las tareas cuya Fecha de Inicio (o Fecha de Fin, según el filtro) sea anterior o igual a la fecha elegida. El filtro de % Avance SHALL filtrar por coincidencia exacta: al ingresar un valor, el listado muestra solo las tareas cuyo `porcentajeAvance` sea igual a ese valor.

El filtro de Responsable SHALL presentarse como un campo de búsqueda: el usuario tipea texto, ve una lista de candidatos que coinciden, y selecciona uno para aplicar el filtro — no un `<select>` desplegable tradicional ni un campo de texto libre.

El filtro de Estado SHALL permitir seleccionar **varios estados a la vez** (Pendiente, En curso, Pausa, Resuelta, Cancelada) mediante checkboxes en un desplegable — no un `<select>` de una sola opción. Sin ningún estado seleccionado, el filtro no acota el listado (equivale a "todos").

#### Scenario: Filtrar por un solo estado

- **WHEN** un usuario agrega el filtro Estado y marca únicamente "Pausa"
- **THEN** el listado muestra únicamente las tareas en estado Pausa

#### Scenario: Filtrar por varios estados a la vez

- **WHEN** un usuario agrega el filtro Estado y marca "Pendiente" y "En curso"
- **THEN** el listado muestra las tareas en estado Pendiente o En curso, y oculta el resto

#### Scenario: Sin estados marcados no filtra

- **GIVEN** el filtro Estado agregado pero sin ningún checkbox marcado
- **WHEN** se renderiza el listado
- **THEN** se muestran tareas de todos los estados, sin acotar

#### Scenario: Filtro sin resultados

- **WHEN** un usuario aplica un filtro que ninguna tarea cumple
- **THEN** el listado muestra un estado "Sin resultados" en vez de filas vacías

#### Scenario: Filtrar por Fecha de Inicio "hasta"

- **GIVEN** tareas con Fecha de Inicio 2026-03-01, 2026-03-10 y 2026-03-20
- **WHEN** un usuario agrega el filtro Fecha de Inicio y elige 2026-03-10
- **THEN** el listado muestra las tareas con Fecha de Inicio 2026-03-01 y 2026-03-10, y oculta la del 2026-03-20

#### Scenario: Filtrar por % Avance exacto

- **GIVEN** tareas con 20%, 50% y 90% de avance
- **WHEN** un usuario agrega el filtro % Avance e ingresa 50
- **THEN** el listado muestra únicamente la tarea con 50% de avance

#### Scenario: Buscar y seleccionar un Responsable en el filtro

- **GIVEN** tareas con distintos Responsables, entre ellos "G. Ruiz"
- **WHEN** un usuario escribe "Ruiz" en el filtro Responsable y selecciona "G. Ruiz" de la lista de resultados
- **THEN** el listado muestra únicamente las tareas cuyo Responsable es "G. Ruiz"

#### Scenario: Filtros por defecto: Nro de Tarea, Responsable y Título

- **WHEN** un usuario abre el listado de tareas por primera vez
- **THEN** ve visibles los campos Nro de Tarea, Responsable y Título; Autor, Fecha de Inicio, Fecha de Fin, Prioridad, % Avance y Estado no están visibles hasta agregarlos con "+ Añadir filtro"

### Requirement: Guardado de configuraciones de filtros

El sistema SHALL permitir guardar la combinación actual de filtros (los siempre visibles y los opcionales agregados, con todos sus valores) con un nombre elegido por el usuario, asociada a ese usuario, y volver a aplicarla en una visita posterior mediante un selector de configuraciones guardadas.

#### Scenario: Guardar una configuración de filtros

- **GIVEN** un usuario que puso Responsable="G. Ruiz" y agregó el filtro Estado="Pausa"
- **WHEN** guarda esa configuración con el nombre "Mis pausas"
- **THEN** la configuración queda disponible para ese usuario en el selector de configuraciones guardadas

#### Scenario: Aplicar una configuración guardada

- **GIVEN** un usuario con la configuración guardada "Mis pausas" (Responsable="G. Ruiz", Estado="Pausa")
- **WHEN** la selecciona desde el selector de configuraciones guardadas
- **THEN** el listado completa Responsable="G. Ruiz" y agrega y completa el filtro Estado="Pausa" tal como se guardaron

#### Scenario: Las configuraciones guardadas son por usuario

- **GIVEN** dos usuarios distintos, cada uno con sus propias configuraciones guardadas
- **WHEN** cada uno abre el selector de configuraciones guardadas
- **THEN** cada uno ve únicamente las configuraciones que guardó él mismo
