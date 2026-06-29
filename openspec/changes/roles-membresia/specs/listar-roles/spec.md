## ADDED Requirements

### Requirement: Visualización de roles en tabla

La pantalla `/roles` SHALL mostrar todos los roles del sistema en una tabla con columnas Nombre y Descripción. El acceso SHALL estar restringido a usuarios con rol `Secretaría` o `Administración`.

#### Scenario: Acceso autorizado carga la tabla

- **WHEN** un usuario con rol Secretaría o Administración navega a `/roles`
- **THEN** la pantalla carga y muestra la tabla con todos los roles disponibles

#### Scenario: Acceso no autorizado redirige

- **WHEN** un usuario sin rol Secretaría ni Administración intenta acceder a `/roles`
- **THEN** el sistema redirige a `/`

#### Scenario: La tabla muestra nombre y descripción

- **WHEN** la tabla de roles es visible
- **THEN** cada fila muestra el Nombre y la Descripción del rol, y un botón "Editar"

### Requirement: Buscador de roles

La pantalla SHALL incluir un campo de búsqueda que filtre los roles visibles en tiempo real por Nombre y Descripción, sin distinción de mayúsculas ni tildes.

#### Scenario: Búsqueda filtra por nombre

- **WHEN** el operador escribe texto en el buscador
- **THEN** la tabla muestra únicamente los roles cuyo Nombre o Descripción contiene el texto ingresado

#### Scenario: Búsqueda vacía muestra todos

- **WHEN** el campo de búsqueda está vacío
- **THEN** la tabla muestra todos los roles sin filtrar

#### Scenario: Sin resultados muestra estado vacío

- **WHEN** el texto de búsqueda no coincide con ningún rol
- **THEN** la tabla muestra un estado vacío informativo
