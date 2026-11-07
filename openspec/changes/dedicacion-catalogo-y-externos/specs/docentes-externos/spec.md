# Spec Delta

## Purpose

Permite registrar y tramitar las designaciones de docentes externos: quienes dictan en Ingeniería pero cobran por otro departamento o área de la universidad, y por eso no tienen cargo ni dedicación propios del Departamento.

## ADDED Requirements

### Requirement: Catálogo de sectores externos

El sistema SHALL mantener un catálogo persistido de sectores externos con identificador canónico, código único, nombre y estado activo, inicializado con ACA, DPU, ECO, ARQ, SEU, DCP, CSA, CYT y HUM. Las nuevas selecciones MUST referir a un sector existente y activo; un sector inactivo SHALL seguir resolviéndose en lo ya registrado.

#### Scenario: Catálogo inicial

- **WHEN** se consulta el catálogo de Designaciones en un ambiente recién migrado
- **THEN** incluye los nueve sectores con su código y nombre, entre ellos `ECO` "Departamento de Ciencias Económicas" y `DPU` "Dirección de Pedagogía Universitaria"

#### Scenario: Sector inactivo

- **GIVEN** un sector desactivado con designaciones que lo refieren
- **WHEN** se consulta una de esas designaciones o se intenta una nueva selección del sector
- **THEN** la designación muestra el sector y la nueva selección MUST rechazarse

### Requirement: Coherencia entre designación interna y externa

Toda designación y todo Alta o Cambio de cargo o dedicación SHALL ser interno, con cargo y dedicación del catálogo y sin sector externo, o externo, con sector externo y sin cargo ni dedicación. La base de datos MUST rechazar cualquier combinación mixta o incompleta, aunque provenga de un cliente o de una carga directa.

#### Scenario: Designación externa válida

- **WHEN** se registra una designación con sector `ECO`, sin cargo ni dedicación y con horas positivas
- **THEN** la operación se acepta y la designación queda vigente

#### Scenario: Combinación mixta rechazada

- **WHEN** se intenta registrar una designación o un pedido con sector externo y además cargo o dedicación
- **THEN** la base de datos MUST rechazar la operación

#### Scenario: Designación interna incompleta rechazada

- **WHEN** se intenta registrar una designación sin sector externo y sin cargo o sin dedicación
- **THEN** la base de datos MUST rechazar la operación

### Requirement: Selección de docente externo en Alta y Cambio

El formulario de Alta y de Cambio de cargo o dedicación SHALL ofrecer la opción "Docente externo". Al activarla, cargo y dedicación MUST deshabilitarse y vaciarse, y SHALL aparecer un selector obligatorio de sector externo con los sectores activos. Las horas SHALL seguir siendo obligatorias. Al desactivarla, el sector se descarta y vuelven a exigirse cargo y dedicación.

#### Scenario: Alta de un docente externo

- **GIVEN** un Jefe de Cátedra cargando un Alta
- **WHEN** activa "Docente externo", elige el sector `DPU` y completa las horas y la documentación
- **THEN** puede guardar y enviar el pedido sin cargo ni dedicación

#### Scenario: Externo sin sector

- **WHEN** se intenta enviar un Alta o Cambio externo sin sector
- **THEN** el formulario y la API MUST rechazarlo con error en el campo de sector, sin avanzar el pedido

#### Scenario: Cliente que envía cargo con sector

- **WHEN** un cliente envía un pedido externo que además trae cargo o dedicación
- **THEN** la API MUST rechazarlo sin persistir cambios

#### Scenario: Volver a docente interno

- **GIVEN** un borrador externo con sector elegido
- **WHEN** se desactiva "Docente externo"
- **THEN** el sector se descarta y el pedido exige cargo y dedicación para enviarse

### Requirement: Materialización de un pedido externo

Al aprobarse un Alta o Cambio externo, la designación resultante SHALL quedar externa, con el sector solicitado y sin cargo ni dedicación. Un Cambio SHALL poder convertir una designación interna en externa y viceversa.

#### Scenario: Cambio de interno a externo

- **GIVEN** un docente con designación interna JTP Categoría 3 en una materia
- **WHEN** Decanato aprueba un Cambio externo con sector `ECO` sobre esa materia
- **THEN** la designación vigente queda con sector `ECO`, sin cargo ni dedicación, y el snapshot del pedido conserva JTP Categoría 3 como estado anterior

### Requirement: Visualización de designaciones externas

Donde el sistema muestre cargo y dedicación de una designación o de un pedido (datos actuales del docente, snapshot, resumen, detalle y tablero de revisión), una designación externa SHALL mostrarse como externa con el nombre y el código de su sector, en lugar de cargo y dedicación vacíos.

#### Scenario: Datos actuales de un externo

- **GIVEN** un docente con designación vigente externa en `CYT`
- **WHEN** un Jefe de Cátedra abre un Cambio para ese docente
- **THEN** los datos actuales muestran que es externo, del sector "Secretaría de Ciencia y Tecnología"

### Requirement: Designaciones externas en la administración de personas

La administración de personas SHALL mostrar una designación externa con su sector, en lugar de cargo y dedicación, sólo a modo informativo. Una designación pasa de interna a externa, o al revés, únicamente mediante un pedido de Cambio aprobado.

#### Scenario: Ver una designación externa

- **GIVEN** una persona con una designación externa en `ACA`
- **WHEN** un administrador abre su ficha
- **THEN** la designación figura como externa de "Secretaría Académica (ACA)", sin controles para editarla

### Requirement: Externos en el lote departamental

En la exportación del lote, una designación o pedido externo SHALL mostrar en la celda de cargo/dedicación únicamente la sigla del sector, tanto en la designación anterior como en la propuesta y en las hojas ALTAS y BAJAS. En `PROPUESTA COMPLETA`, la columna Departamento SHALL mostrar el nombre completo del sector de una designación vigente o un Alta externa, y "Departamento de Ingeniería e Investigaciones Tecnológicas" para una interna.

#### Scenario: Continuidad de un externo

- **GIVEN** un docente con designación vigente externa en `ECO` sin pedido del período
- **WHEN** se exporta el lote
- **THEN** en `PROPUESTA COMPLETA` las celdas de cargo/dedicación anterior y propuesta muestran `ECO` y la columna Departamento muestra "Departamento de Ciencias Económicas"

#### Scenario: Alta externa aprobada

- **GIVEN** un Alta externa en `SEU` en estado `en_lote`
- **WHEN** se exporta el lote
- **THEN** la fila de `ALTAS` muestra `SEU` como cargo/dedicación
