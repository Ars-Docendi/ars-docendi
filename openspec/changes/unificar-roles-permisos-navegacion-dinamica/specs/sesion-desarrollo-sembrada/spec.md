## MODIFIED Requirements

### Requirement: Catálogo de identidades de desarrollo desde backend

En ambientes no productivos habilitados explícitamente, el sistema SHALL ofrecer las identidades sintéticas seleccionables con su rol activo, código, nombre, permisos efectivos y ámbitos obtenidos desde la persistencia canónica, independientemente de que el frontend se ejecute mediante un servidor de desarrollo o un bundle optimizado.

#### Scenario: Abrir selector en desarrollo

- **GIVEN** una ejecución no productiva con suplantación habilitada y una base sembrada
- **WHEN** el usuario abre el selector de ingreso de desarrollo
- **THEN** ve las identidades sembradas, distingue sus roles y ámbitos disponibles y puede obtener sus permisos efectivos

#### Scenario: Seleccionar identidad

- **GIVEN** una identidad sintética activa con más de un rol, incluido uno personalizado
- **WHEN** el usuario la selecciona y elige un rol activo
- **THEN** las solicitudes posteriores representan esa identidad, código de rol, permisos y ámbito resueltos por el backend

#### Scenario: Permisos modificados antes de validar sesión

- **GIVEN** se modificaron los permisos persistidos del rol seleccionado
- **WHEN** el usuario valida nuevamente la sesión de desarrollo
- **THEN** el catálogo o la validación devuelve el conjunto vigente y el frontend reconstruye la navegación con esos permisos

#### Scenario: Abrir selector en un despliegue no productivo

- **GIVEN** un ambiente de staging o preview con frontend optimizado, suplantación habilitada y una base sembrada
- **WHEN** el usuario pulsa el botón de ingreso
- **THEN** el sistema abre el selector de identidades y consulta el catálogo del backend
