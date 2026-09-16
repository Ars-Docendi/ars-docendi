## MODIFIED Requirements

### Requirement: Persona como entidad canónica, independiente de la cuenta

El sistema SHALL persistir a las personas en `identity.personas` como entidad canónica, separada de la cuenta de autenticación `identity.users`. Una persona MUST poder existir sin cuenta de Azure AD asociada. `identity.users` SHALL llevar `persona_id` nullable, y `identity.personas.legajo` SHALL ser nullable para admitir a una persona que todavía no lo tiene asignado. `documento` SHALL ser único entre las personas. El registro de una persona provocado por un Alta MUST no crear automáticamente una cuenta de autenticación ni exigir UPN.

#### Scenario: Alta de una persona sin cuenta ni legajo

- **GIVEN** un pedido de designación de novedad "Alta" sobre una persona cuyo documento no existe
- **WHEN** el sistema registra los datos del Alta
- **THEN** persiste una fila en `identity.personas` con el documento, nombre y apellido recibidos, `legajo` en NULL y sin ninguna fila asociada en `identity.users`

#### Scenario: El Alta no provisiona una cuenta Azure AD

- **GIVEN** una persona registrada por un Alta válida
- **WHEN** finaliza el guardado o envío del pedido
- **THEN** el sistema MUST no crear usuario, UPN, contraseña ni invitación automática de Azure AD

#### Scenario: El primer login vincula la cuenta a la persona

- **GIVEN** una persona ya registrada en `identity.personas`
- **WHEN** esa persona se autentica por primera vez vía Azure AD
- **THEN** el sistema MUST crear o actualizar su fila en `identity.users` fijando `persona_id` hacia la persona existente, sin duplicar la persona

#### Scenario: Documento duplicado es rechazado

- **GIVEN** una persona ya registrada con un documento dado
- **WHEN** se intenta registrar otra persona con el mismo documento
- **THEN** la base de datos MUST rechazar la operación por violación de unicidad y el pedido MUST no quedar persistido de forma parcial

#### Scenario: La PII de la persona queda auditada

- **WHEN** se crea, modifica o elimina una fila de `identity.personas`
- **THEN** `audit.change_log` MUST registrar el evento con su `changed_by`, `changed_at` y `changed_columns`
