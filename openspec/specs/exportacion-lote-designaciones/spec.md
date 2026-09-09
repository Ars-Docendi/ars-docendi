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

El lote SHALL usar el período activo configurado desde Períodos de Designación, identificándolo en la UI y en el archivo junto con su rango de impacto y fecha de generación. La selección y los filtros de la tabla MUST NOT recortar su contenido. Backend MUST verificar la vigencia de esa selección al descargar. Sin período activo MUST impedirse la descarga con explicación visible.

#### Scenario: Tabla filtrada

- **GIVEN** un período activo y una tabla filtrada por docente, estado o por otro período
- **WHEN** se exporta el lote cuyo período configurado se muestra junto al botón
- **THEN** MUST incluir todos los pedidos en_lote del período configurado y ámbito autorizado, aunque estén ocultos por filtros

#### Scenario: Cambia el período durante la consulta

- **GIVEN** una pantalla que conserva el identificador de un período que dejó de estar activo
- **WHEN** solicita la descarga
- **THEN** la API MUST responder conflicto y la UI MUST actualizar la información del período antes de reintentar

#### Scenario: Falta período activo

- **GIVEN** ningún período activo
- **WHEN** se abre Finalizados
- **THEN** Exportar MUST estar deshabilitado con una explicación y la API MUST rechazar intentos directos

### Requirement: Libro con pedidos finalizados y designaciones resultantes

El XLSX SHALL contener dos hojas. Pedidos finalizados SHALL incluir exclusivamente pedidos en_lote del período y ámbito: número, período, docente, legajo, carrera, materia, novedad, cargo y dedicación solicitados, las tres cargas horarias, solicitante, inicio y aprobación. Designaciones resultantes SHALL incluir una fila por persona y materia vigente del ámbito al generar el lote, con período destino, rango de impacto, docente, legajo, carrera, materia, cargo, dedicación, las tres horas y número de pedido del período cuando corresponda o indicación de continuidad. Las bajas aprobadas MUST figurar como pedidos y MUST NOT reabrirse como designaciones. Textos históricos y valores desconocidos MUST conservarse sin números ficticios.

#### Scenario: Pedidos de distintos estados y períodos

- **GIVEN** pedidos en_lote del período activo, de otro período y pedidos rechazados, cancelados o pendientes
- **WHEN** se exporta
- **THEN** la primera hoja MUST contener sólo los en_lote del período activo dentro del ámbito

#### Scenario: Continuidad sin novedad

- **GIVEN** un docente vigente sin pedido aprobado del período
- **WHEN** se exporta
- **THEN** la segunda hoja MUST conservar sus valores e indicar continuidad sin crear un pedido

#### Scenario: Cambio sobre una materia de varias

- **GIVEN** un docente con dos materias y un Cambio aprobado sobre una de ellas
- **WHEN** se exporta
- **THEN** MUST aparecer una fila por materia, con el cambio ya materializado y la otra sin alteraciones, sin duplicar la designación anterior

#### Scenario: Período sin pedidos aprobados

- **GIVEN** un período activo sin pedidos en_lote y con docentes vigentes
- **WHEN** se exporta
- **THEN** MUST generar la primera hoja con encabezados y la segunda con las continuidades

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
