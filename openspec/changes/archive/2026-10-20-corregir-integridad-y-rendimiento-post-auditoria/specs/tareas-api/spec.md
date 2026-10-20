# Spec Delta

## ADDED Requirements

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

## MODIFIED Requirements

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
