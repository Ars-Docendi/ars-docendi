## ADDED Requirements

### Requirement: Redirección de la ruta de membresía legacy

La ruta `/membresia-roles` SHALL redirigir a `/roles` conservando la sesión y sin renderizar una segunda pantalla de gestión. La sidebar SHALL ofrecer sólo la entrada canónica "Roles".

#### Scenario: Navegar a la ruta legacy

- **WHEN** un usuario navega a `/membresia-roles`
- **THEN** el router lo redirige a `/roles` y la pantalla unificada queda disponible

#### Scenario: No duplicar entradas de navegación

- **WHEN** un usuario con `roles.ver` visualiza la configuración
- **THEN** ve una sola entrada para Roles y no ve una entrada separada para Membresía de Roles

## REMOVED Requirements

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

**Reason:** La lista y el panel pasan a ser una única experiencia en `/roles`.

**Migration:** El router mantiene la URL anterior como redirección compatible.

### Requirement: Buscador de roles en membresía

El panel de roles SHALL incluir un campo de búsqueda que filtre los roles visibles en tiempo real por Nombre, sin distinción de mayúsculas ni tildes.

#### Scenario: Búsqueda filtra por nombre

- **WHEN** el operador escribe texto en el buscador del panel izquierdo
- **THEN** la lista muestra únicamente los roles cuyo Nombre contiene el texto ingresado

#### Scenario: Búsqueda vacía muestra todos los roles

- **WHEN** el campo de búsqueda está vacío
- **THEN** la lista muestra todos los roles

**Reason:** El buscador queda consolidado con el buscador de `/roles` y también filtra la descripción.

**Migration:** Los usuarios continúan accediendo al buscador desde `/roles`.

### Requirement: Selección de rol para gestión de permisos

El operador SHALL poder hacer clic en un rol de la lista para seleccionarlo y ver sus permisos en el panel derecho.

#### Scenario: Clic en rol lo marca como seleccionado y muestra sus permisos

- **WHEN** el operador hace clic en un rol de la lista
- **THEN** el rol queda visualmente marcado como activo y el panel derecho muestra los permisos correspondientes a ese rol

**Reason:** La selección se conserva como interacción del panel unificado.

**Migration:** La interacción pasa a la lista lateral de `/roles`.
