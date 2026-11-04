## ADDED Requirements

### Requirement: El breaker abre ante fallos repetidos del proveedor

El sistema SHALL dejar de llamar al proveedor después de una cantidad configurada de fallos consecutivos de transporte o timeout.

#### Scenario: Con fallos consecutivos el breaker abre

- **GIVEN** un proveedor que falla en el transporte
- **WHEN** acumula los fallos consecutivos configurados
- **THEN** el breaker queda abierto

#### Scenario: Con el breaker abierto no se emite ninguna llamada

- **GIVEN** un breaker abierto
- **WHEN** un turno necesita el modelo
- **THEN** el proveedor no recibe ninguna llamada

#### Scenario: Un rechazo semántico no abre el breaker

- **GIVEN** un proveedor que responde correctamente con una respuesta que el pipeline descarta
- **WHEN** eso ocurre repetidas veces
- **THEN** el breaker sigue cerrado

### Requirement: El breaker se recupera solo

El sistema SHALL volver a probar el proveedor después de un período de espera, con una sola llamada, y SHALL cerrar el breaker si esa llamada funciona.

#### Scenario: Tras la espera se prueba de a una

- **GIVEN** un breaker abierto y transcurrido el período de espera
- **WHEN** llegan varios turnos que necesitan el modelo
- **THEN** el proveedor recibe una sola llamada de prueba

#### Scenario: Una prueba exitosa cierra el breaker

- **GIVEN** un breaker en período de prueba
- **WHEN** la llamada de prueba funciona
- **THEN** el breaker se cierra y los turnos siguientes usan el proveedor

#### Scenario: Una prueba fallida lo vuelve a abrir

- **GIVEN** un breaker en período de prueba
- **WHEN** la llamada de prueba falla
- **THEN** el breaker vuelve a abrirse y reinicia la espera

### Requirement: Sin modelo, los carriles deterministas siguen resolviendo

El sistema SHALL resolver por los pasos que no necesitan proveedor aunque el modelo no esté disponible, y MUST NOT abortar el turno completo.

#### Scenario: Un saludo sigue costando cero

- **GIVEN** un breaker abierto
- **WHEN** el usuario saluda
- **THEN** recibe la respuesta social sin que se emita ninguna llamada al proveedor

#### Scenario: Una pregunta ambigua sigue devolviendo su menú

- **GIVEN** un breaker abierto
- **WHEN** el usuario nombra una entidad que colisiona
- **THEN** recibe el menú de aclaración con sus opciones

#### Scenario: La respuesta a una aclaración se sigue reconociendo

- **GIVEN** un breaker abierto y un menú de aclaración pendiente
- **WHEN** el usuario elige una opción
- **THEN** el sistema la reconoce y cierra la aclaración

#### Scenario: Solo el paso que necesitaba el modelo se abstiene

- **GIVEN** un breaker abierto
- **WHEN** el usuario hace una pregunta de datos que exige generar una consulta
- **THEN** el turno resuelve como servicio degradado

### Requirement: La cuota agotada resuelve por el mismo camino que el breaker

El sistema SHALL tratar la falta de cupo del actor como falta de modelo, con la misma resolución degradada.

#### Scenario: Sin cupo, un saludo sigue resolviendo

- **GIVEN** un actor sin cupo
- **WHEN** saluda
- **THEN** recibe la respuesta social sin que se emita ninguna llamada al proveedor
