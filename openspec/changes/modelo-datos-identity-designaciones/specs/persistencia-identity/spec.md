## ADDED Requirements

### Requirement: Persona como entidad canónica, independiente de la cuenta

El sistema SHALL persistir a las personas en `identity.personas` como entidad canónica, separada de la cuenta de autenticación `identity.users`. Una persona MUST poder existir sin cuenta de Azure AD asociada. `identity.users` SHALL llevar `persona_id` nullable, y `identity.personas.legajo` SHALL ser nullable para admitir a una persona que todavía no lo tiene asignado. `documento` SHALL ser único entre las personas.

#### Scenario: Alta de una persona sin cuenta ni legajo

- **GIVEN** un pedido de designación de novedad "Alta" sobre un docente que nunca se autenticó en el sistema
- **WHEN** el sistema registra a esa persona
- **THEN** la fila se persiste en `identity.personas` con `legajo` en NULL y sin ninguna fila asociada en `identity.users`

#### Scenario: El primer login vincula la cuenta a la persona

- **GIVEN** una persona ya registrada en `identity.personas`
- **WHEN** esa persona se autentica por primera vez vía Azure AD
- **THEN** el sistema MUST crear o actualizar su fila en `identity.users` fijando `persona_id` hacia la persona existente, sin duplicar la persona

#### Scenario: Documento duplicado es rechazado

- **GIVEN** una persona ya registrada con un documento dado
- **WHEN** se intenta registrar otra persona con el mismo documento
- **THEN** la base de datos MUST rechazar la operación por violación de unicidad

#### Scenario: La PII de la persona queda auditada

- **WHEN** se crea, modifica o elimina una fila de `identity.personas`
- **THEN** `audit.change_log` MUST registrar el evento con su `changed_by`, `changed_at` y `changed_columns`

### Requirement: Roles creables con scope, y protección del catálogo de sistema

El sistema SHALL permitir crear roles nuevos en `identity.roles` además de los 7 roles de sistema. Todo rol SHALL declarar un `scope` no nulo dentro de `global`, `materia` o `carrera`, y un `code` único. Los roles de sistema SHALL identificarse con `es_sistema = TRUE` y MUST estar protegidos: su `code` y su `scope` son inmutables y su borrado se deniega. `nombre` y `descripcion` SHALL ser editables en todos los roles, incluidos los de sistema.

#### Scenario: Creación de un rol nuevo con scope

- **GIVEN** un operador de Secretaría en la pantalla de roles
- **WHEN** crea un rol nuevo declarando su scope
- **THEN** la fila se persiste con `es_sistema = FALSE` y queda disponible para asignarse a usuarios

#### Scenario: El code de un rol de sistema es inmutable

- **GIVEN** un rol con `es_sistema = TRUE`
- **WHEN** se intenta modificar su `code` o su `scope`
- **THEN** el trigger de protección MUST rechazar la operación

#### Scenario: Un rol de sistema no se puede borrar

- **GIVEN** un rol con `es_sistema = TRUE`
- **WHEN** se intenta eliminarlo
- **THEN** el trigger de protección MUST denegar el DELETE

#### Scenario: El nombre de un rol de sistema sí se puede editar

- **GIVEN** un rol con `es_sistema = TRUE`
- **WHEN** un operador edita su `nombre` o su `descripcion`
- **THEN** la operación MUST completarse correctamente

#### Scenario: Un rol sin scope es rechazado

- **WHEN** se intenta crear un rol sin `scope`, o con un scope fuera de `global`/`materia`/`carrera`
- **THEN** la base de datos MUST rechazar la operación

### Requirement: El scope declarado por el rol gobierna la asignación a usuarios

El sistema SHALL validar que toda fila de `identity.user_roles` sea coherente con el `scope` declarado por su rol: un rol `global` MUST tener `materia_id` y `carrera_id` en NULL; un rol `materia` MUST tener ambos presentes; un rol `carrera` MUST tener `carrera_id` presente y `materia_id` en NULL. Esta validación SHALL aplicarse igual a los roles creados por el operador que a los de sistema.

#### Scenario: Asignación de un rol de materia sin carrera

- **GIVEN** un rol con `scope = 'materia'`
- **WHEN** se intenta asignarlo a un usuario sin `carrera_id`
- **THEN** el sistema MUST rechazar la asignación

#### Scenario: Asignación de un rol global con ámbito

- **GIVEN** un rol con `scope = 'global'`
- **WHEN** se intenta asignarlo a un usuario con `materia_id` o `carrera_id` cargados
- **THEN** el sistema MUST rechazar la asignación

#### Scenario: Un rol creado por el operador respeta su propio scope

- **GIVEN** un rol con `es_sistema = FALSE` y `scope = 'carrera'`
- **WHEN** se lo asigna a un usuario con `carrera_id` y sin `materia_id`
- **THEN** la asignación MUST aceptarse, con la misma validación que un rol de sistema

#### Scenario: Revocar y volver a otorgar la misma asignación

- **GIVEN** una asignación de rol previamente revocada (`deleted_at` no nulo)
- **WHEN** se otorga nuevamente el mismo `(usuario, rol, ámbito)`
- **THEN** la operación MUST aceptarse, porque el índice de unicidad solo alcanza a las asignaciones vivas

### Requirement: Permisos como catálogo cerrado con membresía editable

El sistema SHALL persistir los permisos en `identity.permisos` como catálogo cerrado, identificados por un `code` único que el backend consume en sus checks de autorización. El sistema NO MUST ofrecer la creación de permisos desde ninguna superficie de usuario. La membresía `identity.rol_permisos` SHALL ser editable: un operador autorizado puede otorgar y revocar permisos a cualquier rol.

#### Scenario: Otorgar un permiso a un rol

- **GIVEN** un rol y un permiso del catálogo
- **WHEN** un operador autorizado otorga el permiso al rol
- **THEN** la fila se persiste en `identity.rol_permisos` y queda registrada en `audit.change_log`

#### Scenario: Revocar un permiso de un rol

- **GIVEN** un rol que tiene otorgado un permiso
- **WHEN** un operador autorizado lo revoca
- **THEN** la membresía deja de existir y el evento queda registrado en `audit.change_log`

#### Scenario: La misma membresía no se duplica

- **GIVEN** un rol que ya tiene otorgado un permiso
- **WHEN** se intenta otorgar el mismo permiso otra vez
- **THEN** la base de datos MUST rechazar la fila duplicada

### Requirement: Un rol creado por el operador no participa del circuito de aprobación

El circuito de aprobación de designaciones SHALL resolver la correspondencia etapa → rol revisor únicamente contra los roles con `es_sistema = TRUE`, identificados por su `code`. Un rol con `es_sistema = FALSE` MUST no habilitar a su portador a aceptar, rechazar ni devolver pedidos en ninguna etapa. La superficie de gestión de roles MUST comunicar esta limitación al crear un rol, para no aparentar una capacidad inexistente (invariante #7).

#### Scenario: Un rol creado por el operador no puede actuar sobre un pedido

- **GIVEN** un usuario cuyo único rol es uno creado por el operador (`es_sistema = FALSE`)
- **WHEN** intenta aceptar, rechazar o devolver un pedido en cualquier etapa
- **THEN** el sistema MUST denegar la acción

#### Scenario: La pantalla de roles advierte la limitación

- **WHEN** un operador crea un rol nuevo desde la superficie de gestión de roles
- **THEN** la interfaz MUST indicarle que ese rol no participa del circuito de aprobación de designaciones

### Requirement: Los schemas identity y audit viven en ArsDocendi.Shared

El sistema SHALL alojar los schemas `identity` y `audit` dentro de `ArsDocendi.Shared`, que ya hospeda `ICurrentUser`, `AuditDbConnectionInterceptor` e `IMigradorModulo`. `ArsDocendi.Shared` SHALL exponer una implementación de `IMigradorModulo` para que el arranque `--migrate` del Host aplique estas migraciones junto con las de los módulos. Esta es la **única** I/O admitida en `ArsDocendi.Shared`: cualquier otra I/O, estado mutable o lógica de dominio MUST NOT incorporarse a ese proyecto. El invariante #4 SHALL reflejar esta excepción acotada en todos los documentos que lo enuncian.

#### Scenario: El arranque de migraciones cubre identity y audit

- **GIVEN** una base de datos limpia
- **WHEN** el Host arranca con el argumento `--migrate`
- **THEN** los schemas `identity` y `audit` quedan creados junto con los de los módulos, y el proceso termina con exit 0 sin levantar el web server

#### Scenario: La excepción del invariante #4 está acotada a identity y audit

- **WHEN** se inspecciona `ArsDocendi.Shared`
- **THEN** su única dependencia de I/O MUST ser la persistencia de `identity` y `audit`, sin ninguna otra fuente de I/O, estado mutable ni lógica de dominio

#### Scenario: El invariante enmendado está enunciado de forma consistente

- **WHEN** se revisan `CLAUDE.md`, `openspec/config.yaml` y `docs/quality/golden-principles.md`
- **THEN** los tres MUST enunciar la misma excepción acotada, sin que quede ninguna copia con la redacción anterior

#### Scenario: Las migraciones son idempotentes

- **GIVEN** una base de datos que ya tiene aplicadas todas las migraciones
- **WHEN** el Host vuelve a arrancar con `--migrate`
- **THEN** el proceso MUST terminar con exit 0 sin alterar el estado de la base

### Requirement: Los módulos leen identity pero no lo escriben

Los módulos de negocio SHALL leer `identity` para resolver autorización, y MUST NOT escribir sobre `personas`, `roles`, `permisos` ni `rol_permisos`. Esa escritura SHALL ser exclusiva de la superficie de administración. Esta restricción NO está cubierta por el invariante #1 —no es una relación cross-module, porque todos los módulos referencian `ArsDocendi.Shared` legítimamente— y por eso SHALL quedar enunciada como corolario del invariante #4.

#### Scenario: Un módulo resuelve autorización leyendo identity

- **GIVEN** un módulo que necesita verificar si el actor tiene un rol sobre una materia
- **WHEN** ejecuta la consulta de autorización
- **THEN** la lectura MUST estar permitida sin intermediar Contracts

#### Scenario: Un módulo no escribe sobre las tablas de identidad

- **WHEN** se revisa el código de cualquier `Modules.*`
- **THEN** MUST no existir ninguna escritura sobre `personas`, `roles`, `permisos` ni `rol_permisos`
