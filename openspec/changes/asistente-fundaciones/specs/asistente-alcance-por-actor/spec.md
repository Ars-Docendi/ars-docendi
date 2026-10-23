## ADDED Requirements

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
