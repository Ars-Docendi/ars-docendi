## ADDED Requirements

### Requirement: Confirmación de borrado uniforme

Toda acción de eliminar SHALL pedir confirmación con el mismo diálogo, en cualquier pantalla. El diálogo MUST mostrar un título, la pregunta "¿Estás seguro de que querés eliminar …?" identificando lo que se borra, el aviso "Esta acción no se puede deshacer." y los botones "Cancelar" y "Eliminar".

#### Scenario: Eliminar un período

- **GIVEN** un usuario de Secretaría Académica en Períodos
- **WHEN** hace click en eliminar el período "Segundo cuatrimestre 2026"
- **THEN** el sistema SHALL abrir el diálogo "Eliminar período" con la pregunta "¿Estás seguro de que querés eliminar el período "Segundo cuatrimestre 2026"?" y el aviso "Esta acción no se puede deshacer."

#### Scenario: Eliminar un ítem del Portal

- **GIVEN** un docente en su portal
- **WHEN** elige eliminar una certificación
- **THEN** el sistema SHALL abrir el mismo diálogo, titulado "Eliminar certificación"

### Requirement: El borrado en curso no se puede confirmar dos veces

Mientras el borrado se está procesando, el diálogo MUST mostrar el botón "Eliminar" en estado de carga y MUST deshabilitar "Cancelar". Si el borrado falla, el diálogo SHALL seguir abierto y mostrar el motivo bajo el título "No se pudo eliminar".

#### Scenario: Borrado en curso

- **GIVEN** el diálogo de eliminar abierto
- **WHEN** el usuario confirma y el borrado todavía no terminó
- **THEN** "Cancelar" MUST estar deshabilitado y "Eliminar" MUST indicar que está procesando

#### Scenario: Borrado rechazado

- **GIVEN** un período que no se puede eliminar
- **WHEN** el usuario confirma el borrado y el sistema lo rechaza
- **THEN** el diálogo SHALL seguir abierto y mostrar "No se pudo eliminar" con el motivo
