# API contracts

Documentación de las superficies HTTP públicas del backend. Cada módulo expone su API bajo `/api/{modulo}/`.

Contratos detallados:

- [Administración de identidad y sesión de desarrollo](./api-contracts-administracion.md)
- [Designaciones](./api-contracts-designaciones.md)

## Base URL

- Desarrollo local: `http://localhost:5000`
- Swagger UI: `http://localhost:5000/swagger` (solo en Development)
- Desplegado: mismo origen que el frontend (`https://<DOMINIO>/api`, staging y `pr-N` en sus subdominios; ver [`infrastructure.md`](./infrastructure.md))

## Autenticación y autorización

Las rutas de negocio requieren una identidad autenticada. La autorización se evalúa con permisos (`personas.ver`, `roles.administrar`, `designaciones.gestionar`, etc.) y los ámbitos persistidos en `identity.user_roles`; rol, materia o carrera enviados por el cliente nunca son autoridad.

El ingreso real es con cuentas Microsoft (personales o de cualquier organización) y se habilita con `AutenticacionMicrosoft:Habilitada=true`. El backend hace el intercambio OpenID Connect como cliente confidencial y emite su propia cookie de sesión (`__Host-ars-sesion`, `HttpOnly`, `Secure`, `SameSite=Lax`); los tokens de Microsoft nunca llegan al navegador. La cookie sólo identifica al usuario: en cada solicitud el Host verifica que siga activo y resuelve su rol y permisos desde `identity`, así que desactivarlo o cambiarle el rol aplica en la solicitud siguiente. El ingreso no crea usuarios: sólo acepta un mail verificado por Microsoft que coincida con el UPN de un usuario activo con rol vigente. Sin sesión válida la API responde `401`, nunca una redirección.

| Ruta                                | Acceso           | Uso                                                                                 |
| ----------------------------------- | ---------------- | ----------------------------------------------------------------------------------- |
| `GET /api/auth/login?returnUrl=...` | anónimo          | Inicia el ingreso con Microsoft; un `returnUrl` no local se reemplaza por `/portal` |
| `/api/auth/signin-oidc`             | Microsoft        | Retorno del ingreso; un rechazo redirige a `/login?error=forbidden`                 |
| `GET /api/auth/sesion`              | cookie de sesión | `SesionDto` o `401` (ver abajo)                                                     |
| `POST /api/auth/asignacion`         | cookie de sesión | `{ asignacionId }` → `SesionDto`; elige o cambia el rol activo; ajena → `403`       |
| `POST /api/auth/logout`             | anónimo          | Borra la cookie (`204`); no cierra la sesión de Microsoft                           |

Con el ingreso deshabilitado estas rutas no existen.

La sesión vence a los `AutenticacionMicrosoft:MinutosInactividad` (60) sin solicitudes y a las `HorasMaximas` (10) desde el ingreso, aunque haya actividad. Desde el primer ingreso la cuenta queda vinculada por `oid` y tenant: los ingresos siguientes no dependen del mail, y otra cuenta con el mismo mail se rechaza.

`SesionDto = { usuarioId, nombreParaMostrar, upn, rolCodigo, rolNombre, permisos[], asignacion, asignaciones[], requiereElegirRol }`, con `AsignacionSesion = { id, rolCodigo, rolNombre, ambitoTipo: global|materia|carrera, ambitoNombre? }`. La sesión opera siempre con una única asignación de rol: la elegida con `POST /api/auth/asignacion` o, mientras no se eligió, la primera vigente por nombre de rol y ámbito (`requiereElegirRol` es `true` y el frontend ofrece elegir). La cookie guarda la asignación elegida, no los permisos; si deja de estar vigente se vuelve a la de por defecto. El ámbito del actor (materias, carreras, roles de sistema) se limita a esa asignación.

**Anti-falsificación (CSRF).** Toda solicitud `POST`, `PUT`, `PATCH` o `DELETE` autenticada por la cookie de sesión debe traer el header `X-XSRF-TOKEN`; sin él, o con uno inválido, responde `400` sin aplicar cambios. Cada solicitud segura autenticada por cookie (incluido `GET /api/auth/sesion`) entrega el token en el header de respuesta `X-XSRF-TOKEN` y en la cookie `XSRF-TOKEN` (`HttpOnly`, `SameSite=Strict`, `Path=/`, `Secure` sobre HTTPS). Estas respuestas usan `Cache-Control: no-store`. El cliente conserva únicamente en memoria el token recibido por header y lo devuelve sólo en mutaciones al mismo origen; no lee cookies ni envía o acepta tokens de otros orígenes, aunque se configure `VITE_API_URL`. Tras recargar la página, la consulta de sesión recupera el token; cada nueva respuesta con token lo actualiza. Un logout exitoso o un `401` del mismo origen lo elimina e invalida los tokens de consultas que seguían en vuelo. Las identidades de desarrollo viajan en headers y no lo necesitan.

En desarrollo, y sólo con `DevelopmentAuthentication:Enabled=true`, el cliente puede enviar `X-Dev-User-Id` y, una vez elegida, `X-Dev-Asignacion-Id`. El Host valida la identidad sintética activa y resuelve la asignación con las mismas reglas que la cookie; si llegan, mandan sobre la cookie. `GET /api/desarrollo/sesion` devuelve la misma `SesionDto`. En Production no se registran el esquema, los headers ni `/api/desarrollo/identidades`. Ambos caminos producen el mismo `ICurrentUser`, sin cambiar contratos de negocio.

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

## Superficies

Los DTOs, permisos, códigos de error y respuestas exactas están detallados en [Administración y desarrollo](./api-contracts-administracion.md) y [Designaciones](./api-contracts-designaciones.md).

| Superficie          | Rutas principales                                                                                                                                          | Autorización                                                                            |
| ------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------- |
| Personas            | `GET/POST /api/administracion/personas`, `GET/PUT /{personaId}`, estado, `/catalogos`, `POST /documento/verificar` y `/{personaId}/portal/...`             | `personas.ver` / `personas.ver_ambito` / `personas.administrar` / `portal.administrar`  |
| Roles y permisos    | `/api/administracion/roles`, `/permisos`, `/roles/{id}/permisos`                                                                                           | `roles.ver`, `roles.administrar`, `roles.gestionar_membresia`                           |
| Sistema y auditoría | `GET /api/administracion/sistema/estado`, `GET /api/administracion/auditoria`                                                                              | `sistema.estado.ver`, `auditoria.ver`                                                   |
| Inferencia          | `GET /api/inferencia/estado`                                                                                                                               | `sistema.estado.ver`                                                                    |
| Períodos            | `/api/designaciones/periodos` y comandos activar/desactivar                                                                                                | `periodos.administrar`                                                                  |
| Catálogos           | `GET /api/designaciones/catalogos`                                                                                                                         | `designaciones.ver`                                                                     |
| Pedidos             | `/api/designaciones/pedidos`, detalle, envío, reenvío y revisión                                                                                           | permisos de consulta, gestión o revisión; siempre acotados al actor                     |
| Notificaciones      | `GET /api/notificaciones`, `/no-leidas/cantidad`, `PATCH /{id}/leida`, `PATCH /leidas`; `/push/clave-publica`, `/push/suscripciones`, `/push/preferencias` | autenticado; siempre acotadas al usuario de la sesión (y las lecturas, a su rol activo) |
| Archivos            | `POST /api/archivos/cargas`, subida del objeto, confirmación, metadata, descarga y baja                                                                    | autenticado; archivo propio para mutaciones                                             |
| Sesión dev          | `GET /api/desarrollo/identidades`, `GET /api/desarrollo/sesion`                                                                                            | sólo ambiente no productivo con opt-in                                                  |
| Sesión              | `GET /api/auth/login`, `GET /api/auth/sesion`, `POST /api/auth/asignacion`, `POST /api/auth/logout`                                                        | con `AutenticacionMicrosoft:Habilitada`                                                 |

Todos los DTOs usan JSON `camelCase`, UUIDs canónicos y fechas ISO. Las respuestas de pedidos incluyen historial y `accionesPermitidas`; el frontend no vuelve a ejecutar la autorización ni la máquina de estados. En pedidos, el Alta envía `persona { documento, nombre, apellido }` sin `personaId`; Baja y Cambio envían `personaId` y siempre `materiaId` + `carreraId` explícitos (una materia dictada en más de una carrera exige elegir una).

## Endpoints por módulo

### Designaciones (`/api/designaciones/`)

La grilla conserva todas las filas y acciones, sin documentos ni historia completa;
entrega un resumen acotado de eventos para fechas/actor/orden. El detalle conserva el
snapshot completo; con `designaciones.ver_seguimiento` devuelve el historial completo y
sin él, el historial vacío. Administración y circuito coordinan la vigencia por
persona–materia: snapshot protegido al enviar, revalidación o `409 concurrency-conflict`
antes de commit. Las nuevas referencias validan destinos en la misma transacción;
`409 identity-destination-conflict` no agrega eventos parciales. Ver el
[contrato detallado](./api-contracts-designaciones.md#lectura-de-grilla-versus-detalle).

Rutas, DTOs, permisos y códigos de error: [api-contracts-designaciones.md](./api-contracts-designaciones.md).

### Aulas (`/api/aulas/`)

| Método | Path                                 | Permiso                 | Descripción                                                                             |
| ------ | ------------------------------------ | ----------------------- | --------------------------------------------------------------------------------------- |
| GET    | `/ping`                              | (anónimo)               | Health check del módulo                                                                 |
| POST   | `/solicitudes`                       | `aulas.solicitar`       | Crear una solicitud de reserva propia                                                   |
| GET    | `/solicitudes/materias-propias`      | `aulas.solicitar`       | Listar las materias asignadas al docente autenticado                                    |
| GET    | `/solicitudes/mias`                  | `aulas.solicitar`       | Listar las solicitudes propias del Docente autenticado                                  |
| POST   | `/solicitudes/{id}/cancelar`         | `aulas.solicitar`       | Cancelar una solicitud propia en estado `pendiente`                                     |
| GET    | `/solicitudes`                       | `aulas.aprobar`         | Listar todas las solicitudes (todos los docentes)                                       |
| POST   | `/solicitudes/{id}/asignar`          | `aulas.aprobar`         | Asignar aula a una `pendiente` (→ `aprobada`) o actualizar el aula de una ya `aprobada` |
| POST   | `/solicitudes/{id}/rechazar`         | `aulas.aprobar`         | Rechazar una `pendiente` con motivo obligatorio (→ `rechazada`)                         |
| GET    | `/solicitudes/{id}`                  | dueño o `aulas.aprobar` | Detalle de una solicitud (ver abajo)                                                    |
| POST   | `/solicitudes/{id}/cancelar-reserva` | `aulas.aprobar`         | Cancelar una reserva `aprobada` con motivo obligatorio (→ `cancelada`, libera el aula)  |

`GET /solicitudes/{id}` no lleva policy de permiso: lo ve el dueño (con `aulas.solicitar`) o quien tiene
`aulas.aprobar`. Para cualquier otro, incluso un rol sin permisos de Aulas, responde `404`
`solicitud-not-found`, igual que para un id inexistente (no revela que existe). El DTO de solicitud suma
`motivoCancelacion` (sólo en una reserva cancelada por un Administrativo) y `esPropia` (la solicitud es
de quien consulta; lo calcula el backend). `docente` viene siempre en el detalle.

`POST /solicitudes/{id}/cancelar-reserva` recibe `{ "motivo": string }`. Motivo en blanco: `400`
`solicitud-invalida` en el campo `motivo` ("Debés indicar el motivo de la cancelación."). Estado distinto
de `aprobada`: `409` `solicitud-no-aprobada` ("Sólo se puede cancelar una reserva en estado Aprobada.").
Sin `aulas.aprobar`: `403`. Éxito: `200` con la solicitud en estado `cancelada`, `aulaAsignada` nulo y el
motivo guardado.

### Portal (`/api/portal/`)

| Método          | Path                                                                                       | Rol mínimo  | Descripción                     |
| --------------- | ------------------------------------------------------------------------------------------ | ----------- | ------------------------------- |
| GET             | `/ping`                                                                                    | (anónimo)   | Health check del módulo         |
| GET             | `/perfil`                                                                                  | autenticado | Perfil propio completo          |
| PUT             | `/perfil/contacto`                                                                         | autenticado | Contacto                        |
| PUT/DELETE      | `/perfil/cv`                                                                               | autenticado | Metadata de CV                  |
| POST/PUT/DELETE | `/perfil/experiencia`, `/perfil/educacion`, `/perfil/certificaciones`, `/perfil/proyectos` | autenticado | CRUD propio                     |
| PUT             | `/perfil/habilidades`, `/perfil/intereses`                                                 | autenticado | Reemplazo independiente de tags |

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
2. El cliente sube el objeto con `PUT /cargas/{archivoId}/objeto` y llama
   `POST /cargas/{archivoId}/confirmar` con hash/tamaño observados. El backend valida firma, MIME real, límites y
   antivirus antes de marcarlo `disponible`.
3. `GET /{archivoId}` devuelve solo metadata. `GET /{archivoId}/descarga` no
   revela bucket ni clave y solo devuelve bytes disponibles. `DELETE /{archivoId}`
   da de baja un archivo propio.

Una carga pendiente, rechazada, expirada o de otro propósito produce Problem
Details estable y nunca puede asociarse a Portal o Designaciones.

Garantías de publicación, retención y borrado: spec [almacenamiento-adjuntos](../../openspec/specs/almacenamiento-adjuntos/spec.md).

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

Tarea y Proyecto exponen `version`: PUT y los cambios de estado o avance requieren la versión observada; una ausente responde `400` y una obsoleta `409 concurrency-conflict`. Reglas de jerarquía, herencia de Proyecto y visibilidad: spec [tareas-api](../../openspec/specs/tareas-api/spec.md).

### Notificaciones (`/api/notificaciones/`)

Todas las rutas (salvo el ping) exigen sesión (`401` sin ella) y se acotan al usuario autenticado y a su **rol activo**: una notificación con `rolDestino` sólo es visible bajo ese rol; sin `rolDestino` (Tareas, solicitudes de aula anteriores a la columna) es visible bajo cualquiera. Ninguna ruta recibe un id de usuario. Lo ajeno, lo de otro perfil y lo inexistente se responden igual: `404 notificacion-not-found` ("La notificación no existe").

| Método | Path                  | Descripción                                                                                                                        |
| ------ | --------------------- | ---------------------------------------------------------------------------------------------------------------------------------- |
| GET    | `/ping`               | Health check del módulo (anónimo)                                                                                                  |
| GET    | `/no-leidas/cantidad` | `{ cantidad }`: no leídas visibles                                                                                                 |
| GET    | `/`                   | `?pagina=1&tamanio=20&estado=NO_LEIDAS\|LEIDAS`: `{ datos, total, pagina, tamanio, totalPaginas }`, de la más nueva a la más vieja |
| PATCH  | `/{id}/leida`         | Idempotente: conserva el primer instante de lectura; devuelve la notificación                                                      |
| PATCH  | `/leidas`             | Exactamente uno de `{ ids }` (1 a 100 UUID únicos) o `{ todas: true, hasta? }` (ISO 8601 con zona); `{ cantidadActualizada }`      |

`tamanio` va de 1 a 100 (20 por defecto), `pagina` desde 1, `estado` es opcional: un valor inválido responde `400`. En `PATCH /leidas`, cualquier otra combinación responde `400` con código `notificaciones-seleccion-invalida` y el mensaje en el campo ofensor (`ids`, `todas` o `hasta`); los ids que el usuario no ve se ignoran sin revelar si existen. Cada notificación expone `id, rolDestino, tipoEvento, tipoEntidad, entidadId, titulo, mensaje, contexto, leidaEn, creadaEn`; nunca el destinatario.

La emisión no es un endpoint: la hacen Aulas, Designaciones y Tareas a través de `INotificador` después de confirmar su acción (ver [domains/notificaciones.md](./domains/notificaciones.md)).

#### Notificaciones push (Web Push)

Rutas bajo `/api/notificaciones/push/`, todas con sesión y acotadas al usuario de la sesión (ninguna recibe un id de usuario). La especificación está en [notificaciones-push](../../openspec/changes/notificaciones-push/specs/notificaciones-push/spec.md) (change `notificaciones-push`, hasta su archivo).

| Método | Path                  | Descripción                                                                                                                                                                                                                                                                                                     |
| ------ | --------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| GET    | `/push/clave-publica` | `{ clavePublica }` VAPID para `pushManager.subscribe`; `404` si el servidor no tiene claves (push deshabilitado)                                                                                                                                                                                                |
| POST   | `/push/suscripciones` | Cuerpo de `PushSubscription.toJSON()`: `{ endpoint, keys: { p256dh, auth } }`. Upsert atómico por endpoint (`INSERT ... ON CONFLICT`, seguro ante pedidos simultáneos): si lo tenía otro usuario **pasa al de la sesión**. `204`; `404` sin claves; `400` si el endpoint no es `https` de un servicio permitido |
| DELETE | `/push/suscripciones` | Cuerpo `{ endpoint }`. Borra sólo la suscripción de la sesión; `204` también si no existía o era de otro                                                                                                                                                                                                        |
| GET    | `/push/preferencias`  | `{ modulos: [{ modulo, habilitado }] }` con `AULAS`, `DESIGNACIONES` y `TAREAS` (por defecto habilitados)                                                                                                                                                                                                       |
| PUT    | `/push/preferencias`  | Mismo cuerpo, parcial; guarda y devuelve la lista completa. Un `modulo` desconocido responde `400` (`validation`) sin escribir nada                                                                                                                                                                             |

Endpoints permitidos: `https`, puerto por defecto, sin credenciales y host `fcm.googleapis.com`, `updates.push.services.mozilla.com` o terminado en `.push.apple.com`, `.notify.windows.com` o `.push.services.mozilla.com` (si no, el servidor terminaría haciendo POST a una URL arbitraria del cliente). Cada aviso se envía como `{ titulo, mensaje, url, id }` cifrado de punta a punta (RFC 8291); los servicios de push sólo ven texto cifrado. El envío es asíncrono (cola acotada + `BackgroundService`) y nunca demora la acción de negocio; 404/410 del servicio borran la suscripción. Cada aviso pertenece al módulo de la entidad a la que apunta (`SOLICITUD_AULA` → Aulas, `PEDIDO_DESIGNACION` → Designaciones, `TAREA` → Tareas) y un solo interruptor por módulo silencia todos sus tipos. Las preferencias sólo silencian el push: el aviso in-app se crea siempre.

## Idempotencia

Las transiciones de pedidos (`enviar`, `reenviar`, `aceptar`, `rechazar`, `devolver`, `priorizar`, `despriorizar`) requieren `Idempotency-Key: <uuid>`. La identidad lógica de la clave incluye actor, ruta, recurso y payload durante 24 horas: el replay idéntico no repite la transición y retorna el pedido vigente con el ámbito y la vista autorizados en ese momento y una reutilización incompatible retorna `409 idempotency-key-reused`. La exclusión concurrente garantiza una sola transición y un solo evento de historial.

## Versioning

V1 implícito hasta que sea necesario versionar. Cuando se necesite: prefijo `/api/v2/{modulo}/`. NO romper V1 sin período de coexistencia.

## Auto-discovery

Swagger UI disponible en `/swagger` (solo Development).
