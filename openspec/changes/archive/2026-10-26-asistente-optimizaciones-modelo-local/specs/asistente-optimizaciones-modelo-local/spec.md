## ADDED Requirements

### Requirement: Las optimizaciones no cambian el comportamiento por defecto

Cada optimización de este cambio SHALL activarse por una opción de configuración que por defecto está apagada. Con todas apagadas, el prompt enviado al proveedor MUST NOT cambiar.

#### Scenario: Con los defaults el prefijo es el de siempre

- **GIVEN** las opciones en sus valores por defecto
- **WHEN** se arma el prefijo de la generación
- **THEN** es idéntico al que producía el sistema antes de este cambio

### Requirement: Esquema compacto sin pérdida de significado

Con `EsquemaCompacto`, el prefijo SHALL conservar cada tabla, columna, comentario, descripción y relación del esquema, en una forma más corta.

#### Scenario: Una clave foránea viaja en la línea de su columna

- **GIVEN** `EsquemaCompacto` prendido
- **WHEN** se renderiza una columna que referencia a otra tabla
- **THEN** la línea de la columna nombra la tabla y columna referidas, y no existe una sección de relaciones aparte

### Requirement: Reparación de una consulta rechazada por el motor

Con `RepararConsultaFallida`, si PostgreSQL rechaza la consulta generada por un motivo distinto de privilegio o de timeout, el sistema SHALL pedir una sola corrección al modelo con el error. El error MUST NOT incluir ningún literal que no aparezca en la consulta generada.

#### Scenario: Un literal ajeno a la consulta no viaja

- **GIVEN** un error del motor que cita un valor que no está en la consulta
- **WHEN** se arma el pedido de corrección
- **THEN** ese valor se reemplaza por un marcador

### Requirement: Respuesta sin modelo para resultados triviales

Con `RedaccionConPlantillas`, un resultado de una columna con entre una y cinco filas, visto por un actor de alcance completo, sin recorte ni cobertura autodeclarada, SHALL redactarse sin llamar al modelo.

#### Scenario: Un valor único

- **GIVEN** un resultado de una fila y una columna con valor 4
- **WHEN** se redacta
- **THEN** la respuesta es «El resultado es 4.» y el modelo no se llama

### Requirement: Caché de consultas generadas

Con `VigenciaDeCacheDeConsultasMinutos` mayor que cero, una pregunta sin contexto repetida con la misma variante de rol y la misma fecha de referencia SHALL reutilizar la consulta generada, que MUST ejecutarse bajo el alcance del actor que pregunta. El sistema MUST NOT reutilizar filas.

#### Scenario: La segunda vez no llama al modelo para generar

- **GIVEN** una pregunta que ya se respondió con filas
- **WHEN** otro actor con el mismo rol de lectura la hace el mismo día
- **THEN** la consulta se ejecuta sin una llamada de generación

### Requirement: Telemetría del servidor local

El sistema SHALL exponer a quien tiene `asistente.administrar` el estado del servidor local y de la compuerta. Una métrica que el servidor no publica MUST informarse como ausente, nunca como cero.

#### Scenario: Proveedor no local

- **GIVEN** un proveedor que no es `local`
- **WHEN** se pide la telemetría
- **THEN** responde `configurado: false`

### Requirement: Redacción en flujo

Con `StreamingDeRedaccion`, el sistema SHALL ofrecer un endpoint que emite la redacción por fragmentos y termina siempre con el resultado completo del turno.

#### Scenario: El último evento es el resultado

- **GIVEN** un turno que se responde con filas
- **WHEN** se lo pide por el endpoint de flujo
- **THEN** el último evento es `resultado` con la misma forma que la respuesta del endpoint de siempre
