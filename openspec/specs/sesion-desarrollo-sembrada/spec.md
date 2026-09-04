# sesion-desarrollo-sembrada Specification

## Purpose

Permite recorrer roles y ámbitos reales del dataset sintético durante el desarrollo, sin duplicar identidades en el frontend ni habilitar suplantación en producción.

## Requirements

### Requirement: Catálogo de identidades de desarrollo desde backend

En ambientes no productivos habilitados explícitamente, el sistema SHALL ofrecer las identidades sintéticas seleccionables con sus roles y ámbitos efectivos obtenidos desde la persistencia canónica, independientemente de que el frontend se ejecute mediante un servidor de desarrollo o un bundle optimizado.

#### Scenario: Abrir selector en desarrollo

- **GIVEN** una ejecución no productiva con suplantación habilitada y una base sembrada
- **WHEN** el usuario abre el selector de ingreso de desarrollo
- **THEN** ve las identidades sembradas y puede distinguir sus roles y ámbitos disponibles

#### Scenario: Seleccionar identidad

- **GIVEN** una identidad sintética activa con más de un rol
- **WHEN** el usuario la selecciona y elige un rol activo
- **THEN** las solicitudes posteriores representan esa identidad, rol y ámbito resueltos por el backend

#### Scenario: Abrir selector en un despliegue no productivo

- **GIVEN** un ambiente de staging o preview con frontend optimizado, suplantación habilitada y una base sembrada
- **WHEN** el usuario pulsa el botón de ingreso
- **THEN** el sistema abre el selector de identidades y consulta el catálogo del backend

### Requirement: Ausencia absoluta en producción

La superficie de listado y suplantación de desarrollo MUST no estar registrada en el backend ni disponible en el frontend de producción, independientemente de parámetros enviados por el cliente o de una configuración parcial accidental.

#### Scenario: Solicitud de suplantación en producción

- **GIVEN** una instancia iniciada en ambiente de producción
- **WHEN** un cliente solicita cualquier ruta de suplantación de desarrollo
- **THEN** la ruta no existe y no se crea ninguna sesión suplantada

#### Scenario: Bundle frontend de producción

- **GIVEN** una compilación destinada a producción
- **WHEN** el usuario visita la pantalla de ingreso
- **THEN** el selector de identidades sembradas no está disponible ni se envían headers de suplantación

### Requirement: Restricción a identidades sembradas activas

El backend MUST rechazar la suplantación de identificadores inexistentes, usuarios inactivos o identidades ajenas al dataset sintético autorizado.

#### Scenario: Identidad no elegible

- **GIVEN** un identificador que no corresponde a un usuario sintético activo
- **WHEN** se solicita suplantarlo en desarrollo
- **THEN** el backend rechaza la solicitud y mantiene la sesión sin cambios
