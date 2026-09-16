## MODIFIED Requirements

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
