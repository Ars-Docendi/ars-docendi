## Purpose

Cambio de estado de usuarios (desactivar/activar) desde la tabla de gestión de usuarios, siempre mediado por un modal de confirmación. Un usuario inactivo (`is_active = false`) pierde acceso al sistema; reactivarlo lo restaura.

## Requirements

### Requirement: Acción de desactivar usuario con confirmación

Cada fila de usuario activo SHALL tener una acción "Desactivar" (`variant="ghost"`) que abre un modal de confirmación antes de ejecutar el cambio. No SHALL existir desactivación directa sin confirmación.

#### Scenario: Operador inicia desactivación

- **WHEN** el operador hace clic en "Desactivar" (botón rojo) en la fila de un usuario activo
- **THEN** se abre un modal pidiendo confirmación, mostrando el nombre del usuario afectado

#### Scenario: Confirmar desactivación

- **WHEN** el operador confirma en el modal (botón "Desactivar" rojo)
- **THEN** el usuario queda con `is_active = false`, su fila refleja el `StatusBadge` rojo "Inactivo" y el modal se cierra

#### Scenario: Cancelar desactivación

- **WHEN** el operador cancela en el modal (botón "Cancelar" secondary o Escape)
- **THEN** el modal se cierra sin cambiar el estado del usuario

### Requirement: Usuarios inactivos muestran "Activar" en vez de "Desactivar"

La acción "Desactivar" SHALL estar oculta para usuarios que ya tienen `is_active = false`. En su lugar SHALL mostrarse "Activar".

#### Scenario: Fila de usuario inactivo

- **WHEN** el usuario ya está inactivo
- **THEN** la acción "Desactivar" no aparece; en cambio aparece "Activar" (`variant="ghost"`)

### Requirement: Acción de activar usuario con confirmación

La acción "Activar" SHALL abrir un modal de confirmación antes de poner `is_active = true`. El modal muestra el nombre del usuario y explica que recuperará acceso al sistema.

#### Scenario: Operador inicia activación

- **WHEN** el operador hace clic en "Activar" en la fila de un usuario inactivo
- **THEN** se abre un modal pidiendo confirmación, mostrando el nombre del usuario afectado

#### Scenario: Confirmar activación

- **WHEN** el operador confirma en el modal (botón "Activar" primary)
- **THEN** el usuario queda con `is_active = true`, su fila refleja el `StatusBadge` verde "Activo" y el botón vuelve a ser "Desactivar"

#### Scenario: Cancelar activación

- **WHEN** el operador cancela en el modal (botón "Cancelar" secondary o Escape)
- **THEN** el modal se cierra sin cambiar el estado del usuario

### Requirement: Botones del modal de confirmación

El modal SHALL tener botón "Cancelar" con variante secondary (neutral) a la izquierda y botón "Desactivar" con fondo rojo a la derecha, separados por `justify-content: space-between`.

#### Scenario: Layout de botones

- **WHEN** el modal de confirmación está abierto
- **THEN** "Cancelar" (gris neutro, izquierda) y "Desactivar" (rojo, derecha) están en extremos opuestos del footer
