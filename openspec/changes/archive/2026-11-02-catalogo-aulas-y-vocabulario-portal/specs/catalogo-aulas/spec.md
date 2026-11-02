## ADDED Requirements

### Requirement: Administrar el catálogo de aulas y laboratorios

Un actor con `aulas.gestionar` SHALL poder dar de alta, editar, activar, desactivar y eliminar aulas y laboratorios. Cada aula MUST tener nombre, tipo (`aula` o `laboratorio`) y capacidad entera mayor a cero, y MAY tener una descripción del equipamiento. El nombre MUST ser único sin distinguir mayúsculas ni espacios extremos. Un aula con reservas MUST NOT eliminarse: se desactiva. Un actor con `aulas.aprobar` SHALL poder listar el catálogo para asignar; sin `aulas.gestionar` ni `aulas.aprobar` la API MUST responder 403.

#### Scenario: Crear un aula

- **GIVEN** un Administrativo con `aulas.gestionar`
- **WHEN** crea "Laboratorio 3" de tipo laboratorio con capacidad 30
- **THEN** el aula queda activa en el catálogo

#### Scenario: Nombre repetido

- **GIVEN** un aula "Aula 204"
- **WHEN** se crea o renombra otra como " aula 204 "
- **THEN** la API responde 409 sin cambios

#### Scenario: Eliminar un aula con reservas

- **GIVEN** un aula asignada a una reserva
- **WHEN** se intenta eliminarla
- **THEN** la API responde 409 `aula-en-uso` y el aula se conserva

#### Scenario: Sin permiso

- **GIVEN** un Docente con `aulas.solicitar`
- **WHEN** llama a la API del catálogo
- **THEN** recibe 403 y no ve la pantalla en la navegación
