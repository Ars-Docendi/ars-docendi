## Purpose

Permite traer desde SIU-Guaraní, a demanda y por período lectivo, las comisiones con su materia, carrera, aula, bandas horarias, docentes, modalidad y ubicación, y guardarlas como un snapshot local de solo lectura que reemplaza al anterior del mismo período.

## ADDED Requirements

### Requirement: Importación autorizada por permiso

Consultar los períodos lectivos de Guaraní e importar comisiones MUST requerir el permiso efectivo `comisiones.importar`, validado en backend. El frontend SHALL ocultar el botón "Actualizar desde Guaraní" a quien no tenga ese permiso.

#### Scenario: Actor con permiso de importación

- **GIVEN** un actor con `comisiones.importar`
- **WHEN** abre la pantalla de Comisiones
- **THEN** ve el botón "Actualizar desde Guaraní" y puede abrir el modal de importación

#### Scenario: Actor sin permiso de importación

- **GIVEN** un actor con `comisiones.ver` pero sin `comisiones.importar`
- **WHEN** abre la pantalla de Comisiones o invoca directamente el endpoint de importación
- **THEN** el botón no se muestra y la API responde 403 sin contactar a Guaraní

### Requirement: Selección del período lectivo a importar

El modal de importación SHALL pedir un año académico y un período lectivo. Los años y los períodos lectivos disponibles MUST obtenerse de Guaraní a través del backend; el año de cada período MUST tomarse del dato de año de Guaraní, aunque el nombre del período también lo incluya. El botón de confirmar MUST permanecer deshabilitado hasta que haya un período elegido.

#### Scenario: Períodos lectivos de un año

- **GIVEN** un actor con `comisiones.importar` y Guaraní disponible
- **WHEN** elige el año 2026 en el modal
- **THEN** el modal lista los períodos lectivos que Guaraní informa para 2026

#### Scenario: Sin períodos lectivos en Guaraní

- **GIVEN** que Guaraní no informa ningún período lectivo
- **WHEN** el actor abre el modal de importación
- **THEN** el modal informa que no hay períodos disponibles y no permite confirmar

### Requirement: Reemplazo atómico por período lectivo

Al confirmar, el backend SHALL obtener de Guaraní todas las comisiones del período lectivo elegido, validarlas y, en una única transacción, eliminar las comisiones locales de ese período e insertar las recibidas. MUST NOT modificar las comisiones de otros períodos lectivos. Si la obtención o la validación fallan, MUST conservar intactas las comisiones previas del período.

#### Scenario: Reimportación de un período

- **GIVEN** 120 comisiones importadas para "2026 – 2.º Cuatrimestre" y 110 para "2026 – 1.º Cuatrimestre"
- **WHEN** se reimporta "2026 – 2.º Cuatrimestre" y Guaraní devuelve 125 comisiones
- **THEN** el período importado queda con exactamente esas 125 comisiones
- **AND** "2026 – 1.º Cuatrimestre" conserva sus 110 comisiones

#### Scenario: Guaraní falla durante la importación

- **GIVEN** comisiones importadas previamente para un período
- **WHEN** Guaraní responde con error, excede el tiempo de espera o devuelve una respuesta que no se puede interpretar
- **THEN** la API responde un error identificable de servicio externo (502 o 504)
- **AND** las comisiones previas del período se conservan sin cambios

#### Scenario: Período sin comisiones en Guaraní

- **GIVEN** un período lectivo para el que Guaraní devuelve una lista vacía
- **WHEN** se confirma la importación
- **THEN** la API MUST rechazar el reemplazo con un error identificable y conservar las comisiones previas del período

### Requirement: Contenido de cada comisión importada

Cada comisión importada SHALL guardar, sin requerir que exista nada en los catálogos del sistema: identificador de la comisión en Guaraní, año académico, período lectivo, nombre de la comisión, nombre y código de la materia, carreras (nombre y código de cada una, ya que una comisión puede ofrecerse a más de una), bandas horarias (día, hora de inicio, hora de fin y aula de cada banda, ya que en Guaraní el aula se asigna por banda y no por comisión), docentes (nombre y responsabilidad, tal como se carga en Guaraní), modalidad (código y descripción) y ubicación. Los campos que Guaraní no informe MUST quedar vacíos sin rechazar la comisión. De los docentes MUST NOT guardarse documento, CUIL, correo ni otros datos personales.

#### Scenario: Comisión con varios docentes y bandas horarias

- **GIVEN** una comisión de Guaraní con dos docentes y tres bandas horarias
- **WHEN** se importa su período
- **THEN** la comisión local conserva los dos docentes con nombre y responsabilidad y las tres bandas con día y horario

#### Scenario: Comisión sin aula asignada

- **GIVEN** una comisión de Guaraní con una banda horaria sin aula asignada
- **WHEN** se importa su período
- **THEN** la banda se guarda con el aula vacía y la comisión se importa igual

### Requirement: Vinculación con personas y catálogos del sistema

Durante la importación, cada docente de una comisión SHALL vincularse con la persona del sistema que tenga el mismo documento, cada carrera con la carrera del sistema que tenga el mismo código, y la materia de la comisión con la materia del sistema que tenga ese código dentro de cada carrera vinculada. Lo que no tenga correspondencia MUST importarse igual, sin vínculo. Los vínculos MUST recalcularse completos en cada importación y MUST NOT depender de vínculos cargados a mano. El documento MUST usarse sólo para vincular y MUST NOT persistirse.

#### Scenario: Docente existente en el sistema

- **GIVEN** un docente de Guaraní cuyo documento coincide con una persona del sistema
- **WHEN** se importa su comisión
- **THEN** el docente de la comisión queda vinculado a esa persona

#### Scenario: Docente inexistente en el sistema

- **GIVEN** un docente de Guaraní cuyo documento no coincide con ninguna persona del sistema
- **WHEN** se importa su comisión
- **THEN** el docente queda en la comisión con su nombre y sin vínculo
- **AND** el registro de la importación cuenta un docente sin vincular

#### Scenario: Persona dada de alta después de una importación

- **GIVEN** un docente que quedó sin vincular y una persona con su documento dada de alta después
- **WHEN** se reimporta el período
- **THEN** el docente queda vinculado a esa persona

#### Scenario: La reimportación no pierde vínculos

- **GIVEN** un período importado con docentes vinculados y sin cambios en Guaraní ni en las personas del sistema
- **WHEN** se reimporta el período
- **THEN** cada docente queda vinculado a la misma persona que antes

### Requirement: Identificador estable de comisión

Cada comisión SHALL identificarse por su identificador en Guaraní, y ese identificador MUST conservarse entre reimportaciones. Si Guaraní devuelve para el período importado una comisión que ya está importada en otro período, la importación MUST fallar como respuesta inválida, sin reemplazar nada.

#### Scenario: Reimportación conserva identificadores

- **GIVEN** una comisión importada con identificador de Guaraní 4512
- **WHEN** se reimporta su período y Guaraní la sigue devolviendo
- **THEN** la comisión sigue identificándose como 4512

### Requirement: Una importación a la vez por período

El backend MUST rechazar con 409 una importación de un período lectivo mientras otra importación del mismo período está en curso.

#### Scenario: Doble confirmación concurrente

- **GIVEN** una importación de "2026 – 2.º Cuatrimestre" en curso
- **WHEN** otro actor confirma la importación del mismo período
- **THEN** la segunda solicitud recibe 409 y la primera termina normalmente

### Requirement: Registro de importaciones

Cada intento de importación SHALL registrarse con período lectivo, actor, fecha y hora de inicio y fin, resultado (exitosa o fallida), cantidad de comisiones importadas y cantidad de docentes y carreras que quedaron sin vincular. Un intento fallido MUST registrar una causa genérica, sin credenciales ni contenido de la respuesta de Guaraní.

#### Scenario: Importación exitosa registrada

- **WHEN** una importación termina con 125 comisiones
- **THEN** queda registrada como exitosa, con el actor, el período y la cantidad 125

#### Scenario: Importación fallida registrada

- **WHEN** una importación falla porque Guaraní no responde
- **THEN** queda registrada como fallida con una causa genérica de tiempo de espera

### Requirement: Credenciales de Guaraní en el servidor

Las credenciales de la API de Guaraní MUST provenir de la configuración del servidor de cada ambiente. MUST NOT solicitarse al usuario, enviarse al navegador, persistirse en la base ni escribirse en logs. Si la configuración está incompleta, los endpoints que contactan a Guaraní MUST responder con un error identificable de integración no configurada, sin intentar la llamada.

#### Scenario: Integración no configurada

- **GIVEN** un ambiente sin usuario o contraseña de Guaraní configurados
- **WHEN** un actor con `comisiones.importar` pide los períodos lectivos o confirma una importación
- **THEN** la API responde 503 indicando que la integración con Guaraní no está configurada

#### Scenario: Credenciales rechazadas por Guaraní

- **GIVEN** credenciales configuradas que Guaraní rechaza
- **WHEN** se intenta importar
- **THEN** la API responde 502 con un mensaje genérico de autenticación fallida contra Guaraní
- **AND** ni la respuesta ni el log contienen las credenciales
