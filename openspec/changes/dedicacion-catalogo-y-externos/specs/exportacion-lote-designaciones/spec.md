# Spec Delta

## MODIFIED Requirements

### Requirement: Libro con pedidos finalizados y designaciones resultantes

El XLSX SHALL contener exactamente tres hojas basadas en el modelo institucional:
`PROPUESTA COMPLETA`, `ALTAS` y `BAJAS`. La primera hoja SHALL representar las
asignaciones vigentes, bajas aprobadas y Altas aprobadas aún no materializadas
del período en un registro por docente: como el cargo y la dedicación son
únicos por docente, sus materias SHALL ir en la misma fila, con los códigos
unidos por guion y las horas sumadas. Si un docente tiene combinaciones
distintas de cargo/dedicación (por ejemplo, un cambio o una baja en una sola
materia), cada combinación SHALL ocupar su propia fila. Las columnas del modelo son: número de fila,
apellido y nombre, CUIL, cargo/dedicación de la designación anterior,
cargo/dedicación propuestos para el período activo, observación, horas
semanales, asignatura, departamento, cargo/dedicación departamental y horas
destinadas al ingreso. La designación anterior SHALL provenir del snapshot del
pedido cuando se trate de un cambio o una baja; para una continuidad o una
designación administrativa vigente se SHALL conservar el valor vigente; para un
alta sin designación previa SHALL quedar vacía. La propuesta SHALL mostrar el
estado vigente resultante y las bajas aprobadas SHALL conservar el valor
anterior sin reabrir la designación. En toda celda de cargo/dedicación, una
designación o pedido externo SHALL mostrar únicamente la sigla de su sector
externo, y la columna departamento SHALL mostrar el nombre completo de ese
sector en una designación vigente o un Alta externa; para una interna, "Departamento
de Ingeniería e Investigaciones Tecnológicas".

`ALTAS` y `BAJAS` SHALL agrupar igual: un registro por docente y
cargo/dedicación. La hoja `ALTAS` SHALL contener exclusivamente pedidos `en_lote` del período
activo cuya novedad sea Alta, con número de fila, apellido y nombre, CUIL,
cargo/dedicación del período activo, fecha de nacimiento, correo y teléfono.
La hoja `BAJAS` SHALL contener exclusivamente pedidos `en_lote` del período
activo cuya novedad sea Baja, con número de fila, apellido y nombre, CUIL,
cargo/dedicación anterior y motivo. El correo SHALL consultarse desde el perfil
del Portal; los campos del modelo que no tengan una fuente persistida
equivalente SHALL quedar vacíos. Los valores faltantes o históricos SHALL
conservarse sin sustituciones ficticias.

#### Scenario: Pedidos de distintos estados y períodos

- **GIVEN** pedidos Alta, Baja y Cambio en_lote del período activo, pedidos de otro período y pedidos rechazados, cancelados o pendientes
- **WHEN** se exporta
- **THEN** `ALTAS` y `BAJAS` MUST contener sólo las novedades correspondientes del período activo, y `PROPUESTA COMPLETA` MUST contener sólo las asignaciones vigentes, altas aprobadas y bajas aprobadas que correspondan al lote activo

#### Scenario: Continuidad sin novedad

- **GIVEN** un docente vigente sin pedido aprobado del período
- **WHEN** se exporta
- **THEN** `PROPUESTA COMPLETA` MUST conservar sus valores vigentes como designación anterior y propuesta activa, sin crear un pedido ni duplicar la fila de persona y materia

#### Scenario: Docente con varias materias

- **GIVEN** un docente JTP Categoría 3 con designaciones vigentes en cuatro materias de 2 horas cada una
- **WHEN** se exporta
- **THEN** `PROPUESTA COMPLETA` MUST tener un solo registro para ese docente, con los cuatro códigos de materia unidos por guion y 8 horas

#### Scenario: Cambio sobre una materia de varias

- **GIVEN** un docente con dos materias vigentes y un Cambio aprobado sobre una de ellas
- **WHEN** se exporta
- **THEN** MUST aparecer una fila para la materia del cambio, con el snapshot anterior y el cambio materializado, y otra para la materia sin alteraciones, porque sus combinaciones de cargo/dedicación difieren

#### Scenario: Alta aprobada

- **GIVEN** un pedido Alta del período activo en estado `en_lote`
- **WHEN** se exporta
- **THEN** MUST aparecer en `PROPUESTA COMPLETA` con cargo/dedicación anterior vacío y propuesta activa informada, y MUST aparecer una vez en `ALTAS` con fecha de nacimiento, correo y teléfono disponibles

#### Scenario: Baja aprobada

- **GIVEN** un pedido Baja del período activo en estado `en_lote`
- **WHEN** se exporta
- **THEN** MUST aparecer en `PROPUESTA COMPLETA` y una vez en `BAJAS`, con cargo/dedicación anterior y motivo, y MUST NOT existir una designación vigente para reabrir

#### Scenario: Cambio de interno a externo

- **GIVEN** un Cambio aprobado que pasa una designación de JTP Categoría 3 al sector externo `ECO`
- **WHEN** se exporta
- **THEN** la fila de `PROPUESTA COMPLETA` MUST mostrar JTP / Categoría 3 como cargo/dedicación anterior y `ECO` como propuesta

#### Scenario: Período sin pedidos aprobados

- **GIVEN** un período activo sin pedidos `en_lote` y con docentes vigentes
- **WHEN** se exporta
- **THEN** `PROPUESTA COMPLETA` MUST generar las continuidades y `ALTAS` y `BAJAS` MUST conservar sus encabezados sin filas de datos

#### Scenario: Datos sin fuente equivalente

- **GIVEN** una persona no tiene CUIL, correo o teléfono, o una fila no tiene datos persistidos para departamento o dedicación externa
- **WHEN** se abre la planilla
- **THEN** las celdas correspondientes MUST quedar vacías y MUST NOT reemplazarse por DNI, texto inventado ni números ficticios
