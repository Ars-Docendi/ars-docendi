## MODIFIED Requirements

### Requirement: Asignar y actualizar el aula de una solicitud

Un Administrativo con `aulas.aprobar` SHALL poder asignar un aula del catálogo a una solicitud `Pendiente`; al asignar, el backend MUST transicionar la solicitud a estado `Aprobada` y persistir el aula asignada. Sobre una solicitud ya `Aprobada`, el Administrativo SHALL poder actualizar el aula asignada (sin volver a pasar por `Pendiente` ni afectar el estado). El backend MUST rechazar la operación sobre una solicitud `Cancelada` o `Rechazada`. Sólo se MUST asignar un aula existente y activa [BR-aulas-006]. Un aula MUST NOT tener dos reservas `Aprobada` cuyos horarios se superpongan el mismo día, incluso bajo asignaciones concurrentes; los horarios son intervalos semiabiertos [BR-aulas-005]. Si la cantidad aproximada de alumnos supera la capacidad del aula, la interfaz SHALL advertirlo y la asignación MUST permitirse igual [BR-aulas-007].

#### Scenario: Asignar aula a una solicitud pendiente

- **GIVEN** una solicitud `Pendiente` y el aula activa "Aula 204"
- **WHEN** un Administrativo le asigna "Aula 204"
- **THEN** la solicitud pasa a estado `Aprobada` con "Aula 204" como aula asignada, visible para el Docente en "Mis solicitudes"

#### Scenario: Actualizar el aula de una solicitud ya aprobada

- **GIVEN** una solicitud `Aprobada` con aula "Aula 204"
- **WHEN** un Administrativo le asigna el aula "Aula 305"
- **THEN** la solicitud permanece `Aprobada` y pasa a mostrar "Aula 305" como aula asignada

#### Scenario: No se puede asignar ni actualizar el aula de una solicitud cancelada

- **GIVEN** una solicitud `Cancelada`
- **WHEN** un Administrativo intenta asignarle o actualizarle un aula
- **THEN** la API rechaza la operación sin modificar el estado ni el aula

#### Scenario: No se puede asignar ni actualizar el aula de una solicitud rechazada

- **GIVEN** una solicitud `Rechazada`
- **WHEN** un Administrativo intenta asignarle o actualizarle un aula
- **THEN** la API rechaza la operación sin modificar el estado ni el aula

#### Scenario: Aula ocupada en ese horario [BR-aulas-005]

- **GIVEN** "Aula 204" aprobada para el 10/12 de 9:00 a 11:00
- **WHEN** se le asigna otra solicitud del 10/12 de 10:00 a 12:00
- **THEN** la API responde 409 `aula-ocupada` y la solicitud sigue `Pendiente`

#### Scenario: Horarios contiguos no se superponen [BR-aulas-005]

- **GIVEN** "Aula 204" aprobada para el 10/12 de 9:00 a 11:00
- **WHEN** se le asigna otra solicitud del 10/12 de 11:00 a 13:00
- **THEN** la asignación se aprueba

#### Scenario: Aula inactiva [BR-aulas-006]

- **GIVEN** un aula desactivada
- **WHEN** se intenta asignarla
- **THEN** la API responde validación sobre `aulaId` sin cambios

#### Scenario: Capacidad excedida [BR-aulas-007]

- **GIVEN** una solicitud de 50 alumnos y un aula de capacidad 30
- **WHEN** el Administrativo elige esa aula
- **THEN** ve una advertencia de capacidad y puede asignarla igual
