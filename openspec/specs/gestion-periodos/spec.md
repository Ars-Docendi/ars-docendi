## Purpose

Gestión (ABM) de los períodos de designación docente: la Secretaría Académica crea, edita y elimina los períodos sobre los que el Jefe de Cátedra carga los pedidos de designación (SCRUM-82). Define el contenedor temporal del proyecto docente (cuatrimestre/año, fechas de apertura y cierre, estado).

## Requirements

### Requirement: Listar períodos de designación

El sistema SHALL mostrar una tabla con todos los períodos de designación registrados, incluyendo nombre, cuatrimestre, año, fecha de apertura, fecha de cierre y estado.

#### Scenario: Lista con períodos cargados

- **WHEN** el usuario navega a `/designaciones/periodos`
- **THEN** el sistema muestra una tabla con al menos un período y las columnas: Nombre, Cuatrimestre, Año, Apertura, Cierre, Estado, Acciones

#### Scenario: Estado visual diferenciado

- **WHEN** un período tiene estado Abierto
- **THEN** el badge de estado SHALL mostrarse con color verde y la etiqueta "Abierto"

#### Scenario: Estado visual período cerrado

- **WHEN** un período tiene estado Cerrado
- **THEN** el badge de estado SHALL mostrarse con color neutro y la etiqueta "Cerrado"

#### Scenario: Estado visual período próximo

- **WHEN** un período tiene estado Próximo
- **THEN** el badge de estado SHALL mostrarse con color de advertencia y la etiqueta "Próximo"

---

### Requirement: Crear período de designación

El sistema SHALL permitir crear un nuevo período de designación mediante un formulario modal con los campos: nombre, cuatrimestre, año, fecha de apertura, fecha de cierre y estado.

#### Scenario: Apertura del modal de creación

- **WHEN** el usuario hace clic en "Nuevo período"
- **THEN** el sistema muestra un modal con título "Nuevo período" y todos los campos vacíos

#### Scenario: Campos obligatorios presentes

- **WHEN** el modal de creación está abierto
- **THEN** el sistema SHALL mostrar los campos: Nombre (Input), Cuatrimestre (Select: 1C/2C/Verano), Año (Input numérico), Fecha de apertura (DatePicker), Fecha de cierre (DatePicker), Estado (Select: Abierto/Cerrado/Próximo)

#### Scenario: Cancelar creación

- **WHEN** el usuario hace clic en "Cancelar" en el modal de creación
- **THEN** el modal SHALL cerrarse sin modificar la lista de períodos

#### Scenario: Slot de error disponible para validación de backend

- **WHEN** el backend devuelva un error de validación (ej: solapamiento de fechas)
- **THEN** el sistema SHALL mostrar un InlineAlert de severidad "warning" dentro del modal con el mensaje de error recibido

---

### Requirement: Editar período de designación

El sistema SHALL permitir modificar los datos de un período existente mediante el mismo formulario modal, pre-poblado con los valores actuales.

#### Scenario: Apertura del modal de edición

- **WHEN** el usuario hace clic en el botón de editar de una fila
- **THEN** el sistema muestra un modal con título "Editar período" y los campos pre-poblados con los datos del período seleccionado

#### Scenario: Cancelar edición

- **WHEN** el usuario hace clic en "Cancelar" en el modal de edición
- **THEN** el modal SHALL cerrarse sin modificar los datos del período

#### Scenario: Slot de error disponible en edición

- **WHEN** el backend devuelva un error al guardar la edición
- **THEN** el sistema SHALL mostrar un InlineAlert de severidad "warning" dentro del modal con el mensaje de error

---

### Requirement: Eliminar período de designación

El sistema SHALL requerir confirmación explícita antes de eliminar un período, mostrando un modal de confirmación que identifique el período afectado.

#### Scenario: Apertura del modal de confirmación

- **WHEN** el usuario hace clic en el botón de eliminar de una fila
- **THEN** el sistema muestra un modal con título "Eliminar período" que menciona el nombre del período a eliminar

#### Scenario: Cancelar eliminación

- **WHEN** el usuario hace clic en "Cancelar" en el modal de eliminación
- **THEN** el modal SHALL cerrarse sin eliminar el período

#### Scenario: Confirmar eliminación

- **WHEN** el usuario hace clic en "Eliminar" (variant destructive)
- **THEN** el sistema SHALL eliminar el período de la lista y cerrar el modal

#### Scenario: Slot de error para restricciones de backend

- **WHEN** el backend rechace la eliminación (ej: período con pedidos asociados)
- **THEN** el sistema SHALL mostrar un InlineAlert de severidad "danger" dentro del modal de confirmación con el motivo del rechazo, sin cerrar el modal

---

### Requirement: Mock data para validación visual

El sistema SHALL mostrar datos de prueba representativos que permitan validar todos los estados visuales posibles sin necesidad de backend.

#### Scenario: Variedad de estados en mock

- **WHEN** el usuario accede a `/designaciones/periodos` en modo mock
- **THEN** la tabla SHALL mostrar al menos un período Abierto, al menos un período Cerrado y al menos un período Próximo

#### Scenario: Variedad de cuatrimestres y años

- **WHEN** el usuario accede a `/designaciones/periodos` en modo mock
- **THEN** la tabla SHALL mostrar períodos de distintos años y cuatrimestres para simular un historial realista
