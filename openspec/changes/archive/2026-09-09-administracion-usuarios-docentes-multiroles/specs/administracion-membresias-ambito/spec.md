## Purpose

Permite administrar las membresías de roles como asignaciones explícitas a una materia o carrera, manteniendo separados el resumen visual del rol y sus ámbitos efectivos.

## ADDED Requirements

### Requirement: Resumen y detalle de membresías de roles

La API SHALL devolver un resumen de roles sin repetir cada rol por sus ámbitos y SHALL conservar un detalle de todas las membresías activas con `rolId`, código, nombre, ámbito, `materiaId` y `carreraId` canónicos.

#### Scenario: Usuario con un rol en varias materias

- **GIVEN** un usuario tiene tres membresías activas de `jefe_catedra` en materias distintas
- **WHEN** se consulta el usuario para la administración
- **THEN** el resumen muestra una sola vez "Jefe de Cátedra" y el detalle conserva las tres materias

#### Scenario: Usuario con roles distintos

- **GIVEN** un usuario tiene `docente` en una materia y `jefe_catedra` en otra
- **WHEN** se consulta el usuario
- **THEN** el resumen muestra ambos roles y el detalle mantiene la materia correspondiente a cada uno

### Requirement: Edición explícita y atómica de membresías

Las pantallas administrativas MUST enviar la lista completa de membresías con sus IDs y ámbitos, y la API MUST reemplazarla atómicamente junto con los datos de identidad. Una asignación inválida o repetida MUST dejar intactos los datos y las membresías anteriores.

#### Scenario: Cambio de rol entre materias

- **GIVEN** un usuario es `docente` en Materia A y `jefe_catedra` en Materia B
- **WHEN** el operador cambia la segunda membresía a Materia C y guarda
- **THEN** la consulta posterior conserva sólo `docente + A` y `jefe_catedra + C`

#### Scenario: Repetición de una misma asignación

- **GIVEN** el formulario envía dos veces el mismo `rolId + materiaId + carreraId`
- **WHEN** se intenta guardar
- **THEN** la API rechaza la operación como error de ámbito y no aplica cambios parciales

#### Scenario: Ámbito incompatible

- **GIVEN** un rol global recibe una materia o un rol de materia recibe una carrera incorrecta
- **WHEN** se intenta guardar
- **THEN** la API rechaza la operación y conserva las membresías anteriores

### Requirement: Autorización por ámbito efectivo

El backend MUST considerar una membresía de rol sólo dentro de la materia o carrera que declara. Tener `jefe_catedra` en una materia NO DEBE otorgar ese rol en otra materia del mismo usuario.

#### Scenario: Acción dentro del ámbito

- **GIVEN** el usuario tiene `jefe_catedra` en Materia A
- **WHEN** ejecuta una operación permitida para ese rol sobre Materia A
- **THEN** la operación puede continuar si cumple las demás reglas del circuito

#### Scenario: Acción fuera del ámbito

- **GIVEN** el usuario tiene `jefe_catedra` sólo en Materia A
- **WHEN** intenta actuar como jefe sobre Materia B
- **THEN** el backend deniega la operación aunque el usuario tenga otros roles o membresías
