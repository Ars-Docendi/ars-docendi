## MODIFIED Requirements

### Requirement: Consultas administrativas de identidad

La API SHALL permitir listar y obtener personas (con su cuenta, roles con ámbito, designaciones vigentes y estado), roles, permisos, carreras, materias y cargos necesarios para las pantallas administrativas, a través de `/api/administracion/personas` y sus catálogos. Las respuestas MUST utilizar identificadores canónicos, representar estado y asignaciones vigentes, y distinguir el resumen de roles de las membresías completas con ámbito. Las consultas MUST respetar el ámbito del actor: con `personas.ver_ambito` y sin `personas.ver`, únicamente personas con designación vigente en las materias donde es Jefe de Cátedra.

#### Scenario: Listado autorizado

- **GIVEN** un actor con `personas.ver`
- **WHEN** consulta el listado de personas
- **THEN** la API responde con los registros persistidos y sus relaciones necesarias para renderizar la pantalla, sin repetir un rol por cada ámbito

#### Scenario: Detalle de membresías

- **GIVEN** una persona tiene el mismo rol en varias materias
- **WHEN** se consulta la persona
- **THEN** la respuesta contiene un resumen único del rol y el detalle de cada asignación con sus IDs de materia o carrera

#### Scenario: Consulta sin autorización

- **GIVEN** un actor sin el permiso correspondiente
- **WHEN** consulta una superficie administrativa
- **THEN** la API MUST denegar la operación sin exponer los datos

#### Scenario: Verificación de documento

- **GIVEN** un actor con `personas.administrar`
- **WHEN** consulta si un documento ya está registrado
- **THEN** la API responde si existe otra persona con esos dígitos, con su nombre, legajo y estado, sin exponer otros datos personales

### Requirement: Mutaciones durables de usuarios y docentes

La API SHALL permitir crear y editar personas con su cuenta, activar o desactivar cuentas, administrar membresías de cualquier rol de sistema con el ámbito exigido y mantener las designaciones vigentes en una única operación atómica por persona, con `personas.administrar`. Las validaciones de unicidad (UPN, documento por dígitos, legajo), referencia, ámbito, repetición, rol obligatorio, asignaciones obligatorias para Docente y Jefe de Cátedra, y versión MUST ejecutarse de forma autoritativa antes de confirmar la operación.

#### Scenario: Mutación válida

- **GIVEN** datos válidos, membresías explícitas y un actor autorizado
- **WHEN** crea o modifica una persona
- **THEN** el cambio se confirma atómicamente y una consulta posterior devuelve el nuevo estado

#### Scenario: Conflicto de unicidad

- **GIVEN** una UPN, documento o legajo ya asignado a otro registro
- **WHEN** el operador intenta guardar el conflicto
- **THEN** la API MUST rechazarlo con un código de conflicto identificable y no MUST aplicar cambios parciales

#### Scenario: Versión obsoleta

- **GIVEN** la persona fue modificada después de que el operador abrió el formulario
- **WHEN** se envía una versión obsoleta
- **THEN** la API responde `409` con `concurrency-conflict` y no modifica datos ni membresías

#### Scenario: Ámbito de rol inválido

- **GIVEN** un rol que exige ámbito de materia o carrera
- **WHEN** se intenta asignar sin el ámbito requerido, con uno incompatible o con una asignación repetida
- **THEN** la API MUST rechazar la asignación sin alterar las membresías existentes

#### Scenario: Persona sin rol

- **WHEN** se intenta guardar una persona sin membresías
- **THEN** la API MUST rechazarlo con un error de validación sobre `membresias`
