## MODIFIED Requirements

### Requirement: Tabla de docentes con datos completos

El sistema SHALL mostrar una tabla con todos los docentes que la API autorice para el permiso efectivo de consulta y el ámbito del actor. Cada fila MUST incluir: Apellido y Nombre (formato "Apellido, Nombre"), Documento (DNI), Legajo, Rol (Docente / Jefe de Cátedra), Asignaciones (una por fila: código de materia + cargo abreviado) y Estado (badge visual).

#### Scenario: Carga inicial de la tabla

- **WHEN** un usuario con el permiso de consulta de docentes navega a `/docentes`
- **THEN** se muestra la tabla con los docentes devueltos por la API

#### Scenario: Visualización de roles por materia

- **WHEN** un docente tiene `Docente` en una materia y `Jefe de Cátedra` en otra
- **THEN** la tabla muestra un badge por cada rol y permite consultar el ámbito de cada membresía

#### Scenario: Rol repetido en varias materias

- **WHEN** un docente tiene `Jefe de Cátedra` en tres materias
- **THEN** la tabla muestra un solo badge de rol con el resumen de sus materias, no tres badges idénticos

#### Scenario: Estado de cuenta

- **WHEN** un docente no tiene usuario vinculado
- **THEN** la tabla muestra "Sin cuenta"

#### Scenario: Usuario con permiso personalizado

- **GIVEN** un rol personalizado tiene el permiso de consulta de docentes
- **WHEN** un usuario con ese rol navega a `/docentes`
- **THEN** la pantalla carga sin exigir un nombre de rol institucional y respeta el ámbito devuelto por la API

#### Scenario: Visualización de roles — múltiples

- **WHEN** un docente tiene `roles = ["Docente", "Jefe de Cátedra"]`
- **THEN** la columna Rol muestra un badge por cada rol

#### Scenario: Visualización de roles — único

- **WHEN** un docente tiene `roles = ["Docente"]`
- **THEN** la columna Rol muestra un único badge "Docente"

#### Scenario: Visualización de asignaciones con abreviación de cargo

- **WHEN** un docente tiene asignación `{ materia: "03500 – Matemática Discreta", cargo: "Jefe de Trabajos Prácticos" }`
- **THEN** la columna Asignaciones muestra un badge con texto "03500 – JTP"

#### Scenario: Múltiples asignaciones

- **WHEN** un docente tiene más de una asignación
- **THEN** cada asignación se muestra como un badge separado con código + cargo abreviado

#### Scenario: Estado visual activo/inactivo

- **WHEN** un docente tiene `is_active = true`
- **THEN** la columna Estado muestra `StatusBadge` con `kind="aprobado"` y label "Activo"

#### Scenario: Estado visual inactivo

- **WHEN** un docente tiene `is_active = false`
- **THEN** la columna Estado muestra `StatusBadge` con `kind="rechazado"` y label "Inactivo"

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
