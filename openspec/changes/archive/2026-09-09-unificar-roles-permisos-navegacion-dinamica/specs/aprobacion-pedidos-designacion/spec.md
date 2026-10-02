## MODIFIED Requirements

### Requirement: Routing y gating de las superficies de revisión

El sistema SHALL exponer la ruta `revision` protegida por el permiso `designaciones.revisar`, y las rutas `pedidos/:id` y `pedidos/:id/editar` accesibles a cualquier rol autenticado que tenga la visibilidad indicada por la respuesta autoritativa del backend. La navegación SHALL ofrecer el ítem "Revisión" únicamente a las sesiones con ese permiso, sin links muertos (invariante #7). Los guards de permisos NO SHALL reemplazar las reglas de etapa, ámbito ni la prohibición de que Administración acepte: esas reglas continúan en backend.

#### Scenario: Un usuario sin permiso no accede al tablero

- **GIVEN** un usuario autenticado sin `designaciones.revisar`
- **WHEN** intenta navegar a `/designaciones/revision`
- **THEN** el sistema lo redirige fuera de la ruta y no muestra el ítem "Revisión" en la navegación

#### Scenario: Un rol no revisor no accede al tablero

- **GIVEN** un Docente autenticado sin `designaciones.revisar`
- **WHEN** intenta navegar a `/designaciones/revision`
- **THEN** el sistema lo redirige fuera de la ruta (gate por permiso) y no muestra el ítem "Revisión" en la navegación

#### Scenario: Un rol personalizado con permiso accede al tablero

- **GIVEN** un usuario con rol personalizado y `designaciones.revisar`
- **WHEN** navega a `/designaciones/revision`
- **THEN** el frontend permite la ruta y el backend filtra los pedidos y acciones por la identidad, ámbitos y reglas de dominio vigentes

#### Scenario: El ítem "Revisión" aparece por permiso

- **GIVEN** una sesión con `designaciones.revisar`
- **WHEN** se renderiza la navegación lateral
- **THEN** incluye "Revisión" apuntando a `/designaciones/revision`

#### Scenario: El ítem "Revisión" aparece solo para revisores

- **GIVEN** una sesión con `designaciones.revisar`
- **WHEN** se renderiza la navegación lateral
- **THEN** incluye el ítem "Revisión" apuntando a `/designaciones/revision`
