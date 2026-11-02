# modelo-academico Specification

## Purpose

Define the academic catalog of canonical subjects, study plans, and their informative associations, so shared subjects remain unique while operations can select and validate a career explicitly.

## Requirements

### Requirement: Plan de estudios por carrera, con vigencia

El sistema SHALL persistir los planes de estudio en `identity.planes` asociados a una carrera (`carrera_id`), con código, nombre y bandera `vigente`. Una carrera MUST poder tener varios planes. El código de plan MUST ser único dentro de su propuesta.

#### Scenario: Varios planes para una misma carrera

- **GIVEN** la carrera `201` tiene un plan vigente y otro no vigente
- **WHEN** se consultan los planes de la carrera
- **THEN** el sistema MUST devolver ambos, identificados por su bandera `vigente`

#### Scenario: Código de plan repetido en la misma carrera

- **GIVEN** la carrera `201` ya tiene un plan con código `2010`
- **WHEN** se intenta registrar otro plan con código `2010` en esa carrera
- **THEN** el sistema MUST rechazar el alta

### Requirement: Materia canónica

El sistema SHALL persistir las materias como materias en `identity.materias`, con código de 5 dígitos con ceros a la izquierda, nombre y bandera `activa`. El código de materia MUST ser único en todo el sistema, sin importar en cuántos planes o carreras esté dictado. El código MUST almacenarse siempre con 5 caracteres.

#### Scenario: Materia compartida por varias carreras

- **GIVEN** la materia con código `01032` "Álgebra y Geometría Analítica II" está en los planes de las carreras `201`, `202`, `203` y `207`
- **WHEN** se listan las materias del catálogo
- **THEN** el sistema MUST devolver una sola entrada para `01032`

#### Scenario: Código con ceros a la izquierda

- **GIVEN** una fuente externa entrega el código `1032`
- **WHEN** se registra la materia
- **THEN** el sistema MUST almacenarlo como `01032`

#### Scenario: Código de materia repetido

- **GIVEN** existe una materia con código `01032`
- **WHEN** se intenta registrar otro materia con código `01032`
- **THEN** el sistema MUST rechazar el alta

### Requirement: Pertenencia de una materia a un plan

El sistema SHALL persistir la pertenencia de una materia a un plan en `identity.materias_plan`, con bandera `activo`. Un par `(plan, materia)` MUST aparecer como máximo una vez. Un materia puede pertenecer a varios planes, incluidos planes de propuestas distintas.

#### Scenario: Materia dictada en dos planes de carreras distintas

- **GIVEN** la materia `01032` pertenece al plan `2010` de `201` y al plan `2015` de `202`
- **WHEN** se consultan sus planes
- **THEN** el sistema MUST devolver ambas pertenencias, cada una con su carrera

#### Scenario: Par duplicado

- **GIVEN** la materia `01032` ya pertenece al plan `2010` de `201`
- **WHEN** se intenta registrar la misma pertenencia de nuevo
- **THEN** el sistema MUST rechazar el alta

#### Scenario: Materia que sale de un plan

- **GIVEN** la materia `01032` pertenece al plan `2010` de `201` con `activo = true`
- **WHEN** el plan se actualiza y la materia deja de dictarse
- **THEN** la pertenencia MUST marcarse `activo = false` sin borrar la fila, y las designaciones que la referencian MUST seguir siendo válidas

### Requirement: La carrera de una operación se elige directo, validada contra el catálogo informativo

Un pedido, una designación o una membresía de Docente MUST llevar `materia_id` y `carrera_id` directos, elegidos juntos por quien carga la operación — nunca derivados implícitamente de un plan. El sistema SHALL validar esa combinación contra el catálogo informativo `materia_plan` (que la materia se dicte, vigente, en esa carrera), pero ninguna FK de negocio referencia `materias_plan` ni `planes`: son consulta, no autoridad.

#### Scenario: Misma materia en dos carreras

- **GIVEN** la materia `01032` se dicta en las carreras `201` y `202`
- **WHEN** un pedido se crea con `materia_id = 01032` y `carrera_id = 202`
- **THEN** la carrera del pedido MUST ser `202`

#### Scenario: Carrera sin pertenencia activa

- **GIVEN** la materia `01032` no tiene pertenencia activa en ningún plan de la carrera `207`
- **WHEN** se intenta cargar una operación con `materia_id = 01032` y `carrera_id = 207`
- **THEN** el sistema MUST rechazar la operación
