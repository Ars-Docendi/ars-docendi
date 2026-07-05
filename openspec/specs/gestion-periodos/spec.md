## Purpose

Gestión (ABM) de los períodos de designación docente: la Secretaría Académica crea, edita y elimina los períodos sobre los que el Jefe de Cátedra carga los pedidos de designación (SCRUM-82). Define el contenedor temporal del proyecto docente mediante dos ventanas separadas —la ventana de carga (desde/hasta) donde se admiten pedidos, y la ventana de impacto (desde/hasta) donde esas designaciones tienen efecto— y un estado activo/inactivo, con la regla de que a lo sumo un período puede estar activo a la vez.

## Requirements

### Requirement: Listar períodos de designación

El sistema SHALL mostrar una tabla con todos los períodos de designación registrados, incluyendo nombre, ventana de carga (desde/hasta), ventana de impacto (desde/hasta) y estado activo/inactivo.

#### Scenario: Lista con períodos cargados

- **WHEN** el usuario navega a `/designaciones/periodos`
- **THEN** el sistema muestra una tabla con al menos un período y las columnas: Nombre, Carga desde, Carga hasta, Impacto desde, Impacto hasta, Activo, Acciones

#### Scenario: Impacto se muestra en formato mes/año

- **WHEN** la tabla renderiza las columnas de Impacto desde/hasta
- **THEN** el sistema SHALL mostrar únicamente mes y año (ej. "Agosto 2026"), truncando el día almacenado

#### Scenario: Estado visual del período activo

- **WHEN** un período tiene `activo: true`
- **THEN** la columna Activo SHALL mostrar el texto "Activo" (de solo lectura, sin control interactivo en la tabla)

#### Scenario: Estado visual del período inactivo

- **WHEN** un período tiene `activo: false`
- **THEN** la columna Activo SHALL mostrar el texto "Inactivo" (de solo lectura, sin control interactivo en la tabla)

---

### Requirement: Crear período de designación

El sistema SHALL permitir crear un nuevo período de designación mediante un formulario modal con los campos obligatorios: nombre, fecha de carga desde, fecha de carga hasta, fecha de impacto desde y fecha de impacto hasta.

#### Scenario: Apertura del modal de creación

- **WHEN** el usuario hace clic en "Nuevo período"
- **THEN** el sistema muestra un modal con título "Nuevo período" y todos los campos vacíos

#### Scenario: Campos obligatorios presentes

- **WHEN** el modal de creación está abierto
- **THEN** el sistema SHALL mostrar los campos: Nombre (Input, obligatorio, vacío), Carga desde (DatePicker, obligatorio), Carga hasta (DatePicker, obligatorio), Impacto desde (DatePicker, obligatorio), Impacto hasta (DatePicker, obligatorio)

#### Scenario: Sin campo de cuatrimestre, año o estado en el modal

- **WHEN** el modal de creación o edición está abierto
- **THEN** el sistema NO SHALL mostrar los campos Cuatrimestre, Año ni un selector de Estado

#### Scenario: Sugerencia de fecha de carga hasta

- **WHEN** el usuario completa "Impacto desde" y el campo "Carga hasta" está vacío
- **THEN** el sistema SHALL pre-completar "Carga hasta" con la fecha correspondiente a un mes antes de "Impacto desde", editable por el usuario

#### Scenario: Validación de rango carga inválido

- **WHEN** el usuario intenta guardar con "Carga hasta" anterior a "Carga desde"
- **THEN** el sistema SHALL impedir el guardado y mostrar un error indicando que la fecha de carga hasta debe ser posterior o igual a la de carga desde

#### Scenario: Validación de rango impacto inválido

- **WHEN** el usuario intenta guardar con "Impacto hasta" anterior a "Impacto desde"
- **THEN** el sistema SHALL impedir el guardado y mostrar un error indicando que la fecha de impacto hasta debe ser posterior o igual a la de impacto desde

#### Scenario: Cancelar creación

- **WHEN** el usuario hace clic en "Cancelar" en el modal de creación
- **THEN** el modal SHALL cerrarse sin modificar la lista de períodos

#### Scenario: Slot de error disponible para validación de backend

- **WHEN** el backend devuelva un error de validación (ej: solapamiento de fechas)
- **THEN** el sistema SHALL mostrar un InlineAlert de severidad "warning" dentro del modal con el mensaje de error recibido

---

### Requirement: Editar período de designación

El sistema SHALL permitir modificar los datos de un período existente mediante el mismo formulario modal, pre-poblado con los valores actuales (nombre, ventana de carga, ventana de impacto).

#### Scenario: Apertura del modal de edición

- **WHEN** el usuario hace clic en el botón de editar de una fila
- **THEN** el sistema muestra un modal con título "Editar período" y los campos Nombre, Carga desde, Carga hasta, Impacto desde e Impacto hasta pre-poblados con los datos del período seleccionado

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
- **THEN** la tabla SHALL mostrar al menos un período con `activo: true` y varios con `activo: false`, y exactamente uno con `activo: true`

#### Scenario: Variedad de ventanas de carga e impacto

- **WHEN** el usuario accede a `/designaciones/periodos` en modo mock
- **THEN** la tabla SHALL mostrar períodos con distintas ventanas de carga e impacto para simular un historial realista

---

### Requirement: Activar y desactivar período de designación

El sistema SHALL permitir definir si un período está activo mediante un campo `Toggle` ("Período activo") dentro del formulario de creación/edición, confirmado junto con el resto de los datos al hacer clic en "Guardar". El sistema SHALL impedir que exista más de un período activo simultáneamente.

#### Scenario: Campo "Período activo" presente en creación y edición

- **WHEN** el modal de creación o edición está abierto
- **THEN** el sistema SHALL mostrar un `Toggle` "Período activo" al final del formulario, después de la ventana de carga y el período de impacto

#### Scenario: Rechazo al guardar con un segundo período activo

- **WHEN** el usuario hace clic en "Guardar" con el `Toggle` "Período activo" en `true` mientras otro período (distinto al que se está guardando) ya tiene `activo: true`
- **THEN** el sistema SHALL impedir el guardado y mostrar un error bajo el campo `Toggle` indicando cuál es el período actualmente activo, sin activar el nuevo ni desactivar el existente

#### Scenario: Guardar como activo sin conflicto

- **WHEN** el usuario hace clic en "Guardar" con el `Toggle` "Período activo" en `true` y ningún otro período tiene `activo: true`
- **THEN** el sistema SHALL guardar el período con `activo: true` y cerrar el modal, sin pedir confirmación

#### Scenario: Desactivar un período ya activo pide confirmación

- **WHEN** el usuario edita un período con `activo: true`, apaga el `Toggle` "Período activo" y hace clic en "Guardar"
- **THEN** el sistema SHALL cerrar el modal de edición y mostrar un modal de confirmación identificando el período antes de aplicar el cambio

#### Scenario: Confirmar desactivación

- **WHEN** el usuario confirma la desactivación en el modal de confirmación
- **THEN** el sistema SHALL guardar el período con `activo: false` (junto con el resto de los cambios pendientes del formulario) y cerrar el modal

#### Scenario: Cancelar desactivación

- **WHEN** el usuario cancela en el modal de confirmación de desactivación
- **THEN** el período SHALL permanecer activo, sin aplicar ningún cambio pendiente del formulario, y el modal de confirmación SHALL cerrarse

#### Scenario: Crear o editar sin transición de activo a inactivo

- **WHEN** el usuario hace clic en "Guardar" en cualquier otro caso (creación con cualquier valor de `activo`, o edición sin pasar de `activo: true` a `activo: false`)
- **THEN** el sistema SHALL guardar directamente sin pedir confirmación
