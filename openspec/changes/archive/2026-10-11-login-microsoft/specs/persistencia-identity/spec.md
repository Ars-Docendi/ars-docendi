## MODIFIED Requirements

### Requirement: Persona como entidad canónica, independiente de la cuenta

El sistema SHALL persistir a las personas en `identity.personas` como entidad canónica, separada de la cuenta de autenticación `identity.users`. Una persona MUST poder existir sin cuenta de Azure AD asociada. `identity.users` SHALL llevar `persona_id` nullable, y `identity.personas.legajo` SHALL ser nullable para admitir a una persona que todavía no lo tiene asignado. `documento` SHALL ser único entre las personas. El registro de una persona provocado por un Alta MUST no crear automáticamente una cuenta de autenticación ni exigir UPN. `identity.users.azure_oid` y `identity.users.azure_tid` SHALL permanecer vacíos hasta el primer ingreso con Microsoft: dar de alta un usuario MUST NOT generar un identificador de cuenta Microsoft, y el ingreso MUST NOT crear usuarios ni personas.

#### Scenario: Alta de una persona sin cuenta ni legajo

- **GIVEN** un pedido de designación de novedad "Alta" sobre una persona cuyo documento no existe
- **WHEN** el sistema registra los datos del Alta
- **THEN** persiste una fila en `identity.personas` con el documento, nombre y apellido recibidos, `legajo` en NULL y sin ninguna fila asociada en `identity.users`

#### Scenario: El Alta no provisiona una cuenta Azure AD

- **GIVEN** una persona registrada por un Alta válida
- **WHEN** finaliza el guardado o envío del pedido
- **THEN** el sistema MUST no crear usuario, UPN, contraseña ni invitación automática de Azure AD

#### Scenario: El alta administrativa no genera la cuenta Microsoft

- **WHEN** la administración da de alta un usuario
- **THEN** su fila en `identity.users` queda con `azure_oid` y `azure_tid` vacíos hasta su primer ingreso

#### Scenario: El primer login vincula la cuenta a la persona

- **GIVEN** una persona con un usuario dado de alta por la administración y sin cuenta Microsoft vinculada
- **WHEN** esa persona ingresa por primera vez con una cuenta Microsoft cuyo mail verificado coincide con su UPN
- **THEN** el sistema MUST registrar `azure_oid` y `azure_tid` en esa fila existente de `identity.users`, sin crear usuarios, sin duplicar la persona y sin modificar `persona_id`

#### Scenario: Ingreso de una persona sin usuario

- **GIVEN** una persona registrada en `identity.personas` sin fila asociada en `identity.users`
- **WHEN** ingresa con una cuenta Microsoft cuyo mail verificado no corresponde a ningún usuario
- **THEN** el sistema MUST denegar el acceso sin crear la cuenta de autenticación

#### Scenario: Documento duplicado es rechazado

- **GIVEN** una persona ya registrada con un documento dado
- **WHEN** se intenta registrar otra persona con el mismo documento
- **THEN** la base de datos MUST rechazar la operación por violación de unicidad y el pedido MUST no quedar persistido de forma parcial

#### Scenario: La PII de la persona queda auditada

- **WHEN** se crea, modifica o elimina una fila de `identity.personas`
- **THEN** `audit.change_log` MUST registrar el evento con su `changed_by`, `changed_at` y `changed_columns`
