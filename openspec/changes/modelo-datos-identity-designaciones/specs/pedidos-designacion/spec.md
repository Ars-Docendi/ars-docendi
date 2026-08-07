## ADDED Requirements

### Requirement: Materia y horas del pedido

El pedido de designación SHALL corresponder a exactamente una materia —la cátedra sobre la que el Jefe de Cátedra opera— con su carga horaria. La materia MUST NOT ser editable en ninguna novedad: proviene del ámbito del actor, no del formulario, porque determina a qué Coordinador se rutea el pedido. La carga horaria SHALL ser editable en "Alta" y en "Cambio de cargo o dedicación", y de solo lectura en "Baja" y "Sin novedad", donde es contexto de qué queda vacante o qué sigue vigente. El formulario MUST NOT ofrecer agregar ni quitar materias.

#### Scenario: Alta captura la materia de la cátedra y sus horas

- **GIVEN** un Jefe de Cátedra cargando un pedido de novedad "Alta"
- **WHEN** completa la carga horaria de la materia
- **THEN** el pedido guarda la materia de su cátedra junto con esas horas, sin haberle ofrecido elegir la materia

#### Scenario: Cambio permite editar las horas, no la materia

- **GIVEN** un pedido de novedad "Cambio de cargo o dedicación"
- **WHEN** el Jefe de Cátedra visualiza el formulario
- **THEN** ve la carga horaria en un campo editable y la materia fijada a la cátedra del pedido, sin control para cambiarla

#### Scenario: Cambio precarga las horas vigentes de esa cátedra

- **GIVEN** un docente con designaciones vigentes en más de una materia
- **WHEN** el Jefe de Cátedra lo selecciona en un pedido de "Cambio de cargo o dedicación"
- **THEN** el formulario precarga únicamente las horas de la cátedra del pedido, sin considerar las de sus otras materias

#### Scenario: Baja y Sin novedad muestran materia y horas de solo lectura

- **GIVEN** un pedido de novedad "Baja" o "Sin novedad"
- **WHEN** el Jefe de Cátedra visualiza el formulario
- **THEN** ve la materia y las horas vigentes sin ningún control editable

#### Scenario: El formulario no ofrece agregar ni quitar materias

- **WHEN** el Jefe de Cátedra visualiza el formulario de pedido en cualquier novedad
- **THEN** MUST no existir acción de "Agregar materia" ni control para quitar una fila de materia

#### Scenario: Un docente en dos cátedras requiere un pedido por cada una

- **GIVEN** un docente que dicta en dos materias con Jefes de Cátedra distintos
- **WHEN** se necesita tramitar novedades sobre ambas
- **THEN** cada cátedra MUST cargar su propio pedido, sujeto a la restricción de BR-designaciones-001

## MODIFIED Requirements

### Requirement: Creación de pedido de designación

El sistema SHALL permitir al Jefe de Cátedra crear un pedido de designación desde
`/designaciones/pedidos/nuevo`, capturando los datos comunes del pedido: docente (DNI, nombre),
antigüedad, cargo y dedicación actual (read-only), la materia de su cátedra con su carga horaria,
horas de investigación, horas externas (otro departamento) y novedad. El pedido se crea en
estado `borrador`.

#### Scenario: Alta de un pedido en borrador

- **GIVEN** un Jefe de Cátedra en el form de nuevo pedido
- **WHEN** completa los datos comunes, la carga horaria de la materia y una novedad válida
  con sus campos requeridos
- **THEN** el pedido se persiste en estado `borrador` y aparece en "Mis pedidos"

#### Scenario: Un pedido por docente por período [BR-designaciones-001]

- **GIVEN** un docente que ya tiene un pedido en el período abierto
- **WHEN** el Jefe de Cátedra intenta crear un segundo pedido para ese mismo docente en el mismo
  período
- **THEN** el sistema MUST rechazar la creación e indicar que ya existe un pedido para ese docente en
  el período, **sin importar si el segundo pedido corresponde a otra cátedra**

#### Scenario: El bloqueo por duplicado no expone datos de una cátedra ajena [BR-designaciones-001]

- **GIVEN** un docente con un pedido no terminal cargado por el Jefe de Cátedra de otra materia
- **WHEN** el Jefe de Cátedra de la cátedra actual intenta crear un pedido para ese docente
- **THEN** el sistema MUST rechazar la creación informando que ya existe un trámite en curso para ese
  docente en el período, **sin revelar la cátedra, el contenido ni el autor del pedido bloqueante**

### Requirement: Resumen de cambios en el panel de datos actuales (Cambio)

En "Cambio de cargo o dedicación", el panel de solo lectura de datos actuales SHALL mostrar, además de
antigüedad, la transición `actual → solicitado` de **todos** los campos que Cambio puede modificar:
cargo, dedicación, la carga horaria de la materia, y horas de investigación/externas. Un campo sin
cambios SHALL mostrarse con su valor plano (sin flecha de transición). En "Baja" y "Sin novedad" el
panel NO MUST mostrar transiciones (no hay valores "solicitados" que comparar).

#### Scenario: El panel muestra la transición de cargo y dedicación

- **GIVEN** un pedido de novedad "Cambio de cargo o dedicación" con cargo actual "Adjunto" → cargo
  solicitado "Titular", y dedicación actual "Categoría 3" → dedicación solicitada "Categoría 1"
- **WHEN** el Jefe de Cátedra visualiza el panel de datos actuales
- **THEN** ve "Adjunto → Titular" en la fila de cargo y "Categoría 3 → Categoría 1" en la fila de
  dedicación

#### Scenario: El panel muestra la transición de la carga horaria de la materia

- **GIVEN** un pedido de Cambio sobre una cátedra donde el docente tenía 6h y el Jefe de Cátedra
  cargó 8h
- **WHEN** visualiza el panel de datos actuales
- **THEN** ve la fila de la materia con "6h → 8h"

#### Scenario: Una carga horaria sin cambios se muestra sin transición

- **GIVEN** un pedido de Cambio donde el Jefe de Cátedra no modificó la carga horaria
- **WHEN** visualiza el panel de datos actuales
- **THEN** ve la carga horaria con su valor plano, sin flecha de transición

#### Scenario: El panel muestra la transición de horas de investigación y externas

- **GIVEN** un pedido de Cambio donde el docente tenía 2h de investigación y 0h externas, y el Jefe de
  Cátedra cargó 4h de investigación y dejó las externas sin cambios
- **WHEN** visualiza el panel de datos actuales
- **THEN** ve "Investigación: 2h → 4h" y "Externas: 0h" (sin flecha, no cambió)

## REMOVED Requirements

### Requirement: Materias y horas del pedido

**Reason**: El pedido pasa a cubrir exactamente una materia (design D3). El modelo de 1..N asignaciones
generaba una ambigüedad de ruteo irresoluble: un pedido con materias de dos carreras distintas dejaba a
dos Coordinadores compitiendo por él, sin que BR-designaciones-009 tuviera forma de decidir cuál era el
competente. No era un caso hipotético — el catálogo incluye materias comunes (Análisis Matemático,
Álgebra) que se dictan en varias carreras. Con una sola materia, la carrera se deriva de
`identity.materias.carrera_id` y resuelve un único Coordinador.

**Migration**: Reemplazado por el requirement "Materia y horas del pedido" de este mismo change, que
define el comportamiento del formulario con una materia única por novedad. En el modelo de datos,
`PedidoDesignacion.asignaciones: AsignacionMateria[]` se reemplaza por `materiaId` + `horas` como
campos del pedido; la tabla intermedia no se crea. Un docente que dicta en dos cátedras requiere un
pedido por cada una, sujeto a BR-designaciones-001. Los escenarios retirados con este requirement
("Alta con múltiples materias", "Cambio precarga el listado de materias del docente, editable",
"Cambio permite agregar, quitar y cambiar materias", "No se puede dejar un pedido sin materias",
"Cambio y Baja no repiten la materia como columna plana en la franja superior", "Baja muestra el
listado de materias y horas del docente, de solo lectura", "Sin novedad muestra la materia vigente sin
edición") quedan cubiertos, en su parte vigente, por los escenarios del nuevo requirement.
