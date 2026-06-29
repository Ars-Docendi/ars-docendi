## ADDED Requirements

### Requirement: Activación de docente con confirmación

El sistema SHALL requerir confirmación explícita antes de reactivar un docente. El botón "Activar" MUST aparecer solo en filas de docentes inactivos. Al confirmar, el campo `is_active` SHALL pasar a `true`.

#### Scenario: Botón visible solo para inactivos

- **WHEN** un docente tiene `is_active = false`
- **THEN** la columna Acciones muestra el botón ghost "Activar" en lugar de "Desactivar"

#### Scenario: Apertura del modal de confirmación

- **WHEN** el usuario hace clic en "Activar" de un docente inactivo
- **THEN** se abre el modal de confirmación mostrando el nombre completo del docente

#### Scenario: Confirmación de activación

- **WHEN** el usuario hace clic en "Activar" dentro del modal de confirmación
- **THEN** el docente pasa a `is_active = true`, la badge cambia a "Activo" y el botón de la fila cambia a "Desactivar"

#### Scenario: Cancelar activación

- **WHEN** el usuario hace clic en "Cancelar" en el modal de confirmación
- **THEN** el modal se cierra sin cambiar el estado del docente
