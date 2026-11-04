# Spec Delta

## ADDED Requirements

### Requirement: Perfil usable en pantallas angostas

"Mi portal" SHALL poder leerse y editarse en un viewport de 360 px de ancho o más sin scroll horizontal. Todas las acciones de sección y de ítem MUST seguir alcanzables, y los diálogos de alta y edición MUST entrar en el ancho de la pantalla. Los controles táctiles SHALL tener un área de toque de al menos 24 × 24 px. El orden y el contenido de las secciones no cambian respecto de escritorio.

#### Scenario: Leer el perfil desde el teléfono

- **GIVEN** un docente con el perfil cargado y un viewport de 375 px
- **WHEN** abre `/portal`
- **THEN** ve las mismas secciones, en el mismo orden que en escritorio
- **AND** la página no tiene scroll horizontal

#### Scenario: Editar un ítem desde el teléfono

- **GIVEN** un docente en `/portal` con un viewport de 375 px
- **WHEN** toca "+ Agregar" en Experiencia
- **THEN** el diálogo entra en el ancho de la pantalla, con sus campos y botones visibles sin scroll horizontal
- **AND** al guardar, el aviso de guardado se muestra dentro de la pantalla

#### Scenario: Quitar un tag con el dedo

- **GIVEN** un docente en un viewport angosto, editando Habilidades
- **WHEN** toca el control para quitar un tag
- **THEN** el área de toque del control mide al menos 24 × 24 px
