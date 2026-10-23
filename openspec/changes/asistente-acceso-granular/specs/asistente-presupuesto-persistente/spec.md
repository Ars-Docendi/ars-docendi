## ADDED Requirements

### Requirement: Restablecer el cupo propio de un usuario al del rol

Un administrador con `asistente.administrar` SHALL poder restablecer el override de cupo diario de un usuario. Restablecer MUST cerrar la vigencia del override sin borrar su historia, y el usuario SHALL volver a heredar el cupo de sus roles. La acción SHALL auditarse.

#### Scenario: El usuario vuelve al cupo del rol

- **GIVEN** un usuario con un override vigente de 60 turnos/día y un rol con default de 20
- **WHEN** un administrador restablece su cupo
- **THEN** el cupo efectivo del usuario vuelve a ser 20 con origen «rol», y la fila del override queda con `vigente_hasta` cerrado
