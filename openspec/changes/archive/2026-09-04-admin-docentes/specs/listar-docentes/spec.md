## ADDED Requirements

### Requirement: Tabla de docentes con datos completos

El sistema SHALL mostrar una tabla con todos los docentes registrados. Cada fila MUST incluir: Apellido y Nombre (formato "Apellido, Nombre"), Documento (DNI), Legajo, Rol (Docente / Jefe de Cátedra), Asignaciones (una por fila: código de materia + cargo abreviado) y Estado (badge visual).

#### Scenario: Carga inicial de la tabla

- **WHEN** el usuario con rol Secretaría o Administración navega a `/docentes`
- **THEN** se muestra la tabla con todos los docentes del store mock

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

### Requirement: Filtros fijos por Apellido, Nombre y Documento

El sistema SHALL proveer tres inputs de texto siempre visibles para filtrar la tabla en tiempo real. La búsqueda MUST ser insensible a mayúsculas y tildes (normalización NFD).

#### Scenario: Filtro por apellido sin tilde

- **WHEN** el usuario escribe "lopez" en el filtro de Apellido
- **THEN** la tabla muestra solo docentes cuyo apellido normalizado contiene "lopez"

#### Scenario: Combinación de filtros fijos

- **WHEN** el usuario aplica filtros de Apellido y Documento simultáneamente
- **THEN** la tabla muestra solo docentes que cumplen ambas condiciones (AND lógico)

### Requirement: Filtros opcionales añadibles incluido Rol

El sistema SHALL permitir añadir filtros opcionales desde un selector "Añadir filtro…". Los disponibles son: Código de materia (texto), Materia (selector catálogo), Cargo (texto), Rol (selector: Docente / Jefe de Cátedra) y Estado.

#### Scenario: Filtro por Rol — Jefe de Cátedra

- **WHEN** el usuario agrega el filtro Rol y selecciona "Jefe de Cátedra"
- **THEN** la tabla muestra solo docentes cuyo array `roles` incluye `"Jefe de Cátedra"` (incluso si también tienen `"Docente"`)

#### Scenario: Filtro por Cargo busca en asignaciones

- **WHEN** el usuario agrega el filtro Cargo y escribe "JTP"
- **THEN** la tabla muestra solo docentes que tienen al menos una asignación con cargo "Jefe de Trabajos Prácticos" (búsqueda substring insensible a tildes)

#### Scenario: Quitar filtro opcional

- **WHEN** el usuario presiona el botón × junto a un filtro opcional activo
- **THEN** el filtro se quita y su valor se resetea, actualizando la tabla

### Requirement: Acceso restringido por rol

El sistema SHALL permitir el acceso a `/docentes` a usuarios con rol `Secretaría` (Secretaría Académica), `Administración` (Administrativo) o `Jefe de Cátedra`. Coordinador, Decanato y Docente no pueden acceder.

#### Scenario: Acceso denegado a roles sin permiso

- **WHEN** un usuario con rol Coordinador, Decanato o Docente intenta navegar a `/docentes`
- **THEN** es redirigido automáticamente a `/`

### Requirement: Vista "Mis Docentes" para Jefe de Cátedra

Cuando el usuario autenticado tiene rol `Jefe de Cátedra`, la pantalla SHALL mostrar el título "Mis Docentes" y filtrar automáticamente la tabla para mostrar solo docentes que compartan al menos una materia con el JdC. El JdC se identifica por su UPN en el store de docentes.

#### Scenario: Título y filtro automático para JdC

- **WHEN** un usuario con rol `Jefe de Cátedra` navega a `/docentes`
- **THEN** el título de la página es "Mis Docentes" y la tabla muestra únicamente docentes que tienen asignaciones en las mismas materias que el JdC

#### Scenario: JdC sin registro de docente

- **WHEN** el JdC autenticado no tiene registro en el store de docentes (UPN no encontrada)
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
