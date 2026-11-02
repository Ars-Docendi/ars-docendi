## Purpose

Permite que una persona con varias asignaciones de rol elija con cuál opera la sesión y la cambie sin volver a ingresar, para que permisos y ámbito correspondan siempre a una única asignación.

## ADDED Requirements

### Requirement: Opciones de rol por asignación

El sistema SHALL ofrecer como opción cada asignación de rol vigente de la persona, identificada por el nombre del rol y su ámbito (global, materia o carrera). El mismo rol asignado en dos ámbitos distintos SHALL aparecer como dos opciones. Las opciones MUST NOT incluir asignaciones eliminadas ni de roles inactivos.

#### Scenario: Mismo rol en dos materias

- **GIVEN** una persona con el rol Jefe de Cátedra en dos materias
- **WHEN** consulta sus opciones de rol
- **THEN** recibe dos opciones de Jefe de Cátedra, cada una con su materia

#### Scenario: Asignación de un rol inactivo

- **GIVEN** una persona con una asignación de un rol desactivado
- **WHEN** consulta sus opciones de rol
- **THEN** esa asignación no aparece

### Requirement: Elección del rol al ingresar

Después de un ingreso exitoso la sesión SHALL operar siempre con una asignación: la única vigente, o, con varias y mientras la persona no eligió, la primera por nombre de rol y de ámbito. Con varias asignaciones y sin elegir, el frontend SHALL abrir sobre la aplicación el selector de rol con esa asignación marcada; elegir una opción o cerrar el selector SHALL fijarla como elegida. La sesión SHALL informar si la asignación todavía no fue elegida. El sistema MUST NOT recordar la elección entre ingresos.

#### Scenario: Una sola asignación

- **GIVEN** una persona con una única asignación vigente
- **WHEN** ingresa
- **THEN** llega al inicio operando con esa asignación, sin ver el selector

#### Scenario: Varias asignaciones

- **GIVEN** una persona con dos asignaciones vigentes
- **WHEN** ingresa
- **THEN** llega al inicio operando con la primera, ve sobre la aplicación el selector con esa opción marcada y, al elegir otra, pasa a operar con la elegida

#### Scenario: Cerrar el selector conserva el rol por defecto

- **GIVEN** una persona con varias asignaciones que acaba de ingresar
- **WHEN** cierra el selector sin elegir
- **THEN** la asignación por defecto queda como elegida y el selector no vuelve a abrirse en esa sesión

#### Scenario: Ingreso de desarrollo

- **GIVEN** un ambiente con ingreso de desarrollo y una identidad sembrada con varias asignaciones
- **WHEN** se ingresa eligiendo esa identidad
- **THEN** se muestra el mismo selector de rol que en el ingreso con Microsoft

#### Scenario: Nuevo ingreso vuelve a preguntar

- **GIVEN** una persona con varias asignaciones que eligió una y cerró sesión
- **WHEN** vuelve a ingresar
- **THEN** opera con la asignación por defecto y vuelve a ver el selector

### Requirement: Cambio de rol con la sesión abierta

El menú de usuario SHALL mostrar debajo del nombre de la persona el rol y el ámbito de la asignación activa, y SHALL ofrecer "Cambiar rol", junto a "Cerrar sesión", sólo cuando la persona tiene más de una asignación vigente. Elegir otra opción SHALL cambiar la asignación de la sesión sin volver a ingresar, y el frontend SHALL recargar permisos y navegación y llevar al inicio. El backend MUST rechazar una asignación que no sea una asignación vigente de la propia persona.

#### Scenario: Cambiar a otra asignación

- **GIVEN** una persona operando como Jefe de Cátedra de una materia
- **WHEN** elige "Cambiar rol" y selecciona Docente
- **THEN** la siguiente solicitud opera con los permisos y el ámbito de Docente y la navegación muestra sólo lo que ese rol habilita

#### Scenario: Rol activo visible en el menú

- **GIVEN** una persona operando como Jefe de Cátedra de Programación I
- **WHEN** abre el menú de usuario
- **THEN** ve debajo de su nombre "Jefe de Cátedra" y "Programación I"

#### Scenario: Persona con una sola asignación

- **GIVEN** una persona con una única asignación vigente
- **WHEN** abre el menú de usuario
- **THEN** no ve "Cambiar rol"

#### Scenario: Asignación ajena

- **WHEN** una sesión envía como elegida una asignación de otra persona o una eliminada
- **THEN** el backend responde `403` y la sesión conserva su asignación anterior

### Requirement: Ámbito limitado a la asignación activa

Los permisos de la sesión SHALL ser los del rol de la asignación activa, y toda verificación de ámbito sobre el actor de la sesión SHALL considerar sólo la asignación activa, aunque la persona tenga otras asignaciones del mismo rol.

#### Scenario: Jefe de Cátedra en dos materias

- **GIVEN** una persona con Jefe de Cátedra en las materias A y B, operando con la asignación de A
- **WHEN** consulta las personas de su ámbito
- **THEN** ve sólo las personas con designación vigente en A

### Requirement: Asignación que deja de estar vigente

En cada solicitud el sistema SHALL verificar que la asignación activa siga vigente. Si fue eliminada o su rol desactivado, la sesión SHALL pasar a operar con la asignación por defecto: si la persona conserva una sola, esa cuenta como elegida; si conserva varias, el frontend SHALL volver a ofrecer el selector; si no conserva ninguna, la sesión SHALL invalidarse.

#### Scenario: Asignación eliminada con otras vigentes

- **GIVEN** una persona con tres asignaciones operando con una de ellas
- **WHEN** un administrador elimina esa asignación y la persona hace una nueva solicitud
- **THEN** opera con la primera de las dos restantes y el frontend le vuelve a ofrecer elegir
