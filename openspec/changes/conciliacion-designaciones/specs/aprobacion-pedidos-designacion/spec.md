# Spec Delta

## MODIFIED Requirements

### Requirement: Tablero de revisión filtrado por ámbito

El sistema SHALL ofrecer a los roles revisores (Coordinador, Secretaría, Decanato, Administración) una
superficie de revisión (`/designaciones/revision`) en forma de **tabla** organizada en cuatro grupos
por **estado de avance** — En revisión (toda la cadena), Aceptados, Devueltos, Rechazados — iguales
para todo actor (no hay una columna "propia" por rol). La superficie MUST listar únicamente los
pedidos del ámbito del actor [BR-designaciones-009]: el Coordinador ve solo los de su carrera;
Secretaría, Decanato y Administración ven todo el departamento. Dentro del grupo "En revisión", los
pedidos en el turno del actor (`esTuTurno`) SHALL ordenarse primero. La pantalla MUST representar
explícitamente los estados Loading, Empty, Error y Success.

#### Scenario: El Coordinador ve solo los pedidos de su carrera [BR-designaciones-009]

- **GIVEN** un Coordinador autenticado cuyo ámbito es su carrera
- **WHEN** abre `/designaciones/revision`
- **THEN** el grupo "En revisión" lista los pedidos `en_revision_*` de su carrera
- **AND** no muestra pedidos de otras carreras

#### Scenario: Cada pedido cae en el grupo correcto por estado de avance

- **GIVEN** pedidos en estados `en_revision_*`, `en_lote`, `aprobado`, `no_aprobado`, `rechazado` y
  `devuelto` dentro del ámbito del revisor
- **WHEN** abre la superficie de revisión
- **THEN** los pedidos en `en_revision_*` aparecen en "En revisión" (los del turno del actor primero),
  los `en_lote` y `aprobado` en "Aceptados", los `devuelto` en "Devueltos" y los `rechazado` y
  `no_aprobado` en "Rechazados"
- **AND** cada uno muestra su estado propio: En lote, Aprobado, Rechazado en conciliación o Rechazado

#### Scenario: Superficie sin pedidos en el ámbito

- **GIVEN** un revisor sin pedidos en su ámbito
- **WHEN** abre la superficie de revisión
- **THEN** ve el estado vacío sin filas, sin romper la navegación

### Requirement: Aceptación que avanza la cadena de aprobación

El sistema SHALL permitir al revisor de la etapa actual aceptar un pedido, avanzándolo a la etapa siguiente de la cadena: `en_revision_coordinador` → `en_revision_secretaria` → `en_revision_decanato` → `en_lote`. La transición MUST registrar un evento "aceptar" en el historial. La aceptación de Decanato MUST NOT modificar las designaciones vigentes: `en_lote` queda pendiente de conciliar y sólo admite las acciones de la conciliación.

#### Scenario: El Coordinador acepta y avanza a Secretaría

- **GIVEN** un pedido en `en_revision_coordinador` de la carrera del Coordinador
- **WHEN** el Coordinador lo acepta
- **THEN** el pedido pasa a `en_revision_secretaria` y se registra el evento en el historial

#### Scenario: La Secretaría acepta y avanza a Decanato

- **GIVEN** un pedido en `en_revision_secretaria`
- **WHEN** la Secretaría lo acepta
- **THEN** el pedido pasa a `en_revision_decanato`

#### Scenario: El Decanato acepta y el pedido va a lote

- **GIVEN** un pedido en `en_revision_decanato`
- **WHEN** el Decanato lo acepta
- **THEN** el pedido pasa a `en_lote` y las designaciones vigentes del docente no cambian
- **AND** el pedido no admite aceptar, devolver, rechazar en el circuito ni reenviar

## ADDED Requirements

### Requirement: Estado simplificado sin seguimiento

Para quien no tiene `designaciones.ver_seguimiento`, el backend SHALL informar `en_lote` como "en proceso" (hoy lo informa como "aprobado"), `aprobado` como "aprobado" y `no_aprobado` como "rechazado", con el motivo de la conciliación, igual que un rechazo del circuito. MUST NOT exponer la etapa.

#### Scenario: El Jefe de Cátedra consulta un pedido en lote

- **GIVEN** un Jefe de Cátedra sin `designaciones.ver_seguimiento` y un pedido suyo `en_lote`
- **WHEN** consulta Mis pedidos
- **THEN** lo ve "en proceso" hasta que se sincroniza, y "aprobado" después
