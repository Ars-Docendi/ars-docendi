## MODIFIED Requirements

### Requirement: Consultas administrativas de identidad

La API SHALL permitir listar y obtener usuarios, docentes, roles, permisos, carreras, materias y cargos necesarios para las pantallas administrativas. Las respuestas MUST utilizar identificadores canónicos, representar estado y asignaciones vigentes, y distinguir el resumen de roles de las membresías completas con ámbito.

#### Scenario: Listado autorizado

- **GIVEN** un actor con permiso de administración de identidad
- **WHEN** consulta uno de los listados administrativos
- **THEN** la API responde con los registros persistidos y sus relaciones necesarias para renderizar la pantalla, sin repetir un rol por cada ámbito

#### Scenario: Detalle de membresías

- **GIVEN** un usuario tiene el mismo rol en varias materias
- **WHEN** se consulta el usuario
- **THEN** la respuesta contiene un resumen único del rol y el detalle de cada asignación con sus IDs de materia o carrera

#### Scenario: Consulta sin autorización

- **GIVEN** un actor sin el permiso correspondiente
- **WHEN** consulta una superficie administrativa
- **THEN** la API MUST denegar la operación sin exponer los datos

### Requirement: Mutaciones durables de usuarios y docentes

La API SHALL permitir crear y editar personas y cuentas, activar o desactivar usuarios, administrar membresías de rol con el ámbito exigido y mantener las designaciones docentes vigentes. Las validaciones de unicidad, referencia, ámbito, repetición y versión MUST ejecutarse de forma autoritativa antes de confirmar la operación.

#### Scenario: Mutación válida

- **GIVEN** datos válidos, membresías explícitas y un actor autorizado
- **WHEN** crea o modifica un usuario o docente
- **THEN** el cambio se confirma atómicamente y una consulta posterior devuelve el nuevo estado

#### Scenario: Conflicto de unicidad

- **GIVEN** una UPN, documento o legajo ya asignado a otro registro
- **WHEN** el operador intenta guardar el conflicto
- **THEN** la API MUST rechazarlo con un código de conflicto identificable y no MUST aplicar cambios parciales

#### Scenario: Versión obsoleta

- **GIVEN** el usuario fue modificado después de que el operador abrió el formulario
- **WHEN** se envía una versión obsoleta
- **THEN** la API responde `409` con `concurrency-conflict` y no modifica datos ni membresías

#### Scenario: Ámbito de rol inválido

- **GIVEN** un rol que exige ámbito de materia o carrera
- **WHEN** se intenta asignar sin el ámbito requerido, con uno incompatible o con una asignación repetida
- **THEN** la API MUST rechazar la asignación sin alterar las membresías existentes

### Requirement: Errores HTTP consistentes

Las operaciones administrativas MUST distinguir autenticación, autorización, recurso inexistente, validación, conflicto de unicidad, conflicto de concurrencia y regla de ámbito mediante respuestas HTTP estables con un cuerpo de error consumible por formularios.

#### Scenario: Error de validación en formulario

- **GIVEN** una solicitud autenticada con datos inválidos
- **WHEN** la API rechaza la operación
- **THEN** responde con un código y detalle que permiten asociar el error general o por campo sin depender del texto interno de una excepción

#### Scenario: Conflicto de concurrencia

- **GIVEN** una solicitud contiene una versión que ya no es vigente
- **WHEN** se intenta guardar
- **THEN** responde `409`, `concurrency-conflict` y un mensaje accionable para actualizar los datos

#### Scenario: Regla de ámbito

- **GIVEN** una membresía no cumple el ámbito declarado por el rol
- **WHEN** se intenta guardar
- **THEN** responde `422`, `identity-role-scope-conflict` y conserva el estado previo
