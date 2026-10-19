# API contracts

Documentación de las superficies HTTP públicas del backend. Cada módulo expone su API bajo `/api/{modulo}/`.

Contratos detallados:

- [Administración de identidad y sesión de desarrollo](./api-contracts-administracion.md)
- [Designaciones](./api-contracts-designaciones.md)

## Base URL

- Desarrollo local: `http://localhost:5000`
- Swagger UI: `http://localhost:5000/swagger` (solo en Development)
- Producción: TBD (depende de la VM y reverse proxy — ver [`infrastructure.md`](./infrastructure.md))

## Autenticación y autorización

Las rutas de negocio requieren una identidad autenticada. La autorización se evalúa con permisos (`usuarios.ver`, `roles.administrar`, `designaciones.gestionar`, etc.) y los ámbitos persistidos en `identity.user_roles`; rol, materia o carrera enviados por el cliente nunca son autoridad.

El ingreso real es con cuentas Microsoft (personales o de cualquier organización) y se habilita con `AutenticacionMicrosoft:Habilitada=true`. El backend hace el intercambio OpenID Connect como cliente confidencial y emite su propia cookie de sesión (`__Host-ars-sesion`, `HttpOnly`, `Secure`, `SameSite=Lax`); los tokens de Microsoft nunca llegan al navegador. La cookie sólo identifica al usuario: en cada solicitud el Host verifica que siga activo y resuelve su rol y permisos desde `identity`, así que desactivarlo o cambiarle el rol aplica en la solicitud siguiente. El ingreso no crea usuarios: sólo acepta un mail verificado por Microsoft que coincida con el UPN de un usuario activo con rol vigente. Sin sesión válida la API responde `401`, nunca una redirección.

| Ruta                                | Acceso           | Uso                                                                                 |
| ----------------------------------- | ---------------- | ----------------------------------------------------------------------------------- |
| `GET /api/auth/login?returnUrl=...` | anónimo          | Inicia el ingreso con Microsoft; un `returnUrl` no local se reemplaza por `/portal` |
| `/api/auth/signin-oidc`             | Microsoft        | Retorno del ingreso; un rechazo redirige a `/login?error=forbidden`                 |
| `GET /api/auth/sesion`              | cookie de sesión | `{ usuarioId, nombreParaMostrar, upn, rolCodigo, rolNombre, permisos }` o `401`     |
| `POST /api/auth/logout`             | anónimo          | Borra la cookie (`204`); no cierra la sesión de Microsoft                           |

Con el ingreso deshabilitado estas rutas no existen.

La sesión vence a los `AutenticacionMicrosoft:MinutosInactividad` (60) sin solicitudes y a las `HorasMaximas` (10) desde el ingreso, aunque haya actividad. Desde el primer ingreso la cuenta queda vinculada por `oid` y tenant: los ingresos siguientes no dependen del mail, y otra cuenta con el mismo mail se rechaza.

**Anti-falsificación (CSRF).** Toda solicitud `POST`, `PUT`, `PATCH` o `DELETE` autenticada por la cookie de sesión debe traer el header `X-XSRF-TOKEN`; sin él, o con uno inválido, responde `400` sin aplicar cambios. Cada solicitud segura autenticada por cookie (incluido `GET /api/auth/sesion`) entrega el token en el header de respuesta `X-XSRF-TOKEN` y en la cookie `XSRF-TOKEN` (`HttpOnly`, `SameSite=Strict`, `Path=/`, `Secure` sobre HTTPS). Estas respuestas usan `Cache-Control: no-store`. El cliente conserva únicamente en memoria el token recibido por header y lo devuelve sólo en mutaciones al mismo origen; no lee cookies ni envía o acepta tokens de otros orígenes, aunque se configure `VITE_API_URL`. Tras recargar la página, la consulta de sesión recupera el token; cada nueva respuesta con token lo actualiza. Un logout exitoso o un `401` del mismo origen lo elimina e invalida los tokens de consultas que seguían en vuelo. Las identidades de desarrollo viajan en headers y no lo necesitan.

En desarrollo, y sólo con `DevelopmentAuthentication:Enabled=true`, el cliente puede enviar `X-Dev-User-Id` y `X-Dev-Role-Code`. El Host valida ambos valores contra una identidad sintética activa; si llegan, mandan sobre la cookie. En Production no se registran el esquema, los headers ni `/api/desarrollo/identidades`. Ambos caminos producen el mismo `ICurrentUser`, sin cambiar contratos de negocio.

## Forma de error estándar

Todos los endpoints retornan errores en formato consistente:

```json
{
  "type": "https://ars-docendi.unlam.edu.ar/errors/<error-code>",
  "title": "Mensaje corto y accionable para el usuario",
  "status": 400,
  "detail": "Detalle seguro y accionable",
  "instance": "/api/<modulo>/<endpoint>",
  "traceId": "<correlation-id>",
  "code": "<codigo-estable>",
  "errors": { "campo": ["mensaje de validación"] }
}
```

Sigue la convención RFC 7807 (Problem Details). Status codes habituales:

- `400 Bad Request` — validación de DTO fallida
- `401 Unauthorized` — sin sesión válida (cookie ausente, vencida o usuario desactivado)
- `403 Forbidden` — sesión válida sin el permiso requerido
- `404 Not Found` — recurso inexistente
- `409 Conflict` — colisión de estado (ej. designación ya aprobada)
- `422 Unprocessable Entity` — viola una BR-\* de negocio
- `500 Internal Server Error` — fallo no manejado (el detalle se loggea, no se expone)

Las extensiones `code` y `errors` se incluyen cuando corresponden. Las excepciones inesperadas se registran con Serilog junto con `traceId`, pero la respuesta nunca publica stack traces ni datos sensibles.

## Endpoints nuevos

Los DTOs, permisos, códigos de error y respuestas exactas están detallados en [Administración y desarrollo](./api-contracts-administracion.md) y [Designaciones](./api-contracts-designaciones.md).

| Superficie          | Rutas principales                                                                                 | Autorización                                                        |
| ------------------- | ------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------- |
| Usuarios            | `GET/POST /api/administracion/usuarios`, `GET/PUT /{id}`, `POST /{id}/activar` o `/desactivar`    | `usuarios.ver` / `usuarios.administrar`                             |
| Docentes            | `GET/POST /api/administracion/docentes`, `GET/PUT /{personaId}`, cambios de estado y `/catalogos` | `usuarios.ver` / `usuarios.administrar`                             |
| Roles y permisos    | `/api/administracion/roles`, `/permisos`, `/roles/{id}/permisos`                                  | `roles.ver`, `roles.administrar`, `roles.gestionar_membresia`       |
| Sistema y auditoría | `GET /api/administracion/sistema/estado`, `GET /api/administracion/auditoria`                     | `sistema.estado.ver`, `auditoria.ver`                               |
| Períodos            | `/api/designaciones/periodos` y comandos activar/desactivar                                       | `periodos.administrar`                                              |
| Catálogos           | `GET /api/designaciones/catalogos`                                                                | `designaciones.ver`                                                 |
| Pedidos             | `/api/designaciones/pedidos`, detalle, envío, reenvío y revisión                                  | permisos de consulta, gestión o revisión; siempre acotados al actor |
| Archivos            | `POST /api/archivos/cargas`, confirmación, metadata y descarga                                    | autenticado; archivo propio para mutaciones                         |
| Sesión dev          | `GET /api/desarrollo/identidades`                                                                 | sólo ambiente no productivo con opt-in                              |
| Sesión              | `GET /api/auth/login`, `GET /api/auth/sesion`, `POST /api/auth/logout`                            | con `AutenticacionMicrosoft:Habilitada`                             |

Todos los DTOs usan JSON `camelCase`, UUIDs canónicos y fechas ISO. Las respuestas de pedidos incluyen historial y `accionesPermitidas`; el frontend no vuelve a ejecutar la autorización ni la máquina de estados. En pedidos, el Alta envía `persona { documento, nombre, apellido }` sin `personaId`; Baja y Cambio envían `personaId` y siempre `materiaId` + `carreraId` explícitos (una materia dictada en más de una carrera exige elegir una).

## Endpoints por módulo

### Designaciones (`/api/designaciones/`)

La grilla conserva todas las filas y acciones, sin documentos ni historia completa;
entrega un resumen acotado de eventos para fechas/actor/orden. El detalle conserva
historial y snapshot completos. Administración y circuito coordinan la vigencia por
persona–materia: snapshot protegido al enviar, revalidación o `409 concurrency-conflict`
antes de commit. Las nuevas referencias validan destinos en la misma transacción;
`409 identity-destination-conflict` no agrega eventos parciales. Ver el
[contrato detallado](./api-contracts-designaciones.md#lectura-de-grilla-versus-detalle).

| Método | Path    | Rol mínimo | Descripción                           |
| ------ | ------- | ---------- | ------------------------------------- |
| GET    | `/ping` | (anónimo)  | Health check del módulo               |
| ...    | ...     | ...        | _(a documentar en specs por feature)_ |

### Aulas (`/api/aulas/`)

| Método | Path    | Rol mínimo | Descripción                           |
| ------ | ------- | ---------- | ------------------------------------- |
| GET    | `/ping` | (anónimo)  | Health check del módulo               |
| ...    | ...     | ...        | _(a documentar en specs por feature)_ |

### Portal (`/api/portal/`)

| Método          | Path                                                                                       | Rol mínimo  | Descripción                     |
| --------------- | ------------------------------------------------------------------------------------------ | ----------- | ------------------------------- |
| GET             | `/ping`                                                                                    | (anónimo)   | Health check del módulo         |
| GET             | `/perfil`                                                                                  | autenticado | Perfil propio completo          |
| PUT/DELETE      | `/perfil/contacto`, `/perfil/cv`                                                           | autenticado | Contacto y metadata de CV       |
| POST/PUT/DELETE | `/perfil/experiencia`, `/perfil/educacion`, `/perfil/certificaciones`, `/perfil/proyectos` | autenticado | CRUD propio                     |
| PUT             | `/perfil/habilidades`, `/perfil/intereses`                                                 | autenticado | Reemplazo independiente de tags |

La lectura completa usa seis consultas EF split dentro de un snapshot
`REPEATABLE READ`: preserva las secciones completas sin multiplicar colecciones
hermanas. La identidad se resuelve por cuenta concreta y la metadata documental
mediante un lote de IDs, no mediante directorios globales ni un lookup por adjunto.

Las respuestas de contacto, CV e ítems de trayectoria/proyectos contienen `version`.
PUT y DELETE de secciones existentes requieren la versión observada; DELETE la
recibe como parámetro de query. Habilidades e intereses usan tokens independientes
`versionHabilidades`/`versionIntereses`, enviados como `version` al reemplazar esa
sección. Una creación no exige un token de fila inexistente. Un conflicto devuelve
`409 concurrency-conflict`; el cliente muestra el conflicto, invalida la lectura
y solicita recargar, sin repetir ciegamente el guardado. El perfil incidental y
los términos normalizados se crean mediante UPSERT sin convertir ediciones
incompatibles en operaciones idempotentes.

Los cambios de CV y documentos reciben `archivoId`; no aceptan URI, bucket,
clave ni estado enviado por el cliente. Las descargas pasan por el backend:
`GET /api/portal/perfil/cv/descarga` y
`GET /api/portal/perfil/proyectos/{id}/documento`.

### Archivos (`/api/archivos/`)

1. `POST /cargas` recibe propósito, nombre, MIME declarado y tamaño; devuelve
   `archivoId`, ruta PUT temporal del backend y expiración.
2. El cliente sube el objeto y llama `POST /cargas/{archivoId}/confirmar` con
   hash/tamaño observados. El backend valida firma, MIME real, límites y
   antivirus antes de marcarlo `disponible`.
3. `GET /{archivoId}` devuelve solo metadata. `GET /{archivoId}/descarga` no
   revela bucket ni clave y solo devuelve bytes disponibles.

Una carga pendiente, rechazada, expirada o de otro propósito produce Problem
Details estable y nunca puede asociarse a Portal o Designaciones.

Cada upload reserva un intento persistido con clave inmutable. La confirmación
publica mediante CAS únicamente los bytes verificados de ese intento; un PUT
tardío no puede reemplazar la clave publicada. Una confirmación ya publicada es
idempotente. Reserva, recuperación y limpieza usan tokens de intento y vencimiento;
ninguna transacción PostgreSQL permanece abierta durante stream o antivirus.

Asociar/desvincular y reservar eliminación comparten exclusión transaccional por
archivo, en orden estable de IDs, y participantes locales enlistados en una única
conexión/transacción. Portal y Designaciones consultan sus referencias mediante
contratos puros; un consumidor faltante/fallido impide el borrado. DELETE directo
de un archivo referenciado devuelve conflicto sin quitar su descarga. Reemplazar
CV/documento primero confirma la desvinculación y luego evalúa retención; otra
referencia conserva los bytes. Un fallo remoto tras reservar eliminación mantiene
indisponibilidad y permite reintentar, nunca vuelve a publicar por una revisión
tardía.

### Tareas (`/api/tareas/`)

Acceso por permiso del rol vigente (no por nombre de rol). `tareas.ver` lo tienen todos los roles de sistema; `tareas.gestionar`, Decanato, Secretaría Académica y Administrativo; `proyectos.gestionar`, Decanato y Secretaría Académica. Las reglas finas (autoridad creadora, Responsable, jerarquía de asignación) las aplica el servicio: violación de permiso → 403, de regla → 422, dato inválido → 400.

| Método      | Path                                            | Permiso               | Descripción                                                                                                                    |
| ----------- | ----------------------------------------------- | --------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| GET         | `/ping`                                         | (anónimo)             | Health check del módulo                                                                                                        |
| GET         | `/`                                             | `tareas.ver`          | Listado de tareas (comentarios e historial vacíos)                                                                             |
| GET         | `/{id}`                                         | `tareas.ver`          | Detalle: comentarios, historial y tareas relacionadas                                                                          |
| POST        | `/`                                             | `tareas.gestionar`    | Crear tarea; con `tareaPadreId` es hija y hereda el Proyecto                                                                   |
| PUT         | `/{id}`                                         | `tareas.gestionar`    | Editar campos (solo la autoridad creadora)                                                                                     |
| POST        | `/{id}/estado`                                  | `tareas.ver`          | Cambiar estado (Responsable o autoridad creadora; Pausa exige comentario, Resuelta exige Solución)                             |
| PATCH       | `/{id}/avance`                                  | `tareas.ver`          | % de avance 0-100 (Responsable o autoridad creadora)                                                                           |
| POST        | `/{id}/comentarios`                             | `tareas.ver`          | Comentario interno                                                                                                             |
| POST/DELETE | `/{id}/relaciones`, `/{id}/relaciones/{otraId}` | `tareas.ver`          | Relación simple bidireccional                                                                                                  |
| GET         | `/candidatos[?para=proyecto&q=texto]`           | `tareas.gestionar`    | Usuarios asignables como Responsable según la jerarquía; `q` busca por nombre, apellido, usuario, legajo o documento (máx. 50) |
| GET         | `/proyectos/estados`                            | `tareas.ver`          | Catálogo de estados de proyecto (código, nombre, verbo, inicial, admite tareas)                                                |
| GET         | `/proyectos`, `/proyectos/{id}`                 | `tareas.ver`          | Proyectos de cualquier estado                                                                                                  |
| POST        | `/proyectos`                                    | `proyectos.gestionar` | Crear proyecto (Responsable: Decanato o Secretaría Académica)                                                                  |
| PUT         | `/proyectos/{id}`                               | `proyectos.gestionar` | Editar nombre, descripción, fechas y Responsable (el estado se cambia aparte); el Responsable se revalida solo si cambia       |
| POST        | `/proyectos/{id}/estado`                        | `proyectos.gestionar` | Finalizar o cancelar un proyecto                                                                                               |

Una solicitud valida el Proyecto efectivo de la tarea hija bajo los mismos locks que serializan el cierre del proyecto; nuevas altas no entran en un estado que no admita tareas. Si la edición conserva el Proyecto actual, un estado cerrado no convierte esa edición en una reasignación. El cambio de Proyecto de una raíz propaga el nuevo valor a los descendientes existentes, incluidos `NULL` heredados, dentro de la misma transacción.

Tarea y Proyecto exponen `version` (`xmin`). PUT y cambios de estado/avance requieren la versión observada; ausencia/obsolescencia responde `409 concurrency-conflict` antes de persistir historial parcial. El frontend invalida/recarga y deja la resolución al usuario, sin reintento ciego. La relación bidireccional es idempotente para pares canónicos; borrar una relación ausente también responde éxito.

Los filtros de visibilidad y jerarquía se aplican antes de materializar/límite; nombres e historial se resuelven para IDs visibles, manteniendo identidades inactivas sólo para lectura histórica. El proyecto requerido por hijas y la solución de tareas resueltas se validan además mediante constraints SQL incrementales, usando IDs de catálogos numéricos; no se exige Proyecto a tareas raíz ni se expone reparenting.

## Idempotencia

Las transiciones de pedidos (`enviar`, `reenviar`, `aceptar`, `rechazar`, `devolver`, `priorizar`, `despriorizar`) requieren `Idempotency-Key: <uuid>`. La identidad lógica de la clave incluye actor, ruta, recurso y payload durante 24 horas: el replay idéntico retorna la misma respuesta y una reutilización incompatible retorna `409 idempotency-key-reused`. La exclusión concurrente garantiza una sola transición y un solo evento de historial.

## Versioning

V1 implícito hasta que sea necesario versionar. Cuando se necesite: prefijo `/api/v2/{modulo}/`. NO romper V1 sin período de coexistencia.

## Auto-discovery

Swagger UI disponible en `/swagger` (solo Development). Cada controller debe tener atributos `[ProducesResponseType]` para que el schema sea preciso.
