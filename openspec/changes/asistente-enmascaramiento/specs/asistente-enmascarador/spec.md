## ADDED Requirements

### Requirement: Ninguna columna `sensible-valor` llega al modelo con su valor real

El sistema SHALL reemplazar por un marcador todo valor de una columna clasificada `sensible-valor` antes de armar el prompt de redacción.

El prompt de redacción MUST NOT contener ningún valor real de esas columnas.

#### Scenario: El documento no aparece en el prompt

- **GIVEN** un resultado con una columna `sensible-valor` cuyo valor real es conocido
- **WHEN** se arma el prompt de redacción
- **THEN** el prompt no contiene ese valor

#### Scenario: El alias tampoco lo deja pasar

- **GIVEN** un resultado donde la columna `sensible-valor` viene con un alias arbitrario
- **WHEN** se arma el prompt de redacción
- **THEN** el prompt no contiene el valor real

#### Scenario: Las columnas públicas viajan intactas

- **GIVEN** un resultado con columnas públicas
- **WHEN** se arma el prompt de redacción
- **THEN** sus valores aparecen tal cual

### Requirement: Ninguna columna `sensible-texto` llega al modelo

El sistema SHALL suprimir por completo toda columna clasificada `sensible-texto` antes de armar el prompt de redacción: ni su nombre ni sus valores.

#### Scenario: La columna desaparece del prompt

- **GIVEN** un resultado con una columna `sensible-texto`
- **WHEN** se arma el prompt de redacción
- **THEN** el prompt no contiene ni el nombre de esa columna ni ninguno de sus valores

#### Scenario: Un resultado íntegramente sensible deja un prompt sin filas

- **GIVEN** un resultado cuya única columna es `sensible-texto`
- **WHEN** se enmascara
- **THEN** el resultado enmascarado no tiene columnas y el turno no afirma contenido que el modelo no vio

### Requirement: Los marcadores son estables dentro de una respuesta

El sistema SHALL asignar el mismo marcador al mismo valor real dentro de una misma respuesta, y marcadores distintos a valores distintos.

El marcador MUST NOT derivarse del valor real.

El marcador SHALL nombrar la clase de dato que reemplaza.

#### Scenario: El mismo valor recibe el mismo marcador

- **GIVEN** un resultado donde un mismo valor sensible aparece en varias filas
- **WHEN** se enmascara
- **THEN** todas sus apariciones llevan el mismo marcador

#### Scenario: Valores distintos reciben marcadores distintos

- **GIVEN** un resultado con dos valores sensibles distintos
- **WHEN** se enmascara
- **THEN** llevan marcadores distintos

#### Scenario: El marcador no permite recuperar el valor

- **GIVEN** dos resultados con el mismo valor sensible en posiciones distintas
- **WHEN** se enmascaran por separado
- **THEN** el marcador depende del orden de aparición y no del valor

#### Scenario: El marcador dice de qué es

- **GIVEN** un resultado con una columna `sensible-valor`
- **WHEN** se enmascara
- **THEN** el marcador nombra la clase de dato que reemplaza

### Requirement: Los valores reales siguen viaje al llamador

El sistema SHALL devolver las filas reales, sin enmascarar, en el resultado del turno.

El enmascaramiento MUST afectar únicamente lo que se le manda al modelo.

#### Scenario: El resultado del turno trae los valores reales

- **GIVEN** un turno sobre un resultado con columnas sensibles
- **WHEN** termina el turno
- **THEN** sus filas contienen los valores reales

#### Scenario: El turno declara qué columnas son sensibles

- **GIVEN** un turno sobre un resultado con columnas sensibles
- **WHEN** termina el turno
- **THEN** expone la clasificación de cada columna

### Requirement: Las filas nunca se persisten

El sistema MUST NOT escribir las filas devueltas en ningún registro.

#### Scenario: El log de un turno no contiene valores de filas

- **GIVEN** un turno completo sobre un resultado con columnas sensibles
- **WHEN** se inspecciona lo que el turno registró
- **THEN** no aparece ningún valor de ninguna fila

### Requirement: El enmascaramiento es asimétrico y está declarado

El sistema SHALL documentar, en el código y en la documentación de arquitectura, que el enmascaramiento protege el camino de vuelta y no el de ida.

#### Scenario: La asimetría está escrita donde se la puede leer

- **WHEN** se inspecciona el enmascarador
- **THEN** declara que la pregunta cruda del usuario viaja al proveedor sin enmascarar
