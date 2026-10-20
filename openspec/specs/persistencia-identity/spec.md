# persistencia-identity Specification

## Purpose

Definir la persistencia canónica de personas, usuarios, roles y permisos, junto con sus fronteras de lectura, escritura y auditoría.

## Requirements

### Requirement: Persona como entidad canónica, independiente de la cuenta

El sistema SHALL persistir a las personas en `identity.personas` como entidad canónica, separada de la cuenta de autenticación `identity.users`. Una persona MUST poder existir sin cuenta de Azure AD asociada. `identity.users` SHALL llevar `persona_id` nullable, y `identity.personas.legajo` SHALL ser nullable para admitir a una persona que todavía no lo tiene asignado. `documento` SHALL ser único entre las personas. El registro de una persona provocado por un Alta MUST no crear automáticamente una cuenta de autenticación ni exigir UPN. `identity.users.azure_oid` y `identity.users.azure_tid` SHALL permanecer vacíos hasta el primer ingreso con Microsoft: dar de alta un usuario MUST NOT generar un identificador de cuenta Microsoft, y el ingreso MUST NOT crear usuarios ni personas.

#### Scenario: Alta de una persona sin cuenta ni legajo

- **GIVEN** un pedido de designación de novedad "Alta" sobre una persona cuyo documento no existe
- **WHEN** el sistema registra los datos del Alta
- **THEN** persiste una fila en `identity.personas` con el documento, nombre y apellido recibidos, `legajo` en NULL y sin ninguna fila asociada en `identity.users`

#### Scenario: El Alta no provisiona una cuenta Azure AD

- **GIVEN** una persona registrada por un Alta válida
- **WHEN** finaliza el guardado o envío del pedido
- **THEN** el sistema MUST no crear usuario, UPN, contraseña ni invitación automática de Azure AD

#### Scenario: El alta administrativa no genera la cuenta Microsoft

- **WHEN** la administración da de alta un usuario
- **THEN** su fila en `identity.users` queda con `azure_oid` y `azure_tid` vacíos hasta su primer ingreso

#### Scenario: El primer login vincula la cuenta a la persona

- **GIVEN** una persona con un usuario dado de alta por la administración y sin cuenta Microsoft vinculada
- **WHEN** esa persona ingresa por primera vez con una cuenta Microsoft cuyo mail verificado coincide con su UPN
- **THEN** el sistema MUST registrar `azure_oid` y `azure_tid` en esa fila existente de `identity.users`, sin crear usuarios, sin duplicar la persona y sin modificar `persona_id`

#### Scenario: Ingreso de una persona sin usuario

- **GIVEN** una persona registrada en `identity.personas` sin fila asociada en `identity.users`
- **WHEN** ingresa con una cuenta Microsoft cuyo mail verificado no corresponde a ningún usuario
- **THEN** el sistema MUST denegar el acceso sin crear la cuenta de autenticación

#### Scenario: Documento duplicado es rechazado

- **GIVEN** una persona ya registrada con un documento dado
- **WHEN** se intenta registrar otra persona con el mismo documento
- **THEN** la base de datos MUST rechazar la operación por violación de unicidad y el pedido MUST no quedar persistido de forma parcial

#### Scenario: La PII de la persona queda auditada

- **WHEN** se crea, modifica o elimina una fila de `identity.personas`
- **THEN** `audit.change_log` MUST registrar el evento con su `changed_by`, `changed_at` y `changed_columns`

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

- **WHEN** se revisan `AGENTS.md`, `openspec/config.yaml` y `docs/quality/golden-principles.md`
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

### Requirement: UPN normalizado único para todos los escritores

Los UPN SHALL compararse por una normalización única de espacios exteriores y mayúsculas/minúsculas, común a altas, ediciones e ingreso. La base MUST impedir cuentas equivalentes bajo esa normalización. El upgrade MUST bloquear duplicados existentes sin fusionar cuentas, permisos o referencias automáticamente.

#### Scenario: Duplicado desde SQL

- **GIVEN** una cuenta con UPN normalizado existente
- **WHEN** otro escritor intenta insertar la variante con mayúsculas o espacios exteriores
- **THEN** la base rechaza el duplicado y el ingreso conserva una coincidencia única

#### Scenario: Cuenta existente conserva identidad

- **GIVEN** una cuenta sin duplicados normalizados con roles y persona asociados
- **WHEN** se activa la garantía
- **THEN** conserva ID, persona, roles y vínculo Microsoft sin recreación de identidad

### Requirement: Nombre único de rol activo bajo concurrencia

Los nombres de roles activos MUST ser únicos bajo trim y comparación sin distinguir mayúsculas/minúsculas. La garantía SHALL alcanzar altas y renombres concurrentes de filas distintas; un conflicto MUST devolver error de dominio estable y no 500. Desactivar un rol SHALL conservar su identidad e historial.

#### Scenario: Dos renombres simultáneos

- **GIVEN** dos roles activos distintos y un nombre libre
- **WHEN** ambos se renombran concurrentemente a ese nombre equivalente
- **THEN** solo uno confirma y el otro recibe conflicto sin cambiar su código ni permisos

### Requirement: Consultas de identidad selectivas

Las consultas para actor, usuarios/personas por ID o lote y candidatos SHALL materializar solo los resultados necesarios al caso. Filtrado, jerarquía y límites MUST preceder a materialización para búsquedas acotadas, preservando autorización, términos sin distinguir tildes y resolución histórica de identidades inactivas.

#### Scenario: Resolver actor de Portal

- **GIVEN** una cuenta autenticada entre muchas cuentas
- **WHEN** Portal resuelve su persona
- **THEN** consulta esa identidad sin materializar el directorio completo

#### Scenario: Personas de un conjunto de tareas

- **GIVEN** un lote de IDs de actores/responsables visibles
- **WHEN** se resuelven sus etiquetas
- **THEN** no se carga cada persona individualmente ni el directorio global

### Requirement: Roles personalizados y metadatos de sistema protegidos

El sistema SHALL permitir roles personalizados con `scope` válido y código único. Los roles de sistema SHALL tener `es_sistema = TRUE` y metadatos inmutables: código, ámbito, nombre, descripción, actividad y marca de sistema; su borrado MUST denegarse. Los personalizados SHALL permitir edición autorizada de nombre/descripción y ámbito solo si no tienen asignaciones vivas. La membresía de permisos conserva su superficie autorizada.

#### Scenario: Creación de un rol nuevo con scope

- **GIVEN** un operador autorizado
- **WHEN** crea un rol con ámbito válido
- **THEN** se persiste como personalizado y queda disponible para asignación

#### Scenario: El code de un rol de sistema es inmutable

- **GIVEN** un rol de sistema
- **WHEN** se intenta modificar código o ámbito
- **THEN** la base rechaza la operación

#### Scenario: Un rol de sistema no se puede borrar

- **GIVEN** un rol de sistema
- **WHEN** se intenta borrarlo físicamente
- **THEN** la base rechaza el borrado

#### Scenario: Los metadatos de un rol de sistema no se editan

- **GIVEN** un rol de sistema
- **WHEN** se intenta editar nombre o descripción
- **THEN** se rechaza conforme a la protección vigente y a la capability editar-rol

#### Scenario: Un rol sin scope es rechazado

- **GIVEN** una creación de rol
- **WHEN** se omite el ámbito o se usa uno fuera del catálogo permitido
- **THEN** la base rechaza la operación

### Requirement: Asignaciones coherentes con el ámbito vigente

Toda asignación SHALL ser coherente: global sin materia/carrera; Docente con ambas; Jefe de Cátedra y personalizado de materia con materia y sin carrera; Coordinador y personalizado de carrera con carrera y sin materia. Las referencias SHALL ser canónicas. Cambiar ámbito con asignaciones vivas MUST rechazarse; las revocadas SHALL conservar contexto histórico y poder revocarse aun si existe incoherencia legada, sin habilitar permisos nuevos.

#### Scenario: Asignación de un rol de materia sin materia

- **GIVEN** un rol personalizado de ámbito materia
- **WHEN** se intenta asignarlo sin materia
- **THEN** se rechaza; una materia válida sin carrera sí satisface su ámbito

#### Scenario: Asignación de Docente sin carrera

- **GIVEN** el rol Docente
- **WHEN** se asigna materia sin carrera
- **THEN** se rechaza por ausencia del par requerido

#### Scenario: Jefe de Cátedra con una sola membresía por materia

- **GIVEN** una materia compartida por dos carreras
- **WHEN** se asigna Jefe de Cátedra sobre ella
- **THEN** existe una membresía con materia y sin carrera, válida en ambas

#### Scenario: Asignación de un rol global con ámbito

- **GIVEN** un rol global
- **WHEN** se asigna materia o carrera
- **THEN** se rechaza

#### Scenario: Un rol creado por el operador respeta su propio scope

- **GIVEN** un rol personalizado de carrera
- **WHEN** se asigna carrera válida sin materia
- **THEN** se acepta con la misma integridad del ámbito correspondiente

#### Scenario: Revocar y volver a otorgar la misma asignación

- **GIVEN** una asignación revocada
- **WHEN** se vuelve a otorgar el mismo usuario, rol y ámbito
- **THEN** se permite sin duplicar asignaciones vivas

#### Scenario: Cambiar scope con asignaciones vigentes

- **GIVEN** un rol personalizado global con una asignación viva
- **WHEN** se intenta cambiarlo a materia
- **THEN** API y base rechazan el cambio y mantienen coherencia y permisos anteriores

#### Scenario: Carrera de asignación contra cambio de ámbito

- **GIVEN** una asignación y un cambio de ámbito concurrentes
- **WHEN** ambos intentan confirmar
- **THEN** se serializan o uno falla sin dejar asignaciones vivas incoherentes
