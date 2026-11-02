## ADDED Requirements

### Requirement: Permisos declarados por módulo y artículo

Cada módulo y cada artículo de ayuda SHALL declarar una lista de permisos requeridos. La persona ve el elemento si su sesión tiene al menos uno de los permisos de la lista (cualquiera alcanza); una lista vacía significa visible para todo usuario autenticado. Las cadenas MUST ser las mismas que usa `app/shell/nav.ts`. Un módulo se muestra solo si la persona lo puede ver y tiene al menos un artículo visible en él.

#### Scenario: Permiso cualquiera

- **GIVEN** un artículo que declara `["designaciones.revisar", "periodos.administrar"]`
- **WHEN** lo evalúa un usuario que solo tiene `periodos.administrar`
- **THEN** el artículo es visible

#### Scenario: Lista vacía

- **GIVEN** un artículo con permisos `[]`
- **WHEN** lo evalúa un usuario sin ningún permiso de navegación
- **THEN** el artículo es visible

#### Scenario: Módulo sin artículos visibles

- **GIVEN** un módulo cuyos artículos exigen permisos que el usuario no tiene
- **WHEN** abre el inicio de la ayuda
- **THEN** el módulo no aparece

### Requirement: El filtro rige en toda la superficie de la ayuda

El filtrado MUST aplicarse en la sidebar de documentación, el inicio, la página de módulo, la búsqueda (panel y página de resultados) y las listas de artículos relacionados. Un artículo no visible MUST NOT aparecer en ninguna de esas superficies ni contribuir a conteos.

#### Scenario: Búsqueda no revela contenido ajeno

- **GIVEN** un usuario sin `roles.ver`
- **WHEN** busca un término que solo aparece en el artículo de roles
- **THEN** no hay resultados

#### Scenario: Relacionado oculto

- **GIVEN** un artículo visible que relaciona a otro que el usuario no puede ver
- **WHEN** el usuario abre el primero
- **THEN** el relacionado no se lista

### Requirement: URL directa a contenido no visible

Abrir directamente la URL de un módulo o artículo que la persona no puede ver MUST comportarse como un slug desconocido: redirigir a `/ayuda`, sin revelar que el contenido existe.

#### Scenario: Artículo sin permiso

- **GIVEN** un usuario sin `periodos.administrar`
- **WHEN** abre la URL del artículo de períodos
- **THEN** es redirigido a `/ayuda`

### Requirement: El Centro de ayuda es accesible para todo usuario autenticado

Las rutas bajo `/ayuda` MUST NOT exigir un permiso. Los permisos de la sesión se leen de la sesión vigente; la ayuda MUST NOT usar el nombre del rol. El filtrado es solo de presentación: la autorización de cada pantalla y API sigue a cargo del frontend (guards) y del backend.

#### Scenario: Usuario sin permisos de módulos

- **GIVEN** un usuario autenticado sin permisos de navegación
- **WHEN** abre `/ayuda`
- **THEN** la ruta se renderiza y muestra los módulos y artículos de permisos vacíos

#### Scenario: Usuario no autenticado

- **GIVEN** una persona sin sesión
- **WHEN** abre `/ayuda`
- **THEN** es redirigida a `/login`, igual que cualquier ruta protegida
