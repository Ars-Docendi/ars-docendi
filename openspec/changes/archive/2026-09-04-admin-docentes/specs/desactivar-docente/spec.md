## ADDED Requirements

### Requirement: Desactivación de docente con confirmación

El sistema SHALL requerir confirmación explícita antes de desactivar un docente. El botón "Desactivar" MUST aparecer solo en filas de docentes activos. Al confirmar, el campo `is_active` SHALL pasar a `false`.

#### Scenario: Botón visible solo para activos

- **WHEN** un docente tiene `is_active = true`
- **THEN** la columna Acciones muestra el botón ghost "Desactivar"

#### Scenario: Apertura del modal de confirmación

- **WHEN** el usuario hace clic en "Desactivar" de un docente activo
- **THEN** se abre el modal de confirmación mostrando el nombre completo del docente

#### Scenario: Confirmación de desactivación

- **WHEN** el usuario hace clic en "Desactivar" dentro del modal de confirmación
- **THEN** el docente pasa a `is_active = false`, la badge cambia a "Inactivo" y el botón de la fila cambia a "Activar"

#### Scenario: Cancelar desactivación

- **WHEN** el usuario hace clic en "Cancelar" en el modal de confirmación
- **THEN** el modal se cierra sin cambiar el estado del docente
