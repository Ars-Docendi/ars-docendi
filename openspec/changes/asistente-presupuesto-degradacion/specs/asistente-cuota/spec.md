## ADDED Requirements

### Requirement: La cuota se mide en llamadas al modelo

El sistema SHALL contar el consumo de un actor en llamadas al modelo, y MUST NOT contarlo en requests HTTP recibidos.

#### Scenario: Un turno con reescritor suma tres llamadas

- **GIVEN** un turno que usa reescritor, generación y redacción
- **WHEN** termina
- **THEN** el consumo registrado del actor aumentó en tres

#### Scenario: Un turno resuelto sin modelo no consume cupo

- **GIVEN** un actor con cupo disponible
- **WHEN** envía un saludo, que resuelve por el carril sin datos
- **THEN** su consumo registrado no cambia

#### Scenario: Los reintentos de transporte no suman al cupo

- **GIVEN** una llamada al modelo que falla dos veces en el transporte y funciona a la tercera
- **WHEN** el turno termina
- **THEN** el consumo del actor aumentó en una sola llamada

### Requirement: Superado el cupo no se emite ninguna llamada

El sistema SHALL verificar el cupo del actor antes de iniciar el pipeline del turno, y con el cupo agotado MUST NOT emitir ninguna llamada al proveedor.

#### Scenario: Con el cupo agotado el proveedor no recibe nada

- **GIVEN** un actor que agotó su cupo en la ventana vigente
- **WHEN** envía una pregunta que normalmente requiere modelo
- **THEN** el proveedor no recibe ninguna llamada

#### Scenario: El estado devuelto es explícito

- **GIVEN** un actor que agotó su cupo
- **WHEN** envía una pregunta
- **THEN** el turno resuelve como servicio degradado

#### Scenario: El mensaje es comprensible y no técnico

- **GIVEN** un actor que agotó su cupo
- **WHEN** lee la respuesta
- **THEN** el texto le dice que alcanzó su límite de consultas y cuándo vuelve a tener cupo, sin nombrar ninguna etiqueta interna

### Requirement: La cuota es por identidad autenticada

El sistema SHALL acotar la cuota por el actor autenticado, y MUST NOT acotarla por dirección de origen.

#### Scenario: Dos actores no comparten cupo

- **GIVEN** un actor que agotó su cupo
- **WHEN** otro actor consulta
- **THEN** su turno se resuelve normalmente

### Requirement: La ventana de la cuota es deslizante y se recupera sola

El sistema SHALL descartar del conteo las llamadas más viejas que la ventana configurada.

#### Scenario: Al pasar la ventana vuelve el cupo

- **GIVEN** un actor que agotó su cupo
- **WHEN** transcurre la ventana completa sin consultas
- **THEN** vuelve a tener cupo disponible

#### Scenario: Una ventana en cero desactiva la cuota

- **GIVEN** la cuota configurada en cero
- **WHEN** un actor consulta muchas veces
- **THEN** nunca se le niega el turno por cupo
