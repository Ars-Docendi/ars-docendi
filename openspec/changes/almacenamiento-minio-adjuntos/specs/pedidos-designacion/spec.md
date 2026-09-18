## MODIFIED Requirements

### Requirement: Reglas por novedad

El backend MUST aplicar las reglas por novedad al crear y editar. `Alta` SHALL recibir los datos de una persona nueva —DNI, nombre y apellido— y exigir tres archivos disponibles con propósitos CV, DNI frente y DNI dorso [BR-designaciones-002]. `Baja` exige tipo, un archivo disponible con propósito justificativo y persona con legajo [BR-designaciones-003, BR-designaciones-018]. `Cambio de cargo o dedicación` exige justificación y persona con legajo [BR-designaciones-004, BR-designaciones-018]. Las únicas novedades admitidas para nuevos pedidos, ediciones y avance del circuito SHALL ser Alta, Baja y Cambio de cargo o dedicación. El formulario MUST exigir una elección explícita. `Sin novedad` MUST NOT ofrecerse ni aceptarse como nueva solicitud; los registros legados SHALL conservarse para lectura histórica. Los adjuntos SHALL referenciar `archivoId` emitidos por el backend; el cliente MUST NOT enviar una URI arbitraria ni marcar un archivo pendiente como disponible.

#### Scenario: Alta con datos nuevos

- **WHEN** el Jefe elige "Alta"
- **THEN** el formulario muestra campos de DNI y apellido/nombre, no un selector de personas ya registradas

#### Scenario: Alta con datos y archivos disponibles

- **WHEN** el Jefe confirma DNI, nombre, apellido, CV, DNI frente y DNI dorso disponibles
- **THEN** el backend permite guardar o enviar el pedido y conserva las asociaciones de los tres archivos

#### Scenario: Alta incompleta

- **WHEN** se guarda un Alta sin DNI, nombre, apellido o alguno de sus tres archivos disponibles y validados
- **THEN** el backend rechaza el pedido sin modificarlo ni agregar historial

#### Scenario: Baja o Cambio sin legajo

- **WHEN** la persona no tiene legajo
- **THEN** el backend rechaza una Baja o Cambio, pero permite un Alta con datos y archivos válidos

#### Scenario: Adjunto pendiente, rechazado o fuera de propósito

- **WHEN** un cliente intenta crear o editar un pedido usando un archivo pendiente, rechazado, inexistente o de otro propósito
- **THEN** el backend rechaza la operación sin persistir cambios, asociaciones ni historial parcial

#### Scenario: URI externa no aceptada

- **WHEN** un cliente envía una URI externa en lugar de un `archivoId` confirmado
- **THEN** la API rechaza la solicitud sin consultar ni descargar contenido desde esa URI

#### Scenario: Solicitud Sin novedad rechazada

- **GIVEN** un cliente que omite el formulario
- **WHEN** intenta crear, editar, enviar, reenviar o aprobar un pedido Sin novedad
- **THEN** la API MUST rechazar la operación sin cambiar datos ni historial

#### Scenario: Lectura de pedido legado

- **GIVEN** un pedido Sin novedad anterior al cambio
- **WHEN** se consulta su detalle autorizado
- **THEN** MUST conservar su número, texto original e historial sin transformarse en otra novedad
