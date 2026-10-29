# Spec Delta

## Purpose

Garantizar que cualquier alteración del circuito de un pedido de designación hecha fuera de la
aplicación sea detectable e irrefutable, mediante eventos sellados, encadenados por período,
firmados con una clave ajena a la base y anclados en documentos institucionales.

## ADDED Requirements

### Requirement: Cada evento del circuito queda sellado

Desde el primer envío a revisión, cada evento que el sistema registre en el historial de un pedido
SHALL quedar sellado en la misma transacción [BR-designaciones-019]. El sello MUST cubrir los datos
del evento y el estado canónico del pedido tras el evento. Si el sello no puede registrarse, la
operación MUST fallar completa. Los eventos de un pedido que nunca salió de borrador MUST NOT
sellarse.

#### Scenario: Una transición queda sellada

- **GIVEN** un pedido en `en_revision_coordinador`
- **WHEN** el Coordinador lo acepta
- **THEN** el evento de historial y su sello MUST persistirse juntos
- **AND** el sello MUST referenciar el estado resultante `en_revision_secretaria`

#### Scenario: El envío inicia el sellado

- **GIVEN** un borrador creado y editado por el Jefe de Cátedra
- **WHEN** lo envía a revisión
- **THEN** el evento `enviar` MUST ser el primer sello del pedido y cubrir sus datos completos

#### Scenario: Un borrador sigue siendo eliminable

- **GIVEN** un borrador que nunca se envió
- **WHEN** el Jefe de Cátedra lo elimina
- **THEN** la eliminación MUST proceder como hasta ahora, sin hallazgos de integridad

#### Scenario: Falla del sellado

- **WHEN** el sello de un evento no puede registrarse
- **THEN** ni el evento ni el cambio del pedido MUST persistirse

### Requirement: Cadena única por período

Los sellos de todos los pedidos de un período SHALL formar una única secuencia ordenada sin huecos,
donde cada sello incluye el hash del anterior. Dos operaciones concurrentes sobre el mismo período
MUST NOT producir dos sellos con la misma posición ni con el mismo antecesor.

#### Scenario: Transiciones concurrentes

- **GIVEN** dos revisores que actúan al mismo tiempo sobre pedidos distintos del mismo período
- **WHEN** ambas operaciones confirman
- **THEN** la cadena MUST contener ambos sellos en posiciones consecutivas, encadenados entre sí

### Requirement: Firma con clave ajena a la base

Cada sello SHALL firmarse con una clave secreta configurada en el host de la aplicación e
identificada por versión. La clave MUST NOT almacenarse en la base, exponerse por API ni
registrarse en logs. En producción, la aplicación MUST NOT arrancar sin clave de sellado.

#### Scenario: Sello fabricado por SQL directo

- **GIVEN** alguien con acceso a la base que inserta un evento y un sello con hashes recalculados
- **WHEN** se verifica la cadena del período
- **THEN** la verificación MUST reportar la firma de ese sello como inválida

#### Scenario: Arranque sin clave

- **GIVEN** el ambiente de producción sin clave de sellado configurada
- **WHEN** se inicia la aplicación
- **THEN** el arranque MUST fallar con un error que no revele valores secretos

#### Scenario: Rotación de clave

- **GIVEN** sellos firmados con la versión 1 de la clave y una versión 2 configurada como vigente
- **WHEN** se sella un evento nuevo y se verifica la cadena
- **THEN** el sello nuevo MUST usar la versión 2 y los anteriores MUST seguir verificando con la 1

### Requirement: Registros de integridad inmutables en base

La base SHALL rechazar UPDATE y DELETE sobre los sellos y las anclas, cualquiera sea el cliente que
los ejecute. MUST rechazar también un sello cuya posición o antecesor no correspondan a la cabeza
actual de su período.

#### Scenario: Intento de borrar un sello

- **WHEN** un cliente SQL intenta borrar o modificar un sello o un ancla
- **THEN** la base MUST rechazar la sentencia y conservar la fila

#### Scenario: Sello fuera de secuencia

- **WHEN** se intenta insertar un sello que no encadena con la cabeza actual del período
- **THEN** la base MUST rechazar la inserción

### Requirement: Verificación de integridad por período

El sistema SHALL verificar la cadena de un período y reportar, por categoría y con identificadores
técnicos solamente: sello con hash inválido, sello con firma inválida, secuencia rota, evento sin
sello, pedido cuyo estado actual diverge de su último sello y ancla no reproducida por la cadena.
La verificación MUST NOT modificar datos ni exponer valores de negocio.

#### Scenario: Cadena íntegra

- **GIVEN** un período cuyos pedidos sólo cambiaron mediante la aplicación
- **WHEN** se verifica
- **THEN** el resultado MUST ser íntegro, con la posición y el hash de cabeza

#### Scenario: Estado del pedido alterado por SQL

- **GIVEN** un pedido en `en_revision_secretaria` cuyo estado se cambia por SQL a `en_lote`
- **WHEN** se verifica el período
- **THEN** MUST reportarse el pedido en la categoría de estado divergente

#### Scenario: Evento de historial agregado por SQL

- **GIVEN** un pedido enviado al que se le inserta por SQL un evento de historial sin sello
- **WHEN** se verifica el período
- **THEN** MUST reportarse ese evento como evento sin sello

#### Scenario: Historial alterado con protecciones desactivadas

- **GIVEN** un superusuario que desactiva las protecciones y modifica el comentario de un evento sellado
- **WHEN** se verifica el período
- **THEN** MUST reportarse el sello de ese evento con hash inválido

#### Scenario: Pedido enviado borrado por completo

- **GIVEN** un superusuario que borra un pedido enviado junto con su historial y sus sellos
- **WHEN** se verifica el período
- **THEN** MUST reportarse secuencia rota o ancla no reproducida

### Requirement: Consulta de integridad por API

El sistema SHALL exponer `GET /api/designaciones/periodos/{id}/integridad`, que devuelve si la cadena
es íntegra, la posición y el hash de cabeza y los hallazgos por categoría. La API MUST exigir el
permiso efectivo `auditoria.ver`.

#### Scenario: Consulta autorizada

- **GIVEN** un actor con `auditoria.ver`
- **WHEN** consulta la integridad de un período existente
- **THEN** recibe el resultado de la verificación

#### Scenario: Consulta sin permiso

- **GIVEN** un actor autenticado sin `auditoria.ver`
- **WHEN** consulta la integridad de un período
- **THEN** la API MUST responder 403

### Requirement: Diagnóstico operativo incluye la cadena

El diagnóstico operativo de integridad SHALL incluir los hallazgos de la cadena de todos los
períodos, con las mismas restricciones que el resto del diagnóstico: categorías e identificadores
técnicos, sin valores de negocio y sin habilitar correcciones automáticas.

#### Scenario: Hallazgo visible en el diagnóstico

- **GIVEN** un período con un evento sin sello
- **WHEN** se ejecuta el diagnóstico operativo
- **THEN** MUST aparecer la categoría correspondiente con su cantidad y ejemplos de identificadores

### Requirement: Sellado del historial existente

El sistema SHALL ofrecer una operación explícita del host que sella, en orden cronológico, los
eventos sin sello de pedidos que ya salieron de borrador, y registra un ancla marcada como
retroactiva. MUST ser idempotente y MUST NOT ejecutarse automáticamente al arrancar ni al migrar.

#### Scenario: Primer sellado tras desplegar

- **GIVEN** un período con historial previo a este cambio
- **WHEN** el operador ejecuta el sellado retroactivo
- **THEN** los eventos quedan sellados, se registra un ancla retroactiva y la verificación resulta íntegra

#### Scenario: Reejecución

- **WHEN** el sellado retroactivo se ejecuta sobre un período ya sellado
- **THEN** MUST NOT crear sellos ni anclas nuevas
