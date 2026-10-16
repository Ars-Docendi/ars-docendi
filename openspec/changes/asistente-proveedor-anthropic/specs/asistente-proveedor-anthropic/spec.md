## ADDED Requirements

### Requirement: El adaptador se elige por ambiente y convive con el simulado

El sistema SHALL seleccionar el proveedor del modelo por configuración de ambiente, y el simulado SHALL seguir siendo el valor por omisión.

#### Scenario: Sin configurar nada se sigue usando el simulado

- **GIVEN** un ambiente que no declara proveedor
- **WHEN** el módulo resuelve el proveedor del modelo
- **THEN** obtiene el simulado
- **AND** sus respuestas se declaran simuladas

#### Scenario: Configurar el adaptador real lo selecciona

- **GIVEN** un ambiente que declara el proveedor de Anthropic y su clave
- **WHEN** el módulo resuelve el proveedor del modelo
- **THEN** obtiene el adaptador de Anthropic
- **AND** sus respuestas no se declaran simuladas

#### Scenario: Un proveedor desconocido falla al pedirlo y no al arrancar

- **GIVEN** un ambiente que declara un proveedor que no existe
- **WHEN** el Host arranca
- **THEN** arranca sin error
- **AND** el ping responde

### Requirement: La ausencia de clave falla con un mensaje que la nombra

El sistema SHALL rechazar la construcción del adaptador real cuando no haya clave configurada, y el error SHALL nombrar el valor de configuración faltante.

#### Scenario: Sin clave, el adaptador no se construye

- **GIVEN** un ambiente que declara el proveedor de Anthropic sin clave
- **WHEN** alguien pide el proveedor del modelo
- **THEN** falla con un mensaje que nombra la clave faltante

#### Scenario: Sin clave, el Host igual arranca

- **GIVEN** un ambiente que declara el proveedor de Anthropic sin clave
- **WHEN** el Host arranca
- **THEN** arranca sin error

### Requirement: El adaptador no reintenta por su cuenta

El adaptador MUST NOT reintentar una llamada fallida, porque el reintento de transporte del módulo es la única autoridad de reintento.

#### Scenario: Una falla de transporte produce un solo intento del adaptador

- **GIVEN** un transporte que falla siempre
- **WHEN** el adaptador pide una completación
- **THEN** el transporte recibe exactamente un intento

#### Scenario: El peor caso del turno respeta la cota documentada

- **GIVEN** los topes de llamadas por turno y de intentos de transporte
- **WHEN** se cuenta el peor caso de requests de un turno
- **THEN** es el producto de esos dos topes y ningún múltiplo mayor

### Requirement: El prefijo estable viaja como bloque cacheable y separado del mensaje

El adaptador SHALL enviar el prefijo estable como bloque de sistema marcado para cachear, y SHALL enviar el mensaje del turno aparte.

#### Scenario: El prefijo va marcado para cachear

- **WHEN** el adaptador pide una completación
- **THEN** el prefijo estable viaja como bloque de sistema
- **AND** ese bloque lleva la marca de caché

#### Scenario: La pregunta del turno no contamina el prefijo

- **GIVEN** dos turnos con preguntas distintas y el mismo prefijo
- **WHEN** el adaptador pide las dos completaciones
- **THEN** el bloque de sistema es idéntico en las dos
- **AND** solo difiere el mensaje

### Requirement: No se envía ningún parámetro que el modelo rechace

El adaptador MUST NOT enviar la temperatura a un modelo que no la acepta.

#### Scenario: La temperatura del puerto no llega al request

- **GIVEN** una solicitud con temperatura declarada
- **WHEN** el adaptador arma el request
- **THEN** el request no lleva temperatura

#### Scenario: El puerto conserva la temperatura para otros adaptadores

- **WHEN** se inspecciona el contrato del puerto
- **THEN** la temperatura sigue siendo parte de la solicitud

### Requirement: Toda falla del proveedor llega al pipeline como falla del módulo

El adaptador SHALL traducir cualquier falla del proveedor al vocabulario de fallas del módulo, y MUST NOT dejar escapar una excepción propia del proveedor.

#### Scenario: Un error de servidor del proveedor se cuenta como fallo

- **GIVEN** un proveedor que responde con error de servidor
- **WHEN** el turno pide una completación
- **THEN** la falla llega como falla de transporte del módulo
- **AND** el corte al proveedor la cuenta

#### Scenario: Un límite de tasa del proveedor se cuenta como fallo

- **GIVEN** un proveedor que responde con límite de tasa excedido
- **WHEN** el turno pide una completación
- **THEN** la falla llega como falla de transporte del módulo

#### Scenario: Fallas repetidas del proveedor abren el corte

- **GIVEN** un proveedor que falla siempre
- **WHEN** se supera la cantidad de fallos que abre el corte
- **THEN** la llamada siguiente no llega al proveedor
- **AND** el turno responde con servicio degradado

#### Scenario: El timeout de la llamada lo sigue resolviendo el decorador

- **GIVEN** un proveedor que no responde dentro del tiempo de la llamada
- **WHEN** el turno pide una completación
- **THEN** la falla llega como timeout del proveedor
- **AND** el corte la cuenta

### Requirement: Una credencial rechazada degrada el turno y se registra como error

El sistema SHALL responder con servicio degradado cuando el proveedor rechace la credencial, y SHALL registrar el hecho con severidad de error.

#### Scenario: Con la clave rechazada el usuario recibe degradación y no un fallo

- **GIVEN** un proveedor que rechaza la credencial
- **WHEN** un actor consulta
- **THEN** el turno responde con servicio degradado
- **AND** la respuesta no contiene ningún detalle del proveedor

#### Scenario: La credencial rechazada queda registrada como error

- **GIVEN** un proveedor que rechaza la credencial
- **WHEN** el adaptador pide una completación
- **THEN** queda un registro de severidad error que nombra la causa

### Requirement: Una respuesta sin texto no se presenta como caída del proveedor

El adaptador SHALL devolver texto vacío cuando la respuesta no traiga ningún bloque de texto, y MUST NOT tratarlo como falla de transporte.

#### Scenario: Una respuesta sin texto no abre el corte

- **GIVEN** un proveedor que responde sin ningún bloque de texto
- **WHEN** el adaptador pide una completación
- **THEN** devuelve una respuesta con texto vacío
- **AND** el corte al proveedor no cuenta ningún fallo

#### Scenario: Un rehúso del modelo termina en abstención

- **GIVEN** un proveedor que se rehúsa a responder
- **WHEN** el turno pide generar una consulta
- **THEN** el turno abstiene
- **AND** no responde con servicio degradado

### Requirement: El adaptador informa el consumo real de tokens

El adaptador SHALL tomar los conteos de tokens de la respuesta del proveedor, y MUST NOT estimarlos.

#### Scenario: Los conteos vienen de la respuesta

- **GIVEN** una respuesta del proveedor con sus conteos de tokens
- **WHEN** el adaptador la traduce
- **THEN** los conteos de la respuesta del módulo son los del proveedor

#### Scenario: El registro operativo recibe esos conteos

- **GIVEN** un turno resuelto con el adaptador real
- **WHEN** se lee su registro operativo
- **THEN** los tokens registrados son los que informó el proveedor

### Requirement: La credencial no se escribe ni se registra en ningún lado

El sistema MUST NOT persistir la clave del proveedor en el repositorio, y MUST NOT incluirla en ningún registro.

#### Scenario: Ningún archivo versionado contiene la clave

- **WHEN** se busca la clave en los archivos de configuración versionados
- **THEN** no aparece en ninguno

#### Scenario: El registro operativo guarda el nombre del proveedor y no su clave

- **GIVEN** un turno resuelto con el adaptador real
- **WHEN** se lee su registro operativo
- **THEN** figura el nombre del proveedor
- **AND** no figura ninguna credencial
