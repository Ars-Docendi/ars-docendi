## ADDED Requirements

### Requirement: Un seguimiento cuya referencia no se resolvió lo dice

Cuando un turno de seguimiento termina en rechazo y su pregunta interpretada todavía apunta a algo que no nombra, el sistema SHALL responder que no pudo entender a qué se refería el usuario, y pedirle que lo nombre.

El sistema MUST NOT usar en ese caso el texto genérico de pregunta fuera de alcance: ése afirma algo sobre los datos, y quien lo lee concluye que el dato no existe.

El sistema SHALL exigir las tres condiciones —hubo historial, la pregunta interpretada conserva un demostrativo sin resolver, y el turno terminó en rechazo—. Con cualquiera ausente SHALL conservar el texto que corresponda.

#### Scenario: Un seguimiento sin resolver explica el problema

- **GIVEN** un turno anterior respondido
- **WHEN** el usuario pregunta por «esa materia» y la generación no es contestable
- **THEN** la respuesta dice que no se entendió a qué se refería y pide nombrarlo

#### Scenario: Un rechazo sin demostrativo conserva el texto genérico

- **GIVEN** un turno anterior respondido
- **WHEN** el usuario pregunta algo fuera del esquema, sin demostrativos, y se rechaza
- **THEN** la respuesta es la de pregunta fuera de alcance

#### Scenario: Un primer turno con demostrativo conserva el texto genérico

- **GIVEN** un hilo sin turnos previos
- **WHEN** el usuario usa un demostrativo y el turno se rechaza
- **THEN** la respuesta es la de pregunta fuera de alcance, porque no hubo ningún seguimiento que resolver

#### Scenario: Un seguimiento que se resuelve no cambia de texto

- **GIVEN** un turno anterior respondido
- **WHEN** el usuario pregunta por «esa materia» y la generación sí produce una consulta que devuelve filas
- **THEN** la respuesta es la redactada sobre esas filas
