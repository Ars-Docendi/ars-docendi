# administracion-identidad-api Specification

## Purpose

Expone una superficie HTTP persistente y autorizada para que las pantallas administrativas operen sobre la identidad canónica, sus catálogos y las designaciones docentes vigentes.

## Requirements

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

### Requirement: Gestión durable de roles y permisos

La API SHALL permitir listar roles activos, crear roles personalizados, renombrar roles personalizados conservando su código, reemplazar los permisos de cualquier rol activo y eliminar lógicamente roles personalizados. La creación podrá copiar los permisos vigentes de un rol base activo. Los roles de sistema MUST conservar inmutables su código, nombre, descripción, ámbito, indicador de sistema y estado; sus permisos sí MUST poder reemplazarse. Ninguna mutación de identidad o ciclo de vida de un rol de sistema SHALL afectar el rol.

#### Scenario: Crear un rol a partir de otro

- **GIVEN** un actor con `roles.administrar`, datos válidos y un rol base activo
- **WHEN** crea un rol personalizado utilizando el rol base
- **THEN** el nuevo rol se persiste con una copia de los permisos vigentes del rol base

#### Scenario: Renombrar un rol personalizado

- **GIVEN** un rol personalizado activo y un nombre único válido
- **WHEN** el operador modifica su nombre
- **THEN** la API actualiza el nombre, conserva el código estable del rol y la sesión puede seguir resolviendo sus permisos

#### Scenario: Reemplazar permisos de un rol institucional

- **GIVEN** un rol de sistema activo, un actor con `roles.gestionar_membresia` y un conjunto válido de permisos
- **WHEN** el operador guarda su membresía
- **THEN** la API reemplaza atómicamente sus permisos sin cambiar sus datos de identidad ni su ciclo de vida

#### Scenario: Reemplazar permisos de un rol personalizado

- **GIVEN** un rol personalizado activo y un conjunto válido de identificadores de permiso
- **WHEN** el operador guarda su membresía
- **THEN** la API reemplaza atómicamente el conjunto y una consulta posterior devuelve exactamente esos permisos

#### Scenario: Reemplazar membresía de permisos

- **GIVEN** un rol editable y un conjunto válido de identificadores de permiso
- **WHEN** el operador guarda su membresía
- **THEN** la API reemplaza atómicamente el conjunto y una consulta posterior devuelve exactamente esos permisos

#### Scenario: Intentar mutar la identidad de un rol de sistema

- **GIVEN** un rol con `es_sistema = true`
- **WHEN** se intenta cambiar su código, nombre, descripción, ámbito, indicador de sistema o estado
- **THEN** la API rechaza la operación sin modificar el rol

#### Scenario: Modificar un rol protegido

- **GIVEN** un rol de sistema protegido
- **WHEN** se intenta una mutación no admitida por el modelo
- **THEN** la API MUST rechazarla sin modificar el rol

#### Scenario: Referencia inexistente o inactiva

- **GIVEN** un identificador de rol inexistente o inactivo
- **WHEN** se intenta asignarlo, usarlo como base o modificar sus permisos
- **THEN** la API responde recurso no encontrado o conflicto según corresponda y no aplica cambios parciales

#### Scenario: Concurrencia al modificar un rol

- **GIVEN** un rol modificado por otra sesión
- **WHEN** se envía una mutación con una versión anterior
- **THEN** la API responde conflicto y conserva la última versión persistida

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
