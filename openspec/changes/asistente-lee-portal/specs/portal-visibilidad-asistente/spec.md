## ADDED Requirements

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

### Requirement: El perfil ajeno exige un permiso propio

El sistema SHALL exigir `portal.ver_trayectoria_ajena` para devolver cualquier fila de portal que no pertenezca al actor.

El sistema MUST NOT usar `portal.ver` como condición: lo tienen todos los roles y significa «acceder al portal propio».

El permiso SHALL nacer concedido a ningún rol.

#### Scenario: Sin el permiso no se ve la trayectoria ajena

- **GIVEN** un actor de ámbito global sin `portal.ver_trayectoria_ajena`
- **WHEN** consulta formación, certificaciones o experiencia del padrón
- **THEN** no ve ninguna fila que no sea suya

#### Scenario: Con el permiso se ve todo el padrón

- **GIVEN** un actor con `portal.ver_trayectoria_ajena`
- **WHEN** consulta las tablas de trayectoria
- **THEN** ve las mismas filas que el dueño de las tablas

#### Scenario: El permiso nace vacío

- **GIVEN** una base recién migrada
- **WHEN** se consulta qué roles tienen `portal.ver_trayectoria_ajena`
- **THEN** no lo tiene ninguno

### Requirement: El ámbito no interviene en la visibilidad del portal

El sistema MUST NOT condicionar la visibilidad de portal al ámbito del actor.

Un actor de ámbito de carrera con el permiso SHALL ver el mismo conjunto que uno de ámbito global con el permiso.

#### Scenario: El ámbito no acota

- **GIVEN** dos actores con el permiso, uno de ámbito global y otro de carrera
- **WHEN** los dos consultan la misma tabla de trayectoria
- **THEN** ven el mismo conjunto de filas

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
