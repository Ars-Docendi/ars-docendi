## MODIFIED Requirements

### Requirement: Las tablas fuera de alcance no se conceden

El sistema MUST NOT conceder `portal.contactos`, `portal.cvs` ni `portal.proyecto_documentos` a ningún rol del asistente, ni la columna `portal.proyectos.descripcion`.

El acceso a esas tablas y columnas SHALL fallar por falta de privilegio y no devolver cero filas.

#### Scenario: El contacto personal no se alcanza

- **GIVEN** un actor con el permiso de trayectoria ajena
- **WHEN** intenta leer `portal.contactos`
- **THEN** el motor rechaza la consulta por falta de privilegio

#### Scenario: La descripción de un proyecto no se alcanza

- **GIVEN** un actor con el permiso de trayectoria ajena
- **WHEN** intenta leer `portal.proyectos.descripcion`
- **THEN** el motor rechaza la consulta por falta de privilegio

### Requirement: El perfil ajeno exige un permiso propio y estar dentro del ámbito

El sistema SHALL exigir `portal.ver_trayectoria_ajena` para devolver cualquier fila de portal que no pertenezca al actor, **y además** que la persona esté dentro del ámbito del actor.

Un actor de ámbito global SHALL alcanzar a todas las personas. Un actor de ámbito de carrera o de materia SHALL alcanzar únicamente a quienes tengan una **designación vigente** en una materia que ese ámbito le permite ver.

El sistema MUST NOT exigir ningún privilegio para que el actor consulte su propio perfil: el ámbito acota lo ajeno, nunca lo propio.

El sistema MUST NOT usar `portal.ver` como condición: lo tienen todos los roles y significa «acceder al portal propio».

El permiso SHALL estar concedido únicamente al rol de sistema `jefe_catedra` (decisión del equipo, 2026-11-07, `BR-portal-001`). Ningún otro rol lo tiene de arranque.

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
- **THEN** lo tiene únicamente `jefe_catedra`, y ningún otro rol

## ADDED Requirements

### Requirement: The jefe de cátedra holds the trayectoria permission out of the box

The system SHALL grant `portal.ver_trayectoria_ajena` to the system role `jefe_catedra` through a new versioned Identity migration, idempotent and without editing the migration that created the permission.

The grant MUST NOT widen the scope: a jefe de cátedra SHALL reach only their own profile and the profiles of docentes with a current designación in the materias they head, exactly as `identity.asistente_alcanza_a` resolves it today.

The permission MUST remain ungranted to `docente` and `sys_admin`.

#### Scenario: The jefe finds the docentes of their cátedra by skill

- **GIVEN** a jefe de cátedra of a materia whose designated docentes declared the skill «Cálculo»
- **WHEN** the jefe asks which docentes have that skill
- **THEN** the answer lists those docentes

#### Scenario: A docente outside the cátedra does not appear

- **GIVEN** a docente with the same skill and a current designación only in other materias
- **WHEN** the jefe asks the same question
- **THEN** the docente is not in the answer
- **AND** the answer does not state that nobody else has the skill

#### Scenario: A docente role still sees only their own profile

- **GIVEN** a role without the permission
- **WHEN** it queries another person's portal rows
- **THEN** it sees none

### Requirement: The answer states what the portal cannot say

The system SHALL state, in any answer that ranks or lists docentes by profile for a materia, that the portal records what each docente declared and not the requirements of the materia, and SHALL state the coverage (how many docentes of the scope loaded each section).

The system MUST NOT present a list as «the docentes who meet the profile».

#### Scenario: A profile search carries the disclaimer and the coverage

- **WHEN** the jefe asks which docentes have the profile to teach their materia
- **THEN** the answer says it found who declared related terms, not who meets requirements
- **AND** it says «de N docentes, M cargaron» for the sections consulted

### Requirement: Projects are readable without their description

The system SHALL grant `portal.proyectos(id, perfil_id, nombre, rol, desde, hasta, doi, created_at)` to both read roles, column by column, and SHALL protect it with a `FOR SELECT` policy that names the actor itself and resolves scope through `identity.asistente_alcanza_a` on the owning profile, like `portal.educaciones`.

Every granted column MUST be listed in `manifiesto-privilegios.json` and classified in `manifiesto-sensibilidad.json`; the denied `descripcion` MUST carry its reason (free text that cannot be masked per column).

#### Scenario: A project outside the scope is invisible

- **GIVEN** a jefe de cátedra and a docente designated only in other materias with a loaded project
- **WHEN** the jefe lists projects
- **THEN** that project does not appear

#### Scenario: A project inside the scope exposes name, role, dates and DOI

- **GIVEN** a docente designated in the jefe's materia with a project that has a DOI
- **WHEN** the jefe asks who has projects with a DOI
- **THEN** the answer includes the project name and the DOI and never the description

#### Scenario: The manifest and the grants agree

- **WHEN** the privilege manifest tests run
- **THEN** every granted `proyectos` column is declared and `descripcion` is declared denied
