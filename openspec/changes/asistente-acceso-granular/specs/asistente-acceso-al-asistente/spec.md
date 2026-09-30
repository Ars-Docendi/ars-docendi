## ADDED Requirements

### Requirement: El acceso se hereda del rol y se puede quitar por usuario

El sistema SHALL resolver el acceso operativo de un actor al asistente a partir del acceso de sus roles de sistema y de una revocación por usuario. Un administrador con `asistente.administrar` MAY apagar el acceso de un rol y MAY revocar el acceso de un usuario puntual; el sistema MUST NOT permitir concederle acceso a un usuario cuyo rol no lo tiene. Este acceso SHALL sumarse al permiso `asistente.consultar`, nunca reemplazarlo.

#### Scenario: Un usuario revocado no puede consultar

- **GIVEN** un usuario cuyo rol tiene el acceso habilitado
- **AND** un administrador le revocó el acceso
- **WHEN** el usuario envía un turno
- **THEN** el turno no llama al modelo y se informa el motivo `sin_acceso`

#### Scenario: Restablecer devuelve el acceso heredado

- **GIVEN** un usuario con el acceso revocado cuyo rol tiene el acceso habilitado
- **WHEN** un administrador restablece su acceso
- **THEN** el usuario vuelve a poder consultar y el panel muestra «Con acceso · del rol»

#### Scenario: Un rol apagado bloquea a sus usuarios

- **GIVEN** un usuario cuyo único rol de sistema tiene el acceso apagado
- **WHEN** el usuario envía un turno
- **THEN** el turno no llama al modelo y se informa el motivo `sin_acceso`

#### Scenario: Otro rol con acceso lo mantiene

- **GIVEN** un usuario con dos roles de sistema, uno con acceso apagado y otro habilitado
- **WHEN** el usuario envía un turno
- **THEN** el acceso no lo bloquea

#### Scenario: Cada cambio de acceso se audita

- **WHEN** un administrador cambia el acceso de un rol o de un usuario
- **THEN** se escribe una fila en `asistente.auditoria_administracion` con el antes y el después
