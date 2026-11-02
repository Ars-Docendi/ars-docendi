# Spec Delta

## ADDED Requirements

### Requirement: Numeración de pedidos sin truncamiento

Los números de pedido SHALL conservar el prefijo de año y un correlativo con mínimo de cuatro dígitos, sin truncar valores más largos. La asignación MUST ser concurrentemente segura, conservar números históricos y admitir gaps; MUST NOT reiniciar la secuencia ni renumerar registros para evitar colisiones.

#### Scenario: Correlativo de cinco dígitos

- **GIVEN** valores de secuencia 1000, 9999, 10000 y 10001 en el mismo año
- **WHEN** se asignan sus números
- **THEN** son distintos y los dos últimos conservan completos los sufijos 10000 y 10001

#### Scenario: Intento revertido

- **GIVEN** una creación consume un correlativo y luego se revierte
- **WHEN** se crea el siguiente pedido
- **THEN** no se exige reutilizar el número consumido y no se modifica ninguno existente

### Requirement: Existencia referencial de la materia de una designación

Toda designación MUST referenciar una materia canónica existente. La base SHALL rechazar inserciones o actualizaciones huérfanas y borrados físicos incompatibles. Esta garantía MUST NOT imponer pertenencia a un plan ni una FK compuesta materia–carrera informativa.

#### Scenario: SQL con materia inexistente

- **GIVEN** una persona, cargo, dedicación y carrera válidos
- **WHEN** una escritura usa una materia inexistente
- **THEN** la base rechaza toda la operación

#### Scenario: Materia usada solo por designaciones

- **GIVEN** una materia referenciada por una designación y sin pedidos
- **WHEN** se intenta borrarla físicamente
- **THEN** la base conserva la materia y su designación

### Requirement: Mutaciones de vigencias sin pérdida concurrente

La administración y el circuito SHALL coordinar escrituras sobre la misma persona/materia. Una operación basada en una versión obsoleta MUST fallar con conflicto o revalidarse antes de confirmar; MUST NOT sobrescribir atributos ni materializar un snapshot obsoleto silenciosamente. Cambios de estado, vigencias y sus eventos SHALL confirmarse atómicamente.

#### Scenario: Administración contra aprobación

- **GIVEN** administración y aprobación observan la misma vigencia y una modifica sus horas
- **WHEN** la otra intenta confirmar usando el estado anterior
- **THEN** no pierde el cambio confirmado y una respuesta de conflicto no agrega eventos ni materialización parciales

### Requirement: Materia y carrera canónicas del pedido

El sistema SHALL persistir cada pedido con una materia y carrera canónicas elegidas juntas, y sus horas. Alta exige materia dentro del ámbito vigente del Jefe; Baja/Cambio exige además designación vigente del docente en esa materia. La carrera almacenada SHALL determinar al Coordinador competente; los planes son informativos y MUST NOT convertirse en referencia obligatoria de negocio.

#### Scenario: La carrera del pedido determina su revisión

- **GIVEN** una materia compartida por dos carreras y un pedido para una de ellas
- **WHEN** se consulta el tablero de revisión
- **THEN** aparece solo en el ámbito de la carrera elegida, sin derivarla de una columna inexistente de la materia

#### Scenario: Un Jefe de Cátedra no puede cargar un pedido sobre una cátedra ajena

- **GIVEN** un Jefe sin rol vigente sobre una materia
- **WHEN** intenta crear un pedido sobre ella
- **THEN** se deniega la creación

#### Scenario: Baja sobre una materia no designada al docente

- **GIVEN** un Jefe con ámbito sobre Materia A y un docente con vigencia solo en B
- **WHEN** intenta crear una Baja para A
- **THEN** se rechaza por ausencia de vigencia compatible

#### Scenario: Cambio sobre la materia seleccionada del docente

- **GIVEN** un docente con vigencias en A y B y un Jefe con ámbito sobre ambas
- **WHEN** crea un Cambio para B
- **THEN** el pedido y su snapshot refieren los datos de B

#### Scenario: Un rol revocado deja de habilitar la carga

- **GIVEN** un usuario cuyo rol de Jefe sobre una materia fue revocado
- **WHEN** intenta crear un pedido para ella
- **THEN** se deniega la creación

## MODIFIED Requirements

### Requirement: Aprobación de un pedido materializada sobre las designaciones vigentes

El sistema SHALL materializar aprobación y eventos en una única transacción: Alta abre una vigencia, Baja la cierra y Cambio cierra/abre con valores y origen solicitados. Si la vigente comenzó en la misma fecha efectiva del Cambio, SHALL actualizar esa fila conservando ID e inicio, sin intervalo vacío o solapado, y auditar valores y orígenes anterior/nuevo. Sin novedad aprobada conserva las vigencias sin crear pedido. El origen actual SHALL identificar el pedido aprobado.

#### Scenario: Aprobación de un Alta

- **GIVEN** un pedido de novedad "Alta" que completó el circuito de aprobación
- **WHEN** el sistema materializa el resultado
- **THEN** se abre una designación vigente para esa persona y materia, con `origen_pedido_id` al pedido

#### Scenario: Aprobación de una Baja

- **GIVEN** un pedido de novedad "Baja" que completó el circuito
- **WHEN** el sistema materializa el resultado
- **THEN** la designación vigente de esa persona sobre esa materia queda cerrada con su `vigente_hasta`

#### Scenario: Aprobación de un Cambio

- **GIVEN** un pedido Cambio aprobado y una vigencia iniciada antes de su fecha efectiva
- **WHEN** se materializa
- **THEN** la anterior se cierra y se abre una nueva con cargo, dedicación y tres cargas horarias solicitadas, sin solapamiento y en la misma transacción

#### Scenario: Sin novedad no altera el estado vigente

- **GIVEN** una persona y materia con designación vigente y sin novedad aprobada
- **WHEN** se consulta el resultado para el lote del período
- **THEN** conserva sus valores sin crear un pedido Sin novedad

#### Scenario: Un fallo parcial no deja estado inconsistente

- **GIVEN** un Cambio en proceso de materialización
- **WHEN** falla la apertura o actualización intradía
- **THEN** se revierte la operación completa, conservando el estado vigente, el pedido y sus eventos previos

#### Scenario: Cambio sobre una vigencia iniciada ese día

- **GIVEN** una vigencia iniciada en la fecha efectiva del Cambio
- **WHEN** se aprueba el Cambio
- **THEN** mantiene ID e inicio y adopta cargo, dedicación, horas y origen del pedido nuevo
- **AND** el snapshot inicial del trámite no se recalcula y la auditoría conserva valores y orígenes anterior/nuevo

#### Scenario: Cambio intradía reintentado

- **GIVEN** un Cambio intradía confirmado con su clave idempotente
- **WHEN** se reintenta la aprobación reconocida
- **THEN** no se duplica la actualización ni el historial

## REMOVED Requirements

### Requirement: El pedido cubre exactamente una materia

**Reason**: El requisito antiguo deriva la carrera de una columna de materia que ya no existe y contradice el modelo académico y la API actuales. Sus escenarios de ámbito y elección de materia se preservan en el reemplazo, no se elimina su protección.

**Migration**: Usar el requisito Materia y carrera canónicas del pedido: materia/carrera directas elegidas juntas, planes informativos y revisión por la carrera almacenada. No transformar filas históricas ni restaurar una FK a planes.
