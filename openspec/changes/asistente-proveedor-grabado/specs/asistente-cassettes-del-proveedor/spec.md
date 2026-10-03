## ADDED Requirements

### Requirement: La grabación intercepta el cable y no al adaptador

El sistema SHALL grabar y reproducir las respuestas del proveedor del modelo desde el pipeline del cliente HTTP con nombre que el módulo ya arma, y MUST NOT exigir ningún cambio en el adaptador del proveedor para hacerlo.

El componente de grabación MUST NOT nombrar el SDK de ningún proveedor: lee y escribe cuerpos HTTP, no tipos del SDK.

#### Scenario: El adaptador no distingue una respuesta grabada de una servida por red

- **GIVEN** un cassette grabado para una solicitud
- **WHEN** el adaptador emite esa misma solicitud con la reproducción activa
- **THEN** recibe la misma respuesta traducida que habría recibido del proveedor
- **AND** el adaptador no recibe ninguna señal de que la respuesta vino de disco

#### Scenario: El SDK del proveedor se sigue nombrando en un solo archivo

- **GIVEN** el código del módulo del asistente con la grabación incorporada
- **WHEN** se busca el namespace del SDK del proveedor en todos sus archivos
- **THEN** el único archivo que lo nombra sigue siendo el del adaptador

#### Scenario: La grabación convive con el reintento de transporte

- **GIVEN** el cliente HTTP con nombre del proveedor
- **WHEN** se resuelve su pipeline con la grabación activa
- **THEN** el reintento de transporte sigue presente y conserva su comportamiento

### Requirement: Se graba el cuerpo crudo de la respuesta del proveedor

El sistema SHALL guardar en el cassette los bytes del cuerpo de la respuesta tal como los devolvió el proveedor, sin reinterpretarlos ni reserializarlos, y MUST NOT guardar en su lugar la respuesta ya traducida al contrato del puerto.

#### Scenario: El cuerpo guardado es idéntico al recibido

- **GIVEN** una respuesta del proveedor con un cuerpo determinado
- **WHEN** se la graba
- **THEN** el cuerpo almacenado en el cassette es byte por byte el recibido

#### Scenario: El parseo del adaptador corre sobre el cuerpo grabado

- **GIVEN** un cassette cuyo cuerpo declara un motivo de corte por techo de tokens
- **WHEN** se reproduce
- **THEN** la respuesta traducida declara que se quedó sin tokens
- **AND** el conteo de tokens de entrada, de salida y de caché sale del cuerpo grabado

#### Scenario: Un cuerpo que el adaptador no puede traducir se reproduce igual

- **GIVEN** un cassette cuyo cuerpo no tiene ningún bloque de texto
- **WHEN** se reproduce
- **THEN** el adaptador devuelve texto vacío y no una falla de transporte

### Requirement: La clave del cassette es una huella determinista de la solicitud

El sistema SHALL derivar la clave de cada cassette de una huella criptográfica estable de cuatro campos de la solicitud —prefijo estable, mensaje, esfuerzo y modelo—, y MUST NOT usar ninguna función de hash cuyo resultado dependa del proceso.

#### Scenario: La misma solicitud produce la misma clave en otro proceso

- **GIVEN** una solicitud
- **WHEN** se calcula su clave en dos procesos distintos
- **THEN** las dos claves son iguales

#### Scenario: Cambiar cualquiera de los cuatro campos cambia la clave

- **GIVEN** una solicitud y su clave
- **WHEN** se cambia el prefijo, el mensaje, el esfuerzo o el modelo, de a uno por vez
- **THEN** cada variante produce una clave distinta de la original

#### Scenario: Cambiar algo fuera de los cuatro campos no cambia la clave

- **GIVEN** una solicitud y su clave
- **WHEN** se cambia únicamente el techo de tokens de la llamada
- **THEN** la clave es la misma

### Requirement: Cada cassette lleva su sello de identidad

El sistema SHALL sellar cada cassette con el modelo que lo produjo, la fecha de grabación, el hash del prefijo estable y el hash del fixture contra el que se grabó, y MUST NOT escribir un cassette al que le falte cualquiera de esos cuatro campos.

#### Scenario: El sello se escribe con el cassette

- **GIVEN** una grabación en curso
- **WHEN** se escribe el cassette
- **THEN** el archivo declara modelo, fecha, hash del prefijo y hash del fixture

#### Scenario: Un cassette sin sello completo no se escribe

- **GIVEN** una grabación sin hash de fixture disponible
- **WHEN** se intenta escribir el cassette
- **THEN** la escritura falla nombrando el campo faltante
- **AND** no queda ningún archivo a medio escribir en el directorio

#### Scenario: Un cassette sin sello completo no se sirve

- **GIVEN** un cassette al que le falta un campo del sello
- **WHEN** se lo intenta reproducir
- **THEN** la reproducción falla nombrando el archivo y el campo faltante

### Requirement: Un cassette cuyo sello no corresponde al prefijo vigente se rechaza

El sistema SHALL comparar el hash del prefijo del sello contra el hash del prefijo vigente antes de servir un cassette, y MUST rechazarlo cuando difieran en lugar de servirlo.

#### Scenario: El prefijo cambió y el cassette es viejo

- **GIVEN** un cassette sellado con el hash de un prefijo anterior
- **WHEN** se lo intenta reproducir contra el prefijo vigente
- **THEN** la reproducción falla diciendo que el prefijo del esquema cambió
- **AND** el cassette no se sirve

#### Scenario: El diagnóstico distingue «falta el cassette» de «los cassettes son de otro prefijo»

- **GIVEN** un directorio cuyos cassettes están todos sellados con otro prefijo
- **WHEN** se pide una clave que no existe
- **THEN** el error dice que los cassettes disponibles son de otro prefijo y hay que volver a grabarlos

### Requirement: La reproducción falla cerrado y nunca sale a la red

El sistema SHALL fallar con un error ruidoso cuando no exista el cassette de una solicitud y la variable de re-grabación no esté puesta, y MUST NOT emitir en ese caso ninguna llamada de red.

#### Scenario: Falta el cassette y no está la variable de re-grabación

- **GIVEN** la reproducción activa sin la variable de re-grabación
- **WHEN** llega una solicitud sin cassette
- **THEN** la llamada falla nombrando la clave faltante y el directorio donde se la buscó
- **AND** el transporte subyacente no recibe ninguna solicitud

#### Scenario: Con la variable de re-grabación puesta, la llamada sale y se graba

- **GIVEN** la re-grabación activa
- **WHEN** llega una solicitud sin cassette
- **THEN** la llamada llega al transporte
- **AND** su respuesta queda grabada bajo la clave de esa solicitud

#### Scenario: Con el cassette presente, la llamada no sale aunque la re-grabación esté puesta

- **GIVEN** la re-grabación activa y un cassette ya grabado para la solicitud
- **WHEN** llega esa solicitud
- **THEN** se sirve el cassette existente
- **AND** el transporte subyacente no recibe ninguna solicitud

### Requirement: Se graba la respuesta que el pipeline efectivamente usó

El sistema SHALL grabar una sola respuesta por llamada lógica al proveedor —la que el pipeline le devolvió al adaptador— y MUST NOT grabar los intentos intermedios que el reintento de transporte haya descartado.

#### Scenario: Un reintento no multiplica los cassettes

- **GIVEN** un transporte que devuelve un error reintentable y después una respuesta exitosa
- **WHEN** el adaptador emite una sola solicitud
- **THEN** queda grabado un único cassette
- **AND** su cuerpo es el de la respuesta exitosa

#### Scenario: La reproducción no vuelve a esperar el backoff

- **GIVEN** un cassette grabado a partir de una llamada que necesitó reintentos
- **WHEN** se lo reproduce
- **THEN** la respuesta se sirve sin ninguna espera de backoff

### Requirement: Ningún cassette contiene datos personales reales ni la credencial

El sistema SHALL grabar los cassettes contra el fixture sintético del evaluador y MUST NOT almacenar en un cassette las cabeceras de la solicitud ni ningún valor de la credencial del proveedor.

#### Scenario: El sello declara el fixture sintético

- **GIVEN** los cassettes versionados en el repositorio
- **WHEN** se leen sus sellos
- **THEN** todos declaran el hash del fixture sintético vigente

#### Scenario: El cassette no guarda cabeceras de la solicitud

- **GIVEN** una grabación de una solicitud con la credencial en sus cabeceras
- **WHEN** se lee el cassette escrito
- **THEN** no contiene ninguna cabecera de la solicitud
- **AND** no contiene ningún fragmento de la credencial

#### Scenario: Un guard barre el repositorio

- **GIVEN** los cassettes versionados
- **WHEN** un guard los recorre buscando la forma de la credencial del proveedor
- **THEN** no encuentra ninguna coincidencia

### Requirement: El mecanismo está apagado por default

El sistema SHALL dejar la grabación y la reproducción desactivadas mientras no se las configure explícitamente por ambiente, y MUST NOT alterar el pipeline del cliente HTTP del proveedor cuando estén desactivadas.

#### Scenario: Sin configuración, el pipeline es el de hoy

- **GIVEN** un ambiente sin el directorio de cassettes configurado
- **WHEN** se resuelve el cliente HTTP del proveedor
- **THEN** su pipeline es el mismo que antes de este cambio

#### Scenario: El ping responde igual con el mecanismo apagado

- **GIVEN** un ambiente sin el directorio de cassettes configurado
- **WHEN** se llama al endpoint de ping del módulo
- **THEN** responde correctamente

### Requirement: Los tests de parseo consumen todos los cassettes del directorio

El sistema SHALL ejercitar el parseo de la generación, de la redacción y de la reescritura sobre cada cassette presente en el directorio versionado, sin que agregar un cassette exija escribir un test nuevo.

#### Scenario: Un cassette nuevo se cubre solo

- **GIVEN** el directorio de cassettes versionados
- **WHEN** se agrega un cassette más
- **THEN** la suite lo ejercita sin modificar ningún archivo de test

#### Scenario: Un directorio vacío no pasa en verde

- **GIVEN** un directorio de cassettes sin ningún archivo
- **WHEN** corre la suite
- **THEN** falla diciendo que no hay cassettes que ejercitar
