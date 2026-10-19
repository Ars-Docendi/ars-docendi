## Purpose

Muestra dentro de Ars Docendi las comisiones importadas desde Guaraní para un período lectivo, sin consultar Guaraní en cada visita, junto con la información de cuándo y quién realizó la última importación.

## ADDED Requirements

### Requirement: Consulta autorizada por permiso

Ver la pantalla de Comisiones y consultar su API MUST requerir el permiso efectivo `comisiones.ver`, validado en backend. La sidebar SHALL mostrar el enlace "Comisiones" en el grupo de Designaciones sólo a quien tenga ese permiso. La consulta MUST leer únicamente el snapshot local y MUST NOT contactar a Guaraní.

#### Scenario: Actor con permiso

- **GIVEN** un actor con `comisiones.ver`
- **WHEN** navega a `/designaciones/comisiones`
- **THEN** ve la grilla de comisiones del período seleccionado

#### Scenario: Actor sin permiso

- **GIVEN** un actor sin `comisiones.ver`
- **WHEN** intenta abrir la pantalla o llamar a la API de comisiones
- **THEN** no ve el enlace en la sidebar, la ruta no se muestra y la API responde 403

#### Scenario: Guaraní no disponible

- **GIVEN** comisiones importadas y Guaraní fuera de servicio
- **WHEN** un actor con `comisiones.ver` abre la pantalla
- **THEN** ve las comisiones importadas normalmente

### Requirement: Selección del período lectivo a consultar

La pantalla SHALL ofrecer un selector con los períodos lectivos que tienen comisiones importadas, ordenados del más reciente al más antiguo, y MUST seleccionar por defecto el más reciente. Si no hay ningún período importado, SHALL mostrar un estado vacío que lo explique; a quien tenga `comisiones.importar` SHALL indicarle que puede usar "Actualizar desde Guaraní".

#### Scenario: Varios períodos importados

- **GIVEN** comisiones importadas para "2026 – 1.º Cuatrimestre" y "2026 – 2.º Cuatrimestre"
- **WHEN** el actor abre la pantalla
- **THEN** el selector muestra ambos períodos y "2026 – 2.º Cuatrimestre" está seleccionado

#### Scenario: Sin importaciones

- **GIVEN** que nunca se importaron comisiones
- **WHEN** el actor abre la pantalla
- **THEN** ve un estado vacío que indica que todavía no hay comisiones importadas

### Requirement: Columnas de la grilla de comisiones

La grilla SHALL mostrar, por comisión: Comisión, Materia (nombre y código), Carreras (nombre y código de cada una), Aula, Bandas horarias, Docentes, Modalidad y Ubicación. La columna Aula SHALL mostrar las aulas distintas de las bandas de la comisión. Las bandas horarias SHALL mostrarse como día, rango horario y aula; los docentes, como nombre con su responsabilidad; la modalidad, por su descripción. Los campos vacíos SHALL mostrarse con un guion. La grilla es de solo lectura: MUST NOT ofrecer acciones de edición ni eliminación por fila.

#### Scenario: Comisión con varias bandas y docentes

- **GIVEN** una comisión con bandas "Lunes 08:00–12:00" y "Jueves 08:00–10:00" y dos docentes
- **WHEN** se renderiza su fila
- **THEN** la celda de bandas horarias muestra ambas bandas y la de docentes muestra ambos nombres con su responsabilidad

#### Scenario: Comisión sin aula

- **GIVEN** una comisión importada cuyas bandas no tienen aula
- **WHEN** se renderiza su fila
- **THEN** la celda Aula muestra "—"

### Requirement: Filtros de la grilla de comisiones

La grilla SHALL permitir filtrar dentro del período seleccionado por texto de materia (nombre o código), carrera (nombre o código), docente y modalidad.

#### Scenario: Filtrar por docente

- **GIVEN** 120 comisiones en el período y 3 de ellas con el docente "Pérez"
- **WHEN** el actor filtra por docente "pérez"
- **THEN** la grilla muestra esas 3 comisiones

### Requirement: Información de la última importación

La pantalla SHALL mostrar, para el período seleccionado, la fecha y hora de la última importación exitosa y el nombre de quien la realizó. Si el último intento del período falló, SHALL indicarlo junto con la fecha del intento, manteniendo visibles las comisiones de la última importación exitosa.

#### Scenario: Última importación exitosa

- **GIVEN** un período importado el 15/10/2026 a las 10:30 por "Secretaría Académica"
- **WHEN** el actor lo selecciona
- **THEN** la pantalla muestra "Actualizado el 15/10/2026 10:30 por Secretaría Académica"

#### Scenario: Último intento fallido

- **GIVEN** un período con una importación exitosa anterior y un intento posterior fallido
- **WHEN** el actor lo selecciona
- **THEN** ve las comisiones de la importación exitosa y un aviso de que el último intento de actualización falló
