# listar-roles Specification

## Purpose

Permite a los operadores autorizados consultar y buscar los roles institucionales.

## Requirements

### Requirement: Visualización de roles en tabla

La pantalla `/roles` SHALL mostrar, con el diseño de Membresía Roles, un panel lateral de roles activos y un panel derecho de permisos para el rol seleccionado. El acceso SHALL requerir `roles.ver`, sin depender del nombre del rol.

#### Scenario: Acceso autorizado carga la pantalla unificada

- **WHEN** un usuario con `roles.ver` navega a `/roles`
- **THEN** la pantalla carga la lista de roles activos y el panel de permisos del rol seleccionado

#### Scenario: Acceso autorizado carga la tabla

- **WHEN** un usuario con `roles.ver` navega a `/roles`
- **THEN** la pantalla carga y muestra la lista unificada de roles activos

#### Scenario: Acceso no autorizado redirige

- **WHEN** un usuario sin `roles.ver` intenta acceder a `/roles`
- **THEN** el sistema redirige a `/`

#### Scenario: La lista permite seleccionar y administrar

- **WHEN** la pantalla unificada es visible
- **THEN** cada rol muestra nombre y descripción, permite seleccionarlo y presenta las acciones de edición o eliminación según sus protecciones

#### Scenario: La tabla muestra nombre y descripción

- **WHEN** la lista unificada de roles es visible
- **THEN** cada fila muestra el Nombre y la Descripción del rol, y las acciones permitidas

### Requirement: Buscador de roles

La pantalla `/roles` SHALL incluir en el panel lateral un campo de búsqueda que filtre los roles visibles en tiempo real por Nombre y Descripción, sin distinción de mayúsculas ni tildes.

#### Scenario: Búsqueda filtra por nombre

- **WHEN** el operador escribe texto en el buscador
- **THEN** la lista muestra únicamente los roles cuyo Nombre o Descripción contiene el texto ingresado

#### Scenario: Búsqueda vacía muestra todos

- **WHEN** el campo de búsqueda está vacío
- **THEN** la lista muestra todos los roles activos sin filtrar

#### Scenario: Sin resultados muestra estado vacío

- **WHEN** el texto de búsqueda no coincide con ningún rol
- **THEN** la lista muestra un estado vacío informativo

### Requirement: Tipografía administrativa consistente en Roles

La pantalla `/roles` SHALL usar la misma familia tipográfica institucional y el mismo tamaño base
de texto que las pantallas `/usuarios` y `/docentes` para el buscador, la lista de roles, el panel
de permisos y sus controles equivalentes. La jerarquía de títulos y textos secundarios SHALL
mantener la escala visual del sistema sin tamaños arbitrarios propios de la pantalla.

#### Scenario: Texto de la pantalla de Roles

- **GIVEN** un operador autorizado visualiza `/roles`
- **WHEN** observa el buscador, los roles y los permisos
- **THEN** el texto de controles y contenido usa la misma tipografía y tamaño base perceptible que
  los controles y tablas equivalentes de `/usuarios` y `/docentes`

#### Scenario: Controles nativos de Roles

- **GIVEN** el operador interactúa con el buscador o con un control del panel de permisos
- **WHEN** el control recibe foco o cambia de estado
- **THEN** conserva la tipografía y el tamaño de texto de la interfaz administrativa general
