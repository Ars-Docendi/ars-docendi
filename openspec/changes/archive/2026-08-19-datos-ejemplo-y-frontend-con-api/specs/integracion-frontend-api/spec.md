## Purpose

Establece que los datos de negocio visibles y mutables del frontend provienen de la API persistente, con estados remotos observables y sin fuentes mock en el bundle de runtime.

## ADDED Requirements

### Requirement: API como fuente única de datos de negocio en runtime

Las features `usuarios`, `docentes`, `roles`, `membresia-roles` y `designaciones` MUST obtener y modificar registros mediante la API. El runtime MUST no inicializar esos registros desde arrays TypeScript, stores en memoria ni `localStorage`.

#### Scenario: Recarga de una pantalla

- **GIVEN** un cambio confirmado mediante una pantalla migrada
- **WHEN** el usuario recarga la aplicación o abre otra sesión
- **THEN** la pantalla obtiene desde la API y muestra el estado persistido

#### Scenario: Bundle de runtime

- **GIVEN** una compilación de la aplicación
- **WHEN** se inspeccionan los imports alcanzables desde runtime
- **THEN** no existen seeds ni stores mock de registros de negocio

#### Scenario: Fixtures de tests

- **GIVEN** una prueba automatizada aislada
- **WHEN** necesita datos controlados o simular HTTP
- **THEN** MAY utilizar fixtures ubicadas exclusivamente en código de tests sin incorporarlas al runtime

### Requirement: Estados de consulta remota

Cada pantalla que consulta la API SHALL representar explícitamente carga inicial, resultado vacío, éxito y error recuperable, sin presentar datos obsoletos como si fueran una respuesta exitosa.

#### Scenario: Carga inicial

- **GIVEN** una consulta todavía pendiente
- **WHEN** se renderiza la pantalla
- **THEN** se presenta un estado de carga accesible y no una tabla poblada con datos fallback

#### Scenario: Respuesta vacía

- **GIVEN** una respuesta exitosa sin registros
- **WHEN** finaliza la consulta
- **THEN** se presenta un estado vacío diferenciado de carga y error

#### Scenario: Error recuperable

- **GIVEN** una consulta fallida por un error de red o servidor
- **WHEN** la pantalla recibe el fallo
- **THEN** muestra un mensaje accionable y permite reintentar la consulta

### Requirement: Coherencia después de mutaciones

Luego de una mutación exitosa, el frontend MUST invalidar o actualizar las consultas afectadas; si la mutación falla, MUST conservar el último estado confirmado y mostrar el error retornado.

#### Scenario: Mutación exitosa

- **GIVEN** una lista cargada y una edición confirmada por la API
- **WHEN** termina la mutación
- **THEN** las vistas afectadas convergen al estado persistido sin requerir una recarga manual

#### Scenario: Mutación rechazada

- **GIVEN** una operación que la API rechaza
- **WHEN** el frontend recibe el error
- **THEN** no presenta el cambio como confirmado y muestra el motivo consumible al operador
