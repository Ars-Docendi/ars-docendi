# portal-visibilidad-asistente Specification

## Purpose

TBD - created by archiving change asistente-lee-portal. Update Purpose after archive.

## Requirements

### Requirement: El perfil propio se consulta sin permiso adicional

El sistema SHALL permitir que cualquier actor del asistente consulte su propio perfil profesional sin ningún permiso más allá del de usar el asistente.

Mirar lo propio MUST NOT exigir un privilegio.

#### Scenario: El actor ve su perfil

- **GIVEN** un actor con un perfil cargado y sin `portal.ver_trayectoria_ajena`
- **WHEN** consulta los perfiles del portal
- **THEN** ve exactamente el suyo

#### Scenario: Un usuario sin persona asociada no ve ninguno

- **GIVEN** un actor cuyo usuario no tiene `persona_id`
- **WHEN** consulta los perfiles del portal
- **THEN** no ve ninguna fila, y el turno no falla

### Requirement: El perfil ajeno exige un permiso propio y estar dentro del ámbito

El sistema SHALL exigir `portal.ver_trayectoria_ajena` para devolver cualquier fila de portal que no pertenezca al actor, **y además** que la persona esté dentro del ámbito del actor.

Un actor de ámbito global SHALL alcanzar a todas las personas. Un actor de ámbito de carrera o de materia SHALL alcanzar únicamente a quienes tengan una **designación vigente** en una materia que ese ámbito le permite ver.

El sistema MUST NOT exigir ningún privilegio para que el actor consulte su propio perfil: el ámbito acota lo ajeno, nunca lo propio.

El sistema MUST NOT usar `portal.ver` como condición: lo tienen todos los roles y significa «acceder al portal propio».

El permiso SHALL nacer concedido a ningún rol.

#### Scenario: Un jefe de cátedra ve a los designados en sus materias

- **GIVEN** un jefe de cátedra con `portal.ver_trayectoria_ajena`
- **WHEN** consulta los perfiles del portal
- **THEN** ve el suyo y el de quienes tienen designación vigente en sus materias

#### Scenario: Un jefe de cátedra no ve a un docente de otra materia

- **GIVEN** un jefe de cátedra con `portal.ver_trayectoria_ajena`
- **WHEN** consulta el perfil de alguien designado sólo en materias ajenas
- **THEN** no ve ninguna fila de ese perfil

#### Scenario: Un docente sin designación vigente sólo lo alcanzan los roles globales

- **GIVEN** una persona sin ninguna designación vigente
- **WHEN** un jefe de cátedra o un coordinador con el permiso consulta el portal
- **THEN** no ve el perfil de esa persona
- **AND** un actor de ámbito global con el permiso sí lo ve

#### Scenario: Un coordinador ve a los de su carrera y no a los de otra

- **GIVEN** un coordinador de carrera con `portal.ver_trayectoria_ajena`
- **WHEN** consulta los perfiles del portal
- **THEN** ve a los designados vigentes en materias de su carrera, y a nadie de otra carrera

#### Scenario: El ámbito no acota el perfil propio

- **GIVEN** un actor sin `portal.ver_trayectoria_ajena` y sin ninguna designación vigente
- **WHEN** consulta su propio perfil
- **THEN** lo ve

#### Scenario: Sin el permiso no se ve la trayectoria ajena

- **GIVEN** un actor de ámbito global sin `portal.ver_trayectoria_ajena`
- **WHEN** consulta formación, certificaciones o experiencia del padrón
- **THEN** no ve ninguna fila que no sea suya

#### Scenario: Con el permiso se ve todo el padrón

- **GIVEN** un actor de ámbito global con `portal.ver_trayectoria_ajena`
- **WHEN** consulta las tablas de trayectoria
- **THEN** ve las mismas filas que el dueño de las tablas

#### Scenario: El permiso nace vacío

- **GIVEN** una base recién migrada
- **WHEN** se consulta qué roles tienen `portal.ver_trayectoria_ajena`
- **THEN** no lo tiene ninguno

### Requirement: Cada policy nombra al actor por sí misma

El sistema SHALL escribir el predicado del actor completo en cada policy de portal, sin depender de que la policy de `portal.perfiles` se aplique dentro de su subconsulta.

Toda referencia a una columna de la fila externa dentro de un `EXISTS` MUST estar calificada con su tabla.

#### Scenario: Cada policy menciona al actor

- **GIVEN** las policies de portal
- **WHEN** se leen sus predicados
- **THEN** todas mencionan la resolución de la persona del actor y la del permiso

### Requirement: Las tablas fuera de alcance no se conceden

El sistema MUST NOT conceder `portal.contactos`, `portal.cvs`, `portal.proyectos` ni `portal.proyecto_documentos` a ningún rol del asistente.

El acceso a esas tablas SHALL fallar por falta de privilegio y no devolver cero filas.

#### Scenario: El contacto personal no se alcanza

- **GIVEN** un actor con el permiso de trayectoria ajena
- **WHEN** intenta leer `portal.contactos`
- **THEN** el motor rechaza la consulta por falta de privilegio

### Requirement: La RLS de portal no cubre la API REST

El sistema SHALL documentar que estas policies acotan únicamente el camino del asistente.

La defensa de los endpoints del portal MUST seguir siendo la resolución de la persona actual en el servicio más el filtro por dueño del repositorio.

#### Scenario: El dueño de las tablas no queda sometido

- **GIVEN** la conexión de la aplicación, que es el dueño de las tablas
- **WHEN** consulta los perfiles sin fijar ningún actor
- **THEN** los ve todos
