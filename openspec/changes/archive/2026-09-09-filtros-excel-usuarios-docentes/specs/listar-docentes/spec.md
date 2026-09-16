## REMOVED Requirements

### Requirement: Filtros fijos por Apellido, Nombre y Documento

**Reason**: Los filtros de texto dejan de vivir en una barra superior y pasan a estar asociados a los encabezados de las columnas.
**Migration**: Los filtros de Apellido, Nombre y Documento se encuentran en los menús de filtro de sus respectivas columnas.

### Requirement: Filtros opcionales añadibles incluido Rol

**Reason**: El selector "+ Añadir filtro…" y sus controles opcionales se reemplazan por menús de filtro por columna.
**Migration**: Los criterios de Código de materia, Materia, Cargo, Rol, Cuenta y Estado se encuentran en los menús de las columnas relacionadas.

## ADDED Requirements

### Requirement: Filtros por encabezado en la tabla de docentes

La página SHALL ofrecer un control de filtro accesible en cada encabezado filtrable de la tabla de Docentes. Los encabezados filtrables SHALL ser Apellido y Nombre, Documento, Legajo, Rol, Ámbitos, Asignaciones, Cuenta y Estado. La columna Acciones MUST NOT ofrecer filtro, y la vista de solo lectura de Jefe de Cátedra SHALL conservar sus restricciones actuales.

El menú de una columna textual SHALL permitir buscar por coincidencia parcial sin distinguir mayúsculas ni tildes. El filtro de Rol SHALL permitir seleccionar roles docentes. El filtro de Ámbitos SHALL permitir seleccionar uno o más tipos de ámbito presentes en las membresías. El filtro de Asignaciones SHALL permitir buscar por código o nombre de materia y por cargo. El filtro de Cuenta SHALL distinguir entre "Con cuenta" y "Sin cuenta"; el filtro de Estado SHALL distinguir entre "Activo" e "Inactivo". Los filtros de distintas columnas SHALL combinarse con lógica AND.

#### Scenario: Abrir el filtro de una columna

- **WHEN** el operador activa el control de filtro del encabezado "Asignaciones"
- **THEN** se muestra un menú asociado a ese encabezado con búsqueda de materia o cargo y una acción para limpiar el filtro

#### Scenario: Buscar docentes por apellido sin tilde

- **WHEN** el operador escribe "lopez" en el filtro de Apellido y Nombre
- **THEN** la tabla muestra sólo docentes cuyo apellido o nombre normalizado contiene "lopez"

#### Scenario: Filtrar por rol

- **WHEN** el operador selecciona "Jefe de Cátedra" en el filtro de Rol
- **THEN** la tabla muestra sólo docentes que tienen ese rol, aunque también tengan otros roles

#### Scenario: Filtrar por asignación

- **WHEN** el operador escribe "JTP" en el filtro de Asignaciones
- **THEN** la tabla muestra sólo docentes que tienen al menos una asignación cuyo cargo o abreviatura coincide con "JTP", sin distinguir mayúsculas ni tildes

#### Scenario: Combinar filtros de encabezados

- **WHEN** el operador filtra Cuenta por "Sin cuenta" y Estado por "Activo"
- **THEN** la tabla muestra sólo docentes que cumplen ambos criterios

#### Scenario: Limpiar un filtro de encabezado

- **WHEN** el operador activa "Limpiar filtro" en una columna filtrada
- **THEN** el criterio de esa columna se elimina y los resultados se recalculan conservando los filtros de las demás columnas

#### Scenario: Cerrar un menú sin perder filtros

- **WHEN** el operador cierra el menú de una columna que ya tiene un filtro aplicado
- **THEN** el filtro continúa visible mediante un indicador del encabezado y sigue afectando la tabla

### Requirement: Ordenamiento por encabezado en la tabla de docentes

La tabla SHALL permitir ordenar desde los encabezados Apellido y Nombre, Documento, Legajo, Cuenta y Estado. El orden SHALL aplicarse únicamente sobre las filas que cumplen los filtros activos. Los encabezados Rol, Ámbitos, Asignaciones y Acciones MUST NOT ser ordenables mientras su valor se represente como una colección o resumen.

Cada encabezado ordenable SHALL alternar entre orden ascendente, descendente y sin orden manual. Al quitar el orden manual, la tabla SHALL recuperar su orden predeterminado por Apellido y Nombre.

#### Scenario: Ordenar docentes por apellido

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

Los controles de filtro y ordenamiento SHALL poder operarse con teclado y SHALL comunicar su estado al lector de pantalla. Abrir o cerrar un menú de encabezado MUST NOT activar acciones de una fila ni ampliar el ámbito de datos devuelto por la API.

#### Scenario: Estado de orden accesible

- **WHEN** una columna queda ordenada en sentido ascendente o descendente
- **THEN** el encabezado comunica su dirección de orden al lector de pantalla

#### Scenario: Operación por teclado

- **WHEN** el operador enfoca el control de filtro y presiona Enter o Espacio
- **THEN** se abre el menú de esa columna sin requerir el uso del mouse
