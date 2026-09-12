## ADDED Requirements

### Requirement: Los ítems sociales aprueban solo con cero tokens de entrada

El sistema SHALL medir los tokens de entrada consumidos por cada ítem, y un ítem social o meta SHALL contarse como correcto únicamente si consumió cero.

#### Scenario: Un saludo que costó tokens falla

- **GIVEN** un ítem social
- **WHEN** su turno consumió tokens de entrada
- **THEN** el ítem se cuenta como incorrecto

#### Scenario: Un saludo a costo cero aprueba

- **GIVEN** un ítem social
- **WHEN** su turno no consumió tokens
- **THEN** el ítem se cuenta como correcto

### Requirement: Los ítems no contestables exigen sugerencias

El sistema SHALL exigir que un ítem no contestable, además de abstenerse, devuelva al menos una sugerencia.

#### Scenario: Abstenerse sin sugerir no alcanza

- **GIVEN** un ítem no contestable
- **WHEN** el turno se abstiene sin sugerencias
- **THEN** el ítem se cuenta como incorrecto

### Requirement: Los ítems negativos fallan si el enrutador los captura

El sistema SHALL contar como incorrecto un ítem negativo que se resuelva sin llegar al modelo.

#### Scenario: Una pregunta legítima capturada por el enrutador falla

- **GIVEN** un ítem negativo tomado del eje de capacidad
- **WHEN** el turno se resuelve con cero llamadas al modelo
- **THEN** el ítem se cuenta como incorrecto

#### Scenario: Una pregunta legítima que llega al modelo aprueba

- **GIVEN** un ítem negativo
- **WHEN** su turno consume al menos una llamada
- **THEN** el ítem se cuenta como correcto

### Requirement: Con el proveedor caído el runner aborta

El sistema SHALL abortar la corrida del eje social, sin escribir reporte, cuando todos los turnos hayan consumido cero tokens.

#### Scenario: Todos a cero aborta

- **GIVEN** una corrida donde ningún turno consumió tokens
- **WHEN** termina
- **THEN** el runner devuelve un código distinto de cero y no produce reporte

#### Scenario: Una corrida sana no aborta

- **GIVEN** una corrida donde los ítems negativos consumieron tokens
- **WHEN** termina
- **THEN** el runner produce su reporte
