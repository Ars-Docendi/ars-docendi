## ADDED Requirements

### Requirement: Permisos de acceso al módulo Tareas

El sistema SHALL controlar el acceso a `/api/tareas/**` por permisos del rol vigente del usuario, no por nombre de rol: `tareas.ver` habilita consultar tareas y proyectos, y participar de una tarea (comentar, relacionar, y —siendo su Responsable o su autoridad creadora— cambiar estado y avance); `tareas.gestionar` habilita crear tareas (incluidas hijas) y editar los campos de una tarea propia; `proyectos.gestionar` habilita crear proyectos y cambiar su estado. Todos los roles de sistema MUST tener `tareas.ver`. `tareas.gestionar` MUST estar asignado a Decanato, Secretaría Académica y Administrativo; `proyectos.gestionar` MUST estar asignado solo a Decanato, Secretaría Académica y el Administrador de Sistemas. Un usuario sin sesión MUST recibir 401 y uno sin el permiso requerido MUST recibir 403, en todos los endpoints excepto `GET /api/tareas/ping`.

#### Scenario: Cualquier rol de sistema consulta las tareas

- **GIVEN** un usuario con rol Docente
- **WHEN** solicita `GET /api/tareas`
- **THEN** recibe 200 con el listado de tareas

#### Scenario: Un Docente no puede crear tareas

- **GIVEN** un usuario con rol Docente
- **WHEN** solicita `POST /api/tareas`
- **THEN** recibe 403 y no se persiste ninguna tarea

#### Scenario: Administrativo no puede crear proyectos

- **GIVEN** un usuario con rol Administrativo
- **WHEN** solicita `POST /api/tareas/proyectos`
- **THEN** recibe 403 y no se persiste ningún proyecto

#### Scenario: Sin sesión

- **WHEN** se solicita `GET /api/tareas` sin autenticación
- **THEN** recibe 401

### Requirement: Administrador de Sistemas

El Administrador de Sistemas (`sys_admin`) SHALL poder hacer todo en el módulo y ser la máxima jerarquía de asignación: tiene `tareas.ver`, `tareas.gestionar` y `proyectos.gestionar`, puede asignar tareas a cualquier usuario, y nadie puede asignárselas a él. No está sujeto a las reglas de autoría: MAY editar, cancelar, reabrir y actualizar estado y avance de cualquier tarea aunque no sea su autor ni su Responsable.

#### Scenario: El Administrador gestiona una tarea ajena

- **GIVEN** una tarea creada por Secretaría Académica y asignada a un Jefe de Cátedra
- **WHEN** el Administrador de Sistemas la edita y la cancela
- **THEN** ambas operaciones son exitosas

#### Scenario: Nadie asigna al Administrador

- **GIVEN** un usuario con rol Decanato
- **WHEN** consulta los candidatos a Responsable
- **THEN** el Administrador de Sistemas no figura entre ellos

### Requirement: Reglas de negocio validadas en el servidor

El servidor SHALL aplicar las mismas reglas de las capabilities `tareas`, `flujo-estado-tareas` y `proyectos`, sin depender de que el cliente las anticipe: jerarquía de asignación de Responsable (Administrador de Sistemas > Decanato > Secretaría Académica > Administrativo > Coordinador de Carrera > Jefe de Cátedra > Docente; solo del mismo nivel o inferior, según el rol de sesión del actor), Responsable de Proyecto acotado a Decanato y Secretaría Académica con la misma jerarquía, edición de campos y cancelación exclusivas de la autoridad creadora, Pausa con comentario obligatorio, Resuelta con Solución obligatoria, avance entre 0 y 100, Fecha de Fin posterior o igual a la de Inicio, Proyecto heredado obligatoriamente por las tareas hijas, y ausencia de rollup automático hacia el padre. Una violación de regla MUST responder 422 (o 400 si es un dato inválido) con un Problem Details que no exponga datos personales.

#### Scenario: Asignación hacia arriba rechazada

- **GIVEN** un usuario con rol de sesión Secretaría Académica
- **WHEN** crea una tarea con un Decanato como Responsable
- **THEN** recibe 422 y la tarea no se crea

#### Scenario: La tarea hija hereda el Proyecto

- **GIVEN** una tarea padre asociada al Proyecto P
- **WHEN** se crea una tarea hija indicando otro Proyecto Q
- **THEN** la hija queda asociada a P

#### Scenario: Pasar a Resuelta sin Solución

- **WHEN** el Responsable pasa la tarea a Resuelta sin Solución
- **THEN** recibe 422 y la tarea conserva su estado

### Requirement: Persistencia en el schema `tareas`

El módulo SHALL persistir en un schema PostgreSQL propio `tareas` (tareas, proyectos, relaciones entre tareas, comentarios e historial), definido por DDL versionado en `database/tareas/` y aplicado por migración. Las personas (Responsable, Autor, autores de comentarios) MUST guardarse como identificador de usuario sin clave foránea entre schemas. Tareas y Proyectos MUST recibir un número correlativo legible generado por secuencia de base de datos. Las tablas de negocio MUST quedar registradas en la auditoría (`audit.attach`).

#### Scenario: Numeración correlativa

- **WHEN** se crean dos tareas consecutivas
- **THEN** la segunda tiene un número mayor que la primera, sin repetirse

#### Scenario: Estado inválido rechazado por la base

- **WHEN** se intenta guardar una tarea con un estado fuera del conjunto permitido
- **THEN** la base rechaza la escritura por una restricción CHECK

### Requirement: Candidatos a Responsable

El servidor SHALL exponer `GET /api/tareas/candidatos` (permiso `tareas.gestionar`) con los usuarios activos que el actor puede asignar como Responsable según la jerarquía (y, con `?para=proyecto`, además acotados a Decanato y Secretaría Académica), cada uno con identificador, nombre y rol.

#### Scenario: Secretaría Académica no ve a Decanato como candidato

- **GIVEN** un usuario con rol de sesión Secretaría Académica
- **WHEN** solicita `GET /api/tareas/candidatos`
- **THEN** la respuesta no incluye usuarios cuyo rol de mayor jerarquía sea Decanato
