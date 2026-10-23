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
| Inferencia          | `GET /api/inferencia/estado`                                                                      | `sistema.estado.ver`                                                |
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

### Asistente (`/api/asistente/`)

| Método | Path                                                     | Permiso                          | Descripción                                                           |
| ------ | -------------------------------------------------------- | -------------------------------- | --------------------------------------------------------------------- |
| GET    | `/ping`                                                  | (anónimo)                        | Health check del módulo                                               |
| POST   | `/consultas`                                             | `asistente.consultar`            | Un turno. Exige `Idempotency-Key`                                     |
| POST   | `/consultas/flujo`                                       | `asistente.consultar`            | El mismo turno, con la redacción por fragmentos (`text/event-stream`) |
| GET    | `/menciones`                                             | `asistente.consultar`            | Busca materias o docentes dentro del alcance, para el popover «@»/«#» |
| GET    | `/capacidades`                                           | `asistente.consultar`            | Qué puede hacer el asistente para este actor                          |
| POST   | `/retroalimentacion`                                     | `asistente.consultar`            | Califica un turno respondido (thumbs + razón)                         |
| GET    | `/historial`                                             | `asistente.consultar`            | Lista (y busca en) las conversaciones propias                         |
| GET    | `/historial/{id}`                                        | `asistente.consultar`            | El detalle de una conversación propia                                 |
| PATCH  | `/historial/{id}`                                        | `asistente.consultar`            | Renombra una conversación propia                                      |
| POST   | `/historial/{id}/archivar`                               | `asistente.consultar`            | Archiva una conversación propia                                       |
| POST   | `/historial/{id}/desarchivar`                            | `asistente.consultar`            | Desarchiva una conversación propia                                    |
| DELETE | `/historial/{id}`                                        | `asistente.consultar`            | Marca una conversación propia pendiente de borrado                    |
| DELETE | `/historial`                                             | `asistente.consultar`            | Marca TODAS las conversaciones propias pendientes de borrado          |
| POST   | `/historial/borrados/{lote}/deshacer`                    | `asistente.consultar`            | Deshace un lote de borrado propio, dentro de su ventana               |
| POST   | `/historial/{id}/reanudar`                               | `asistente.consultar`            | Reanuda una conversación propia                                       |
| POST   | `/historial/turnos/{id}/reejecutar`                      | `asistente.consultar`            | «Volver a consultar» un turno propio ya respondido                    |
| POST   | `/soporte/historial/{actorId}/listar`                    | `asistente.leer_historial_ajeno` | Lista el historial de OTRO actor, con razón obligatoria               |
| POST   | `/soporte/historial/{actorId}/{id}/leer`                 | `asistente.leer_historial_ajeno` | Lee una conversación de OTRO actor, con razón obligatoria             |
| PATCH  | `/administracion/mantenimiento`                          | `asistente.administrar`          | Prende/apaga el modo mantenimiento. Razón obligatoria para prenderlo  |
| GET    | `/administracion/uso`                                    | `asistente.administrar`          | Panel de uso: por usuario, por rol y organizacional                   |
| GET    | `/administracion/presupuestos`                           | `asistente.administrar`          | Tope organizacional, cupos por rol/usuario y gasto estimado del mes   |
| PUT    | `/administracion/presupuestos/roles/{rol}`               | `asistente.administrar`          | Edita el cupo diario default de un rol                                |
| PUT    | `/administracion/presupuestos/usuarios/{actorId}`        | `asistente.administrar`          | Edita el override de cupo diario de un usuario                        |
| DELETE | `/administracion/presupuestos/usuarios/{actorId}`        | `asistente.administrar`          | Restablece el cupo de un usuario al de su rol                         |
| PUT    | `/administracion/presupuestos/roles/{rol}/acceso`        | `asistente.administrar`          | Prende/apaga el acceso de un rol al asistente                         |
| PUT    | `/administracion/presupuestos/usuarios/{actorId}/acceso` | `asistente.administrar`          | Quita o restablece el acceso de un usuario (nunca lo da)              |
| PUT    | `/administracion/tope-organizacional`                    | `asistente.administrar`          | Edita el tope de gasto mensual de la organización                     |
| GET    | `/administracion/servidor-local`                         | `asistente.administrar`          | Carga del servidor del modelo propio y de la compuerta del backend    |

Es el único ping declarado `[AllowAnonymous]` en el código. Los otros cuatro responden anónimos porque el Host no tiene una política global que exija autenticación, no porque lo declaren; si algún día se agrega esa política, dejan de responder. Hay un test que lo demuestra en `PingAsistenteTests`.

El ping vive en un controller **propio y sin constructor**, y eso no es prolijidad: mientras compartió controller con el turno, construirlo exigía resolver las cadenas de solo lectura del asistente —cuya fábrica falla si el ambiente no las configuró— y el ping devolvía 500 sin base. Un ping que necesita configuración de base deja de poder distinguir «el módulo está cargado» de «la base responde». Hay un guard de arquitectura que lo fija.

#### `GET /api/asistente/menciones`

Query: `tipo` (`materia` | `docente`, cualquier otro valor → `400`) y `q` (2–100
caracteres, si no → `400`). Respuesta: `{ resultados: [...], hayMas }`, a lo sumo
6 filas — nunca un conteo de cuántas quedaron afuera (design.md D10 de
asistente-rediseno-v3, ARS-148). Cada fila de `resultados`:

| Campo     | Materia                                                                                                                   | Docente                                      |
| --------- | ------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------- |
| `id`      | `identity.materias.id`                                                                                                    | `identity.personas.id`                       |
| `nombre`  | Nombre de la materia                                                                                                      | Nombre completo («Nombre Apellido»)          |
| `carrera` | La carrera que distingue el homónimo, según `identity.materias_plan` JOIN `identity.planes` (planes `vigente AND activo`) | `null`                                       |
| `codigo`  | Código de la materia                                                                                                      | `null`                                       |
| `cargo`   | `null`                                                                                                                    | Cargo de la designación visible más reciente |

Corre sobre el rol de sólo lectura básico con el actor fijado (transacción de
sólo lectura): el alcance de las materias lo decide
`identity.asistente_materias_visibles()`, el de los docentes la RLS de
`designaciones.designaciones` — que ya conjunta `designaciones.ver` con el
ámbito del actor. Un actor sin ese permiso encuentra cero docentes porque la
policy le deja la tabla vacía, no por un chequeo del backend. Ninguna columna
que toca está clasificada `sensible-*` en el manifiesto (`identity.materias.name/code`,
`identity.carreras.name`, `identity.personas.nombre/apellido`,
`designaciones.cargos.nombre` son todas `publica`); el `id` sí llega al
navegador —eso es lo que D10 registra como fuera de lo que esa clasificación
gobierna—, pero nunca llega al modelo (ver más abajo).

#### `POST /api/asistente/consultas`

Pedido: `{ mensaje, hilo?, reemplaza?, referencias? }`. **No lleva actor**: sale de la identidad de la sesión, porque un identificador tomado del cuerpo sería un selector de alcance controlado por el cliente.

`referencias` (design.md D10/D11 de asistente-rediseno-v3, ARS-148): las
menciones «@materia»/«#docente» elegidas en el composer, a lo sumo 5 —
`[{ tipo: "materia"|"docente", id }]`. El controller revalida cada una con la
misma búsqueda de `GET /menciones` **antes del candado y de todo el pipeline**:
una mención desconocida o fuera del alcance ACTUAL del actor (sus permisos
pueden haber cambiado desde que abrió el popover) responde `400` — el **mismo**
para las dos causas, sin oráculo de existencia — y no cobra cupo ni escribe
historial. Una sexta referencia nunca llega a la acción: el filtro de validación
del modelo la rechaza con `400` antes.

El id de cada referencia **nunca llega al proveedor del modelo**. El generador de
SQL le describe la entidad por nombre y le reserva un marcador `$refN`
(`materias.id = $ref1`, nunca por nombre); el ejecutor liga cada marcador como
parámetro `uuid` recién al correr la consulta. El validador rechaza una consulta
que use un marcador no declarado, o que declare una mención nueva y no la use —
en los dos casos el turno se abstiene, nunca ejecuta contra la entidad
equivocada. Los mismos bindings se guardan con el turno (`asistente.turno_historico.referencias`)
para que «Volver a consultar» y «Reanudar» puedan re-ligarlos —revalidándolos
contra el alcance ACTUAL en el momento de reusarlos, con el mismo criterio que
al crear el turno.

`reemplaza` (asistente-edicion-de-la-ultima-pregunta, design.md D9 de asistente-rediseno-v3, ARS-147): el identificador del turno que este turno reemplaza — la propia `Idempotency-Key` de este pedido, con la que se mandó el turno vivo que se está editando, o el `id` de `GET /historial/{hiloId}` de un turno restaurado por `POST /historial/{hiloId}/reanudar`. Ausente en un turno nuevo cualquiera, que es el caso de siempre.

Se honra **solo** si nombra el último turno vigente del hilo del actor. Si no —un turno que no es el último, o un hilo que ya venció—, el pedido responde `409 Conflict` y **no cambia nada**: ni el hilo, ni el historial, ni el cupo. Sobre éxito, el nuevo turno se resuelve con el mismo contexto conversacional que tenía el reemplazado — segmento vigente y aclaración pendiente incluidos —, pisa el turno viejo en el hilo efímero, revoca su `claveDeRetroalimentacion` (que pasa a rechazarse igual que un token desconocido) y reemplaza, en una sola transacción, su fila en `asistente.turno_historico`: el historial conserva solo la versión final, sin ningún rastro de la pregunta reemplazada ni contador de versiones. El título de la conversación **no cambia** por un reemplazo. Un reemplazo es, para el cupo, la idempotencia y la exclusión de turno concurrente, un turno como cualquier otro: se cobra una sola vez, y un rechazo por candado o un reintento con la misma `Idempotency-Key` y el mismo `reemplaza` no aplican una segunda vez.

Respuesta:

| Campo                      | Qué                                                                                                                 |
| -------------------------- | ------------------------------------------------------------------------------------------------------------------- |
| `estado`                   | `respondida` · `no_contestable` · `necesita_aclaracion` · `servicio_degradado`                                      |
| `respuesta`                | El texto que lee el usuario                                                                                         |
| `hilo`                     | Para mandarlo en el turno siguiente                                                                                 |
| `preguntaInterpretada`     | Solo si difiere del mensaje                                                                                         |
| `razonamiento`             | Cómo se interpretó la pregunta, tal como lo devolvió la generación                                                  |
| `opciones[]`               | El menú de una aclaración. **Bloquean** el turno                                                                    |
| `columnas[]`               | Nombre y marca de sensibilidad                                                                                      |
| `filas[]`                  | Los valores reales, incluidos los que no viajaron al modelo                                                         |
| `truncado`                 | Booleano, **nunca** un conteo                                                                                       |
| `vinculos[]`               | Qué celdas llevan a una pantalla del sistema                                                                        |
| `vinculosDelTexto[]`       | Dónde, dentro de `respuesta`, aparece un valor que una celda ya vinculó (`{ desde, largo, tipo, id }`)              |
| `principal`                | El elemento sobre el que trata la respuesta, con su ficha (`{ tipo, id, datos }`); `null` si no hay                 |
| `origenes[]`               | De qué áreas salen los datos: códigos de schema (`designaciones`, `identity`, `portal`)                             |
| `sql`                      | Solo con `asistente.ver_consulta`                                                                                   |
| `metricas`                 | Llamadas al modelo y categoría                                                                                      |
| `claveDeRetroalimentacion` | Solo cuando `estado = respondida`. Ver `POST /api/asistente/retroalimentacion`                                      |
| `cupoRestante`             | El cupo diario del actor, YA COBRADO este turno (asistente-cupo-visible)                                            |
| `conversacion`             | El id de `GET /historial` en que quedó este turno; nulo si no se persistió (design.md D13 de asistente-rediseno-v3) |

`opciones` sólo viaja cuando `estado = necesita_aclaracion`: bloquea el turno esperando una elección. No hay un campo equivalente para después de una respuesta o un rechazo — el asistente ya no sugiere próximos pasos fuera de la pantalla de bienvenida (ARS-140, ARS-149, design.md D12 de asistente-rediseno-v3); el catálogo de `GET /capacidades` sigue siendo la única fuente de ejemplos clicables.

`estado` usa etiquetas propias del contrato y no el nombre del enum del backend: renombrar un valor interno no puede romper a los clientes en silencio.

**`vinculos[]`** trae `{ fila, columna, tipo, id }` y **no una URL**: el backend dice qué clase de recurso identifica cada celda y con qué identificador se abre, y la ruta la resuelve el cliente, que es donde viven las rutas. Un cliente que no reconoce un `tipo` **no pinta nada** — así, el día que se ofrezcan vínculos a otro módulo, un cliente viejo no muestra un enlace roto.

Que una fila esté en `filas[]` **no implica** que traiga vínculo. Las filas las filtra el motor con las policies de RLS del asistente; la pantalla la autoriza el módulo dueño del recurso, con su propia regla. **No son la misma regla y divergen hoy**: el ámbito departamental es una lista fija de códigos de rol en el módulo y `identity.roles.scope` en las funciones del asistente; los roles del módulo salen del token y están acotados al rol seleccionado en la sesión, y los del asistente son las asignaciones vigentes leídas en vivo; el permiso es un claim de un lado y la matriz en vivo del otro. Por eso el vínculo se le pregunta al módulo dueño —`IDesignacionesQueries.UbicarPedidosAsync` para el trámite— y no se deduce de que la fila haya llegado: deducirlo produce un botón que responde 403 al apretarlo, que es lo que el invariante #7 prohíbe.

**`vinculosDelTexto[]`** trae `{ desde, largo, tipo, id }` por cada aparición, dentro de `respuesta`, del valor de una celda que **ya tiene vínculo**. Es la forma de enlazar la prosa sin que el modelo escriba enlaces:

- **Sólo se señala lo que el resultado ya vinculó.** Un número que aparece únicamente en el texto, o cuya celda no se resolvió, no lleva enlace. No hay una regex por tipo en el asistente ni un segundo resolutor.
- **La coincidencia es exacta, ordinal y por palabra completa**: inmediatamente antes y después del valor no puede haber una letra, un dígito ni un guion (`2026-90051` no enlaza `2026-9005`). Sin normalizar mayúsculas ni tildes.
- **`desde` y `largo` son índices de `string` de .NET**, es decir unidades de código UTF-16: las mismas que usa `String.prototype.slice` en el cliente, así que no hace falta convertir nada. Se calculan sobre `respuesta` **tal como sale**, después de cualquier reescritura del rechazo; el cliente tiene que pintar el texto sin transformarlo (sin recortar ni normalizar espacios) o las posiciones dejan de coincidir.
- Las apariciones vienen en orden y **sin superponerse**. `tipo` e `id` son los de `vinculos[]`; un cliente que no reconoce el `tipo` no pinta el enlace.
- Un turno restaurado del historial no las trae: el historial no persiste el texto.

**`principal`** es el elemento sobre el que trata la respuesta, con una ficha armada por el módulo dueño. Hay principal cuando el resultado tiene **exactamente una fila y esa fila tiene exactamente un vínculo**, y el módulo entregó la ficha: es una regla determinista, no una decisión del modelo. Si el módulo no la entrega (el actor no alcanza el trámite, el proveedor falló, no hay adaptador), `principal` es `null` y la respuesta se muestra como si no existiera la ficha; un fallo se registra como advertencia y **nunca** tumba el turno.

```json
{
  "tipo": "pedido-designacion",
  "id": "d5000000-0000-4000-8000-000000000005",
  "datos": {
    "numero": "2026-9005",
    "novedad": "Cambio de cargo o dedicación",
    "materia": "Ingeniería de Software",
    "estado": "en_revision_coordinador",
    "estadoEtiqueta": "En Coordinación",
    "periodo": "2026 – 1.er cuatrimestre",
    "cargo": "Profesor Adjunto",
    "dedicacion": "Categoría 3",
    "ultimaActividad": "2026-09-03T14:30:00.0000000+00:00"
  }
}
```

`datos` es un diccionario de **claves documentadas por tipo**, no un tipo del contrato: el asistente no referencia los DTO de otros módulos (ARS-46) y las transporta tal cual. Para `pedido-designacion`, `cargo` y `dedicacion` sólo están si el trámite los tiene; `ultimaActividad` es ISO 8601; `estadoEtiqueta` la pone Designaciones (dueña del vocabulario) y el tono visual lo decide el cliente a partir de `estado`, con un neutro para un código que no conozca. **Estos datos no pasan por el modelo**, y se autorizan con el mismo criterio que `GET /api/designaciones/pedidos/{id}` (permiso `designaciones.ver` más el ámbito del actor). Un cliente que no reconoce el `tipo` ignora la ficha.

**`origenes[]`** lista los **códigos de schema** que leyó la consulta ejecutada —`designaciones`, `identity`, `portal`—, sin repetir y en orden alfabético. Se derivan de los identificadores calificados de la consulta validada, restringidos a los schemas que el asistente tiene habilitados. Es vacío cuando el turno no ejecutó consulta (aclaración, rechazo, error, degradado) y cuando la consulta no devolvió filas. El cliente traduce el código a una etiqueta («Designaciones», «Portal», «Institucional» para `identity`) y omite los que no conoce. **Nunca** viajan la consulta, sus tablas ni la cantidad de filas: en una respuesta truncada un conteo podría leerse como el total, que es justo lo que `truncado` se niega a revelar.

**`Idempotency-Key` obligatoria.** Cada turno cuesta dos o tres llamadas al modelo, así que un doble submit se factura completo dos veces. Se resuelve **en memoria con expiración corta y acotada por actor** — no se reusa ni se copia `designaciones.idempotencia_comandos`, que guarda el cuerpo completo de la respuesta HTTP, que es exactamente lo que este módulo decidió no persistir.

**`409 Conflict`**: únicamente cuando `reemplaza` no nombra el último turno vigente del hilo del actor (target de reemplazo inválido o hilo vencido). No cambia nada — ni el hilo, ni el historial, ni el cupo —, así que un reintento con un objetivo válido es seguro.

#### `POST /api/asistente/consultas/flujo`

El mismo pedido, la misma `Idempotency-Key` y la misma lógica que `POST /consultas` (asistente-optimizaciones-modelo-local, design.md D9). El cliente lo usa sólo si `GET /capacidades` trae `redaccionEnFlujo: true`.

Todo lo que falla **antes de escribir nada** —clave faltante, mención no disponible, hilo ajeno, reemplazo inválido— responde con el mismo status y el mismo `ProblemDetails` de `/consultas`. Después responde `200 text/event-stream` (con `Cache-Control: no-cache` y `X-Accel-Buffering: no`) y estos eventos:

| Evento      | `data`                                           | Cuándo                                                        |
| ----------- | ------------------------------------------------ | ------------------------------------------------------------- |
| `redaccion` | `{ "texto": "<fragmento>" }`                     | Cada fragmento de la redacción, en orden                      |
| `resultado` | La misma `RespuestaDelAsistente` de `/consultas` | Al final, siempre que el turno termine                        |
| `error`     | `{ "status": 500 }`                              | Si algo falla después de empezar; no llega ningún `resultado` |

`vinculosDelTexto`, `principal` y `origenes` viajan **sólo en el `resultado` final**: los fragmentos de `redaccion` son borradores sin posiciones, así que los enlaces del texto y la ficha aparecen recién cuando llega ese evento —el mismo momento en que aparecen la tabla y `vinculos[]`—.

Los fragmentos son una **vista previa**: lo que vale es `resultado`, que puede no coincidir carácter a carácter con su concatenación (se recorta y se descarta razonamiento). Un turno que no redacta con el modelo —sin filas, plantilla, abstención, degradado, idempotencia recordada— manda sólo `resultado`. Sólo el proveedor `local` emite fragmentos; con otro, el flujo trae sólo `resultado`. Si el cliente se desconecta el turno termina igual y queda guardado bajo su clave.

#### `GET /api/asistente/capacidades`

Devuelve `cubre[]` con sus conteos, `tablas`, `columnas`, `ejemplos[]`, `noPuede[]`, `alcance`, `presentacion`, `mantenimiento` y `cupo` (asistente-modo-mantenimiento / asistente-cupo-visible).

`redaccionEnFlujo` (booleano) dice si conviene pedir el turno a `POST /consultas/flujo`; refleja la opción `StreamingDeRedaccion` del despliegue, no al actor.

`mantenimiento: { activo, razon }` refleja el estado GLOBAL del kill switch, sin bypass — así el banner es consistente para todo el mundo aunque un admin no esté bloqueado por él.

`cupo: { restante, bloqueado, motivo, vuelveA }` es el cupo diario de ESTE actor, con el bypass de mantenimiento del admin ya aplicado: un actor con `asistente.administrar` no se ve a sí mismo como bloqueado por mantenimiento. `motivo` es uno de `presupuesto_propio` | `tope_organizacional` | `mantenimiento` | `sin_acceso` (asistente-acceso-granular; ningún bypass lo levanta), nulo si no está bloqueado. `restante` vale `2147483647` (`int.MaxValue`) cuando el cupo está desactivado (0). `vuelveA` sólo se conoce para `presupuesto_propio`.

`presentacion` es la única parte del catálogo que mira el **rol** del actor y no sus GRANT: es la línea que le dice por qué cosas suele venir a preguntar. Sale del código de su único rol vigente; con varios roles, con ninguno, o con uno que el backend no reconoce, devuelve un texto genérico que no promete nada de más. Vive en el backend porque el cliente no tiene catálogo de roles y no debe crecer uno: `identity.roles` no es cerrado —Secretaría crea roles desde la aplicación— así que una lista embebida en el cliente se desactualizaría sola. El rol **no** influye en `alcance`, en los conteos, en los ejemplos ni en qué conexión de lectura se usa.

Se deriva de los **GRANT efectivos** del rol con el que el actor consulta y **nunca del payload del prompt**: el prefijo trae el esquema entero, columnas personales incluidas, así que un catálogo derivado de ahí ofrecería preguntas sobre columnas que el rol no puede leer.

Cada ejemplo se valida con `EXPLAIN` contra los privilegios del actor antes de ofrecerse. Cuesta cero tokens, así que sigue respondiendo con el proveedor caído.

#### `POST /api/asistente/retroalimentacion`

Pedido: `{ token, voto, razones?, comentario? }`. `token` es `claveDeRetroalimentacion` de un turno `respondida` — autoriza calificar **ese turno**, no identifica a quién lo envía. `voto` es booleano (👍/👎). `razones` es opcional y, solo cuando `voto` es falso, una lista de cero o más de cuatro valores cerrados, sin repetidos: `datos_incorrectos`, `no_entendio_la_pregunta`, `faltan_datos`, `otro`. `comentario` es opcional, texto libre de hasta 500 caracteres, recortado (los espacios al principio y al final no cuentan) antes de validar el largo y de guardarse; vacío después de recortar se trata como ausente. `razones`/`comentario` presentes junto a `voto: true` se ignoran del lado del servidor — nunca se confía en que el cliente los haya omitido. `lento` (retirado por `asistente-rediseno-v3`, design.md D7, PO-changed 2026-09-26) se rechaza igual que cualquier otro valor desconocido — nada shippeó a producción con esa razón, así que no hay ninguna fila vieja que preservar. `comentario` nunca se loguea, nunca viaja al proveedor del modelo y no tiene superficie de lectura en ninguna pantalla (ver TD-012 en `docs/quality/tech-debt.md`).

Respuestas:

| Estado            | Cuándo                                                                                                                              |
| ----------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| `204 No Content`  | El voto quedó registrado (alta o cambio de voto)                                                                                    |
| `400 Bad Request` | Una razón no es una de las cuatro permitidas, una razón está repetida, o `comentario` supera los 500 caracteres después de recortar |
| `404 Not Found`   | El token no existe o venció. **Mismo cuerpo** para los dos casos —para que un llamador no pueda distinguir «vencido» de «inventado» |
| `401`/`403`       | Igual que el resto del módulo: sin `asistente.consultar` no hay token que valga                                                     |

**Autorización por posesión del token, no por identidad del actor.** El token es un UUID aleatorio devuelto una sola vez, en la misma respuesta del turno; una ventana de validez de 120 minutos (config `Asistente__VigenciaDeRetroalimentacionMinutos`) lo vence, en memoria y sin persistencia, con el mismo criterio que la idempotencia de `/consultas`. Ver `asistente-retroalimentacion` en `docs/architecture/domains/asistente.md` y TD-012.

**Es un upsert.** Reenviar con el mismo token reemplaza el voto y la razón anteriores — no se conserva historial de votos previos (una fila por turno, la última gana).

#### `GET /api/asistente/historial` y `/historial/{hiloId}`

`GET /historial?q=<texto>` devuelve `[{ id, titulo, creadoEn, ultimaActividad, archivada, pendienteDeBorrado }]`, acotado a las conversaciones propias del actor de la sesión que **no** están pendientes de borrado, de la más reciente a la más vieja. `q` opcional busca por texto completo (español, con stemming) contra las preguntas propias — nunca contra las de otro actor. Incluye las archivadas, flagueadas: `archivada` distingue el estado; `pendienteDeBorrado` siempre viaja en `false` acá — una conversación pendiente nunca aparece en este endpoint, ver más abajo (design.md D3/D4 de asistente-rediseno-v3).

`GET /historial/{hiloId}` devuelve la conversación con sus turnos: `{ id, titulo, creadoEn, ultimaActividad, turnos: [{ id, pregunta, sql, estado, ocurrioEn, menciones? }] }`. `sql` viaja **solo** con `asistente.ver_consulta` — mismo gate que `sql` en `POST /consultas`. `404` si el id no existe, no es del actor, o está pendiente de borrado: **mismo cuerpo** para los tres casos.

`menciones` (decisión 15 del PO, 2026-09-26, design.md D11 de asistente-rediseno-v3): `[{ tipo, id, etiqueta }]`, una por cada referencia que el turno guardó (`turno_historico.referencias`), re-resuelta contra el alcance **actual** de quien lee — nunca el que tenía el actor que hizo la pregunta originalmente. Una referencia que ese alcance ya no llega a ver **se omite de la lista** — nunca se filtra, y su texto en `pregunta` queda tal cual, plano —, con el mismo criterio sin oráculo de existencia que ya usa la revalidación de «Volver a consultar». `etiqueta` es el texto exacto que insertó el composer al elegirla («@Nombre» / «#Nombre»), para que el cliente la ubique en `pregunta` con la misma función que ya usa para un turno en vivo, sin que este lado calcule posiciones. El campo siempre viaja en este endpoint (una lista vacía si ninguna referencia sobrevivió), pero **está ausente** —no `[]`— en la lectura de soporte de más abajo.

#### `PATCH /api/asistente/historial/{hiloId}`, `.../archivar`, `.../desarchivar`

`PATCH` renombra: `{ titulo }`, `204` si es propia, `404` si no. `POST .../archivar` y `.../desarchivar` no llevan cuerpo: `204`/`404`, mismo criterio. Archivar no toca `ultimaActividad` — la retención de 180 días sigue contando igual — y una conversación archivada se desarchiva sola en cuanto recibe un turno nuevo.

#### `DELETE /api/asistente/historial/{hiloId}`, `DELETE /historial` y `POST .../borrados/{lote}/deshacer`

**Rotura de contrato:** las dos formas de `DELETE` devolvían `204` sin cuerpo; ahora devuelven `200 { "loteDeBorrado": "<uuid>" }` (design.md D4). `DELETE /historial/{hiloId}` marca pendiente de borrado una conversación propia — desaparece de inmediato de `GET /historial` y de cualquier otro endpoint propio, aunque todavía exista en la base — y `404` si no es propia o ya estaba pendiente. `DELETE /historial` (sin id) marca pendientes **todas** las conversaciones propias no pendientes, archivadas incluidas, con un lote nuevo; siempre `200`, aunque no haya ninguna.

`POST /historial/borrados/{lote}/deshacer` (sin cuerpo) limpia esa marca si el lote es del actor y todavía está dentro de la ventana del servidor (`Asistente__VentanaDeDeshacerSegundos`, default 15 s — los 10 s que la interfaz muestra «Deshacer» más 5 s de margen de red): `204`. Un lote ajeno, desconocido o vencido responde el mismo `404` — los tres casos son indistinguibles a propósito. Un borrado que nunca se deshace es físicamente permanente dentro de `Asistente__PeriodoDeBarridoDeBorradosSegundos` (default 60 s) desde que vence su ventana, sin depender de que el cliente siga abierto.

#### `POST /api/asistente/historial/{hiloId}/reanudar`

Sin cuerpo. Devuelve `{ hilo, turnos: [...] }`: `hilo` es un id **efímero nuevo** —el mismo campo que `POST /consultas` ya round-tripea— con el que seguir preguntando; `turnos` son los persistidos, para que la interfaz los pinte de una, cada uno con el mismo `menciones` de `GET /historial/{hiloId}` (decisión 15 del PO). El servidor siembra un hilo conversacional nuevo con esos turnos ya cargados, así que un seguimiento con anáfora ("¿y el de Pérez?") resuelve contra ese contexto igual que si la conversación nunca se hubiera cortado. `404` si la conversación no es propia.

#### `POST /api/asistente/historial/turnos/{turnoId}/reejecutar`

Sin cuerpo. «Volver a consultar»: re-ejecuta la SQL guardada de un turno propio ya `respondida`, bajo el alcance **actual** del actor — nunca llama al modelo, nunca escribe una fila de historial ni de los registros existentes. Devuelve `{ exitosa, mensaje?, columnas[], filas[], truncado }`: con `exitosa: false` (SQL que ya no corre — privilegios que se achicaron, esquema que cambió), `mensaje` trae una explicación no técnica y `columnas`/`filas` vienen vacías — **nunca** un error HTTP crudo por un rechazo del motor. `400` si el turno no terminó `respondida` o no dejó SQL guardada; `404` si el turno no es propio.

Si la SQL guardada usa un marcador `$refN` (design.md D11 de asistente-rediseno-v3), cada uno se revalida con la misma búsqueda de `GET /menciones` antes de re-ejecutar: una referencia que perdió su alcance desde que se hizo la pregunta se ve igual que cualquier otra SQL que ya no corre (`exitosa: false`), nunca un error crudo.

#### `POST /api/asistente/soporte/historial/{actorId}/listar` y `/{actorId}/{hiloId}/leer`

Exigen `asistente.leer_historial_ajeno` — sembrado a **ningún** rol por default, distinto de `asistente.consultar` — y un cuerpo `{ razon }` con texto no vacío. `POST` (no `GET`) a propósito: la razón nunca viaja en la URL, donde terminaría en un log de acceso o el historial del navegador. `400` sin razón o con razón en blanco.

`listar` devuelve la lista de conversaciones del actor indicado (mismo shape que `GET /historial`, sin turnos) — con una diferencia deliberada: **también incluye las que están pendientes de borrado, mientras su ventana no haya vencido**, con `pendienteDeBorrado: true` (asistente-acceso-de-soporte-al-historial, design.md D4). Vencida la ventana, desaparece de acá también. `leer` devuelve una conversación puntual con `sql` **siempre** presente (sin el gate de `asistente.ver_consulta`: el permiso de soporte ya es el de diagnóstico) — nunca filas de resultado, y ninguna acción de re-ejecución en la respuesta ni en ningún otro endpoint de este controller; `404` si la conversación no existe, no es del actor indicado, o su ventana de borrado ya venció. **Nunca trae `menciones`** (decisión 15 del PO): quien lee acá nunca es el actor cuyo alcance decide si una mención se ve, así que el campo va ausente en vez de una lista siempre vacía — a diferencia del lado propio, de arriba. Cada llamada escribe, ANTES de devolver nada, una fila en `asistente.auditoria_acceso_historial` — inclusive al leer una conversación pendiente; si esa escritura falla, no se devuelve ningún dato. Ningún endpoint del módulo expone, al actor cuyo historial fue leído, que alguien lo haya leído — decisión final, no pendiente.

#### `PATCH /api/asistente/administracion/mantenimiento`

Pedido: `{ activo, razon? }`. Exige `asistente.administrar` — sembrado directamente a `sys_admin` (design.md D13 de asistente-administracion-de-uso), distinto de `asistente.leer_historial_ajeno`. `razon` es obligatoria para `activo: true`; `400` si viene vacía o en blanco y el flag no cambia. Desactivar no exige razón. Devuelve `{ activo, razon }`. Cada toggle (encendido o apagado) escribe, ANTES de devolver éxito, una fila en `asistente.auditoria_administracion` con actor, momento, acción y el par antes/después.

#### `GET /api/asistente/administracion/uso`

Query: `periodo` (`dia` | `semana` | `mes`, default `dia`) o el rango explícito `desde`/`hasta`. Exige `asistente.administrar`. Devuelve `{ porUsuario[], porRol[], organizacion }`, cada uno con `{ clave, nombreParaMostrar?, turnos, porEstado, llamadasAlModelo, tokensDeEntrada, tokensDeSalida, tokensDeCache, latenciaPromedioMs, latenciaP95Ms, proveedores[], costoEstimado, esEstimado: true, turnosSinPrecio, codigosDeRol[], cupoEfectivo, origenDeCupo, accesoEfectivo, origenDeAcceso }`.

Se agrega **sólo** desde `asistente.registro_operativo` — nunca `registro_analitico` (TD-012): no hay ningún campo con el texto de una pregunta. `nombreParaMostrar` se resuelve vía `IConsultasIdentity.ListarUsuariosAsync` (design.md D12), nunca vía `usuarios.ver`: un admin con sólo `asistente.administrar` ve nombres igual. `costoEstimado` sale de `CalculadoraDeCosto` contra `asistente.tabla_de_precios`, con el precio vigente en el momento en que cada fila ocurrió; `turnosSinPrecio` cuenta las filas sin ningún precio vigente para su proveedor/modelo — **nunca** se costean en cero. Un actor con más de un rol de sistema vigente suma su uso a TODOS esos roles en `porRol`.

`codigosDeRol`, `cupoEfectivo` y `origenDeCupo` sólo tienen contenido en una fila de `porUsuario` (lista/`null`/`null` en una fila de `porRol` o en `organizacion`, donde el concepto no aplica). `codigosDeRol` son los códigos de rol de sistema vigentes del actor, resueltos vía `IConsultasIdentity.ObtenerCodigosDeRolesDeSistemaAsync` — nunca un nombre legible: esa interfaz no expone ningún catálogo de roles con nombre, así que el frontend traduce el código. `cupoEfectivo`/`origenDeCupo` (`"override"` | `"rol"` | `null`) resuelven, con la misma regla que aplicaría un turno real (`CuotaPersistente.CupoEfectivoAsync`, espejada en `ReglaDeCupoEfectivo`), el cupo diario efectivo de ese actor contra `GET …/presupuestos`: un override vigente gana siempre; si no hay override, es el mínimo cupo entre los roles del actor cuyo default está activado (mayor que cero); `null`/`null` sólo cuando el actor no tiene override NI ningún rol de sistema del que heredar un default — el único caso donde no hay nada que mostrar sin inventarlo.

`accesoEfectivo`/`origenDeAcceso` (`"rol"` | `"propio"`) también sólo en `porUsuario` (`null`/`null` en el resto): si el actor tiene hoy acceso operativo al asistente, resuelto con `ReglaDeAccesoEfectivo` — la **misma** clase que usa el bloqueo del turno, no un espejo (asistente-acceso-granular, design.md D2). Una revocación propia gana (`false`/`"propio"`); si no, basta un rol del actor con el acceso habilitado; un actor sin roles de sistema tiene acceso (lo decide sólo `asistente.consultar`).

#### `GET /api/asistente/administracion/presupuestos`

Exige `asistente.administrar`. Cierra el gap de "sólo `PUT`, nunca `GET`" que documentaban `TopeOrganizacionalCard`/`EditorDeCupoEnFila` (tarea 12.8 de `sistema-seccion-unificada`): expone el tope y los cupos que las tres rutas `PUT` de abajo editan, más el gasto estimado del mes en curso. Devuelve:

```json
{
  "topeMensualUsd": 500.00,
  "gastoEstimadoDelMes": 123.45,
  "esEstimado": true,
  "cuposPorRol": [{ "rol": "docente", "cupoDiarioTurnos": 20, "accesoHabilitado": true }, ...],
  "overridesPorUsuario": [{ "actorId": "...", "nombreParaMostrar": "...", "cupoDiarioTurnos": 5 }, ...],
  "accesosRevocados": ["<actorId>", ...]
}
```

`topeMensualUsd` y cada `cupoDiarioTurnos` son `0` cuando ese límite está desactivado — mismo convenio que sus `PUT`. `cuposPorRol` trae los siete roles de sistema, siempre (seed de `006_asistente_administracion.sql`); `overridesPorUsuario` sólo trae actores con un override vigente (`presupuesto_usuario.vigente_hasta IS NULL`). `nombreParaMostrar` se resuelve vía `IConsultasIdentity.ListarUsuariosAsync`, mismo seam que `GET …/uso` (design.md D12) — no hay un método para el nombre legible de un ROL, así que `cuposPorRol` sólo trae el código.

`gastoEstimadoDelMes` es el costo estimado del mes **calendario** en curso, con el mismo límite UTC que ya usa el acumulador que aplica el tope (design.md D2 de `asistente-administracion-de-uso`) — no América/Argentina/Buenos_Aires: introducir una segunda noción de "mes" para este único campo hubiera sido una inconsistencia nueva, no una mejora. Se calcula llamando al mismo `IConsultasDeUso` que `GET …/uso` usa, con el rango `[inicio del mes, ahora)`, así que por construcción coincide con lo que ese otro endpoint reportaría para el mismo rango — nunca la factura real del proveedor (`esEstimado: true`, mismo campo que `UsoAgregadoDto`).

**Deliberadamente fuera de alcance** (ver el reporte de apply del cambio `sistema-seccion-unificada`, tarea 12.8): la serie diaria de uso para un gráfico de tendencia y la telemetría de proveedor cloud/local (GPU, KV cache, slots del servidor local). La del servidor local llegó después, con `GET …/servidor-local`. El acceso por rol (`accesoHabilitado`) y las revocaciones por usuario (`accesosRevocados`) se suman con `asistente-acceso-granular`.

#### `PUT /api/asistente/administracion/presupuestos/roles/{rol}` y `/presupuestos/usuarios/{actorId}`

Pedido: `{ cupo }` (turnos por día; `0` desactiva). Exigen `asistente.administrar`. El primero edita el default de un código de rol de sistema; el segundo, el override de un actor puntual, que **siempre** gana sobre el default de su rol (design.md D2/D3, tareas 3.4/9.5), más chico o más grande. Ambos escriben, antes de devolver `204`, una fila en `asistente.auditoria_administracion` con el par antes/después.

#### `DELETE /api/asistente/administracion/presupuestos/usuarios/{actorId}`

Exige `asistente.administrar`. Restablece el cupo del usuario al de su rol: cierra la vigencia del override (`vigente_hasta = now()`) sin abrir otro, así la historia queda entera (asistente-acceso-granular, design.md D5). Sin override vigente, no cambia nada. Audita `presupuesto.usuario.restablecer` y devuelve `204`.

#### `PUT /api/asistente/administracion/presupuestos/roles/{rol}/acceso` y `/presupuestos/usuarios/{actorId}/acceso`

Pedido: `{ habilitado }`. Exigen `asistente.administrar`. El acceso operativo es una capa **adicional** a `asistente.consultar` y sólo puede restringir (design.md D1). En un rol, prende o apaga `presupuesto_rol.acceso_habilitado`. En un usuario, `false` lo revoca y `true` **sólo borra la revocación**: a un usuario se le puede quitar el acceso, no darlo por encima de su rol (D3). Un actor sin acceso efectivo recibe sus turnos como `servicio_degradado` con el motivo `sin_acceso` en `GET /capacidades` (`cupo.motivo`), sin llamar al modelo ni correr el pipeline. Auditan `acceso.rol` / `acceso.usuario` y devuelven `204`.

#### `PUT /api/asistente/administracion/tope-organizacional`

Pedido: `{ topeMensualUsd }` (`0` desactiva). Exige `asistente.administrar`. Igual disciplina de auditoría que los dos anteriores.

#### `GET /api/asistente/administracion/servidor-local`

Exige `asistente.administrar`. La carga del servidor del modelo propio (asistente-optimizaciones-modelo-local, design.md D8):

```json
{
  "configurado": true,
  "alcanzable": true,
  "motor": "vllm",
  "enCurso": 3,
  "enEspera": 0,
  "usoDeKvCache": 0.42,
  "aciertosDeCacheDePrefijo": 0.87,
  "compuerta": { "capacidad": 8, "enCurso": 3, "enEspera": 0 }
}
```

`configurado` es `false` con un proveedor que no es `local`, y entonces todo lo demás viene vacío. `alcanzable` es `false` si `/metrics` no respondió en 3 s o no tiene métricas conocidas. `motor` es `vllm` o `llama.cpp`, según qué métricas publica. Cada métrica es `null` si el servidor no la publica —llama-server no informa aciertos de prefijo—, nunca `0`. `usoDeKvCache` y `aciertosDeCacheDePrefijo` van de 0 a 1; el segundo es acumulado desde que arrancó el servidor. `compuerta` es `null` sin `MaximoDeLlamadasConcurrentes`.

La URL de métricas es la del proveedor sin `/v1`, con la misma clave como `Bearer`. La clave nunca aparece en la respuesta ni en el log.

## Idempotencia

Las transiciones de pedidos (`enviar`, `reenviar`, `aceptar`, `rechazar`, `devolver`, `priorizar`, `despriorizar`) requieren `Idempotency-Key: <uuid>`. La identidad lógica de la clave incluye actor, ruta, recurso y payload durante 24 horas: el replay idéntico retorna la misma respuesta y una reutilización incompatible retorna `409 idempotency-key-reused`. La exclusión concurrente garantiza una sola transición y un solo evento de historial.

## Versioning

V1 implícito hasta que sea necesario versionar. Cuando se necesite: prefijo `/api/v2/{modulo}/`. NO romper V1 sin período de coexistencia.

## Auto-discovery

Swagger UI disponible en `/swagger` (solo Development). Cada controller debe tener atributos `[ProducesResponseType]` para que el schema sea preciso.
