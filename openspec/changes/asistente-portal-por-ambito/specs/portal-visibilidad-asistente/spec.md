## MODIFIED Requirements

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
