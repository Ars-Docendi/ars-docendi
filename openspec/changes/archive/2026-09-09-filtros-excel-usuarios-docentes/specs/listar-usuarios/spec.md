## REMOVED Requirements

### Requirement: Barra de filtros en dos filas

**Reason**: La barra separada se reemplaza por controles asociados a cada encabezado, con una interacción más cercana a una tabla de Excel.
**Migration**: Los criterios de Apellido, Nombre, Documento, Legajo, Mail/UPN, Rol, Perfil docente y Estado se encuentran en los menús de filtro de sus respectivas columnas.

## ADDED Requirements

### Requirement: Filtros por encabezado en la tabla de usuarios

La página SHALL ofrecer un control de filtro accesible en cada encabezado filtrable de la tabla de Usuarios. Los encabezados filtrables SHALL ser Apellido y Nombre, Documento, Legajo, UPN/Email, Roles, Perfil docente y Estado. La columna Acciones MUST NOT ofrecer filtro.

El menú de una columna textual SHALL permitir buscar por coincidencia parcial sin distinguir mayúsculas ni tildes. El menú de una columna categórica SHALL permitir seleccionar uno o más valores presentes en los datos cargados, incluyendo la opción de valores sin dato cuando corresponda. Los filtros de distintas columnas SHALL combinarse con lógica AND.

#### Scenario: Abrir el filtro de una columna

- **WHEN** el operador activa el control de filtro del encabezado "Roles"
- **THEN** se muestra un menú asociado a ese encabezado con los roles disponibles y una acción para limpiar el filtro

#### Scenario: Buscar una columna textual

- **WHEN** el operador escribe "lopez" en el filtro de Apellido y Nombre
- **THEN** la tabla muestra sólo usuarios cuyo apellido o nombre normalizado contiene "lopez"

#### Scenario: Seleccionar valores de una columna categórica

- **WHEN** el operador selecciona "Con perfil docente" en el filtro de Perfil docente
- **THEN** la tabla muestra sólo usuarios con `perfilDocente.esDocente = true`

#### Scenario: Combinar filtros de encabezados

- **WHEN** el operador filtra Estado por "Activo" y Roles por "Docente"
- **THEN** la tabla muestra sólo usuarios que cumplen ambos criterios

#### Scenario: Limpiar un filtro de encabezado

- **WHEN** el operador activa "Limpiar filtro" en una columna filtrada
- **THEN** el criterio de esa columna se elimina y los resultados se recalculan conservando los filtros de las demás columnas

#### Scenario: Cerrar un menú sin perder filtros

- **WHEN** el operador cierra el menú de una columna que ya tiene un filtro aplicado
- **THEN** el filtro continúa visible mediante un indicador del encabezado y sigue afectando la tabla

### Requirement: Ordenamiento por encabezado en la tabla de usuarios

La tabla SHALL permitir ordenar desde los encabezados Apellido y Nombre, Documento, Legajo, UPN/Email y Estado. El orden SHALL aplicarse únicamente sobre las filas que cumplen los filtros activos. La columna Acciones MUST NOT ser ordenable.

Cada encabezado ordenable SHALL alternar entre orden ascendente, descendente y sin orden manual. Al quitar el orden manual, la tabla SHALL recuperar su orden predeterminado por Apellido y Nombre.

#### Scenario: Ordenar usuarios por apellido

- **WHEN** el operador activa el orden ascendente del encabezado Apellido y Nombre
- **THEN** las filas visibles se presentan por apellido y nombre en orden alfabético, sin distinguir tildes

#### Scenario: Ordenar legajos numéricos

- **WHEN** el operador activa el orden ascendente del encabezado Legajo
- **THEN** los legajos se ordenan por valor numérico cuando corresponda, y no por comparación lexicográfica

#### Scenario: Quitar el orden manual

- **WHEN** el operador activa por tercera vez el encabezado que está ordenado en sentido descendente
- **THEN** el indicador de orden desaparece y la tabla vuelve al orden predeterminado

#### Scenario: Ordenar después de filtrar

- **WHEN** existen filtros activos y el operador ordena una columna
- **THEN** sólo se reordenan las filas resultantes, sin recuperar filas excluidas por los filtros

### Requirement: Accesibilidad de los controles de encabezado

Los controles de filtro y ordenamiento SHALL poder operarse con teclado y SHALL comunicar su estado al lector de pantalla. Abrir o cerrar un menú de encabezado MUST NOT activar acciones de una fila.

#### Scenario: Estado de orden accesible

- **WHEN** una columna queda ordenada en sentido ascendente o descendente
- **THEN** el encabezado comunica su dirección de orden al lector de pantalla

#### Scenario: Operación por teclado

- **WHEN** el operador enfoca el control de filtro y presiona Enter o Espacio
- **THEN** se abre el menú de esa columna sin requerir el uso del mouse
