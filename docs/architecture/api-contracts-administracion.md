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
