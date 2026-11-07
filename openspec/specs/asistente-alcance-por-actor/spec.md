# asistente-alcance-por-actor Specification

## Purpose

TBD - created by archiving change asistente-fundaciones. Update Purpose after archive.

## Requirements

### Requirement: Funciones de resolución del actor

El sistema SHALL proveer en el schema `identity` cuatro funciones `SECURITY DEFINER`: la que resuelve el actor del turno desde el ajuste de sesión `app.asistente_user_id`, la que determina si su alcance es global, la que devuelve las materias visibles para él, y la que responde si tiene un permiso dado.

La función de permiso MUST resolver la membresía en vivo recorriendo `identity.user_roles`, `identity.rol_permisos` e `identity.permisos`, ignorando las asignaciones con `deleted_at` no nulo. Ninguna función MAY contener códigos de rol embebidos.

Las funciones MUST NOT quedar ejecutables por `PUBLIC`.

#### Scenario: El permiso se resuelve en vivo

- **GIVEN** un actor cuyo rol tiene `designaciones.ver`
- **WHEN** se revoca ese permiso al rol y se vuelve a evaluar la función
- **THEN** la función devuelve falso sin necesidad de reiniciar la aplicación ni redesplegar

#### Scenario: Un rol nuevo con el permiso queda habilitado

- **GIVEN** un rol creado desde la superficie de administración, con `es_sistema = false` y con `designaciones.ver`
- **WHEN** un usuario con ese rol es el actor del turno
- **THEN** la función de permiso devuelve verdadero

#### Scenario: Un rol nuevo sin el permiso queda excluido

- **GIVEN** un rol creado desde la superficie de administración, sin `designaciones.ver`
- **WHEN** un usuario con ese rol es el actor del turno
- **THEN** la función de permiso devuelve falso

#### Scenario: Las asignaciones dadas de baja no cuentan

- **GIVEN** un actor cuya única asignación de rol tiene `deleted_at` no nulo
- **WHEN** se evalúa la función de permiso
- **THEN** devuelve falso

#### Scenario: Las funciones no son públicas

- **WHEN** se inspeccionan los privilegios de ejecución de las cuatro funciones
- **THEN** `PUBLIC` no tiene `EXECUTE` sobre ninguna de ellas

### Requirement: Row Level Security sobre las tablas del trámite

El sistema SHALL habilitar Row Level Security sobre `designaciones.pedidos`, `designaciones.designaciones`, `designaciones.pedido_historial` y `designaciones.pedido_adjuntos`, con una policy `FOR SELECT` dirigida a los roles del asistente.

El predicado de cada policy MUST conjuntar la verificación del permiso de dominio con la restricción de alcance del actor. El sistema MUST NOT usar `FORCE ROW LEVEL SECURITY` sobre estas tablas.

#### Scenario: Un actor de alcance de carrera solo ve su carrera

- **GIVEN** un actor con rol de ámbito de carrera y con `designaciones.ver`
- **WHEN** una sesión del asistente ejecuta `SELECT count(*) FROM designaciones.pedidos` con ese actor fijado
- **THEN** el resultado cuenta únicamente los pedidos de materias de su carrera

#### Scenario: Un actor de alcance global ve todo

- **GIVEN** un actor con rol de ámbito global y con `designaciones.ver`
- **WHEN** una sesión del asistente ejecuta `SELECT count(*) FROM designaciones.pedidos` con ese actor fijado
- **THEN** el resultado cuenta todos los pedidos

#### Scenario: Sin permiso de dominio no se ve ninguna fila

- **GIVEN** un actor con rol de ámbito de materia y **sin** `designaciones.ver`
- **WHEN** una sesión del asistente ejecuta `SELECT count(*) FROM designaciones.pedidos` con ese actor fijado
- **THEN** el resultado es cero, aunque existan pedidos en su materia

#### Scenario: El predicado no se esquiva reestructurando la consulta

- **GIVEN** un actor de alcance de materia
- **WHEN** una sesión del asistente ejecuta una consulta que une las cuatro tablas protegidas
- **THEN** cada tabla aporta únicamente las filas del alcance del actor

#### Scenario: El backend no queda afectado

- **GIVEN** las policies aplicadas
- **WHEN** la aplicación consulta las mismas tablas con la conexión del rol dueño
- **THEN** obtiene todas las filas, sin cambios respecto del comportamiento previo

### Requirement: Propagación del actor acotada a la transacción

El sistema SHALL fijar el actor del turno con un ajuste **transaction-local** dentro de una transacción declarada `READ ONLY`, abriendo conexión y transacción nuevas en cada ejecución. El valor fijado MUST ser el identificador de `identity.users`, obtenido del usuario autenticado del sistema.

El sistema MUST NOT aceptar la identidad del actor desde ningún dato enviado por el cliente, y MUST NOT usar el identificador de objeto del proveedor de identidad externo como valor del ajuste.

#### Scenario: El actor no sobrevive a la transacción

- **GIVEN** una ejecución que fijó el actor y terminó
- **WHEN** se reutiliza la misma conexión física desde el pool y se lee el ajuste
- **THEN** el ajuste está vacío

#### Scenario: La transacción es de solo lectura

- **GIVEN** una ejecución del asistente en curso
- **WHEN** se intenta escribir dentro de esa transacción
- **THEN** PostgreSQL rechaza la escritura por transacción de solo lectura

#### Scenario: La identidad no se toma del cliente

- **WHEN** un request incluye un encabezado o campo que pretende declarar la identidad del actor
- **THEN** el sistema lo ignora por completo y resuelve el actor desde el usuario autenticado

### Requirement: Un resultado vacío solo se presenta como ausencia cuando el actor alcanza todo lo consultado

El sistema SHALL determinar si «cero filas significa que no hay filas» conjugando el ámbito del actor **y** su permiso de dominio, que son ejes independientes.

El sistema MUST NOT usar el ámbito global por sí solo como equivalente de «ve todo».

Cuando el actor no alcanza todo lo consultado, el texto del resultado vacío SHALL reconocer el límite de alcance en lugar de afirmar que el dato no existe.

#### Scenario: Un actor global sin el permiso de dominio no alcanza todo

- **GIVEN** un actor de ámbito global sin `designaciones.ver`
- **WHEN** se resuelve su perfil
- **THEN** su ámbito sigue siendo global y no alcanza todo lo consultable

#### Scenario: El texto no afirma ausencia cuando el vacío puede venir del alcance

- **GIVEN** un actor que no alcanza todo lo consultado y una consulta con cero filas
- **WHEN** se arma el texto del resultado vacío
- **THEN** el texto reconoce el límite de alcance y no afirma que no hay registros

#### Scenario: No se gasta el reintento donde no puede ayudar

- **GIVEN** un resultado vacío de un actor que no alcanza todo lo consultado
- **WHEN** se decide si reintentar la generación
- **THEN** no se reintenta

### Requirement: El alcance se evalúa por dominio consultado

El sistema SHALL decidir si un resultado vacío puede presentarse como «no hay datos» evaluando los **dominios que la consulta tocó**, y no con un único booleano del turno.

Una consulta que toca tablas de portal SHALL exigir, además de lo que exija por sus otros dominios, el permiso de trayectoria ajena y el alcance de portal del actor.

El sistema SHALL derivar qué dominios tocó la consulta de la **misma** detección que alimenta la declaración de cobertura. El sistema MUST NOT mantener una segunda detección del mismo hecho.

Ante un dominio que no reconoce, el sistema MUST NOT concluir que el actor alcanza todo.

#### Scenario: Un actor global sin el permiso de portal no afirma inexistencia

- **GIVEN** un actor de ámbito global con `designaciones.ver` y sin `portal.ver_trayectoria_ajena`
- **WHEN** pregunta por una habilidad que otras personas declararon
- **THEN** la respuesta reconoce el límite de alcance y no afirma que no haya registros

#### Scenario: Un jefe de cátedra con el permiso tampoco afirma inexistencia sobre el padrón

- **GIVEN** un jefe de cátedra con `portal.ver_trayectoria_ajena`
- **WHEN** pregunta por una habilidad que sólo declararon docentes fuera de sus materias
- **THEN** la respuesta reconoce el límite de alcance

#### Scenario: Una consulta que no toca portal conserva su evaluación

- **GIVEN** un actor de ámbito global con `designaciones.ver` y sin el permiso de portal
- **WHEN** pregunta algo que sólo toca designaciones e identity
- **THEN** un resultado vacío puede presentarse como que no hay registros
