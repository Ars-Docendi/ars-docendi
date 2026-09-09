## MODIFIED Requirements

### Requirement: Tabla de docentes con datos completos

El sistema SHALL mostrar una tabla con todos los docentes registrados. Cada fila MUST incluir: Apellido y Nombre, Documento, Legajo, roles docentes resumidos sin duplicados, asignaciones académicas, estado de cuenta y Estado activo/inactivo.

#### Scenario: Carga inicial de la tabla

- **WHEN** el usuario con rol Secretaría o Administración navega a `/docentes`
- **THEN** se muestra la tabla con los docentes devueltos por la API

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

### Requirement: Filtros fijos por Apellido, Nombre y Documento

El sistema SHALL proveer tres inputs de texto siempre visibles para filtrar la tabla en tiempo real. La búsqueda MUST ser insensible a mayúsculas y tildes.

#### Scenario: Filtro por apellido sin tilde

- **WHEN** el usuario escribe "lopez" en el filtro de Apellido
- **THEN** la tabla muestra sólo docentes cuyo apellido normalizado contiene "lopez"

#### Scenario: Combinación de filtros fijos

- **WHEN** el usuario aplica filtros de Apellido y Documento simultáneamente
- **THEN** la tabla muestra sólo docentes que cumplen ambas condiciones

### Requirement: Filtros opcionales añadibles incluido Rol

El sistema SHALL permitir añadir filtros opcionales desde un selector "Añadir filtro…". Los disponibles SHALL incluir Código de materia, Materia, Cargo, Rol, Estado y Cuenta.

#### Scenario: Filtro por Rol — Jefe de Cátedra

- **WHEN** el usuario agrega el filtro Rol y selecciona "Jefe de Cátedra"
- **THEN** la tabla muestra sólo docentes cuyo resumen de membresías incluye "Jefe de Cátedra", aunque también tengan "Docente"

#### Scenario: Filtro por Cargo busca en asignaciones

- **WHEN** el usuario agrega el filtro Cargo y escribe "JTP"
- **THEN** la tabla muestra sólo docentes que tienen al menos una asignación con cargo "Jefe de Trabajos Prácticos", con búsqueda insensible a tildes

#### Scenario: Filtro por Cuenta

- **WHEN** el usuario selecciona "Sin cuenta"
- **THEN** la tabla muestra sólo docentes sin usuario vinculado

#### Scenario: Quitar filtro opcional

- **WHEN** el usuario presiona el botón × junto a un filtro opcional activo
- **THEN** el filtro se quita y su valor se resetea, actualizando la tabla
