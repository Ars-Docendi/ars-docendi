# Spec Delta

## MODIFIED Requirements

### Requirement: Aprobación de un pedido materializada sobre las designaciones vigentes

El sistema SHALL materializar la sincronización de la conciliación y sus eventos en una única transacción: Alta abre una vigencia, Baja la cierra y Cambio cierra/abre con valores y origen solicitados. Si la vigente comenzó en la misma fecha efectiva del Cambio, SHALL actualizar esa fila conservando ID e inicio, sin intervalo vacío o solapado, y auditar valores y orígenes anterior/nuevo. Sin novedad aprobada conserva las vigencias sin crear pedido. El origen actual SHALL identificar el pedido aprobado. La aceptación de Decanato MUST NOT materializar: sólo la sincronización lo hace, y la fecha efectiva es la de la sincronización.

#### Scenario: Aprobación de un Alta

- **GIVEN** un pedido de novedad "Alta" `en_lote` aceptado en la conciliación
- **WHEN** se sincroniza
- **THEN** se abre una designación vigente para esa persona y materia, con `origen_pedido_id` al pedido

#### Scenario: Aprobación de una Baja

- **GIVEN** un pedido de novedad "Baja" `en_lote` aceptado en la conciliación
- **WHEN** se sincroniza
- **THEN** la designación vigente de esa persona sobre esa materia queda cerrada con su `vigente_hasta`

#### Scenario: Aprobación de un Cambio

- **GIVEN** un pedido Cambio `en_lote` aceptado y una vigencia iniciada antes de la fecha de sincronización
- **WHEN** se sincroniza
- **THEN** la anterior se cierra y se abre una nueva con cargo, dedicación y tres cargas horarias solicitadas, sin solapamiento y en la misma transacción

#### Scenario: La aceptación de Decanato no escribe designaciones

- **GIVEN** un pedido en `en_revision_decanato`
- **WHEN** Decanato lo acepta
- **THEN** el pedido pasa a `en_lote` y ninguna designación se abre, se cierra ni se modifica

#### Scenario: Sin novedad no altera el estado vigente

- **GIVEN** una persona y materia con designación vigente y sin novedad aprobada
- **WHEN** se consulta el resultado para el lote del período
- **THEN** conserva sus valores sin crear un pedido Sin novedad

#### Scenario: Un fallo parcial no deja estado inconsistente

- **GIVEN** un Cambio en proceso de materialización
- **WHEN** falla la apertura o actualización intradía
- **THEN** se revierte la operación completa, conservando el estado vigente, el pedido y sus eventos previos

#### Scenario: Cambio sobre una vigencia iniciada ese día

- **GIVEN** una vigencia iniciada en la fecha de sincronización del Cambio
- **WHEN** se sincroniza el Cambio
- **THEN** mantiene ID e inicio y adopta cargo, dedicación, horas y origen del pedido nuevo
- **AND** el snapshot inicial del trámite no se recalcula y la auditoría conserva valores y orígenes anterior/nuevo

#### Scenario: Cambio intradía reintentado

- **GIVEN** un Cambio intradía confirmado con su clave idempotente
- **WHEN** se reintenta la sincronización reconocida
- **THEN** no se duplica la actualización ni el historial

## ADDED Requirements

### Requirement: Estados y eventos de la conciliación en el modelo

El modelo persistido SHALL admitir los estados de pedido `aprobado` y `no_aprobado`, y los eventos de historial `sincronizar` y `no_aprobar`, con la base de datos como autoridad. `aprobado` y `no_aprobado` MUST ocupar el cupo de BR-designaciones-001. Cada evento nuevo MUST quedar sellado en la cadena del período [BR-designaciones-019] sin cambiar la forma canónica de los sellos existentes.

#### Scenario: Estado desconocido rechazado por la base

- **GIVEN** una escritura directa con un estado de pedido fuera del catálogo
- **WHEN** se intenta confirmar
- **THEN** la base MUST rechazarla

#### Scenario: Un rechazado en conciliación sigue ocupando el cupo

- **GIVEN** un pedido `no_aprobado` de un docente en el período
- **WHEN** se intenta crear otro pedido para el mismo docente y período
- **THEN** el sistema MUST rechazarlo como duplicado [BR-designaciones-001]

#### Scenario: Verificación de integridad tras conciliar

- **GIVEN** un período con pedidos sincronizados y rechazados en la conciliación
- **WHEN** se verifica la cadena de sellos
- **THEN** la verificación MUST resultar íntegra, incluidos los sellos anteriores al cambio

### Requirement: Pedidos en lote materializados antes del cambio

Un pedido que llegó a `en_lote` cuando la aceptación de Decanato todavía materializaba SHALL quedar marcado como ya materializado. Sincronizarlo MUST pasarlo a `aprobado` sin volver a escribir las designaciones. Rechazarlo en la conciliación MUST denegarse con un mensaje que indique corregir la designación desde la administración.

#### Scenario: Sincronizar un pedido en lote heredado

- **GIVEN** un Alta `en_lote` cuya designación ya se abrió al aceptar Decanato
- **WHEN** se sincroniza
- **THEN** el pedido pasa a `aprobado` y no se abre una segunda designación
