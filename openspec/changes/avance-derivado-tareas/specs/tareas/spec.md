## MODIFIED Requirements

### Requirement: Tareas hijas (jerarquía padre/hijas)

El sistema SHALL permitir crear tareas hijas dentro de una tarea (su padre), para descomponer una tarea compleja en partes más chicas de gestión independiente. Cada tarea hija SHALL comportarse como una tarea completa e independiente: tiene su propio Responsable (que puede coincidir o no con el Responsable de su padre), sus propias Fecha de Inicio y Fecha de Fin y su propio Estado, que el padre no recalcula a partir de sus hijas. El % de avance del padre, en cambio, SHALL derivarse de sus hijas según el requisito de porcentaje de avance. Una tarea hija MAY a su vez tener sus propias tareas hijas (jerarquía de más de un nivel). Crear una tarea hija MUST estar restringido a los mismos roles habilitados para crear tareas (Secretaría Académica, Decanato, Administrativos).

#### Scenario: Crear una tarea hija

- **GIVEN** una tarea padre "Migración del campus virtual"
- **WHEN** una autoridad crea una tarea hija "Migrar el módulo de calificaciones" desde el Detalle del padre
- **THEN** la hija se crea como una tarea independiente, con su propio Responsable, Estado y % de avance en 0, y aparece listada como hija de "Migración del campus virtual"

#### Scenario: Modificar el avance de una hija actualiza al padre

- **GIVEN** una tarea padre con dos hijas, una con 10% y otra con 50% de avance
- **WHEN** el Responsable de la primera actualiza su avance a 90%
- **THEN** el % de avance de la tarea padre pasa a 70%

#### Scenario: Modificar una hija no afecta al padre

- **GIVEN** una tarea padre En curso con una tarea hija
- **WHEN** el Responsable de la hija la marca como Resuelta
- **THEN** el estado, las fechas y el Responsable del padre no cambian; sólo se actualiza su % de avance

#### Scenario: Una tarea hija puede tener sus propias hijas

- **GIVEN** una tarea hija "Migrar el módulo de calificaciones"
- **WHEN** una autoridad crea una tarea hija de esa hija
- **THEN** la nueva tarea se crea correctamente, anidada dos niveles debajo de la tarea padre original

#### Scenario: Un rol sin permiso no puede crear tareas hijas

- **GIVEN** un usuario con rol Docente
- **WHEN** intenta crear una tarea hija de una tarea existente
- **THEN** el sistema rechaza la acción

### Requirement: Porcentaje de avance editable por el Responsable

Toda tarea SHALL tener un porcentaje de avance entre 0 y 100, inicializado en 0 al crearse. Únicamente el Responsable de la tarea (o la autoridad creadora) SHALL poder modificarlo, desde la pantalla de Detalle. El sistema MUST rechazar valores fuera del rango 0-100.

Si la tarea tiene hijas no canceladas, su avance SHALL ser el promedio redondeado del avance de esas hijas directas, recalculado en toda la cadena de ancestros al cambiar el avance o el estado de una hija, o al crearse una. Esa tarea MUST NOT admitir la edición manual del avance. Una tarea Resuelta o Cancelada MUST NOT recalcularse mientras siga cerrada; al reabrirla SHALL volver a tomar el promedio. Cada recálculo que cambie el valor SHALL quedar en el historial.

#### Scenario: El Responsable actualiza el porcentaje de avance

- **GIVEN** una tarea con 20% de avance, asignada como Responsable al usuario actual
- **WHEN** el Responsable lo actualiza a 60
- **THEN** el porcentaje de avance de la tarea pasa a 60 y queda registrado en el historial

#### Scenario: Valor fuera de rango rechazado

- **WHEN** el Responsable intenta ingresar un porcentaje de avance de 120
- **THEN** el sistema muestra un error de validación y no guarda el cambio

#### Scenario: Un tercero no puede editar el avance

- **GIVEN** una tarea asignada a otro Responsable, creada por otra autoridad
- **WHEN** un usuario ajeno a la tarea intenta editar el porcentaje de avance
- **THEN** el sistema no permite la edición

#### Scenario: El avance se propaga por toda la jerarquía

- **GIVEN** una tarea con una hija que a su vez tiene una hija propia
- **WHEN** el Responsable de la tarea del último nivel actualiza su avance a 100%
- **THEN** su padre y la tarea superior reflejan el nuevo promedio

#### Scenario: Una hija cancelada no cuenta para el promedio

- **GIVEN** una tarea padre con una hija al 100% y otra al 0%
- **WHEN** la autoridad creadora cancela la hija al 0%
- **THEN** el avance del padre pasa a 100%

#### Scenario: Una tarea con hijas no se edita a mano

- **GIVEN** una tarea con una hija no cancelada
- **WHEN** su Responsable intenta editar el porcentaje de avance
- **THEN** el sistema rechaza el cambio con un error de regla de negocio y no ofrece el campo en el Detalle

#### Scenario: Una tarea cerrada conserva su avance hasta reabrirse

- **GIVEN** una tarea padre Resuelta con 100% de avance y una hija al 40%
- **WHEN** la hija cambia su avance, y luego la autoridad creadora reabre el padre
- **THEN** el padre sigue en 100% mientras está Resuelta y toma el promedio de sus hijas al reabrirse
