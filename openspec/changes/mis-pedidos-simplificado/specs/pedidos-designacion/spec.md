## MODIFIED Requirements

### Requirement: Lista "Mis pedidos" del Jefe de Cátedra

El sistema SHALL ofrecer al Jefe de Cátedra una pantalla "Mis pedidos" (`/designaciones/mis-pedidos`)
que liste los pedidos de designación de su cátedra para el período abierto, mostrando por cada pedido
el docente (sin prefijo de tratamiento), su **legajo** (o un placeholder si todavía no tiene, caso de
una Alta reciente), la materia asociada, el **tipo** (novedad), el estado (vía `StatusBadge`) y el flag
de prioritario. La pantalla MUST ofrecer un filtro con dos campos de texto siempre visibles —
**Docente** (angosto, mismo ancho que el campo equivalente de la pantalla Usuarios) y **N°** — más un
mecanismo para agregar como filtros opcionales **Legajo**, **Tipo** y **Estado** (mismo patrón que el
filtro de la pantalla de Usuarios, implementado mediante un componente de filtro genérico y reutilizable
por otras pantallas). Cada fila MUST navegar al detalle del pedido al hacer click
en cualquier parte de la fila. Al final de la fila, un botón **"Ver"** MUST estar siempre visible
(navega al mismo detalle) y un botón **"Editar"** MUST mostrarse únicamente cuando el pedido es
editable por el actor — ambos con el mismo formato que las acciones de fila de la pantalla Usuarios
(`Button variant="ghost" size="sm"`, sin ícono). Cuando el pedido está en `borrador`, la fila MUST
mostrar además un control "Eliminar" (X roja, ver "Eliminar un pedido en borrador"). La pantalla MUST
representar explícitamente los cuatro estados de carga: Loading, Empty, Error y Success.

#### Scenario: Lista con pedidos existentes

- **GIVEN** un Jefe de Cátedra autenticado con pedidos cargados en el período abierto
- **WHEN** abre `/designaciones/mis-pedidos`
- **THEN** ve la lista de sus pedidos con docente (sin prefijo), legajo, materia, tipo, `StatusBadge`
  por estado y el flag de prioritario cuando corresponde

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

#### Scenario: Click en la fila abre el detalle del pedido

- **GIVEN** cualquier pedido listado, en cualquier estado
- **WHEN** el Jefe de Cátedra hace click en la fila (fuera del botón "Editar")
- **THEN** el sistema navega a `/designaciones/pedidos/:id` de ese pedido

#### Scenario: El botón Editar solo aparece cuando el pedido es editable

- **GIVEN** un pedido en `borrador`, o `devuelto` con el Jefe de Cátedra como propietario actual
- **WHEN** se lista en "Mis pedidos"
- **THEN** su fila MUST mostrar el botón "Editar"
- **AND** un pedido en cualquier otro estado (en revisión, rechazado, cancelado, en lote) NO MUST
  mostrar ese botón

#### Scenario: El botón Ver está siempre disponible y navega al detalle

- **GIVEN** cualquier pedido listado, en cualquier estado
- **WHEN** el Jefe de Cátedra hace click en el botón "Ver" de su fila
- **THEN** el sistema navega a `/designaciones/pedidos/:id` de ese pedido, sin disparar dos veces la
  navegación (el click en "Ver" no burbujea al `onClick` de la fila)

#### Scenario: La X roja de eliminar solo aparece en pedidos en borrador

- **GIVEN** una fila con un pedido en `borrador` y otra con un pedido `devuelto`
- **WHEN** se lista en "Mis pedidos"
- **THEN** la fila del `borrador` MUST mostrar el control "Eliminar" (X roja)
- **AND** la fila del `devuelto` (y de cualquier otro estado) NO MUST mostrarlo

#### Scenario: El legajo se muestra en la tabla, o un placeholder si el docente no tiene uno todavía

- **GIVEN** un pedido de un docente con legajo asignado, y otro de una Alta reciente sin legajo
- **WHEN** se lista en "Mis pedidos"
- **THEN** la fila del primero MUST mostrar su legajo
- **AND** la fila del segundo MUST mostrar un placeholder ("—"), sin inventar un valor

#### Scenario: Filtrar por Docente o N° acota la lista

- **GIVEN** varios pedidos listados
- **WHEN** el Jefe de Cátedra tipea en el filtro Docente o en el filtro N°
- **THEN** solo quedan visibles los pedidos que coinciden (contiene, sin distinguir mayúsculas ni
  acentos)

#### Scenario: Agregar el filtro opcional Legajo, Tipo o Estado

- **GIVEN** el filtro colapsado (sin Legajo, Tipo ni Estado agregados)
- **WHEN** el Jefe de Cátedra elige "Legajo" (o "Tipo" o "Estado") en el selector "+ Añadir filtro"
- **THEN** aparece el campo correspondiente, y aplicarlo acota la lista a los pedidos que coinciden
  (Legajo: contiene, sin distinguir mayúsculas ni acentos, igual que Docente/N°)
- **AND** puede quitarlo con el botón "×", volviendo a ver todos los pedidos sujetos al resto de los
  filtros activos

### Requirement: Adjuntos y justificación obligatorios por novedad

El sistema SHALL validar los adjuntos y la justificación obligatorios según la novedad antes de
permitir guardar o enviar el pedido. Una novedad "Alta" MUST exigir CV + foto de DNI frente + foto de
DNI dorso [BR-designaciones-002]; "Baja" MUST exigir un adjunto justificativo [BR-designaciones-003];
"Cambio de cargo o dedicación" MUST exigir una justificación [BR-designaciones-004]. Además, "Baja" y
"Cambio de cargo o dedicación" MUST exigir que el docente referenciado tenga legajo asignado
[BR-designaciones-018] — ambas novedades operan sobre un docente ya existente en el sistema, que por
eso ya tiene un legajo; "Alta" no exige legajo (el docente todavía no existe, se lo asigna el
sistema/RRHH después). (En el prototipo los adjuntos son solo metadata mock.)

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

#### Scenario: Baja o Cambio exigen que el docente tenga legajo [BR-designaciones-018]

- **GIVEN** un pedido con novedad "Baja" o "Cambio de cargo o dedicación"
- **WHEN** el docente referenciado no tiene legajo asignado
- **THEN** la validación MUST bloquear el guardado e indicar que el legajo es obligatorio

#### Scenario: Alta no exige legajo [BR-designaciones-018]

- **GIVEN** un pedido con novedad "Alta"
- **WHEN** el docente (nuevo) no tiene legajo asignado
- **THEN** la validación NO MUST bloquear el guardado por esa razón

#### Scenario: La validación inline bloquea el submit inválido

- **WHEN** el usuario intenta guardar o enviar un pedido con campos requeridos faltantes
- **THEN** el form muestra el error inline en el campo afectado y no envía la acción al store

## ADDED Requirements

### Requirement: Enviar y reenviar desde el form de pedido

El sistema SHALL ofrecer, dentro del form de pedido (`/designaciones/pedidos/nuevo` o
`/designaciones/pedidos/:id/editar`), un botón adicional a "Guardar pedido" que guarda los datos y, en
el mismo paso, envía el pedido a revisión: **"Guardar y enviar"** cuando el resultado queda en
`borrador` (creación, o edición de un borrador existente), o **"Guardar y reenviar"** cuando se está
editando un pedido `devuelto`. **"Guardar pedido" MUST guardar siempre el estado actual del form, sin
exigir que los campos obligatorios estén completos** — el Jefe de Cátedra tiene que poder guardar un
borrador a medio completar y retomarlo después. La validación completa (adjuntos, justificación, tipo
de baja, legajo [BR-designaciones-018], etc.) MUST aplicarse únicamente al enviar/reenviar: "Guardar y
enviar" y "Guardar y reenviar" MUST bloquear la acción si hay errores, sin guardar ni enviar nada — a
diferencia de "Guardar pedido", que nunca se bloquea por esta causa. El sistema NO MUST ofrecer
ninguna acción para cancelar (pasar a `cancelado`) un pedido en esta pantalla ni en el form.

#### Scenario: Crear y enviar en un solo paso

- **GIVEN** un Jefe de Cátedra completando el form de un pedido nuevo, con todos los datos válidos
- **WHEN** hace click en "Guardar y enviar"
- **THEN** el sistema crea el pedido en `borrador` y de inmediato lo envía a revisión
  (`en_revision_coordinador`)

#### Scenario: Editar un borrador y enviarlo

- **GIVEN** un Jefe de Cátedra editando un pedido en `borrador`, con datos válidos
- **WHEN** hace click en "Guardar y enviar"
- **THEN** el sistema guarda los cambios y envía el pedido a revisión en el mismo paso

#### Scenario: Editar un devuelto y reenviarlo

- **GIVEN** un Jefe de Cátedra editando un pedido `devuelto` del que es propietario, con datos válidos
- **WHEN** hace click en "Guardar y reenviar"
- **THEN** el sistema guarda los cambios y reenvía el pedido, retomando la etapa que lo devolvió

#### Scenario: Guardar pedido siempre guarda, aunque falten campos obligatorios

- **GIVEN** el form con datos incompletos (p. ej. falta un adjunto obligatorio, o el tipo de baja)
- **WHEN** el Jefe de Cátedra hace click en "Guardar pedido"
- **THEN** el sistema MUST guardar el estado actual del form tal cual está, sin bloquear la acción ni
  exigir que los campos obligatorios estén completos

#### Scenario: Guardar y enviar bloquea si faltan campos obligatorios

- **GIVEN** el form con datos inválidos (p. ej. falta un adjunto obligatorio)
- **WHEN** el Jefe de Cátedra hace click en "Guardar y enviar" (o "Guardar y reenviar")
- **THEN** el sistema MUST bloquear la acción y mostrar los errores correspondientes, sin guardar ni
  enviar nada

### Requirement: Eliminar un pedido en borrador

El sistema SHALL permitir al Jefe de Cátedra propietario eliminar definitivamente un pedido propio
que esté en `borrador` — a diferencia de las transiciones de estado (aceptar, rechazar, devolver,
enviar, cancelar), eliminar no deja un evento en el historial: el pedido deja de existir. La acción
MUST estar disponible tanto desde la fila del pedido en "Mis pedidos" (control "Eliminar", X roja)
como desde el detalle del pedido (`/designaciones/pedidos/:id`), únicamente cuando el pedido está en
`borrador` y el actor es su Jefe de Cátedra propietario. El sistema MUST exigir una confirmación
explícita en un modal antes de eliminar. Un pedido `devuelto` — aunque también sea editable — NO MUST
ofrecer esta acción: ya tiene una revisión asociada en su historial.

#### Scenario: Eliminar un borrador propio, con confirmación

- **GIVEN** un Jefe de Cátedra viendo un pedido propio en `borrador` (en la lista o en el detalle)
- **WHEN** dispara "Eliminar" y confirma en el modal
- **THEN** el sistema borra el pedido definitivamente y ya no aparece en "Mis pedidos"

#### Scenario: Cancelar el modal no elimina nada

- **GIVEN** el modal de confirmación de "Eliminar" abierto sobre un borrador
- **WHEN** el Jefe de Cátedra elige "Cancelar" (o cierra el modal)
- **THEN** el sistema no ejecuta ninguna mutación y el pedido sigue listado

#### Scenario: Un pedido devuelto no ofrece la acción Eliminar

- **GIVEN** un pedido `devuelto` del que el actor es el Jefe de Cátedra propietario
- **WHEN** se lista en "Mis pedidos" o se abre su detalle
- **THEN** el sistema NO MUST ofrecer la acción "Eliminar" (solo "Editar")

#### Scenario: Eliminar desde el detalle vuelve a "Mis pedidos"

- **GIVEN** un Jefe de Cátedra viendo el detalle de su propio borrador
- **WHEN** elimina el pedido y confirma
- **THEN** el sistema navega de regreso a "Mis pedidos"
