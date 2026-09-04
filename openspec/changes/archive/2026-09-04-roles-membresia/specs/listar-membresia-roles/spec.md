## ADDED Requirements

### Requirement: Visualización de roles en panel de membresía

La pantalla `/membresia-roles` SHALL mostrar la lista de roles del sistema en un panel lateral izquierdo. El acceso SHALL estar restringido a usuarios con rol `Secretaría` o `Administración`.

#### Scenario: Acceso autorizado carga el panel de roles

- **WHEN** un usuario con rol Secretaría o Administración navega a `/membresia-roles`
- **THEN** la pantalla carga y muestra la lista de roles en el panel izquierdo

#### Scenario: Acceso no autorizado redirige

- **WHEN** un usuario sin rol Secretaría ni Administración intenta acceder a `/membresia-roles`
- **THEN** el sistema redirige a `/`

#### Scenario: Sin rol seleccionado el panel derecho muestra placeholder

- **WHEN** ningún rol está seleccionado
- **THEN** el panel derecho muestra un mensaje indicando que se debe seleccionar un rol

### Requirement: Buscador de roles en membresía

El panel de roles SHALL incluir un campo de búsqueda que filtre los roles visibles en tiempo real por Nombre, sin distinción de mayúsculas ni tildes.

#### Scenario: Búsqueda filtra por nombre

- **WHEN** el operador escribe texto en el buscador del panel izquierdo
- **THEN** la lista muestra únicamente los roles cuyo Nombre contiene el texto ingresado

#### Scenario: Búsqueda vacía muestra todos los roles

- **WHEN** el campo de búsqueda está vacío
- **THEN** la lista muestra todos los roles

### Requirement: Selección de rol para gestión de permisos

El operador SHALL poder hacer clic en un rol de la lista para seleccionarlo y ver sus permisos en el panel derecho.

#### Scenario: Clic en rol lo marca como seleccionado y muestra sus permisos

- **WHEN** el operador hace clic en un rol de la lista
- **THEN** el rol queda visualmente marcado como activo y el panel derecho muestra los permisos correspondientes a ese rol
