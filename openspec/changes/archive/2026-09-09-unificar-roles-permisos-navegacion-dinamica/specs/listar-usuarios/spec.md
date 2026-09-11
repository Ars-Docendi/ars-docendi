## MODIFIED Requirements

### Requirement: Tabla de usuarios visible para Secretaría y Administración

La página `/usuarios` SHALL mostrar una tabla con todos los usuarios del sistema. El acceso SHALL requerir el permiso efectivo de consulta de usuarios, no un nombre o conjunto fijo de roles; cualquier usuario sin ese permiso SHALL ser redirigido a `/`.

#### Scenario: Usuario con permiso accede a la página

- **WHEN** un usuario autenticado con el permiso de consulta de usuarios navega a `/usuarios`
- **THEN** se muestra la tabla con columnas: Apellido y Nombre, Documento, Legajo, UPN/Email, Roles, Estado, Acciones

#### Scenario: Secretaría accede a la página

- **WHEN** un usuario Secretaría con el permiso de consulta de usuarios navega a `/usuarios`
- **THEN** se muestra la tabla con columnas: Apellido y Nombre, Documento, Legajo, UPN/Email, Roles, Estado, Acciones

#### Scenario: Administración accede a la página

- **WHEN** un usuario Administración con el permiso de consulta de usuarios navega a `/usuarios`
- **THEN** se muestra la tabla con las mismas columnas

#### Scenario: Usuario sin permiso intenta acceder directamente

- **WHEN** un usuario autenticado sin el permiso de consulta de usuarios navega a `/usuarios`
- **THEN** es redirigido a `/` sin ver el contenido

#### Scenario: Otro rol intenta acceder directamente

- **WHEN** un usuario autenticado sin el permiso de consulta de usuarios navega a `/usuarios`
- **THEN** es redirigido a `/` sin ver el contenido

### Requirement: Sidebar muestra entrada "Usuarios" para Secretaría y Administración

El sidebar SHALL mostrar el ítem de navegación "Usuarios" dentro del grupo "Configuración" únicamente a los usuarios cuya sesión tenga el permiso de consulta de usuarios. Para cualquier otro usuario el ítem SHALL estar oculto.

#### Scenario: Secretaría ve el ítem

- **WHEN** el usuario logueado tiene el permiso de consulta de usuarios
- **THEN** el sidebar muestra "Usuarios" en el grupo "Configuración"

#### Scenario: Otro rol no ve el ítem

- **WHEN** el usuario logueado no tiene el permiso de consulta de usuarios
- **THEN** el ítem "Usuarios" no aparece en el sidebar
