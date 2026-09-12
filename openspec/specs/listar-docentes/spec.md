# listar-docentes Specification

## Purpose

Permite consultar docentes, sus asignaciones, roles y estado dentro del ámbito autorizado.

## Requirements

### Requirement: Tabla de docentes con datos completos

El sistema SHALL mostrar una tabla con todos los docentes registrados. Cada fila MUST incluir: Apellido y Nombre, Documento, Legajo, roles docentes resumidos sin duplicados, asignaciones académicas, estado de cuenta y Estado activo/inactivo.

#### Scenario: Carga inicial de la tabla

- **WHEN** el usuario con rol Secretaría o Administración navega a `/docentes`
- **THEN** se muestra la tabla con los docentes devueltos por la API

#### Scenario: Usuario con permiso personalizado

- **GIVEN** un rol personalizado tiene el permiso de consulta de docentes
- **WHEN** un usuario con ese rol navega a `/docentes`
- **THEN** la pantalla carga sin exigir un nombre de rol institucional y respeta el ámbito devuelto por la API

#### Scenario: Visualización de roles por materia

- **WHEN** un docente tiene `Docente` en una materia y `Jefe de Cátedra` en otra
- **THEN** la tabla muestra un badge por cada rol y permite consultar el ámbito de cada membresía

#### Scenario: Visualización de roles — múltiples

- **WHEN** un docente tiene `roles = ["Docente", "Jefe de Cátedra"]`
- **THEN** la columna Rol muestra un badge único por cada rol y el detalle conserva sus materias

#### Scenario: Visualización de roles — único

- **WHEN** un docente tiene `roles = ["Docente"]`
- **THEN** la columna Rol muestra un único badge "Docente"

#### Scenario: Visualización de asignaciones con abreviación de cargo

- **WHEN** un docente tiene asignación `{ materia: "03500 – Matemática Discreta", cargo: "Jefe de Trabajos Prácticos" }`
- **THEN** la columna Asignaciones muestra un badge con texto "03500 – JTP"

#### Scenario: Múltiples asignaciones

- **WHEN** un docente tiene más de una asignación
- **THEN** cada asignación se muestra como un badge separado con código y cargo abreviado

#### Scenario: Rol repetido en varias materias

- **WHEN** un docente tiene `Jefe de Cátedra` en tres materias
- **THEN** la tabla muestra un solo badge de rol con el resumen de sus materias, no tres badges idénticos

#### Scenario: Estado de cuenta

- **WHEN** un docente no tiene usuario vinculado
- **THEN** la tabla muestra "Sin cuenta"

#### Scenario: Estado visual activo/inactivo

- **WHEN** un docente tiene `is_active = true`
- **THEN** la columna Estado muestra `StatusBadge` con `kind="aprobado"` y label "Activo"

#### Scenario: Estado visual inactivo

- **WHEN** un docente tiene `is_active = false`
- **THEN** la columna Estado muestra `StatusBadge` con `kind="rechazado"` y label "Inactivo"

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

### Requirement: Acceso restringido por rol

El sistema SHALL permitir el acceso a `/docentes` a usuarios con el permiso efectivo de consulta de docentes. La API MUST conservar sus restricciones de ámbito; en particular, la vista acotada de Jefe de Cátedra seguirá resolviéndose por sus membresías y designaciones, no por el guard del frontend. Un usuario sin el permiso SHALL ser redirigido.

#### Scenario: Acceso denegado sin permiso

- **WHEN** un usuario sin el permiso efectivo de consulta de docentes intenta navegar a `/docentes`
- **THEN** es redirigido automáticamente a `/`

#### Scenario: Acceso denegado a roles sin permiso

- **WHEN** un usuario sin el permiso efectivo de consulta de docentes intenta navegar a `/docentes`
- **THEN** es redirigido automáticamente a `/`

#### Scenario: Rol personalizado con permiso y ámbito

- **WHEN** un usuario con rol personalizado tiene el permiso y un ámbito válido
- **THEN** puede abrir la pantalla y sólo recibe los datos autorizados por el backend

### Requirement: Vista "Mis Docentes" para Jefe de Cátedra

Cuando el usuario autenticado tiene rol `Jefe de Cátedra`, la pantalla SHALL mostrar el título "Mis Docentes" y la API MUST devolver sólo docentes con una designación vigente en alguna materia a cargo del Jefe.

#### Scenario: Título y filtro automático para JdC

- **WHEN** un usuario con rol `Jefe de Cátedra` navega a `/docentes`
- **THEN** el título de la página es "Mis Docentes" y la tabla muestra únicamente docentes que tienen asignaciones en las mismas materias que el JdC

#### Scenario: JdC sin registro de docente

- **WHEN** el JdC autenticado no tiene materias a cargo
- **THEN** la tabla muestra cero docentes

#### Scenario: Botón "Nuevo docente" oculto para JdC

- **WHEN** el usuario autenticado tiene rol `Jefe de Cátedra`
- **THEN** el botón "Nuevo docente" y la columna de acciones de escritura no se muestran

#### Scenario: API de docentes limitada al ámbito del JdC

- **WHEN** un usuario autenticado como `Jefe de Cátedra` consulta `GET /api/administracion/docentes`
- **THEN** la API responde exitosamente y devuelve únicamente docentes con al menos una designación vigente en las materias donde el usuario tiene ese rol

#### Scenario: Catálogos de docentes limitados para el JdC

- **WHEN** un usuario autenticado como `Jefe de Cátedra` consulta `GET /api/administracion/docentes/catalogos`
- **THEN** la API devuelve únicamente sus materias, no expone personas elegibles y conserva los catálogos necesarios para filtrar la vista

#### Scenario: Escritura docente denegada al JdC

- **WHEN** un usuario autenticado como `Jefe de Cátedra` intenta crear, editar, activar o desactivar un docente
- **THEN** la API responde `403` porque esas operaciones siguen requiriendo `usuarios.administrar`

### Requirement: Proyección completa dentro del ámbito docente

La API de docentes MUST aplicar el conjunto de materias visibles del actor a todos los datos de cada respuesta, no sólo a la selección de personas. Para un actor acotado, cada docente listado o consultado SHALL incluir únicamente asignaciones vigentes y membresías docentes vinculadas a materias visibles. Un docente sin asignación visible SHALL quedar fuera del listado y su detalle SHALL responder como recurso no visible. Los usuarios con alcance departamental SHALL conservar todas sus asignaciones y membresías.

#### Scenario: Listado con asignaciones mixtas

- **GIVEN** un Jefe de Cátedra que puede ver la Materia A y un docente con asignaciones vigentes en A y B
- **WHEN** consulta el listado de docentes
- **THEN** el docente aparece una sola vez, con la asignación de A, sin la asignación de B ni sus datos derivados

#### Scenario: Detalle con asignaciones mixtas

- **GIVEN** un Jefe de Cátedra que puede ver la Materia A y un docente con asignaciones vigentes en A y B
- **WHEN** consulta el detalle del docente
- **THEN** la respuesta contiene sólo la asignación de A y las membresías docentes de materias visibles

#### Scenario: Persona sólo fuera de ámbito

- **GIVEN** un docente con una única asignación vigente en la Materia B y un actor que sólo puede ver A
- **WHEN** el actor lista o consulta ese docente
- **THEN** el listado no lo incluye y el detalle responde `404`

#### Scenario: Consulta global

- **GIVEN** un usuario con alcance departamental
- **WHEN** consulta el listado o detalle de docentes
- **THEN** recibe todas las asignaciones y membresías vigentes permitidas por la vista global
