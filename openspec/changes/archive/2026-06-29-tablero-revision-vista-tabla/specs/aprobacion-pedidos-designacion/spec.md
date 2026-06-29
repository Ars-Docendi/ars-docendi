## ADDED Requirements

### Requirement: Presentación destacada del pedido rechazado

En el tablero de revisión, un pedido en estado `rechazado` MUST presentarse con un **distintivo de estado "Rechazado"** (en lugar del distintivo de novedad que usan los demás estados) y su **motivo de rechazo** MUST mostrarse **destacado** (citado y diferenciado del resto del detalle), de modo que el revisor identifique de un vistazo que fue rechazado y por qué.

#### Scenario: La card rechazada muestra el distintivo "Rechazado"

- **GIVEN** un pedido en estado `rechazado`
- **WHEN** se renderiza su card en el tablero
- **THEN** la card muestra un distintivo de estado "Rechazado" en lugar del distintivo de novedad

#### Scenario: La card rechazada destaca el motivo

- **GIVEN** un pedido `rechazado` con un motivo de rechazo registrado
- **WHEN** se renderiza su card
- **THEN** el motivo se muestra destacado (citado), diferenciado visualmente del detalle de los demás estados
