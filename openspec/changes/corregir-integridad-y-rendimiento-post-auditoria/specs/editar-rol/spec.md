# Spec Delta

## ADDED Requirements

### Requirement: Cambio de ámbito compatible con asignaciones

Editar el ámbito de un rol personalizado SHALL permitirse solo sin asignaciones vivas. Si existen, la API MUST responder conflicto 409 seguro y la interfaz MUST explicar que debe resolverlas explícitamente; MUST NOT revocarlas o reasignarlas automáticamente. Los roles de sistema SHALL mantener sus metadatos de solo lectura.

#### Scenario: Ámbito de rol asignado

- **GIVEN** un rol personalizado con asignaciones vivas
- **WHEN** el operador cambia su ámbito
- **THEN** recibe 409 y ni el rol ni sus asignaciones o permisos cambian

#### Scenario: Rol sin asignaciones vivas

- **GIVEN** un rol personalizado con solo asignaciones revocadas
- **WHEN** un operador autorizado cambia su ámbito con versión válida
- **THEN** cambia el ámbito y conserva el contexto de las asignaciones históricas

### Requirement: Renombre concurrente mantiene unicidad y código

La edición SHALL conservar el código del rol y aplicar unicidad normalizada de nombres activos incluso entre solicitudes concurrentes. Un conflicto de nombre MUST devolverse como 409 consumible por el formulario sin sustituir la edición del otro operador ni alterar permisos.

#### Scenario: Otro rol gana el nombre

- **GIVEN** dos operadores editan roles distintos al mismo nombre libre
- **WHEN** uno confirma antes que el otro
- **THEN** el segundo recibe conflicto de nombre y conserva valores, código y permisos previos

#### Scenario: El propio nombre sigue permitido

- **GIVEN** un rol activo existente
- **WHEN** se guarda sin cambiar su nombre normalizado y con versión válida
- **THEN** no se rechaza como duplicado de sí mismo
