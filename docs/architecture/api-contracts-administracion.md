# Contratos API — Administración y desarrollo

Complementa [api-contracts.md](./api-contracts.md). Todas las rutas administrativas requieren autenticación y responden Problem Details; la única excepción es el catálogo de identidades de desarrollo, que sólo existe cuando el Host habilita explícitamente ese esquema fuera de producción.

## Permisos

| Recurso               | Lectura                                   | Escritura                   |
| --------------------- | ----------------------------------------- | --------------------------- |
| Personas              | `personas.ver` o `personas.ver_ambito`    | `personas.administrar`      |
| Mi Portal de otros    | lectura de la persona                     | `portal.administrar`        |
| Roles                 | `roles.ver`                               | `roles.administrar`         |
| Membresía de permisos | `roles.ver`                               | `roles.gestionar_membresia` |
| Revisión de pedidos   | `designaciones.revisar`                   | —                           |
| Estado del sistema    | `sistema.estado.ver`                      | —                           |
| Auditoría             | `auditoria.ver`                           | —                           |
| Catálogos             | permiso de lectura del recurso consumidor | —                           |

`personas.ver` lee todas las personas. `personas.ver_ambito` (Jefe de Cátedra) lee sólo las personas con designación vigente en las materias donde es jefe; la API deriva ese ámbito desde sus asignaciones vigentes en `identity.user_roles`, nunca desde parámetros del cliente, y fuera de él responde 404. `portal.administrar` corrige el Mi Portal de otra persona dentro del mismo ámbito. Los permisos `usuarios.ver`, `usuarios.administrar` y `docentes.ver` se retiraron con las pantallas de Usuarios y Docentes.

## DTOs

Los nombres JSON son `camelCase`. IDs y fechas se representan como UUID y `YYYY-MM-DD`.

```text
RolResumenDto          = { id, codigo, nombre }
AsignacionRolDto       = { id, rolId, codigo, nombre, ambito, materiaId?, carreraId? }
                         Docente → `materiaId` + `carreraId` juntos (una materia compartida entre
                         carreras exige elegir una); Jefe de Cátedra → `materiaId` (canónica) solo;
                         Coordinador → `carreraId` solo.
DesignacionVigenteDto  = { id, materiaId, materiaCodigo, materiaNombre, carreraId,
                           cargo{id,codigo,nombre,abreviatura},
                           dedicacion?, dedicacionId?, horas, horasInvestigacion?, horasExternas?, vigenteDesde }
PersonaAdministracionDto = { personaId, usuarioId?, nombre, apellido, documento, legajo?, cuil?,
                           fechaNacimiento?, telefono?, upn?, tieneCuenta, activo, version?,
                           roles[], membresias[], asignaciones[] }
RolDto                 = { id, codigo, nombre, descripcion?, ambito, esSistema, activo, version, permisos[] }
CrearRolDto            = { nombre, descripcion?, ambito, rolBaseId? }
EditarRolDto           = { nombre, descripcion?, ambito, version }
EliminarRolDto         = { version }
PermisoDto             = { id, codigo, nombre, descripcion }
EstadoBaseDatosDto     = { estado: "disponible" | "no_disponible", comprobadoEn, duracionMs }
CambioAuditoriaDto     = { campo, etiquetaCampo, valorAnterior?, valorNuevo?, oculto }
EventoAuditoriaDto     = { id, schema, tabla, rowPk, accion, cambiadoEn, cambiadoPor?, requestId?,
                           columnasCambiadas[], cambios: CambioAuditoriaDto[], actor, accionEtiqueta,
                           modulo, objeto, resumen }
PaginaAuditoriaDto     = { elementos: EventoAuditoriaDto[], pagina, tamanoPagina, total }
ReemplazarPermisosDto  = { permisoIds[], version }
CatalogoIdentityDto    = { roles[{ id, codigo, nombre, ambito, esSistema }], permisos[],
                           carreras[], materias[{ id, codigo, nombre }], materiasPlan[{ id, codigo, nombre, carreraId, carreraNombre, carreraCodigo }], personasElegibles[] }
                           `materias` son canónicas (jefe y, junto con `carreraId`, docente);
                           `materiasPlan` es el catálogo informativo materia–carrera (pares
                           deduplicados) que acota qué carreras ofrecer para una materia de
                           Docente — ninguna FK de negocio depende de él. Su `id` es el de la
                           materia canónica (no el de la fila materia–plan) y `carreraCodigo`
                           permite buscar por código de carrera. Mismo formato en
                           `/api/administracion/personas/catalogos`.
```

Los roles personalizados conservan su `codigo` al renombrarse y exigen nombre único entre roles
activos. Un rol base debe estar activo y la copia de permisos ocurre sólo al crear el nuevo rol.
La baja de un rol personalizado es lógica (`activo = false`): conserva el registro, permisos y
asignaciones históricas, pero lo excluye de listados, asignaciones nuevas y roles base. Los roles de
sistema mantienen inmutables código, nombre, descripción, ámbito, marca y estado; sus permisos sí
pueden reemplazarse.

## Rutas de personas

| Método | Ruta                                                  | Permiso                                | Entrada / salida                                                                                                    |
| ------ | ----------------------------------------------------- | -------------------------------------- | ------------------------------------------------------------------------------------------------------------------- |
| GET    | `/api/administracion/personas`                        | `personas.ver` o `personas.ver_ambito` | `PersonaAdministracionDto[]` (acotado por ámbito)                                                                   |
| GET    | `/api/administracion/personas/{personaId}`            | `personas.ver` o `personas.ver_ambito` | `PersonaAdministracionDto`; 404 fuera del ámbito                                                                    |
| GET    | `/api/administracion/personas/catalogos`              | `personas.ver` o `personas.ver_ambito` | roles, materias, materias–carrera, carreras, cargos y dedicaciones                                                  |
| POST   | `/api/administracion/personas/documento/verificar`    | `personas.administrar`                 | `{ documento, exceptoPersonaId? }` → `{ existe, personaId?, nombre?, legajo?, activo? }` (`activo` nulo sin cuenta) |
| POST   | `/api/administracion/personas`                        | `personas.administrar`                 | `GuardarPersonaDto` → `201 PersonaAdministracionDto`                                                                |
| PUT    | `/api/administracion/personas/{personaId}`            | `personas.administrar`                 | `GuardarPersonaDto` con `version` → `PersonaAdministracionDto`                                                      |
| POST   | `/api/administracion/personas/{personaId}/activar`    | `personas.administrar`                 | `{ version }` → `PersonaAdministracionDto`                                                                          |
| POST   | `/api/administracion/personas/{personaId}/desactivar` | `personas.administrar`                 | `{ version }` → `PersonaAdministracionDto`                                                                          |

`GuardarPersonaDto = { nombre, apellido, documento, legajo, cuil?, fechaNacimiento, telefono?, upn,
membresias[{ rolId, materiaId?, carreraId? }], designaciones[{ materiaId, carreraId, cargoId,
dedicacionId?, horas }], version? }`. Reglas: al menos un rol con su ámbito; asignaciones obligatorias
con rol Docente o Jefe de Cátedra; `dedicacionId` es obligatorio en una asignación nueva y sólo puede
faltar en una histórica que conserva su dedicación legada; el documento se guarda y compara por sus dígitos y no se repite;
la lista de roles y la de designaciones se reemplazan completas en una sola transacción.

## Mi Portal de una persona

Bajo `/api/administracion/personas/{personaId}/portal` se exponen las mismas operaciones que
`/api/portal/perfil` (lectura, contacto, CV, experiencia, educación, certificaciones, proyectos,
habilidades, intereses y descargas de CV y documento de proyecto). Leer y descargar requiere poder
ver a la persona; las mutaciones, además, `portal.administrar`; fuera del ámbito, 404. El Host
autoriza y delega en `IAdministracionPortal` (`Modules.Portal.Contracts`), que usa las mismas
validaciones y versiones de Mi Portal. El operador es el autor auditado y el dueño en Storage de los
archivos que sube; el archivo queda vinculado al perfil de la persona.

Alta/edición de persona es atómica desde la perspectiva HTTP. Si falla identity o el comando público de Designaciones, el servidor revierte o compensa la unidad completa y retorna Problem Details.

`roles` es un resumen único por `rolId`; no se repiten por cada materia. `membresias` conserva cada
asignación activa completa. Los payloads de escritura usan sólo IDs canónicos: las etiquetas visibles
de roles, carreras y materias no son identificadores. Un mismo usuario puede tener, por ejemplo,
`docente + Materia A` y `jefe_catedra + Materia B` sin combinaciones implícitas entre filas.

Las rutas de frontend `/usuarios` y `/docentes` (con `?personaId=`) redirigen a `/personas` y
`/personas/{id}`.

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

| Método | Ruta                                 | Permiso              | Entrada / salida               |
| ------ | ------------------------------------ | -------------------- | ------------------------------ |
| GET    | `/api/administracion/sistema/estado` | `sistema.estado.ver` | `EstadoBaseDatosDto`           |
| GET    | `/api/administracion/auditoria`      | `auditoria.ver`      | filtros → `PaginaAuditoriaDto` |
| GET    | `/api/inferencia/estado`             | `sistema.estado.ver` | `EstadoInferenciaDto`          |

El estado de PostgreSQL se comprueba mediante `SELECT 1` con timeout de 3 segundos. La respuesta sólo contiene `estado`, `comprobadoEn` y `duracionMs`; una falla retorna `no_disponible` sin revelar excepción, host ni configuración. El dashboard combina este resultado con los pings HTTP existentes de Aulas, Tareas, Designaciones y Portal; cada sonda se muestra independientemente.

`EstadoInferenciaDto = { estado: "deshabilitado" | "disponible" | "no_disponible", comprobadoEn, duracionMs }`. Lo sirve `ArsDocendi.Inferencia`: sin `Inferencia:Url` responde `deshabilitado` sin contactar el servicio; si no, consulta `GET /api/version` de Ollama con timeout de 3 segundos y cualquier falla (red, 401/403 de Access o BasicAuth, 5xx, timeout) es `no_disponible` sin revelar URL, usuario ni excepción. El dashboard lo muestra como fila "Inferencia (IA)".

La consulta de auditoría acepta `desde`, `hasta`, `accion` (`INSERT`, `UPDATE`, `DELETE`), `schema`, `tabla`, `cambiadoPor` (UUID, conservado para compatibilidad), `actor` (fragmento del nombre visible, sin distinguir mayúsculas), `rowPk`, `pagina` (predeterminada 1) y `tamanoPagina` (predeterminado 50, máximo 100). La búsqueda por actor usa el nombre visible presentado en la tabla y se aplica junto con los filtros antes del conteo y la paginación; el orden permanece estable por fecha descendente e ID descendente. El actor se resuelve mediante joins izquierdos opcionales a `identity.users` y `identity.personas`: se prioriza `Apellido, Nombre`, luego `display_name` y, si no hay cuenta, `Actor no identificado`. Los módulos conocidos `identity`, `designaciones` y `portal` se muestran como **Identidad**, **Designaciones** y **Portal**; otros schemas usan una etiqueta legible de fallback. Cada consulta tiene timeout de 5 segundos. Fechas invertidas, acción inválida, actor de más de 100 caracteres o límites de página fuera de rango responden `400 validation`. La API es exclusivamente GET. La acción se presenta como Alta, Actualización o Eliminación física; un `UPDATE` no se convierte en baja por inferencia. La UI muestra fecha, usuario, acción, módulo y resumen; el objeto, clave de fila y `requestId` quedan en el detalle. No se exponen UPN/correo, `client_ip` ni snapshots crudos. Los valores del detalle sólo se incluyen para campos aprobados; PII, secretos y campos no clasificados se devuelven con `oculto: true` y valores nulos.

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

El catálogo de personas incluye `dedicaciones` con `{ id, codigo, nombre, orden, activo }`. Cada asignación leída conserva `dedicacion` y agrega `dedicacionId` opcional, `horasInvestigacion` y `horasExternas`; las mutaciones usan `{ materiaId, carreraId, cargoId, dedicacionId?, horas }`. Alta y edición ofrecen las seis categorías activas. Una asignación histórica puede conservar su dedicación sin recategorizarla al editar otros campos.
