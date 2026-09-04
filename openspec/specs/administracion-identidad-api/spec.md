# administracion-identidad-api Specification

## Purpose

Expone una superficie HTTP persistente y autorizada para que las pantallas administrativas operen sobre la identidad canónica, sus catálogos y las designaciones docentes vigentes.

## Requirements

### Requirement: Consultas administrativas de identidad

La API SHALL permitir listar y obtener usuarios, docentes, roles, permisos, carreras, materias y cargos necesarios para las pantallas administrativas. Las respuestas MUST utilizar identificadores canónicos y representar roles, ámbitos, estado y asignaciones vigentes.

#### Scenario: Listado autorizado

- **GIVEN** un actor con permiso de administración de identidad
- **WHEN** consulta uno de los listados administrativos
- **THEN** la API responde con los registros persistidos y sus relaciones necesarias para renderizar la pantalla

#### Scenario: Consulta sin autorización

- **GIVEN** un actor sin el permiso correspondiente
- **WHEN** consulta una superficie administrativa
- **THEN** la API MUST denegar la operación sin exponer los datos

### Requirement: Mutaciones durables de usuarios y docentes

La API SHALL permitir crear y editar personas y cuentas, activar o desactivar usuarios, administrar sus roles con el ámbito exigido y mantener las designaciones docentes vigentes. Las validaciones de unicidad, referencia y ámbito MUST ejecutarse de forma autoritativa antes de confirmar la operación.

#### Scenario: Mutación válida

- **GIVEN** datos válidos y un actor autorizado
- **WHEN** crea o modifica un usuario o docente
- **THEN** el cambio se confirma atómicamente y una consulta posterior devuelve el nuevo estado

#### Scenario: Conflicto de unicidad

- **GIVEN** una UPN o documento ya asignado a otro registro
- **WHEN** el operador intenta guardar el conflicto
- **THEN** la API MUST rechazarlo con un error de validación identificable y no MUST aplicar cambios parciales

#### Scenario: Ámbito de rol inválido

- **GIVEN** un rol que exige ámbito de materia o carrera
- **WHEN** se intenta asignar sin el ámbito requerido o con uno incompatible
- **THEN** la API MUST rechazar la asignación sin alterar las membresías existentes

### Requirement: Gestión durable de roles y permisos

La API SHALL permitir listar, crear y editar roles no protegidos y reemplazar su conjunto de permisos. Los roles y permisos de sistema MUST respetar las protecciones definidas por el modelo de identidad.

#### Scenario: Crear un rol a partir de otro

- **GIVEN** un actor autorizado, datos válidos y un rol base
- **WHEN** crea un rol utilizando el rol base
- **THEN** el nuevo rol se persiste con una copia de los permisos vigentes del rol base

#### Scenario: Reemplazar membresía de permisos

- **GIVEN** un rol editable y un conjunto válido de identificadores de permiso
- **WHEN** el operador guarda su membresía
- **THEN** la API reemplaza atómicamente el conjunto y una consulta posterior devuelve exactamente esos permisos

#### Scenario: Modificar un rol protegido

- **GIVEN** un rol de sistema protegido
- **WHEN** se intenta una mutación no admitida por el modelo
- **THEN** la API MUST rechazarla sin modificar el rol

### Requirement: Errores HTTP consistentes

Las operaciones administrativas MUST distinguir autenticación, autorización, recurso inexistente, validación y conflicto mediante respuestas HTTP estables con un cuerpo de error consumible por formularios.

#### Scenario: Error de validación en formulario

- **GIVEN** una solicitud autenticada con datos inválidos
- **WHEN** la API rechaza la operación
- **THEN** responde con un código y detalle que permiten asociar el error general o por campo sin depender del texto interno de una excepción
