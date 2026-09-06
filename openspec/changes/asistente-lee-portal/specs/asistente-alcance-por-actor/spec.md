## MODIFIED Requirements

### Requirement: Un resultado vacío solo se presenta como ausencia cuando el actor alcanza todo lo consultado

El sistema SHALL determinar si «cero filas significa que no hay filas» conjugando el ámbito del actor **y** su permiso de dominio, que son ejes independientes.

El sistema MUST NOT usar el ámbito global por sí solo como equivalente de «ve todo».

Cuando el actor no alcanza todo lo consultado, el texto del resultado vacío SHALL reconocer el límite de alcance en lugar de afirmar que el dato no existe.

#### Scenario: Un actor global sin el permiso de dominio no alcanza todo

- **GIVEN** un actor de ámbito global sin `designaciones.ver`
- **WHEN** se resuelve su perfil
- **THEN** su ámbito sigue siendo global y no alcanza todo lo consultable

#### Scenario: El texto no afirma ausencia cuando el vacío puede venir del alcance

- **GIVEN** un actor que no alcanza todo lo consultado y una consulta con cero filas
- **WHEN** se arma el texto del resultado vacío
- **THEN** el texto reconoce el límite de alcance y no afirma que no hay registros

#### Scenario: No se gasta el reintento donde no puede ayudar

- **GIVEN** un resultado vacío de un actor que no alcanza todo lo consultado
- **WHEN** se decide si reintentar la generación
- **THEN** no se reintenta
