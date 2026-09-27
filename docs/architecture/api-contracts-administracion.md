# Contratos API — Administración y desarrollo

Complementa [api-contracts.md](./api-contracts.md). Todas las rutas administrativas requieren autenticación y responden Problem Details; la única excepción es el catálogo de identidades de desarrollo, que sólo existe cuando el Host habilita explícitamente ese esquema fuera de producción.

## Permisos

| Recurso               | Lectura                                    | Escritura                   |
| --------------------- | ------------------------------------------ | --------------------------- |
| Usuarios              | `usuarios.ver`                             | `usuarios.administrar`      |
| Docentes              | `docentes.ver` o JdC con ámbito de materia | `usuarios.administrar`      |
| Roles                 | `roles.ver`                                | `roles.administrar`         |
| Membresía de permisos | `roles.ver`                                | `roles.gestionar_membresia` |
| Revisión de pedidos   | `designaciones.revisar`                    | —                           |
| Estado del sistema    | `sistema.estado.ver`                       | —                           |
| Auditoría             | `auditoria.ver`                            | —                           |
| Catálogos             | permiso de lectura del recurso consumidor  | —                           |

Docentes usa `docentes.ver` para lectura global. La vista de Jefe de Cátedra es una excepción de lectura acotada: la API deriva el ámbito desde sus asignaciones vigentes en `identity.user_roles`, nunca desde parámetros del cliente, y no habilita la API de usuarios ni escrituras. `usuarios.ver` conserva acceso de lectura global por compatibilidad con la administración de identidad.

## DTOs

Los nombres JSON son `camelCase`. IDs y fechas se representan como UUID y `YYYY-MM-DD`.

```text
RolResumenDto          = { id, codigo, nombre }
AsignacionRolDto       = { id, rolId, codigo, nombre, ambito, materiaId?, carreraId? }
PerfilDocenteDto       = { esDocente, cantidadMaterias }
UsuarioResumenDto      = { id, personaId, nombre, apellido, documento, legajo?, cuil?,
                           fechaNacimiento?, telefono?, upn, activo, roles[], membresias[],
                           perfilDocente }
GuardarUsuarioDto      = { nombre, apellido, documento, legajo?, cuil?, fechaNacimiento?,
                           telefono?, upn, membresias[{ rolId, materiaId?, carreraId? }], version? }
DesignacionVigenteDto  = { id, materia{id,codigo,nombre}, cargo{id,codigo,nombre,abreviatura},
                           dedicacion?, dedicacionId?, horas, horasInvestigacion?, horasExternas?, vigenteDesde }
DocenteResumenDto      = { personaId, usuarioId?, datosPersona..., tieneCuenta, activo?,
                           roles[], membresias[], designaciones[] }
GuardarDocenteDto      = { personaId? | personaNueva, datosPersona..., membresias[],
                           designaciones[], version? }
RolDto                 = { id, codigo, nombre, descripcion?, ambito, esSistema, activo, version, permisos[] }
CrearRolDto            = { nombre, descripcion?, ambito, rolBaseId? }
EditarRolDto           = { nombre, descripcion?, ambito, version }
EliminarRolDto         = { version }
PermisoDto             = { id, codigo, nombre, descripcion }
EstadoSistemaDto       = { estado: "disponible" | "no_disponible", comprobadoEn, duracionMs,
                           mantenimientoAsistente: "activo" | "inactivo" | "desconocido" }
CambioAuditoriaDto     = { campo, etiquetaCampo, valorAnterior?, valorNuevo?, oculto }
EventoAuditoriaDto     = { id: string, origen: "cambios" | "asistente", schema, tabla, rowPk, accion,
                           cambiadoEn, cambiadoPor?, requestId?, columnasCambiadas[],
                           cambios: CambioAuditoriaDto[], actor, tipoActor: "persona" | "proceso" |
                           "no_identificado", accionEtiqueta, modulo, objeto, resumen }
PaginaAuditoriaDto     = { elementos: EventoAuditoriaDto[], pagina, tamanoPagina, total,
                           parcial: boolean, fuentesNoDisponibles: string[] }
ReemplazarPermisosDto  = { permisoIds[], version }
CatalogoIdentityDto    = { roles[{ id, codigo, nombre, ambito, esSistema }], permisos[],
                           carreras[], materias[{ id, codigo, nombre, carreraId? }], personasElegibles[] }
```

Los roles personalizados conservan su `codigo` al renombrarse y exigen nombre único entre roles
activos. Un rol base debe estar activo y la copia de permisos ocurre sólo al crear el nuevo rol.
La baja de un rol personalizado es lógica (`activo = false`): conserva el registro, permisos y
asignaciones históricas, pero lo excluye de listados, asignaciones nuevas y roles base. Los roles de
sistema mantienen inmutables código, nombre, descripción, ámbito, marca y estado; sus permisos sí
pueden reemplazarse.

## Rutas de usuarios

| Método | Ruta                                           | Permiso                | Entrada / salida                                        |
| ------ | ---------------------------------------------- | ---------------------- | ------------------------------------------------------- |
| GET    | `/api/administracion/usuarios`                 | `usuarios.ver`         | `UsuarioResumenDto[]`                                   |
| GET    | `/api/administracion/usuarios/{id}`            | `usuarios.ver`         | `UsuarioResumenDto`                                     |
| POST   | `/api/administracion/usuarios`                 | `usuarios.administrar` | `GuardarUsuarioDto` → `201 UsuarioResumenDto`           |
| PUT    | `/api/administracion/usuarios/{id}`            | `usuarios.administrar` | `GuardarUsuarioDto` con `version` → `UsuarioResumenDto` |
| POST   | `/api/administracion/usuarios/{id}/activar`    | `usuarios.administrar` | `{ version }` → `UsuarioResumenDto`                     |
| POST   | `/api/administracion/usuarios/{id}/desactivar` | `usuarios.administrar` | `{ version }` → `UsuarioResumenDto`                     |

## Rutas de docentes y catálogos

| Método | Ruta                                                  | Permiso                      | Entrada / salida                                   |
| ------ | ----------------------------------------------------- | ---------------------------- | -------------------------------------------------- |
| GET    | `/api/administracion/docentes`                        | `usuarios.ver` o JdC acotado | `DocenteResumenDto[]`                              |
| GET    | `/api/administracion/docentes/{personaId}`            | `usuarios.ver` o JdC acotado | `DocenteResumenDto`                                |
| POST   | `/api/administracion/docentes`                        | `usuarios.administrar`       | `GuardarDocenteDto` → `201 DocenteResumenDto`      |
| PUT    | `/api/administracion/docentes/{personaId}`            | `usuarios.administrar`       | `GuardarDocenteDto` → `DocenteResumenDto`          |
| POST   | `/api/administracion/docentes/{personaId}/activar`    | `usuarios.administrar`       | `DocenteResumenDto`                                |
| POST   | `/api/administracion/docentes/{personaId}/desactivar` | `usuarios.administrar`       | `DocenteResumenDto`                                |
| GET    | `/api/administracion/docentes/catalogos`              | `usuarios.ver` o JdC acotado | materias, cargos y personas elegibles según ámbito |
| GET    | `/api/administracion/catalogos`                       | autenticado                  | `CatalogoIdentityDto` filtrado por permisos        |

Alta/edición docente es atómica desde la perspectiva HTTP. Si falla identity o el comando público de Designaciones, el servidor revierte o compensa la unidad completa y retorna Problem Details.

`roles` es un resumen único por `rolId`; no se repiten por cada materia. `membresias` conserva cada
asignación activa completa. Los payloads de escritura usan sólo IDs canónicos: las etiquetas visibles
de roles, carreras y materias no son identificadores. Un mismo usuario puede tener, por ejemplo,
`docente + Materia A` y `jefe_catedra + Materia B` sin combinaciones implícitas entre filas.
`perfilDocente` se compone con esas membresías y las designaciones vigentes de la misma `personaId`;
no es un campo persistido.

Las tablas administrativas navegan entre las fichas mediante `/usuarios?personaId={id}` y
`/docentes?personaId={id}`; abrir esos enlaces sólo selecciona el recurso existente y no crea registros.

## Rutas de roles

| Método | Ruta                                      | Permiso                     | Entrada / salida                           |
| ------ | ----------------------------------------- | --------------------------- | ------------------------------------------ |
| GET    | `/api/administracion/roles`               | `roles.ver`                 | `RolDto[]`                                 |
| GET    | `/api/administracion/roles/{id}`          | `roles.ver`                 | `RolDto`                                   |
| POST   | `/api/administracion/roles`               | `roles.administrar`         | `CrearRolDto` → `201 RolDto`               |
| PUT    | `/api/administracion/roles/{id}`          | `roles.administrar`         | `EditarRolDto` con `version` → `RolDto`    |
| DELETE | `/api/administracion/roles/{id}`          | `roles.administrar`         | `{ version }` → `204 No Content`           |
| GET    | `/api/administracion/roles/{id}/permisos` | `roles.ver`                 | `PermisoDto[]`                             |
| PUT    | `/api/administracion/roles/{id}/permisos` | `roles.gestionar_membresia` | `{ permisoIds, version }` → `PermisoDto[]` |
| GET    | `/api/administracion/permisos`            | `roles.ver`                 | catálogo cerrado `PermisoDto[]`            |

## Estado del sistema y auditoría

> Rediseñado por `sistema-seccion-unificada` (ARS-154/155/157): la vista de salud, el uso
> del asistente y la auditoría se unifican en la sección «Sistema» de un único frontend
> con tabs permission-gated; ver el design spec del producto para la UI. Esta sección
> describe únicamente el contrato HTTP.

| Método | Ruta                                 | Permiso              | Entrada / salida               |
| ------ | ------------------------------------ | -------------------- | ------------------------------ |
| GET    | `/api/administracion/sistema/estado` | `sistema.estado.ver` | `EstadoSistemaDto`             |
| GET    | `/api/administracion/auditoria`      | `auditoria.ver`      | filtros → `PaginaAuditoriaDto` |

### Estado del sistema

El estado de PostgreSQL se comprueba mediante `SELECT 1` con timeout de 3 segundos, igual que
antes. `mantenimientoAsistente` es nuevo (design.md D7): corre **concurrente** con la
comprobación de PostgreSQL, con su **propio** techo de 3 segundos, leído a través de
`Modules.Asistente.Contracts.IConsultaDeMantenimiento` — el Host nunca abre una conexión al
schema `asistente` para esto. Vale `"activo"` o `"inactivo"` según el interruptor de
mantenimiento del asistente, o `"desconocido"` ante cualquier falla o vencimiento de ese techo
propio (logueado como warning, nunca como error 500: una falla del asistente no puede tirar
abajo la comprobación de PostgreSQL). La respuesta nunca incluye la razón del mantenimiento ni
quién lo activó — esos datos siguen exclusivamente detrás de `asistente.consultar`
(`GET /api/asistente/capacidades`); `sistema.estado.ver` sólo ve el interruptor. El dashboard
combina este resultado con los pings HTTP existentes de Aulas, Tareas, Designaciones, Portal y
—desde este cambio— el ping anónimo del asistente (`GET /api/asistente/ping`); cada sonda se
muestra independientemente.

### Auditoría: feed unificado de dos fuentes

`GET /api/administracion/auditoria` deja de ser una consulta de sólo `audit.change_log`: fusiona
esa tabla con el rastro de administración del asistente
(`asistente.auditoria_administracion`, leído exclusivamente a través del contrato
`Modules.Asistente.Contracts.IConsultasDeAuditoriaDeAdministracion` — el Host jamás hace SQL
directo contra el schema `asistente`, ni siquiera de sólo lectura) en un único feed ordenado y
paginado. El orden total es `cambiadoEn` descendente; en un empate exacto de instante,
`origen: "cambios"` va antes que `origen: "asistente"`; y por último `id` numérico descendente
dentro de cada fuente. `total` suma el conteo de las dos fuentes. El rastro del asistente tiene
un cupo de lectura de 2000 filas por consulta (retención 365 días a ritmo humano de edición, así
que en la práctica nunca se alcanza); si se alcanza, o si esa fuente falla por cualquier otro
motivo, la página se sirve igual con `parcial: true` y `fuentesNoDisponibles: ["asistente"]`,
usando sólo `audit.change_log` — nunca falla la request completa por una falla del asistente.

**Parámetros**: `desde`, `hasta`, `accion` (`INSERT` | `UPDATE` | `DELETE`), `modulo` (`identity`
| `designaciones` | `portal` | `asistente`, o cualquier nombre de schema ≤ 63 caracteres como
fallback para módulos futuros), `tabla` (`schema.tabla` calificada o el nombre de tabla desnudo, ≤
127 caracteres), `rowPk` (≤ 200 caracteres), `cambiadoPor` (UUID, conservado por compatibilidad),
`q` (≤ 100 caracteres — **reemplaza** a `actor`, ver abajo), `pagina` (predeterminada 1),
`tamanoPagina` (predeterminado 50, máximo 100). `schema` y `actor` ya **no existen**: el frontend
es el único consumidor y el cambio va en el mismo diff. Cuando `modulo` selecciona una sola
fuente (`"asistente"`, o cualquier otro valor — que sólo puede vivir en `audit.change_log`), la
otra fuente ni se consulta: es a la vez una optimización de costo y un aislamiento de fallas
(si `modulo=portal`, una caída del asistente no afecta la respuesta en absoluto).

**Búsqueda (`q`)**: reemplaza a `actor` con alcance más amplio, pero es **estrictamente sobre
etiquetas, nunca sobre valores**. `q` se normaliza (recortado, minúsculas, sin acentos) y se
compara contra: la etiqueta de módulo (`identity`→«Identidad», etc.), la etiqueta de objeto
(`schema.tabla`→«Persona», «Rol», …), las etiquetas de los campos cambiados (para un `UPDATE`) o
de las claves del snapshot (para un `INSERT`/`DELETE`), el nombre `schema.tabla` y la clave de
fila mismos, y el actor tal como se muestra (incluyendo los literales «Proceso automático» /
«Actor no identificado»). Ninguna rama lee `old_row`/`new_row`/un valor mapeado, así que `q`
nunca es un oráculo para un dato enmascarado o sin clasificar — hay un test que sembra un valor
enmascarado y uno seguro y verifica que ninguno de los dos aparece en los resultados de una
búsqueda por ese valor. La comparación usa la extensión PostgreSQL `unaccent` más `ILIKE`
(`database/audit/002_audit_busqueda.sql`, aplicada por una migración de `IdentityDbContext` —
el Host no depende del orden de migración del asistente, que ya la requiere por su cuenta).

**`q` también encuentra por el sujeto humanizado (fix, 2026-09-27)**: como el objeto de
`identity.users`/`identity.personas`/`identity.user_roles` puede nombrar al sujeto afectado
(«Cuenta de usuario de {nombre}», «Persona {nombre}», «Roles de {nombre}»), `q` resuelve —en UNA
consulta aparte, nunca por fila— qué cuentas/personas tienen HOY un nombre que matchea, y agrega
tres ramas por sus **ids** (`identity.users`/`identity.personas` por su propio `rowPk`,
`identity.user_roles` por el `user_id` del snapshot, extraído con `->>` y casteado, nunca
comparado contra el texto de `q`). Repite la misma exclusión que ya usa la etiqueta: un evento
que cambió `display_name` (para `users`) o `nombre`/`apellido` (para `personas`) nunca es un hit
por nombre, aunque el nombre actual ya matchee — un renombre no se filtra por búsqueda tampoco.

**Actor y evidencia (`tipoActor`)**: person names se muestran en orden natural, «Nombre Apellido»
(no «Apellido, Nombre»), para el actor y para un eventual usuario afectado (design.md D6). Cuatro
casos, cada uno con su propio `tipoActor`:

| `changed_by` | `request_id` | Se muestra como         | `tipoActor`       |
| ------------ | ------------ | ----------------------- | ----------------- |
| resuelve     | cualquiera   | persona / display name  | `persona`         |
| no resuelve  | cualquiera   | «Actor no identificado» | `no_identificado` |
| nulo         | presente     | «Actor no identificado» | `no_identificado` |
| nulo         | nulo         | «Proceso automático»    | `proceso`         |

«Proceso automático» exige la **ausencia total** de contexto de solicitud (ni actor ni
`request_id`): sólo una migración, un seed o un proceso de fondo escriben así, nunca un request
HTTP con un usuario anónimo. Ese último caso —hubo un request pero su actor no se pudo
identificar— es indistinguible de un `changed_by` estampado sin cuenta que lo resuelva, y los
dos se muestran «Actor no identificado». Una sesión SQL directa sin las GUC de sesión (fuera de
la aplicación, ya prohibido por AGENTS.md) también cae en «Proceso automático»: es el único otro
escritor sin contexto de request, y queda documentado acá en vez de tratarse como un caso a
prevenir en código.

**Acción y resumen (`accionEtiqueta`, `resumen`)**: la acción se presenta como **Alta**, **Cambio**
o **Eliminación** — nunca «Actualización» ni «Eliminación física»; un `UPDATE` nunca puede leer
«Eliminación» porque sólo un `DELETE` físico la produce. El resumen sólo muestra valores en un
caso preciso: una `UPDATE` de **exactamente un** campo de valor seguro lee
`"{Objeto} #{rowPk}: {Campo} {antes} → {después}"` (el `#rowPk` sólo cuando `rowPk` es numérico,
para que una clave UUID no inunde la línea). Todo lo demás —un `INSERT`, un `DELETE`, más de un
campo cambiado, o cualquier campo enmascarado o sin clasificar— se queda en la forma genérica
`"{accionEtiqueta} de {objeto} · {campos}"`, sin valores. Los eventos del asistente siguen la
misma regla de enmascarado (`cupo`, `tope_mensual_usd` y `activo` son seguros; `razon` queda sin
clasificar y por lo tanto enmascarada) con sus propios resúmenes por tipo de acción
(`presupuesto.rol`, `presupuesto.usuario` — Alta si el cupo previo era nulo, si no Cambio —,
`tope_organizacional`, `mantenimiento.activar`/`desactivar`; un código de acción futuro no
mapeado cae en un «Cambio» genérico humanizado, nunca en un error).

**Eventos de identidad humanizados (design.md D5, «Humanized identity events»)**: un valor
booleano de un campo seguro (`activo`, `is_active`, `es_sistema`, …) se presenta como **«Sí»**/
**«No»**, nunca `true`/`false` — tanto en `resumen` como en cada `CambioAuditoriaDto.valorAnterior`/
`valorNuevo` del panel de detalle. Para `identity.users`, `identity.personas` e
`identity.user_roles`, `objeto` nombra al sujeto humano: «Cuenta de usuario de {nombre}»,
«Persona {nombre}» y, para una asignación de rol, «Roles de {nombre}». El nombre se resuelve de
las filas de identidad **actuales** — la misma resolución que ya usa el actor —, nunca del
snapshot del evento; si el propio evento cambió `nombre`, `apellido` o `display_name`, o la
búsqueda no encuentra a la persona, `objeto` se queda en su forma genérica sin nombre (`«Cuenta de
usuario»`, `«Persona»`, `«Asignación de rol»`) para que un cambio de nombre nunca se exponga como
diff. Un alta o baja de `identity.user_roles` además reemplaza el `resumen` genérico por «Rol
{rol} asignado a {nombre}» / «Rol {rol} quitado a {nombre}», con `{rol}` resuelto por `role_id`
contra `identity.roles` — **la misma regla cubre una `UPDATE` de `identity.user_roles` cuyas
columnas cambiadas incluyan `deleted_at`** (nulo → con valor lee «quitado a»; con valor → nulo
lee «asignado a»), porque la revocación real (`ServicioUsuarios.ReemplazarAsignaciones`) es un
soft-delete por `UPDATE`, nunca un `DELETE` físico; la acción mostrada (`accionEtiqueta`) sigue
siendo la real (`Cambio` para esa `UPDATE`), sólo el `resumen` cambia. El `user_id`/`role_id` del
snapshot son ids internos usados sólo para esa
búsqueda, nunca valores mostrados. La resolución de nombres de sujeto es **una sola consulta
batched por página** (los ids se juntan de toda la página antes de consultar), no una por fila.

**Formato de valores en `cambios` (fix del panel de detalle, 2026-09-27)**: un `null` de JSON
llega como `valorAnterior`/`valorNuevo` **`null`** (nunca el texto `"null"`); el panel lo muestra
como «—». Un campo TIMESTAMPTZ de valor seguro (`created_at`, `deleted_at`) se presenta en hora de
`America/Argentina/Buenos_Aires` como `d/m/aaaa HH:mm:ss`; un campo DATE de valor seguro
(`vigente_desde`, `vigente_hasta`) como `d/m/aaaa` — nunca el ISO/UTC crudo del snapshot; el mismo
valor ya formateado es el que reusa un resumen con valores. Un campo cuyo `valorAnterior` Y
`valorNuevo` son ambos `null`/ausentes en el snapshot crudo **no aparece** en `cambios` — ni
siquiera enmascarado — porque no aporta información (evaluado antes de enmascarar: el DTO de un
campo enmascarado siempre lleva `null`/`null` aunque el valor real no lo sea).

Cada consulta a `audit.change_log` tiene timeout de 5 segundos; el rastro del asistente no
declara uno propio explícito (lee como máximo 2000 filas pequeñas de una tabla propia). Fechas
invertidas, acción inválida, `q`/`tabla`/`modulo` fuera de longitud o límites de página fuera de
rango responden `400 validation`. La API es exclusivamente GET; no muta ni borra eventos de
ninguna fuente. La UI muestra fecha, usuario, acción, módulo y resumen; el objeto, clave de fila
y `requestId` quedan en el detalle (`requestId` siempre `null`/«—» para un evento del asistente:
ese rastro no registra ningún id de solicitud). No se exponen UPN/correo, `client_ip` ni
snapshots crudos de ninguna fuente. Los valores del detalle sólo se incluyen para campos
aprobados; PII, secretos y campos no clasificados se devuelven con `oculto: true` y valores
nulos.

## Desarrollo

`GET /api/desarrollo/identidades` devuelve únicamente roles y permisos activos:

```text
IdentidadDesarrolloDto = { usuarioId, nombreParaMostrar, upn, roles[
  { codigo, nombre, permisos[], materias[{id,codigo,nombre}], carreras[{id,codigo,nombre}] }
] }
```

El cliente envía `X-Dev-User-Id` y `X-Dev-Role-Code`. El handler valida usuario activo, marca de dataset sintético, asignación vigente y ámbito. No acepta ámbitos declarados por el cliente. En Production la ruta y el esquema no están registrados, por lo que el resultado es `404` aunque se envíen esos headers.

## Códigos de error

| Código                          | HTTP | Uso                                                               |
| ------------------------------- | ---- | ----------------------------------------------------------------- |
| `validation`                    | 400  | forma o campos inválidos; extensión `errors` por campo            |
| `not-authenticated`             | 401  | identidad ausente o inválida                                      |
| `forbidden`                     | 403  | permiso o ámbito insuficiente                                     |
| `resource-not-found`            | 404  | recurso inexistente o no visible                                  |
| `identity-upn-conflict`         | 409  | UPN usada por otra cuenta                                         |
| `identity-document-conflict`    | 409  | documento usado por otra persona                                  |
| `identity-file-number-conflict` | 409  | legajo usado por otra persona                                     |
| `identity-role-scope-conflict`  | 422  | ámbito incompatible con el rol                                    |
| `identity-protected-role`       | 422  | mutación prohibida de identidad o ciclo de vida de rol de sistema |
| `identity-role-name-conflict`   | 409  | el nombre ya pertenece a otro rol activo                          |
| `identity-permission-invalid`   | 422  | permiso inexistente o membresía duplicada                         |
| `identity-role-code-conflict`   | 409  | el código normalizado del rol ya existe                           |
| `concurrency-conflict`          | 409  | el recurso cambió desde su lectura                                |

POST/PUT administrativos representan reemplazos o comandos naturalmente repetibles, pero no prometen replay de respuesta. `Idempotency-Key` es obligatorio sólo en transiciones de dominio que lo declaran en el contrato de Designaciones.

El catálogo de docentes incluye `dedicaciones` con `{ id, codigo, nombre, orden, activo }`. Cada asignación leída conserva `dedicacion` y agrega `dedicacionId` opcional, `horasInvestigacion` y `horasExternas`; las mutaciones usan `{ materiaId, cargoId, dedicacionId?, horas }`. Alta y edición ofrecen las seis categorías activas. Una asignación histórica puede conservar su dedicación sin recategorizarla al editar otros campos.
