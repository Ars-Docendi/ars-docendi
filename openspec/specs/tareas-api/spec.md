# tareas-api Specification

## Purpose

Definir la API HTTP del módulo Tareas, sus permisos, reglas de negocio, persistencia y catálogos.

## Requirements

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

El módulo SHALL persistir tareas, proyectos, relaciones, comentarios e historial en `tareas` mediante SQL versionado. Las referencias a personas SHALL guardar IDs de usuario sin FKs interschema y sujetarse a integridad lógica coordinada. Tareas/Proyectos MUST recibir correlativos de secuencia. Las tablas de negocio MUST tener auditoría; estados, prioridades y tipos SHALL usar FKs numéricas a sus catálogos.

#### Scenario: Numeración correlativa

- **GIVEN** una instalación reconocida
- **WHEN** se crean dos tareas consecutivas
- **THEN** la segunda tiene un número mayor, sin repetirse

#### Scenario: Estado inválido rechazado por la base

- **GIVEN** un identificador inexistente en el catálogo de estados
- **WHEN** se intenta persistirlo en una tarea
- **THEN** la base rechaza la escritura por integridad referencial y no por una lista textual duplicada

### Requirement: Candidatos a Responsable

El servidor SHALL exponer `GET /api/tareas/candidatos` (permiso `tareas.gestionar`) con los usuarios activos que el actor puede asignar como Responsable según la jerarquía (y, con `?para=proyecto`, además acotados a Decanato y Secretaría Académica), cada uno con identificador, nombre, rol, usuario, legajo y documento. El parámetro `q` MUST acotar la lista por nombre, apellido, usuario, legajo o documento: todas las palabras deben aparecer, sin distinguir mayúsculas ni acentos. La respuesta MUST tener un tope de resultados (50) y ordenarse por nombre.

#### Scenario: Secretaría Académica no ve a Decanato como candidato

- **GIVEN** un usuario con rol de sesión Secretaría Académica
- **WHEN** solicita `GET /api/tareas/candidatos`
- **THEN** la respuesta no incluye usuarios cuyo rol de mayor jerarquía sea Decanato

#### Scenario: Búsqueda por legajo, documento o apellido

- **WHEN** un usuario con `tareas.gestionar` solicita `GET /api/tareas/candidatos?q=0058`, `?q=35678901` o `?q=gómez`
- **THEN** cada búsqueda devuelve al usuario cuyo legajo, documento o apellido coincide

### Requirement: Visibilidad de las tareas según el permiso

Un usuario sin `tareas.gestionar` SHALL ver únicamente las tareas que tiene asignadas como Responsable: el listado MUST devolver solo esas, y cualquier operación sobre una tarea ajena (detalle, estado, avance, comentarios, relaciones) MUST responder 404, como si no existiera. Quien tiene `tareas.gestionar` ve todas las tareas.

#### Scenario: Un Docente solo ve sus tareas

- **GIVEN** un Docente con una tarea asignada y otra tarea asignada a un Jefe de Cátedra
- **WHEN** solicita `GET /api/tareas`
- **THEN** recibe solo la tarea que tiene asignada

#### Scenario: Una tarea ajena responde como inexistente

- **WHEN** ese Docente solicita `GET /api/tareas/{id}` de la tarea del Jefe de Cátedra
- **THEN** recibe 404

### Requirement: Catálogos de estados, prioridades y tipos en la base

Los estados de tarea, los estados de proyecto, las prioridades y los tipos de tarea MUST definirse en tablas de catálogo del schema `tareas`, referenciadas por clave foránea numérica (`id` SMALLINT, columnas `estado_id`, `prioridad_id` y `tipo_id`) desde las tablas de negocio, sin guardar el código como texto en ellas: el DDL MUST NOT repetir la lista de valores en CHECKs ni en DEFAULTs. Los estados MUST declarar su comportamiento con banderas del catálogo — cuál es el estado inicial (`es_inicial`) y, para los proyectos, si admiten tareas nuevas (`admite_tareas`) —, y el servidor MUST leerlas del catálogo en vez de nombrar estados concretos para esas decisiones. `GET /api/tareas/proyectos/estados` (permiso `tareas.ver`) MUST exponer el catálogo de estados de proyecto (código, nombre, verbo de la acción, inicial, admite tareas).

#### Scenario: Estado inicial tomado del catálogo

- **WHEN** se crea un proyecto o una tarea
- **THEN** nace en el estado que el catálogo marca como inicial

#### Scenario: Valor fuera del catálogo rechazado

- **WHEN** se intenta guardar un proyecto o una tarea con un estado, prioridad o tipo que no está en el catálogo
- **THEN** la base rechaza la escritura por la clave foránea, y la API responde 400 antes de llegar a ella

### Requirement: Proyecto coherente en toda la jerarquía

Toda hija SHALL conservar el mismo proyecto que su padre, incluido NULL. Cambiar el proyecto de una raíz MUST propagarlo atómicamente a todos sus descendientes, sin modificar sus estados, avances ni responsables. La jerarquía MUST ser acíclica; una escritura inválida MUST rechazarse sin cambios parciales.

#### Scenario: Trasladar raíz con varios niveles

- **GIVEN** una raíz, hija y nieta asociadas a P
- **WHEN** su autoridad autorizada cambia la raíz a Q elegible
- **THEN** las tres quedan en Q en una sola confirmación y cada tarea conserva estado, avance y responsable

#### Scenario: Raíz sin proyecto

- **GIVEN** una raíz sin proyecto
- **WHEN** se crea una hija válida
- **THEN** la hija hereda NULL y no se impone un proyecto obligatorio a la raíz

#### Scenario: Escritura con proyecto distinto al padre

- **GIVEN** un padre en P
- **WHEN** una escritura intenta persistir una hija en Q diferente
- **THEN** la persistencia la rechaza al confirmar, sin estados intermedios observables

#### Scenario: Ciclo de varios nodos

- **GIVEN** A es ancestro de B
- **WHEN** una escritura intenta hacer A hija de B
- **THEN** la base rechaza el ciclo completo incluso si A y B son diferentes

### Requirement: Proyectos elegibles también para hijas

Crear una tarea, raíz o hija, MUST verificar que su proyecto efectivo admita tareas nuevas. La verificación SHALL coordinarse con el cierre concurrente del proyecto. Editar una tarea existente sin cambiar su proyecto cerrado SHALL conservar el comportamiento permitido actual.

#### Scenario: Hija de un proyecto finalizado

- **GIVEN** un padre cuyo proyecto no admite tareas nuevas
- **WHEN** se intenta crear una hija del padre
- **THEN** se rechaza igual que una raíz nueva en ese proyecto

#### Scenario: Cierre contra creación

- **GIVEN** una creación y un cierre concurrentes del mismo proyecto
- **WHEN** ambas intentan confirmar
- **THEN** se serializan o una falla; no se crea una tarea usando elegibilidad obsoleta después del cierre confirmado

### Requirement: Mutaciones versionadas sin autorización obsoleta

Las lecturas SHALL entregar la versión de tareas/proyectos. Editar campos, estado o avance de una entidad existente MUST aportar la versión observada y confirmar sin sobrescribir cambios posteriores; una versión faltante SHALL producir validación y una obsoleta conflicto 409. Los guards de actor, responsable y estado MUST evaluarse sobre el estado protegido y los eventos confirmarse atómicamente.

#### Scenario: Responsable guarda después de una cancelación

- **GIVEN** el responsable leyó una tarea no terminal y el creador la cancela
- **WHEN** el responsable solicita En curso con su versión anterior
- **THEN** recibe 409, no reabre la tarea y no agrega un evento exitoso

#### Scenario: Reasignación concurrente

- **GIVEN** una reasignación confirmada después de la lectura del responsable anterior
- **WHEN** este intenta una mutación con versión obsoleta
- **THEN** no actúa con la autoridad del estado anterior ni pierde la reasignación

#### Scenario: Cliente sin versión

- **GIVEN** una entidad existente
- **WHEN** se solicita una mutación versionada sin versión
- **THEN** responde validación, sin aceptar silenciosamente el protocolo viejo

### Requirement: Relacionar tareas es idempotente también bajo concurrencia

Relacionar el mismo par autorizado SHALL ser idempotente aun con solicitudes concurrentes. MUST persistir una sola relación canónica y resolver conflictos de unicidad como reutilización del resultado confirmado, sin 500 ni bypass de permisos. Quitar una relación ausente SHALL conservar su idempotencia.

#### Scenario: Dos altas del mismo vínculo

- **GIVEN** dos solicitudes autorizadas que observan ausencia del mismo par
- **WHEN** se ejecutan concurrentemente
- **THEN** ambas reciben un resultado idempotente y existe una sola relación

### Requirement: Consulta acotada a las tareas visibles

Listados y detalles SHALL resolver personas y relaciones necesarias para sus tareas visibles, sin materializar todo el directorio ni relaciones ajenas. La búsqueda de candidatos MUST aplicar criterios y jerarquía antes del límite de 50, preservando búsqueda por palabras sin distinguir mayúsculas/tildes y sin paginar visiblemente las grillas completas.

#### Scenario: Crecen datos ajenos

- **GIVEN** datasets con las mismas tareas visibles y creciente cantidad de usuarios y relaciones ajenos
- **WHEN** se consulta una tarea o el listado del responsable
- **THEN** no se materializan directorios ni relaciones globales para construir esa respuesta

#### Scenario: Búsqueda selectiva entre muchos candidatos

- **GIVEN** más de 50 usuarios elegibles y uno que coincide con todas las palabras buscadas
- **WHEN** se consulta con esos criterios
- **THEN** el filtro precede al límite y no pierde al usuario por truncar primero el directorio

### Requirement: Resolución consistente también en persistencia

Una tarea Resuelta MUST tener solución no vacía en la aplicación y al confirmar escrituras directas. La validación SHALL resolver el código del catálogo numérico sin fijar un ID accidental ni duplicar la lista de estados como texto. Cambios del catálogo MUST NOT invalidar silenciosamente tareas existentes.

#### Scenario: SQL resuelve sin solución

- **GIVEN** el ID de Resuelta tomado del catálogo actual
- **WHEN** una escritura directa lo asigna con solución nula o vacía
- **THEN** la base rechaza la confirmación

#### Scenario: IDs diferentes en el catálogo

- **GIVEN** un catálogo válido cuyos IDs no coinciden con los de una fixture anterior
- **WHEN** se verifica la obligatoriedad de solución
- **THEN** depende del significado Resuelta y no de una constante numérica
