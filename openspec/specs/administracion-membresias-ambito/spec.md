# administracion-membresias-ambito Specification

## Purpose

Permite administrar las membresías de roles como asignaciones explícitas a una materia o carrera, manteniendo separados el resumen visual del rol y sus ámbitos efectivos.

## Requirements

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

Las pantallas administrativas MUST enviar la lista completa de membresías con sus IDs y ámbitos, y la API MUST reemplazarla atómicamente junto con los datos de identidad. Una asignación inválida o repetida MUST dejar intactos los datos y las membresías anteriores. Para una membresía de Docente, la API MUST validar que `materia_id` y `carrera_id` vengan juntos y que esa combinación se dicte de verdad (catálogo informativo materia–plan, vigente y activo). Para una membresía de Jefe de Cátedra, la API MUST validar que `materia_id` referencie una materia activa; no se envía carrera. Solo el rol de Coordinador lleva `carrera_id` solo.

#### Scenario: Cambio de rol entre materias

- **GIVEN** un usuario es `docente` en la materia/carrera A y `jefe_catedra` en la materia B
- **WHEN** el operador cambia la segunda membresía a la materia C y guarda
- **THEN** la consulta posterior conserva sólo `docente + A` y `jefe_catedra + C`

#### Scenario: Repetición de una misma asignación

- **GIVEN** el formulario envía dos veces el mismo `rolId + materiaId + carreraId`
- **WHEN** se intenta guardar
- **THEN** la API rechaza la operación como error de ámbito y no aplica cambios parciales

#### Scenario: Ámbito incompatible

- **GIVEN** un rol global recibe una materia o un rol de materia recibe una carrera incorrecta
- **WHEN** se intenta guardar
- **THEN** la API rechaza la operación y conserva las membresías anteriores

#### Scenario: Docente sin carrera

- **GIVEN** un rol `docente` recibe `materia_id` pero no `carrera_id`
- **WHEN** se intenta guardar la membresía
- **THEN** la API rechaza la operación y conserva las membresías anteriores

#### Scenario: Combinación materia–carrera que no se dicta

- **GIVEN** un rol `docente` referencia una materia y una carrera que no tienen pertenencia activa en el catálogo informativo materia–plan
- **WHEN** se intenta guardar la membresía
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
