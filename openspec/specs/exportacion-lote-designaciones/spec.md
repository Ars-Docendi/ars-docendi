# exportacion-lote-designaciones Specification

## Purpose

Permitir descargar el lote docente del período configurado, reuniendo los pedidos aprobados y las designaciones que continúan vigentes sin necesidad de generar pedidos artificiales.

## Requirements

### Requirement: Descarga autorizada desde Finalizados

En Revisión, la pestaña Finalizados SHALL ofrecer un botón Exportar en el extremo derecho de la fila de pestañas a Decanato, Secretaría Académica y Administrativo. La API MUST comprobar esos roles y su ámbito con la identidad persistida. La descarga MUST NOT modificar pedidos, designaciones, estados ni permisos de aprobación.

#### Scenario: Rol habilitado descarga

- **GIVEN** cualquiera de los tres roles habilitados en Finalizados
- **WHEN** pulsa Exportar
- **THEN** MUST descargar un archivo XLSX real, mostrando progreso y evitando solicitudes simultáneas desde el botón

#### Scenario: Rol no habilitado llama directamente a la API

- **GIVEN** un Jefe, Coordinador o Docente sin ninguno de los roles habilitados
- **WHEN** intenta descargar el lote por API
- **THEN** MUST recibir denegación sin datos del lote y MUST NOT ver el botón en UI

#### Scenario: Fallo de descarga

- **GIVEN** un error de servidor o red
- **WHEN** se intenta exportar
- **THEN** la UI MUST informar el error y permitir reintentar sin presentar un archivo fallido como exitoso

### Requirement: Período asignado desde Períodos de Designación

El lote SHALL usar el período activo configurado desde Períodos de Designación,
identificándolo en la UI y en el archivo junto con su rango de impacto y fecha de
generación. La selección y los filtros de la tabla MUST NOT recortar su
contenido. Backend MUST verificar la vigencia de esa selección al descargar.
Los encabezados de cargo y dedicación SHALL usar el nombre del período activo,
excepto la columna D de la primera hoja, que SHALL usar el nombre del período
inmediatamente anterior según `ImpactoDesde`. Si no existe un período anterior,
esa referencia SHALL quedar vacía y no SHALL conservar el texto estático del
modelo. Sin período activo MUST impedirse la descarga con explicación visible.

#### Scenario: Tabla filtrada

- **GIVEN** un período activo y una tabla filtrada por docente, estado o por otro período
- **WHEN** se exporta el lote cuyo período configurado se muestra junto al botón
- **THEN** MUST incluir todos los datos correspondientes al período configurado, aunque estén ocultos por filtros

#### Scenario: Encabezados parametrizados

- **GIVEN** un período activo y un período anterior configurados en el sistema
- **WHEN** se genera la planilla institucional
- **THEN** la primera hoja MUST mostrar el período anterior en la columna D, el activo en la columna E y las hojas `ALTAS` y `BAJAS` MUST mostrar el período activo en sus columnas de cargo y dedicación

#### Scenario: No hay período anterior

- **GIVEN** el período activo es el primero configurado por fecha de impacto
- **WHEN** se genera la planilla institucional
- **THEN** la columna D de la primera hoja MUST conservar su encabezado base sin mostrar un período inexistente ni un período fijo del archivo modelo

#### Scenario: Cambia el período durante la consulta

- **GIVEN** una pantalla que conserva el identificador de un período que dejó de estar activo
- **WHEN** solicita la descarga
- **THEN** la API MUST responder conflicto y la UI MUST actualizar la información del período antes de reintentar

#### Scenario: Falta período activo

- **GIVEN** ningún período activo
- **WHEN** se abre Finalizados
- **THEN** Exportar MUST estar deshabilitado con una explicación y la API MUST rechazar intentos directos

### Requirement: Libro con pedidos finalizados y designaciones resultantes

El XLSX SHALL contener exactamente tres hojas basadas en el modelo institucional:
`PROPUESTA COMPLETA`, `ALTAS` y `BAJAS`. La primera hoja SHALL representar una
fila por asignación vigente, baja aprobada o Alta aprobada aún no materializada
del período, sin duplicar una misma
combinación de persona y materia, con las columnas del modelo: número de fila,
apellido y nombre, CUIL, cargo/dedicación de la designación anterior,
cargo/dedicación propuestos para el período activo, observación, horas
semanales, asignatura, departamento, cargo/dedicación departamental y horas
destinadas al ingreso. La designación anterior SHALL provenir del snapshot del
pedido cuando se trate de un cambio o una baja; para una continuidad o una
designación administrativa vigente se SHALL conservar el valor vigente; para un
alta sin designación previa SHALL quedar vacía. La propuesta SHALL mostrar el
estado vigente resultante y las bajas aprobadas SHALL conservar el valor
anterior sin reabrir la designación.

La hoja `ALTAS` SHALL contener exclusivamente pedidos `en_lote` del período
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

#### Scenario: Cambio sobre una materia de varias

- **GIVEN** un docente con dos materias vigentes y un Cambio aprobado sobre una de ellas
- **WHEN** se exporta
- **THEN** MUST aparecer una fila por materia en `PROPUESTA COMPLETA`, con el snapshot anterior y el cambio materializado en la materia afectada, y la otra materia sin alteraciones

#### Scenario: Alta aprobada

- **GIVEN** un pedido Alta del período activo en estado `en_lote`
- **WHEN** se exporta
- **THEN** MUST aparecer en `PROPUESTA COMPLETA` con cargo/dedicación anterior vacío y propuesta activa informada, y MUST aparecer una vez en `ALTAS` con fecha de nacimiento, correo y teléfono disponibles

#### Scenario: Baja aprobada

- **GIVEN** un pedido Baja del período activo en estado `en_lote`
- **WHEN** se exporta
- **THEN** MUST aparecer en `PROPUESTA COMPLETA` y una vez en `BAJAS`, con cargo/dedicación anterior y motivo, y MUST NOT existir una designación vigente para reabrir

#### Scenario: Período sin pedidos aprobados

- **GIVEN** un período activo sin pedidos `en_lote` y con docentes vigentes
- **WHEN** se exporta
- **THEN** `PROPUESTA COMPLETA` MUST generar las continuidades y `ALTAS` y `BAJAS` MUST conservar sus encabezados sin filas de datos

#### Scenario: Datos sin fuente equivalente

- **GIVEN** una persona no tiene CUIL, correo o teléfono, o una fila no tiene datos persistidos para departamento o dedicación externa
- **WHEN** se abre la planilla
- **THEN** las celdas correspondientes MUST quedar vacías y MUST NOT reemplazarse por DNI, texto inventado ni números ficticios

### Requirement: Integridad y seguridad del archivo

La descarga SHALL representar una lectura consistente y quedar acotada al ámbito autorizado. Sus números y fechas SHALL ser celdas tipadas; los datos textuales MUST permanecer texto incluso si empiezan con caracteres de fórmula. UUIDs internos, credenciales y datos de ámbitos ajenos MUST NOT exportarse. Repetir una descarga sin cambios de negocio MUST conservar las mismas filas y valores, salvo la fecha de generación.

#### Scenario: Aprobación concurrente

- **GIVEN** una aprobación concurrente con la descarga
- **WHEN** se generan ambas hojas
- **THEN** MUST mostrar un estado consistente, sin combinar el pedido anterior con su designación posterior

#### Scenario: Texto con apariencia de fórmula

- **GIVEN** un campo textual que comienza con = o @ y un legajo con ceros iniciales
- **WHEN** se abre el libro
- **THEN** MUST conservarse como texto literal, sin ejecución de fórmula ni pérdida de ceros
