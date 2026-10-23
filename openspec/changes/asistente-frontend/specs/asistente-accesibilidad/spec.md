## ADDED Requirements

### Requirement: La región viva contiene sólo los mensajes

El sistema SHALL acotar la región viva a la lista de mensajes, y MUST NOT incluir en ella la línea de métricas ni el resto de la vista.

#### Scenario: Las métricas quedan fuera de la región viva

- **GIVEN** la vista del asistente con una respuesta
- **WHEN** se inspecciona la región viva
- **THEN** la línea de métricas no está adentro

#### Scenario: La región de mensajes tiene rol de registro

- **GIVEN** la vista del asistente
- **WHEN** se inspecciona la lista de mensajes
- **THEN** tiene el rol de registro de conversación

### Requirement: El estado de proceso se anuncia con umbral

El sistema SHALL anunciar a lectores de pantalla que está procesando, y SHALL mostrar el indicador recién después de un umbral de tiempo.

#### Scenario: Una respuesta rápida no muestra indicador

- **GIVEN** un turno que resuelve antes del umbral
- **WHEN** el usuario espera
- **THEN** no aparece ningún indicador de proceso

#### Scenario: Una respuesta lenta sí lo muestra

- **GIVEN** un turno que tarda más que el umbral
- **WHEN** el usuario espera
- **THEN** aparece el indicador, anunciado como estado

#### Scenario: No hay etapas simuladas

- **GIVEN** un turno en curso
- **WHEN** se observa el indicador
- **THEN** muestra un solo estado honesto y no una secuencia de pasos inventada

### Requirement: El foco vuelve al campo de entrada

El sistema SHALL dejar el foco en el campo de entrada cuando llega la respuesta.

#### Scenario: Al responder, se puede seguir escribiendo

- **GIVEN** un turno enviado
- **WHEN** llega la respuesta
- **THEN** el foco está en el campo de entrada

#### Scenario: Elegir una opción también devuelve el foco

- **GIVEN** un turno que necesita aclaración
- **WHEN** el usuario elige una opción y llega la respuesta
- **THEN** el foco está en el campo de entrada
