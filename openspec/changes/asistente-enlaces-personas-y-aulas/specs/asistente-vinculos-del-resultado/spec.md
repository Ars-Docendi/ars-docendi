## ADDED Requirements

### Requirement: Un docente del resultado enlaza a su perfil sólo si el actor puede abrirlo

Cuando una fila del resultado cita el identificador de una persona, el sistema SHALL ofrecer en el rótulo de esa fila un vínculo de tipo `persona`. El sistema SHALL ofrecerlo únicamente si el actor tiene `personas.ver`, o tiene `personas.ver_ambito` y la persona está designada en una materia de su jefatura, el mismo criterio que el endpoint de detalle de personas. En cualquier otro caso MUST NOT ofrecer vínculo y la fila se muestra igual.

#### Scenario: Un jefe de cátedra ve el vínculo de un docente de su materia

- **GIVEN** un actor con `personas.ver_ambito` y una persona designada en su materia
- **WHEN** el resultado cita el identificador de esa persona
- **THEN** el nombre de la fila es un vínculo de tipo `persona`

#### Scenario: Una persona fuera del ámbito no ofrece vínculo

- **GIVEN** un actor con `personas.ver_ambito` y una persona designada sólo en otras materias
- **WHEN** el resultado cita su identificador
- **THEN** la respuesta no trae vínculo para esa fila

#### Scenario: Sin permiso de personas no hay vínculo

- **GIVEN** un actor sin `personas.ver` ni `personas.ver_ambito`
- **WHEN** el resultado cita identificadores de personas
- **THEN** no se consulta a Personas y la respuesta no trae vínculos

### Requirement: Una solicitud de reserva enlaza a su detalle sólo si el actor puede abrirla

Cuando una fila cita el identificador de una solicitud de reserva de aula, el sistema SHALL ofrecer un vínculo de tipo `solicitud-reserva-aula` únicamente si el actor tiene `aulas.aprobar`, o la solicitud es suya y tiene `aulas.solicitar`. El criterio lo aplica el módulo de Aulas, el mismo del detalle.

#### Scenario: Quien resuelve solicitudes ve todos los vínculos

- **GIVEN** un actor con `aulas.aprobar`
- **WHEN** el resultado cita dos solicitudes
- **THEN** ambas filas traen vínculo

#### Scenario: Un docente sólo ve el vínculo de su propia solicitud

- **GIVEN** un actor con `aulas.solicitar` y sin `aulas.aprobar`
- **WHEN** el resultado cita una solicitud propia y una ajena
- **THEN** sólo la propia trae vínculo

### Requirement: Una columna de clave interna no se muestra

Cuando un destino se declara de clave interna, el sistema SHALL mover el vínculo a la primera celda de texto de la fila que no sea clave y SHALL quitar la columna de clave del resultado. Si una fila no tiene tal celda, la columna MUST permanecer. Las posiciones en el texto se calculan antes del movimiento.

#### Scenario: El identificador no llega a la tabla

- **GIVEN** un resultado con columnas `persona_id`, `apellido`, `nombre` cuyos identificadores resuelven
- **WHEN** se arma la respuesta
- **THEN** la tabla no tiene la columna `persona_id` y `apellido` es el vínculo de cada fila

### Requirement: Una respuesta sobre un único docente lo presenta en una tarjeta

Cuando todos los vínculos del resultado apuntan a la misma persona, el sistema SHALL designarla elemento principal y pedir su ficha al módulo dueño con el mismo criterio de ámbito. La ficha lleva apellido y nombre, las designaciones vigentes dentro del ámbito del actor y el correo institucional (el UPN de la cuenta) cuando la persona lo tiene; MUST NOT llevar documento, teléfono ni correo personal, y los datos de la ficha MUST NOT pasar por el modelo. Un fallo MUST NOT impedir el turno.

#### Scenario: Un docente con dos designaciones trae su tarjeta

- **GIVEN** un resultado de dos filas del mismo docente
- **WHEN** el actor puede abrir su perfil
- **THEN** la respuesta trae como principal esa persona con sus dos designaciones

#### Scenario: Varios docentes no tienen principal

- **GIVEN** un resultado con dos personas distintas
- **WHEN** se arma la respuesta
- **THEN** no hay elemento principal
