# pedidos-designacion

## Purpose

Flujo del Jefe de Cátedra para cargar, editar y enviar a revisión los pedidos de designación docente de su cátedra dentro del período abierto. Cubre la lista "Mis pedidos", el alta/edición de pedidos con secciones condicionales por novedad, la validación de adjuntos y justificaciones obligatorias, la máquina de estados pura del lado del Jefe de Cátedra y la persistencia mock del flujo entre roles y recargas.

## Requirements

### Requirement: Lista "Mis pedidos" del Jefe de Cátedra

El sistema SHALL ofrecer al Jefe de Cátedra una pantalla "Mis pedidos" (`/designaciones/mis-pedidos`) que liste los pedidos de designación de su cátedra para el período abierto, mostrando por cada pedido el docente, la materia asociada, la novedad, el estado (vía `StatusBadge`) y el flag de prioritario. La pantalla MUST representar explícitamente los cuatro estados de carga: Loading, Empty, Error y Success.

#### Scenario: Lista con pedidos existentes

- **GIVEN** un Jefe de Cátedra autenticado con pedidos cargados en el período abierto
- **WHEN** abre `/designaciones/mis-pedidos`
- **THEN** ve la lista de sus pedidos con docente, materia, novedad, `StatusBadge` por estado y el flag de prioritario cuando corresponde

#### Scenario: Estado vacío sin pedidos

- **GIVEN** un Jefe de Cátedra sin pedidos cargados en el período abierto
- **WHEN** abre `/designaciones/mis-pedidos`
- **THEN** ve un estado vacío con la acción "Nuevo pedido", sin filas de pedidos

#### Scenario: Estado de carga y de error

- **WHEN** la consulta de pedidos está en curso
- **THEN** la pantalla muestra el estado Loading
- **AND WHEN** la consulta falla
- **THEN** la pantalla muestra el estado Error sin romper la navegación

#### Scenario: Precarga de docentes del período anterior

- **GIVEN** un período abierto recién disponible para el Jefe de Cátedra
- **WHEN** abre "Mis pedidos" por primera vez
- **THEN** ve precargados los docentes del período anterior como pedidos con novedad "Sin novedad"

### Requirement: Creación de pedido de designación

El sistema SHALL permitir al Jefe de Cátedra crear un pedido de designación desde `/designaciones/pedidos/nuevo`, capturando los datos comunes del pedido: docente (DNI, nombre), antigüedad, cargo y dedicación actual (read-only, mock), materia asociada, novedad y el flag "hace más horas en otro Departamento". El pedido se crea en estado `borrador`.

#### Scenario: Alta de un pedido en borrador

- **GIVEN** un Jefe de Cátedra en el form de nuevo pedido
- **WHEN** completa los datos comunes y una novedad válida con sus campos requeridos
- **THEN** el pedido se persiste en estado `borrador` y aparece en "Mis pedidos"

#### Scenario: Un pedido por docente por período [BR-designaciones-001]

- **GIVEN** un docente que ya tiene un pedido en el período abierto
- **WHEN** el Jefe de Cátedra intenta crear un segundo pedido para ese mismo docente en el mismo período
- **THEN** el sistema MUST rechazar la creación e indicar que ya existe un pedido para ese docente en el período

### Requirement: Secciones condicionales por novedad

El form de pedido SHALL mostrar u ocultar secciones según la novedad seleccionada (Radio: "Sin novedad" / "Alta" / "Baja" / "Cambio de cargo o dedicación"). "Alta" y "Cambio de cargo o dedicación" exponen cargo y dedicación solicitados; "Sin novedad" no expone campos adicionales.

#### Scenario: La sección de solicitud aparece solo para Alta y Cambio

- **WHEN** el usuario selecciona la novedad "Alta" o "Cambio de cargo o dedicación"
- **THEN** el form muestra los campos de cargo y dedicación solicitados
- **AND WHEN** selecciona "Sin novedad"
- **THEN** el form oculta los campos de cargo y dedicación solicitados

#### Scenario: La sección de adjuntos se adapta a la novedad

- **WHEN** el usuario cambia la novedad seleccionada
- **THEN** la sección de adjuntos requeridos se actualiza para reflejar los adjuntos exigidos por esa novedad

### Requirement: Adjuntos y justificación obligatorios por novedad

El sistema SHALL validar los adjuntos y la justificación obligatorios según la novedad antes de permitir guardar o enviar el pedido. Una novedad "Alta" MUST exigir CV + foto de DNI frente + foto de DNI dorso [BR-designaciones-002]; "Baja" MUST exigir un adjunto justificativo [BR-designaciones-003]; "Cambio de cargo o dedicación" MUST exigir una justificación [BR-designaciones-004]. (En el prototipo los adjuntos son solo metadata mock.)

#### Scenario: Alta exige CV, DNI frente y DNI dorso [BR-designaciones-002]

- **GIVEN** un pedido con novedad "Alta"
- **WHEN** falta alguno de los adjuntos CV, DNI frente o DNI dorso
- **THEN** la validación MUST bloquear el guardado e indicar el adjunto faltante

#### Scenario: Baja exige justificativo [BR-designaciones-003]

- **GIVEN** un pedido con novedad "Baja"
- **WHEN** falta el adjunto justificativo
- **THEN** la validación MUST bloquear el guardado e indicar que el justificativo es obligatorio

#### Scenario: Cambio exige justificación [BR-designaciones-004]

- **GIVEN** un pedido con novedad "Cambio de cargo o dedicación"
- **WHEN** la justificación está vacía
- **THEN** la validación MUST bloquear el guardado e indicar que la justificación es obligatoria

#### Scenario: La validación inline bloquea el submit inválido

- **WHEN** el usuario intenta guardar o enviar un pedido con campos requeridos faltantes
- **THEN** el form muestra el error inline en el campo afectado y no envía la acción al store

### Requirement: Edición de pedido en borrador o devuelto

El sistema SHALL permitir editar un pedido únicamente cuando está en estado `borrador`, o en estado `devuelto` cuando el actor es su propietario actual. La edición no cambia el estado del pedido [BR-designaciones-008].

#### Scenario: Editar un borrador propio

- **GIVEN** un pedido en estado `borrador` del Jefe de Cátedra
- **WHEN** el Jefe de Cátedra lo edita desde `/designaciones/pedidos/:id/editar` y guarda
- **THEN** los cambios se persisten y el pedido permanece en `borrador`

#### Scenario: No se puede editar tras enviar a revisión [BR-designaciones-008]

- **GIVEN** un pedido en estado `en_revision_coordinador`
- **WHEN** el Jefe de Cátedra intenta editarlo
- **THEN** el sistema MUST denegar la edición (el pedido queda read-only para el JC salvo devolución)

### Requirement: Cancelación de pedido en borrador

El sistema SHALL permitir al Jefe de Cátedra cancelar un pedido únicamente en estado `borrador`, llevándolo al estado terminal `cancelado`.

#### Scenario: Cancelar un borrador

- **GIVEN** un pedido en estado `borrador`
- **WHEN** el Jefe de Cátedra lo cancela
- **THEN** el pedido pasa a estado `cancelado` y deja de ofrecer acciones de edición o envío

#### Scenario: No se puede cancelar fuera de borrador

- **GIVEN** un pedido en estado `en_revision_coordinador`
- **WHEN** se intenta cancelarlo
- **THEN** el sistema MUST denegar la acción

### Requirement: Envío de pedido a revisión

El sistema SHALL permitir al Jefe de Cátedra dueño de la cátedra enviar a revisión un pedido en estado `borrador`, transicionándolo a `en_revision_coordinador` e iniciando la cadena de aprobación [BR-designaciones-008].

#### Scenario: Enviar un borrador a revisión

- **GIVEN** un pedido válido en estado `borrador`
- **WHEN** el Jefe de Cátedra lo envía a revisión
- **THEN** el pedido pasa a `en_revision_coordinador` y se registra el evento "enviar" en su historial

#### Scenario: No se puede enviar un pedido que no está en borrador

- **GIVEN** un pedido en un estado distinto de `borrador`
- **WHEN** se intenta enviarlo a revisión
- **THEN** el sistema MUST denegar la acción

### Requirement: Guards e idempotencia de la máquina de estados (lado Jefe de Cátedra)

La máquina de estados SHALL implementarse como lógica pura (sin React ni I/O) que, dada `(pedido, acción, actor)`, valida los guards y devuelve el pedido resultante o lanza un error de dominio. Las acciones sobre pedidos en estados terminales (`cancelado`, `rechazado`, `en_lote`) MUST ser denegadas (idempotencia terminal). Cada transición MUST registrar un evento en el historial del pedido.

#### Scenario: Acción sobre un pedido terminal es denegada

- **GIVEN** un pedido en estado `cancelado`
- **WHEN** se intenta enviar, editar o cancelar
- **THEN** la máquina de estados MUST lanzar un error de dominio y no modificar el pedido

#### Scenario: Cada transición deja rastro en el historial

- **WHEN** se aplica una transición válida (`enviar`, `cancelar`, `editar`)
- **THEN** el pedido resultante incluye un nuevo evento de historial con la acción, el rol, la etapa y la fecha

### Requirement: Persistencia del flujo mock entre roles y recargas

El sistema SHALL persistir el estado de los pedidos en un store que sobreviva a recargas de página y a cambios de rol dentro de la sesión (store singleton hidratado desde `localStorage`). Toda lectura y escritura de pedidos MUST pasar por una capa `api/` asíncrona que actúe como único punto de reemplazo por el backend real.

#### Scenario: El estado persiste tras recargar

- **GIVEN** un pedido creado y enviado a revisión
- **WHEN** se recarga la página
- **THEN** el pedido conserva su estado e historial

#### Scenario: El estado es coherente al cambiar de rol

- **GIVEN** un pedido enviado por el Jefe de Cátedra
- **WHEN** el usuario cambia al rol revisor de la etapa correspondiente
- **THEN** el pedido es visible en el estado en que quedó, sin perder su historial
