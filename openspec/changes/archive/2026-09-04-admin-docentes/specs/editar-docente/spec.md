## ADDED Requirements

### Requirement: Modal de edición con dos pestañas

El modal de edición SHALL presentar dos pestañas usando el componente `Tabs` de la ui-lib:

- **"Datos docentes"**: Roles (checkboxes) y Asignaciones (materia + cargo + horas). Es la pestaña que se abre por defecto.
- **"Datos personales"**: Nombre, Apellido, Documento, Legajo, CUIL, Fecha de nacimiento, UPN, Teléfono. Todos los campos son editables.

Un único botón "Guardar cambios" en el footer valida y persiste ambas pestañas. Si al intentar guardar desde la pestaña "Datos docentes" hay campos obligatorios incompletos en "Datos personales", se muestra un `InlineAlert` de advertencia indicando que hay errores en la otra pestaña.

#### Scenario: Modal abre en pestaña Datos docentes por defecto

- **WHEN** el usuario hace clic en "Editar" de cualquier docente
- **THEN** el modal SHALL abrirse con la pestaña "Datos docentes" activa y la pestaña "Datos personales" visible pero no activa

#### Scenario: InlineAlert cuando hay errores en pestaña oculta

- **WHEN** el usuario está en la pestaña "Datos docentes" y hace clic en "Guardar cambios" con campos obligatorios incompletos en "Datos personales"
- **THEN** el sistema SHALL mostrar un `InlineAlert` de severidad `warning` en la pestaña activa indicando que hay errores en la otra pestaña

### Requirement: Edición de datos del docente incluyendo Roles y Asignaciones

Los Roles MUST mostrarse como checkboxes (permitiendo seleccionar ambos simultáneamente) y las Asignaciones SHALL pre-cargarse con los valores actuales y ser editables.

#### Scenario: Apertura del modal pre-poblado con Roles y Asignaciones

- **WHEN** el usuario hace clic en "Editar" de un docente con `roles = ["Jefe de Cátedra"]` y 2 asignaciones
- **THEN** el modal muestra el checkbox "Jefe de Cátedra" tildado y el `AsignacionesSelector` con 2 filas pre-pobladas

#### Scenario: Asignación de múltiples roles

- **WHEN** el usuario tilda ambos checkboxes ("Docente" y "Jefe de Cátedra") y guarda
- **THEN** el docente queda con `roles = ["Docente", "Jefe de Cátedra"]` y la tabla muestra un badge por cada rol

#### Scenario: Ningún rol seleccionado bloquea el guardado

- **WHEN** el usuario destilda todos los checkboxes y hace clic en "Guardar cambios"
- **THEN** se muestra el error "Seleccioná al menos un rol"

#### Scenario: Edición y guardado exitoso

- **WHEN** el usuario modifica el Rol o una asignación y hace clic en "Guardar cambios"
- **THEN** el modal se cierra y la tabla refleja los nuevos datos

#### Scenario: Validación al editar — sin asignaciones

- **WHEN** el usuario quita todas las asignaciones y hace clic en "Guardar cambios"
- **THEN** se muestra error "Agregá al menos una asignación"

#### Scenario: Validación al editar — fila incompleta

- **WHEN** el usuario agrega una fila con materia pero sin cargo y hace clic en "Guardar cambios"
- **THEN** se muestra error "Completá o quitá las filas incompletas"

#### Scenario: UPN duplicada al editar

- **WHEN** el usuario cambia la UPN a una que pertenece a otro docente y guarda
- **THEN** se muestra el error de UPN duplicada

#### Scenario: La propia UPN no se considera duplicada

- **WHEN** el usuario no cambia la UPN y guarda
- **THEN** los cambios se guardan correctamente

#### Scenario: Cambio de Rol visible en tabla

- **WHEN** el usuario cambia el Rol de "Docente" a "Jefe de Cátedra" y guarda
- **THEN** la columna Rol de la tabla muestra "Jefe de Cátedra"

#### Scenario: Cancelar edición

- **WHEN** el usuario hace clic en "Cancelar"
- **THEN** el modal se cierra sin persistir ningún cambio
