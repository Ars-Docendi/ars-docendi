# Spec Delta

## ADDED Requirements

### Requirement: Completitud de Alta y Cambio antes del circuito

Enviar, reenviar y aprobar un Alta o Cambio MUST exigir cargo válido, dedicación válida y horas de materia positivas y no nulas, además de las reglas existentes por novedad. La falta de datos SHALL devolver validación por campo sin avance, snapshot ni eventos parciales. El guardado de borradores MUST NOT ampliar la admisión de datos inválidos ni imponer a una Baja los campos de un Alta.

#### Scenario: Enviar Alta sin cargo

- **GIVEN** un borrador Alta con documentación válida pero sin cargo
- **WHEN** se intenta enviarlo
- **THEN** se rechaza con error de campo y conserva su estado, asociaciones e historial

#### Scenario: Reenviar Cambio sin horas

- **GIVEN** un Cambio devuelto con horas nulas
- **WHEN** su propietario intenta reenviarlo
- **THEN** se rechaza antes de confirmar la transición y el snapshot inicial permanece inmutable

#### Scenario: Aprobar legado incompleto

- **GIVEN** un pedido antiguo en revisión con cargo u horas faltantes
- **WHEN** el revisor autorizado intenta aprobarlo
- **THEN** recibe un error consumible sin materialización parcial ni valores inventados

#### Scenario: Baja válida conserva su semántica

- **GIVEN** una Baja con tipo, justificativo, legajo y vigencia válidos
- **WHEN** se envía sin cargo nuevo ni horas nuevas
- **THEN** no se rechaza por campos exclusivos de Alta/Cambio

### Requirement: Listados completos sin cargar historial innecesario

Los listados SHALL conservar todas las filas visibles, filtros, orden y acciones vigentes sin paginación visible. La respuesta y su obtención MUST cargar solo datos necesarios para la grilla y autorización, sin historias o documentos completos no utilizados. El detalle SHALL seguir entregando historial estable, snapshot y documentación autorizada.

#### Scenario: Lista de pedidos con historias extensas

- **GIVEN** varios pedidos visibles con historiales extensos
- **WHEN** se consulta Mis pedidos o Revisión
- **THEN** se reciben todas las filas autorizadas y sus acciones sin transportar cada historia completa

#### Scenario: Abrir el detalle desde la fila

- **GIVEN** un pedido incluido en un listado liviano
- **WHEN** el actor abre su detalle
- **THEN** obtiene el snapshot inmutable, historial ordenado y documentación que tiene permiso para consultar

#### Scenario: Crece el historial sin crecer la lista

- **GIVEN** dos datasets con las mismas filas visibles y distinta cantidad de eventos por pedido
- **WHEN** se compara la obtención del listado
- **THEN** la cardinalidad de resultados materializados no se multiplica por el historial ajeno al contrato de la grilla

## MODIFIED Requirements

### Requirement: Alta de persona sin cuenta de autenticación

Un Alta SHALL poder registrar una persona nueva o reutilizar la referencia canónica resuelta por el backend cuando corresponda, sin exigir UPN, cuenta Microsoft ni legajo. MUST conservar la referencia de persona y rechazar documentos duplicados sin pedido parcial. El primer ingreso posterior SHALL vincular únicamente un usuario previamente creado por administración; MUST NOT provisionarlo automáticamente.

#### Scenario: Alta de persona nueva

- **GIVEN** un DNI inexistente
- **WHEN** el Jefe guarda o envía un Alta válida
- **THEN** se registra la persona sin legajo ni cuenta Microsoft y se persiste el pedido con su referencia canónica

#### Scenario: Documento ya registrado

- **GIVEN** un DNI registrado
- **WHEN** se intenta registrar otra persona para un Alta
- **THEN** se rechaza el conflicto sin duplicar la persona ni persistir un pedido parcial

#### Scenario: Primer login posterior al Alta

- **GIVEN** una persona registrada por Alta y luego vinculada a un usuario creado por administración sin cuenta Microsoft
- **WHEN** ingresa con Microsoft y cumple la política de vinculación vigente
- **THEN** se vincula la cuenta externa al usuario existente sin crear otro usuario ni persona
