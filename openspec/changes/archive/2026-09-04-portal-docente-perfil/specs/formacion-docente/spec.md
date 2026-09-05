## ADDED Requirements

### Requirement: Gestión de la formación académica

El sistema SHALL permitir al docente administrar su formación como una lista de ítems, pudiendo agregar, editar y eliminar entradas. Cada ítem MUST tener un **nivel** (enum cerrado: Grado / Especialización / Maestría / Doctorado), la carrera o título, la institución y el período cursado. El alta y la edición MUST hacerse en un diálogo. Los cambios se persisten en el store mock local. La formación es **informativa**: el sistema MUST NOT someterla a ningún circuito de aprobación o validación.

#### Scenario: Agregar un título

- **GIVEN** un docente en la sección Educación de su Portal
- **WHEN** agrega un ítem con nivel, carrera, institución y período, y confirma
- **THEN** el ítem aparece en su lista de formación

#### Scenario: Nivel restringido al enum

- **WHEN** el docente selecciona el nivel de un ítem de formación
- **THEN** solo puede elegir entre Grado, Especialización, Maestría y Doctorado

#### Scenario: Editar un título cargado

- **GIVEN** un ítem de formación ya cargado
- **WHEN** el docente lo edita y confirma
- **THEN** la lista refleja los valores actualizados

#### Scenario: Eliminar un título

- **GIVEN** un ítem de formación ya cargado
- **WHEN** el docente lo elimina y confirma la acción
- **THEN** el ítem desaparece de su lista

#### Scenario: Sección de formación vacía

- **GIVEN** un docente sin formación cargada
- **WHEN** abre `/portal`
- **THEN** la sección Educación se presenta como fila compacta con su control de alta

### Requirement: Gestión de certificaciones

El sistema SHALL permitir al docente administrar sus certificaciones como una lista de ítems, pudiendo agregar, editar y eliminar entradas. Cada ítem MUST tener nombre, emisor y fecha, y MAY tener una fecha de vencimiento. Las certificaciones son **informativas**: el sistema MUST NOT someterlas a ningún circuito de aprobación o validación.

#### Scenario: Agregar una certificación

- **GIVEN** un docente en la sección Certificaciones de su Portal
- **WHEN** agrega un ítem con nombre, emisor y fecha, y confirma
- **THEN** el ítem aparece en su lista de certificaciones

#### Scenario: Certificación con vencimiento

- **WHEN** el docente carga una certificación e indica su fecha de vencimiento
- **THEN** la lista muestra la certificación con su vencimiento

#### Scenario: Certificación sin vencimiento

- **WHEN** el docente carga una certificación sin indicar vencimiento
- **THEN** el ítem se guarda igualmente, sin exigir esa fecha

#### Scenario: Eliminar una certificación

- **GIVEN** una certificación ya cargada
- **WHEN** el docente la elimina y confirma la acción
- **THEN** el ítem desaparece de su lista

### Requirement: Confirmación antes de eliminar

El sistema SHALL pedir confirmación antes de eliminar un ítem de formación o una certificación, indicando qué se está por borrar y advirtiendo que la acción no se puede deshacer. El sistema MUST NOT exigir un justificativo: es información propia del docente.

#### Scenario: Confirmar el borrado

- **GIVEN** un ítem cargado
- **WHEN** el docente pide eliminarlo
- **THEN** el sistema pide confirmación indicando qué se borra y que no se puede deshacer

#### Scenario: Cancelar el borrado

- **GIVEN** la confirmación de borrado abierta
- **WHEN** el docente cancela
- **THEN** el ítem se conserva en la lista
