## ADDED Requirements

### Requirement: Estructura uniforme del encabezado de página

Toda pantalla autenticada SHALL encabezarse con un breadcrumb seguido de un `PageHeader` con título, y opcionalmente meta y acciones. El encabezado MUST NOT mostrar un pretitle (línea en mayúsculas sobre el título). El meta SHALL mostrar solo información que no aparece en otro lugar de la misma pantalla, y MUST NOT repetir el rol del usuario, que ya se muestra en la barra superior.

#### Scenario: Ninguna pantalla muestra pretitle

- **GIVEN** un usuario con permisos para todas las pantallas
- **WHEN** navega a cualquiera de ellas
- **THEN** el encabezado MUST NOT mostrar un pretitle (por ejemplo "Designaciones", "Configuración" o "Cuatrimestre 2026 · 1C")

#### Scenario: El meta de Revisión no repite el rol

- **GIVEN** un usuario con `designaciones.revisar`
- **WHEN** abre `/designaciones/revision`
- **THEN** el meta MUST NOT incluir el nombre del rol del usuario

### Requirement: Mayúscula solo en la primera palabra

Los títulos de página, los niveles del breadcrumb, los labels del sidebar y los nombres de grupo del sidebar SHALL escribirse con mayúscula solo en la primera palabra (y en nombres propios). Los nombres de grupo MAY mostrarse en mayúsculas mediante estilo visual, pero su texto fuente MUST respetar esta regla.

#### Scenario: Grupo Designaciones con el mismo formato que los demás

- **WHEN** se define la navegación del sidebar
- **THEN** el grupo de designaciones MUST tener como texto "Designaciones", igual que "Personal", "Trabajo" y "Configuración"

#### Scenario: Título sin mayúsculas intermedias

- **WHEN** el usuario abre `/aulas`
- **THEN** el título MUST ser "Reserva de aulas" (no "Reserva de Aulas")

### Requirement: Breadcrumb sin niveles de sección

El breadcrumb SHALL comenzar en "Inicio" y terminar en la página actual, y cada nivel intermedio MUST ser una pantalla navegable. MUST NOT incluir el nombre del grupo del sidebar (Personal, Trabajo, Designaciones, Configuración), porque los grupos no son pantallas. El último nivel SHALL coincidir con el label del sidebar de la pantalla, cuando lo tiene.

#### Scenario: Períodos sin nivel de grupo

- **WHEN** el usuario abre `/designaciones/periodos`
- **THEN** el breadcrumb MUST ser "Inicio › Períodos"

#### Scenario: Revisión usa el label del sidebar

- **WHEN** el usuario abre `/designaciones/revision`
- **THEN** el breadcrumb MUST ser "Inicio › Revisión"

#### Scenario: Configuración no agrega nivel de grupo

- **WHEN** el usuario abre `/usuarios`
- **THEN** el breadcrumb MUST ser "Inicio › Usuarios"

### Requirement: Títulos de las pantallas

El título de cada pantalla SHALL coincidir con su label del sidebar. Las pantallas de administración de Configuración MUST titularse "Administración de …", y el Jefe de Cátedra MUST ver "Mis docentes" en la pantalla de docentes. Los títulos vigentes son:

| Ruta                                | Título                                                      | Último nivel del breadcrumb               |
| ----------------------------------- | ----------------------------------------------------------- | ----------------------------------------- |
| `/portal`                           | Mi portal                                                   | Mi portal                                 |
| `/aulas`                            | Reserva de aulas                                            | Reserva de aulas                          |
| `/tareas`                           | Tareas                                                      | Tareas                                    |
| `/designaciones/mis-pedidos`        | Mis pedidos                                                 | Mis pedidos                               |
| `/designaciones/pedidos/nuevo`      | Nuevo pedido                                                | Nuevo pedido                              |
| `/designaciones/pedidos/:id/editar` | Editar pedido                                               | Editar pedido                             |
| `/designaciones/revision`           | Revisión                                                    | Revisión                                  |
| `/designaciones/pedidos/:id`        | Tipo de novedad del pedido                                  | Detalle del pedido                        |
| `/designaciones/periodos`           | Períodos                                                    | Períodos                                  |
| `/usuarios`                         | Administración de usuarios                                  | Usuarios                                  |
| `/docentes`                         | Administración de docentes / Mis docentes (Jefe de Cátedra) | Docentes / Mis docentes (Jefe de Cátedra) |
| `/roles`                            | Administración de roles                                     | Roles                                     |

#### Scenario: Título igual al sidebar

- **WHEN** el usuario abre `/designaciones/revision`
- **THEN** el título MUST ser "Revisión"

#### Scenario: Pantallas de administración conservan su prefijo

- **GIVEN** un usuario de Secretaría Académica
- **WHEN** abre `/docentes`
- **THEN** el título MUST ser "Administración de docentes"

#### Scenario: El Jefe de Cátedra ve sus docentes

- **GIVEN** un Jefe de Cátedra
- **WHEN** abre `/docentes`
- **THEN** el título y el último nivel del breadcrumb MUST ser "Mis docentes"

#### Scenario: El formulario de pedido usa el encabezado estándar

- **GIVEN** un Jefe de Cátedra editando un pedido en borrador
- **WHEN** abre `/designaciones/pedidos/:id/editar`
- **THEN** el sistema SHALL mostrar el título "Editar pedido" mediante `PageHeader`
- **AND** el breadcrumb MUST ser "Inicio › Detalle del pedido › Editar pedido", sin el número de pedido

### Requirement: Encabezado del detalle del pedido sin datos repetidos

El encabezado del detalle del pedido (`/designaciones/pedidos/:id`) SHALL tener como título solo el tipo de novedad del pedido, y como meta solo el período del pedido. El encabezado MUST NOT mostrar el número de pedido, la cátedra ni la carrera, que ya se presentan en la tarjeta de datos del pedido.

#### Scenario: Detalle de un pedido de cambio

- **GIVEN** un pedido de "Cambio de cargo o dedicación" de la cátedra Ingeniería de Software en el período "Segundo cuatrimestre 2026"
- **WHEN** el usuario abre su detalle
- **THEN** el título MUST ser "Cambio de cargo o dedicación"
- **AND** el meta MUST ser "Segundo cuatrimestre 2026"
- **AND** el encabezado MUST NOT mostrar el número de pedido ni el nombre de la cátedra

#### Scenario: El número se conserva fuera del detalle

- **GIVEN** un pedido con número asignado
- **WHEN** el Jefe de Cátedra consulta "Mis pedidos"
- **THEN** la columna N° SHALL seguir mostrando el número del pedido

### Requirement: Breadcrumb del detalle según el origen

El breadcrumb del detalle del pedido SHALL mostrar como nivel intermedio la pantalla desde la que el usuario llegó: "Mis pedidos" (`/designaciones/mis-pedidos`) o "Revisión" (`/designaciones/revision`). Si no se conoce el origen, porque el usuario abrió el detalle con un link directo, el sistema MUST usar "Revisión" cuando el usuario tiene `designaciones.revisar`, y "Mis pedidos" en caso contrario. El breadcrumb MUST NOT llevar a una pantalla para la que el usuario no tiene permiso.

#### Scenario: Desde Mis pedidos

- **GIVEN** un Jefe de Cátedra en "Mis pedidos"
- **WHEN** abre el detalle de un pedido
- **THEN** el breadcrumb MUST ser "Inicio › Mis pedidos › Detalle del pedido", con "Mis pedidos" apuntando a `/designaciones/mis-pedidos`

#### Scenario: Desde Revisión

- **GIVEN** un Coordinador en "Revisión"
- **WHEN** abre el detalle de un pedido
- **THEN** el breadcrumb MUST ser "Inicio › Revisión › Detalle del pedido", con "Revisión" apuntando a `/designaciones/revision`

#### Scenario: Link directo sin permiso de revisión

- **GIVEN** un Jefe de Cátedra sin `designaciones.revisar`
- **WHEN** abre `/designaciones/pedidos/:id` con un link directo
- **THEN** el nivel intermedio del breadcrumb MUST ser "Mis pedidos"

#### Scenario: Link directo con permiso de revisión

- **GIVEN** un Coordinador con `designaciones.revisar`
- **WHEN** abre `/designaciones/pedidos/:id` con un link directo
- **THEN** el nivel intermedio del breadcrumb MUST ser "Revisión"
