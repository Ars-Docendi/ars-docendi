# Contratos API — Administración y desarrollo

Complementa [api-contracts.md](./api-contracts.md). Todas las rutas administrativas requieren autenticación y responden Problem Details; la única excepción es el catálogo de identidades de desarrollo, que sólo existe cuando el Host habilita explícitamente ese esquema fuera de producción.

## Permisos

| Recurso               | Lectura                                    | Escritura                   |
| --------------------- | ------------------------------------------ | --------------------------- |
| Usuarios              | `usuarios.ver`                             | `usuarios.administrar`      |
| Docentes              | `usuarios.ver` o JdC con ámbito de materia | `usuarios.administrar`      |
| Roles                 | `roles.ver`                                | `roles.administrar`         |
| Membresía de permisos | `roles.ver`                                | `roles.gestionar_membresia` |
| Catálogos             | permiso de lectura del recurso consumidor  | —                           |

Docentes reutiliza los permisos de usuarios porque administra la misma identidad canónica y sus designaciones vigentes. La vista de Jefe de Cátedra es una excepción de lectura acotada: la API deriva el ámbito desde sus asignaciones vigentes en `identity.user_roles`, nunca desde parámetros del cliente, y no habilita la API de usuarios ni escrituras.

## DTOs

Los nombres JSON son `camelCase`. IDs y fechas se representan como UUID y `YYYY-MM-DD`.

```text
AsignacionRolDto       = { id, rolId, codigo, nombre, ambito, materiaId?, carreraId? }
UsuarioResumenDto      = { id, personaId, nombre, apellido, documento, legajo?, cuil?,
                           fechaNacimiento?, telefono?, upn, activo, roles[] }
GuardarUsuarioDto      = { nombre, apellido, documento, legajo?, cuil?, fechaNacimiento?,
                           telefono?, upn, roles[{ rolId, materiaId?, carreraId? }] }
DesignacionVigenteDto  = { id, materia{id,codigo,nombre}, cargo{id,codigo,nombre,abreviatura},
                           dedicacion?, horas, vigenteDesde }
DocenteResumenDto      = { personaId, usuarioId?, datosPersona..., activo?, roles[], designaciones[] }
GuardarDocenteDto      = { personaId? | personaNueva, usuarioId?, roles[], designaciones[] }
RolDto                 = { id, codigo, nombre, descripcion?, ambito, esSistema, activo }
CrearRolDto            = { codigo, nombre, descripcion?, ambito, rolBaseId? }
EditarRolDto           = { nombre, descripcion?, ambito }
PermisoDto             = { id, codigo, nombre, descripcion }
ReemplazarPermisosDto  = { permisoIds[] }
CatalogoIdentityDto    = { roles[], permisos[], carreras[], materias[], personasElegibles[] }
```

La edición de un rol de sistema ignora ninguna protección: código, ámbito y marca de sistema no aparecen como campos editables. Si llegan por payload adicional, la validación los rechaza.

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

## Rutas de roles

| Método | Ruta                                      | Permiso                     | Entrada / salida                           |
| ------ | ----------------------------------------- | --------------------------- | ------------------------------------------ |
| GET    | `/api/administracion/roles`               | `roles.ver`                 | `RolDto[]`                                 |
| GET    | `/api/administracion/roles/{id}`          | `roles.ver`                 | `RolDto`                                   |
| POST   | `/api/administracion/roles`               | `roles.administrar`         | `CrearRolDto` → `201 RolDto`               |
| PUT    | `/api/administracion/roles/{id}`          | `roles.administrar`         | `EditarRolDto` con `version` → `RolDto`    |
| GET    | `/api/administracion/roles/{id}/permisos` | `roles.ver`                 | `PermisoDto[]`                             |
| PUT    | `/api/administracion/roles/{id}/permisos` | `roles.gestionar_membresia` | `{ permisoIds, version }` → `PermisoDto[]` |
| GET    | `/api/administracion/permisos`            | `roles.ver`                 | catálogo cerrado `PermisoDto[]`            |

## Desarrollo

`GET /api/desarrollo/identidades` devuelve únicamente:

```text
IdentidadDesarrolloDto = { usuarioId, nombreParaMostrar, upn, roles[
  { codigo, nombre, materias[{id,codigo,nombre}], carreras[{id,codigo,nombre}] }
] }
```

El cliente envía `X-Dev-User-Id` y `X-Dev-Role-Code`. El handler valida usuario activo, marca de dataset sintético, asignación vigente y ámbito. No acepta ámbitos declarados por el cliente. En Production la ruta y el esquema no están registrados, por lo que el resultado es `404` aunque se envíen esos headers.

## Códigos de error

| Código                         | HTTP | Uso                                                    |
| ------------------------------ | ---- | ------------------------------------------------------ |
| `validation`                   | 400  | forma o campos inválidos; extensión `errors` por campo |
| `not-authenticated`            | 401  | identidad ausente o inválida                           |
| `forbidden`                    | 403  | permiso o ámbito insuficiente                          |
| `resource-not-found`           | 404  | recurso inexistente o no visible                       |
| `identity-upn-conflict`        | 409  | UPN usada por otra cuenta                              |
| `identity-document-conflict`   | 409  | documento usado por otra persona                       |
| `identity-role-scope-conflict` | 422  | ámbito incompatible con el rol                         |
| `identity-protected-role`      | 422  | mutación prohibida de rol de sistema                   |
| `identity-permission-invalid`  | 422  | permiso inexistente o membresía duplicada              |
| `identity-role-code-conflict`  | 409  | el código normalizado del rol ya existe                |
| `concurrency-conflict`         | 409  | el recurso cambió desde su lectura                     |

POST/PUT administrativos representan reemplazos o comandos naturalmente repetibles, pero no prometen replay de respuesta. `Idempotency-Key` es obligatorio sólo en transiciones de dominio que lo declaran en el contrato de Designaciones.
