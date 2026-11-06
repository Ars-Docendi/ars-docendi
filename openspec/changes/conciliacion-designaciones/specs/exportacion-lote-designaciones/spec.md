# Spec Delta

## MODIFIED Requirements

### Requirement: Libro con pedidos finalizados y designaciones resultantes

El XLSX SHALL contener exactamente tres hojas basadas en el modelo institucional:
`PROPUESTA COMPLETA`, `ALTAS` y `BAJAS`. La primera hoja SHALL representar una
fila por asignación vigente, baja del lote o Alta del lote aún no materializada
del período, sin duplicar una misma
combinación de persona y materia, con las columnas del modelo: número de fila,
apellido y nombre, CUIL, cargo/dedicación de la designación anterior,
cargo/dedicación propuestos para el período activo, observación, horas
semanales, asignatura, departamento, cargo/dedicación departamental y horas
destinadas al ingreso. La designación anterior SHALL provenir del snapshot del
pedido cuando se trate de un cambio o una baja; para una continuidad o una
designación administrativa vigente se SHALL conservar el valor vigente; para un
alta sin designación previa SHALL quedar vacía. La propuesta SHALL mostrar el
estado resultante: para un Cambio todavía `en_lote`, los valores solicitados en
el pedido; para uno `aprobado`, los de la designación sincronizada. Las bajas del
lote SHALL conservar el valor anterior sin reabrir la designación.

Los pedidos del lote son los `en_lote` y `aprobado` del período activo; los
`no_aprobado` MUST NOT exportarse. La hoja `ALTAS` SHALL contener exclusivamente
pedidos del lote cuya novedad sea Alta, con número de fila, apellido y nombre, CUIL,
cargo/dedicación del período activo, fecha de nacimiento, correo y teléfono.
La hoja `BAJAS` SHALL contener exclusivamente pedidos del lote cuya novedad sea
Baja, con número de fila, apellido y nombre, CUIL,
cargo/dedicación anterior y motivo. El correo SHALL consultarse desde el perfil
del Portal; los campos del modelo que no tengan una fuente persistida
equivalente SHALL quedar vacíos. Los valores faltantes o históricos SHALL
conservarse sin sustituciones ficticias.

#### Scenario: Pedidos de distintos estados y períodos

- **GIVEN** pedidos Alta, Baja y Cambio `en_lote` y `aprobado` del período activo, pedidos de otro período y pedidos rechazados, rechazados en la conciliación, cancelados o pendientes
- **WHEN** se exporta
- **THEN** `ALTAS` y `BAJAS` MUST contener sólo las novedades del lote del período activo, y `PROPUESTA COMPLETA` MUST contener sólo las asignaciones vigentes, altas y bajas del lote activo
- **AND** ningún pedido `no_aprobado` MUST aparecer en ninguna hoja

#### Scenario: Continuidad sin novedad

- **GIVEN** un docente vigente sin pedido del lote en el período
- **WHEN** se exporta
- **THEN** `PROPUESTA COMPLETA` MUST conservar sus valores vigentes como designación anterior y propuesta activa, sin crear un pedido ni duplicar la fila de persona y materia

#### Scenario: Cambio sobre una materia de varias

- **GIVEN** un docente con dos materias vigentes y un Cambio del lote sobre una de ellas
- **WHEN** se exporta
- **THEN** MUST aparecer una fila por materia en `PROPUESTA COMPLETA`, con el snapshot anterior y el cambio propuesto en la materia afectada, y la otra materia sin alteraciones

#### Scenario: Cambio todavía no sincronizado

- **GIVEN** un Cambio `en_lote` cuya designación vigente conserva los valores anteriores
- **WHEN** se exporta
- **THEN** `PROPUESTA COMPLETA` MUST mostrar como propuesta el cargo, la dedicación y las horas solicitadas en el pedido, y como anterior los del snapshot

#### Scenario: Alta aprobada

- **GIVEN** un pedido Alta del período activo en estado `en_lote` o `aprobado`
- **WHEN** se exporta
- **THEN** MUST aparecer en `PROPUESTA COMPLETA` con cargo/dedicación anterior vacío y propuesta activa informada, y MUST aparecer una vez en `ALTAS` con fecha de nacimiento, correo y teléfono disponibles

#### Scenario: Baja aprobada

- **GIVEN** un pedido Baja del período activo en estado `en_lote` o `aprobado`
- **WHEN** se exporta
- **THEN** MUST aparecer una sola vez en `PROPUESTA COMPLETA` como baja y una vez en `BAJAS`, con cargo/dedicación anterior y motivo, sin repetirse como continuidad aunque la designación siga vigente hasta sincronizar

#### Scenario: Período sin pedidos aprobados

- **GIVEN** un período activo sin pedidos del lote y con docentes vigentes
- **WHEN** se exporta
- **THEN** `PROPUESTA COMPLETA` MUST generar las continuidades y `ALTAS` y `BAJAS` MUST conservar sus encabezados sin filas de datos

#### Scenario: Datos sin fuente equivalente

- **GIVEN** una persona no tiene CUIL, correo o teléfono, o una fila no tiene datos persistidos para departamento o dedicación externa
- **WHEN** se abre la planilla
- **THEN** las celdas correspondientes MUST quedar vacías y MUST NOT reemplazarse por DNI, texto inventado ni números ficticios
