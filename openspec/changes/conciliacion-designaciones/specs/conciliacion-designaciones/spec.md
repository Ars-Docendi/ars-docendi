# Spec Delta

## Purpose

Conciliar los pedidos en lote del período activo con el resultado de la rectificación. La conciliación decide qué pedidos se aprueban y se sincronizan con las designaciones vigentes, y cuáles se rechazan.

## ADDED Requirements

### Requirement: Permisos de conciliación y edición posterior al circuito

El catálogo de permisos SHALL incluir `designaciones.conciliar` y `designaciones.editar`, asignables por rol desde la membresía de roles. Una migración SHALL concederlos inicialmente a Secretaría y Administrativo. Backend y frontend MUST decidir por permiso, nunca por código de rol.

#### Scenario: Rol personalizado con permiso de conciliar

- **GIVEN** un rol personalizado al que se le concedió `designaciones.conciliar`
- **WHEN** un usuario con ese rol abre la conciliación
- **THEN** el sistema MUST permitir el acceso dentro de su ámbito

#### Scenario: Llamada directa sin permiso

- **GIVEN** un usuario sin `designaciones.conciliar`
- **WHEN** llama a la consulta o a la sincronización de la conciliación por API
- **THEN** el sistema MUST responder 403 sin datos de pedidos

### Requirement: Pantalla de conciliación por docente

La ruta `/designaciones/conciliacion` SHALL mostrar los pedidos del período activo agrupados por docente, en tres pestañas: Por conciliar (`en_lote`), Rechazados (`no_aprobado`) y Sincronizados (`aprobado`). Cada docente SHALL mostrar su CUIL, sus carreras y sus materias con lo que tiene hoy, cómo queda, la carrera, la novedad y el número de pedido. La pantalla MUST representar los estados Loading, Empty, Error y Success.

#### Scenario: Docente con un Cambio y otras materias

- **GIVEN** un docente con designaciones vigentes en dos materias de carreras distintas y un Cambio `en_lote` sobre una de ellas
- **WHEN** quien concilia despliega al docente
- **THEN** ve las dos materias con su carrera, la del Cambio con sus valores de hoy y los que quedan, y la otra marcada "Sin cambios"

#### Scenario: Alta de una persona sin designaciones

- **GIVEN** un Alta `en_lote` de una persona sin designaciones vigentes
- **WHEN** se muestra en la conciliación
- **THEN** la columna Hoy dice "Sin designación" y el docente se identifica como docente nuevo

#### Scenario: Baja

- **GIVEN** una Baja `en_lote`
- **WHEN** se muestra en la conciliación
- **THEN** la columna Queda dice "Se da de baja" y se muestra el tipo de baja, con su detalle si es Otro

#### Scenario: Sin pedidos por conciliar

- **GIVEN** un período activo sin pedidos `en_lote` en el ámbito
- **WHEN** se abre la pestaña Por conciliar
- **THEN** el sistema informa que acá se ven únicamente los pedidos en lote del período activo

### Requirement: Datos completos de cada movimiento

Cada movimiento SHALL mostrar cargo, dedicación, horas de materia, horas de investigación y horas externas, tanto de hoy como de cómo queda. Además SHALL mostrar la justificación de un Cambio y el tipo de baja de una Baja.

#### Scenario: Cambio de horas complementarias

- **GIVEN** un Cambio que sólo modifica las horas de investigación
- **WHEN** se muestra el movimiento
- **THEN** se ven las horas de investigación de hoy y las que quedan, junto con cargo, dedicación y horas de materia

### Requirement: Aceptación por docente

Quien concilia SHALL aceptar los cambios de un docente en conjunto, o de todos los docentes visibles a la vez, y nunca uno por uno. La aceptación es una selección de la pantalla y MUST NOT persistirse hasta sincronizar. Editar o rechazar un cambio de un docente aceptado MUST quitarle la aceptación.

#### Scenario: Editar retira la aceptación

- **GIVEN** un docente aceptado en la pantalla
- **WHEN** quien concilia edita uno de sus pedidos
- **THEN** el docente queda sin aceptar hasta que se lo vuelva a tildar

### Requirement: Rechazo en la conciliación

Quien tiene `designaciones.conciliar` SHALL poder rechazar un pedido `en_lote` con un motivo obligatorio. El pedido pasa a `no_aprobado` y queda un evento en el historial con el motivo. Un pedido `no_aprobado` MUST NOT materializarse ni exportarse en el lote, y MUST seguir ocupando el cupo del docente en el período [BR-designaciones-001].

#### Scenario: Rechazo sin motivo

- **GIVEN** un pedido `en_lote`
- **WHEN** quien concilia lo rechaza sin motivo
- **THEN** el sistema MUST denegarlo indicando que el motivo es obligatorio

#### Scenario: Rechazo con motivo

- **GIVEN** un pedido `en_lote`
- **WHEN** quien concilia lo rechaza con motivo
- **THEN** el pedido pasa a `no_aprobado`, aparece en Rechazados con su motivo y queda registrado y sellado en el historial

#### Scenario: Pedido rechazado en el circuito

- **GIVEN** un pedido `rechazado` antes de llegar al lote
- **WHEN** se abre la conciliación
- **THEN** el pedido no aparece y MUST NOT poder editarse ni conciliarse

### Requirement: Edición con permiso antes de sincronizar

Quien tiene `designaciones.editar` SHALL poder editar cualquier campo de un pedido `en_lote` o `no_aprobado` de su ámbito, con las mismas validaciones del alta y edición. Al guardar un `no_aprobado`, éste vuelve a `en_lote`, y el formulario MUST avisar antes de guardar que después se podrá conciliar y sincronizar. Un pedido `aprobado` MUST NOT editarse.

#### Scenario: Editar un rechazado de la conciliación

- **GIVEN** un pedido `no_aprobado`
- **WHEN** un usuario con `designaciones.editar` lo edita con datos válidos
- **THEN** el pedido vuelve a `en_lote`, aparece en Por conciliar y el historial registra la edición

#### Scenario: Editar un sincronizado

- **GIVEN** un pedido `aprobado`
- **WHEN** cualquier usuario intenta editarlo por API
- **THEN** el sistema MUST denegarlo sin modificar el pedido ni las designaciones

#### Scenario: Cambio sin justificación

- **GIVEN** un pedido `en_lote` de Cambio
- **WHEN** se lo edita dejando la justificación vacía
- **THEN** el sistema MUST denegarlo [BR-designaciones-004]

### Requirement: Vista previa de la sincronización

Antes de sincronizar, la pantalla SHALL mostrar una vista previa con la cantidad de docentes nuevos, altas, cambios y bajas, y el detalle por docente de cada movimiento con su carrera. También SHALL explicar, sin tono de alerta, qué se actualiza en la base (designaciones por materia y rol Docente) y que los pedidos ya no se podrán editar.

#### Scenario: Vista previa con docentes aceptados

- **GIVEN** dos docentes aceptados, uno con un Cambio y otro con una Baja
- **WHEN** quien concilia pulsa Sincronizar
- **THEN** ve 1 Cambio y 1 Baja con el detalle de cada docente antes de confirmar

### Requirement: Sincronización atómica

La sincronización SHALL recibir los pedidos aceptados con su versión. En una única transacción SHALL materializar cada uno sobre las designaciones vigentes, pasarlo a `aprobado` y registrar y sellar su evento. Si algún pedido no está `en_lote`, cambió de versión o falla su materialización, MUST NOT aplicarse ninguno. La operación MUST ser idempotente ante reintentos.

#### Scenario: Sincronización exitosa

- **GIVEN** un Alta, una Baja y un Cambio `en_lote` aceptados
- **WHEN** se confirma la sincronización
- **THEN** se abre, se cierra y se cambia la designación correspondiente, los tres pedidos quedan `aprobado` y aparecen en Sincronizados como "Sincronizado"

#### Scenario: Pedido modificado mientras se concilia

- **GIVEN** un pedido aceptado cuya versión cambió después de cargar la pantalla
- **WHEN** se confirma la sincronización
- **THEN** el sistema MUST responder conflicto identificando el pedido por número y MUST NOT aplicar ningún cambio

#### Scenario: Reintento de la misma sincronización

- **GIVEN** una sincronización confirmada con su clave idempotente
- **WHEN** el cliente la reintenta
- **THEN** no se duplican designaciones, estados ni eventos

### Requirement: Persona, rol y materia al sincronizar

Al sincronizar un Alta, la persona del pedido SHALL quedar con su designación vigente (cargo, dedicación y tres horas) y con el rol Docente en esa materia y carrera. Si ya tiene cuenta, la asignación MUST escribirse en el mismo momento. Si no tiene mail, la administración SHALL ver ese rol precargado al crearle la cuenta y SHALL poder quitarlo antes de guardar. Una Baja MUST retirar el rol Docente de esa materia y carrera.

#### Scenario: Alta de una persona con cuenta

- **GIVEN** un Alta `en_lote` de una persona que ya tiene cuenta
- **WHEN** se sincroniza
- **THEN** la persona tiene la designación vigente y el rol Docente en la materia y carrera del pedido, en la misma transacción

#### Scenario: Alta de una persona sin mail

- **GIVEN** un Alta sincronizada de una persona sin cuenta
- **WHEN** la administración abre la persona, le carga el mail y guarda sin tocar los roles
- **THEN** el editor muestra precargado el rol Docente en cada materia y carrera de sus designaciones vigentes, y la cuenta se crea con esos roles sin que el operador los cargue a mano

#### Scenario: El operador quita el rol precargado

- **GIVEN** una persona sin cuenta con el rol Docente precargado
- **WHEN** el operador lo quita, carga el mail y guarda
- **THEN** la cuenta se crea sin ese rol y la designación vigente se conserva

#### Scenario: Baja con rol asignado

- **GIVEN** una persona con cuenta y rol Docente en la materia de una Baja `en_lote`
- **WHEN** se sincroniza
- **THEN** la designación se cierra y el rol Docente de esa materia y carrera queda dado de baja, y los otros roles se conservan

#### Scenario: Cambio de cargo o dedicación

- **GIVEN** un Cambio `en_lote` de una persona con rol Docente en esa materia
- **WHEN** se sincroniza
- **THEN** la designación toma el cargo, la dedicación y las horas nuevas, y el rol Docente queda igual

### Requirement: Navegación a Conciliación

El sector DESIGNACIONES SHALL mostrar el enlace Conciliación sólo a quien tiene `designaciones.conciliar`. La ruta MUST estar protegida por ese permiso.

#### Scenario: Usuario sin permiso

- **GIVEN** un Jefe de Cátedra sin `designaciones.conciliar`
- **WHEN** navega la aplicación o abre `/designaciones/conciliacion` directamente
- **THEN** no ve el enlace y la ruta le niega el acceso
