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

En desarrollo, y sólo con `DevelopmentAuthentication:Enabled=true`, el cliente puede enviar `X-Dev-User-Id` y `X-Dev-Role-Code`. El Host valida ambos valores contra una identidad sintética activa. En Production no se registran el esquema, los headers ni `/api/desarrollo/identidades`. La futura integración Azure AD deberá producir el mismo `ICurrentUser` sin cambiar contratos de negocio.

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
- `401 Unauthorized` — falta token o inválido
- `403 Forbidden` — token válido pero rol no autorizado
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
| Sesión dev          | `GET /api/desarrollo/identidades`                                                                 | sólo ambiente no productivo con opt-in                              |

Todos los DTOs usan JSON `camelCase`, UUIDs canónicos y fechas ISO. Las respuestas de pedidos incluyen historial y `accionesPermitidas`; el frontend no vuelve a ejecutar la autorización ni la máquina de estados. En pedidos, el Alta envía `persona { documento, nombre, apellido }` sin `personaId`; Baja y Cambio envían `personaId` y siempre `materiaId` explícito.

## Endpoints por módulo

### Designaciones (`/api/designaciones/`)

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

### Tareas (`/api/tareas/`)

| Método | Path    | Rol mínimo | Descripción                           |
| ------ | ------- | ---------- | ------------------------------------- |
| GET    | `/ping` | (anónimo)  | Health check del módulo               |
| ...    | ...     | ...        | _(a documentar en specs por feature)_ |

### Asistente (`/api/asistente/`)

| Método | Path                                              | Permiso                          | Descripción                                                           |
| ------ | ------------------------------------------------- | -------------------------------- | --------------------------------------------------------------------- |
| GET    | `/ping`                                           | (anónimo)                        | Health check del módulo                                               |
| POST   | `/consultas`                                      | `asistente.consultar`            | Un turno. Exige `Idempotency-Key`                                     |
| GET    | `/menciones`                                      | `asistente.consultar`            | Busca materias o docentes dentro del alcance, para el popover «@»/«#» |
| GET    | `/capacidades`                                    | `asistente.consultar`            | Qué puede hacer el asistente para este actor                          |
| POST   | `/retroalimentacion`                              | `asistente.consultar`            | Califica un turno respondido (thumbs + razón)                         |
| GET    | `/historial`                                      | `asistente.consultar`            | Lista (y busca en) las conversaciones propias                         |
| GET    | `/historial/{id}`                                 | `asistente.consultar`            | El detalle de una conversación propia                                 |
| PATCH  | `/historial/{id}`                                 | `asistente.consultar`            | Renombra una conversación propia                                      |
| POST   | `/historial/{id}/archivar`                        | `asistente.consultar`            | Archiva una conversación propia                                       |
| POST   | `/historial/{id}/desarchivar`                     | `asistente.consultar`            | Desarchiva una conversación propia                                    |
| DELETE | `/historial/{id}`                                 | `asistente.consultar`            | Marca una conversación propia pendiente de borrado                    |
| DELETE | `/historial`                                      | `asistente.consultar`            | Marca TODAS las conversaciones propias pendientes de borrado          |
| POST   | `/historial/borrados/{lote}/deshacer`             | `asistente.consultar`            | Deshace un lote de borrado propio, dentro de su ventana               |
| POST   | `/historial/{id}/reanudar`                        | `asistente.consultar`            | Reanuda una conversación propia                                       |
| POST   | `/historial/turnos/{id}/reejecutar`               | `asistente.consultar`            | «Volver a consultar» un turno propio ya respondido                    |
| POST   | `/soporte/historial/{actorId}/listar`             | `asistente.leer_historial_ajeno` | Lista el historial de OTRO actor, con razón obligatoria               |
| POST   | `/soporte/historial/{actorId}/{id}/leer`          | `asistente.leer_historial_ajeno` | Lee una conversación de OTRO actor, con razón obligatoria             |
| PATCH  | `/administracion/mantenimiento`                   | `asistente.administrar`          | Prende/apaga el modo mantenimiento. Razón obligatoria para prenderlo  |
| GET    | `/administracion/uso`                             | `asistente.administrar`          | Panel de uso: por usuario, por rol y organizacional                   |
| PUT    | `/administracion/presupuestos/roles/{rol}`        | `asistente.administrar`          | Edita el cupo diario default de un rol                                |
| PUT    | `/administracion/presupuestos/usuarios/{actorId}` | `asistente.administrar`          | Edita el override de cupo diario de un usuario                        |
| PUT    | `/administracion/tope-organizacional`             | `asistente.administrar`          | Edita el tope de gasto mensual de la organización                     |

Es el único ping declarado `[AllowAnonymous]` en el código. Los otros cuatro responden anónimos porque el Host no tiene una política global que exija autenticación, no porque lo declaren; si algún día se agrega esa política, dejan de responder. Hay un test que lo demuestra en `PingAsistenteTests`.

El ping vive en un controller **propio y sin constructor**, y eso no es prolijidad: mientras compartió controller con el turno, construirlo exigía resolver las cadenas de solo lectura del asistente —cuya fábrica falla si el ambiente no las configuró— y el ping devolvía 500 sin base. Un ping que necesita configuración de base deja de poder distinguir «el módulo está cargado» de «la base responde». Hay un guard de arquitectura que lo fija.

#### `GET /api/asistente/menciones`

Query: `tipo` (`materia` | `docente`, cualquier otro valor → `400`) y `q` (2–100
caracteres, si no → `400`). Respuesta: `{ resultados: [...], hayMas }`, a lo sumo
6 filas — nunca un conteo de cuántas quedaron afuera (design.md D10 de
asistente-rediseno-v3, ARS-148). Cada fila de `resultados`:

| Campo     | Materia                              | Docente                                      |
| --------- | ------------------------------------ | -------------------------------------------- |
| `id`      | `identity.materias.id`               | `identity.personas.id`                       |
| `nombre`  | Nombre de la materia                 | Nombre completo («Nombre Apellido»)          |
| `carrera` | La carrera que distingue el homónimo | `null`                                       |
| `codigo`  | Código de la materia                 | `null`                                       |
| `cargo`   | `null`                               | Cargo de la designación visible más reciente |

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
| `sql`                      | Solo con `asistente.ver_consulta`                                                                                   |
| `metricas`                 | Llamadas al modelo y categoría                                                                                      |
| `claveDeRetroalimentacion` | Solo cuando `estado = respondida`. Ver `POST /api/asistente/retroalimentacion`                                      |
| `cupoRestante`             | El cupo diario del actor, YA COBRADO este turno (asistente-cupo-visible)                                            |
| `conversacion`             | El id de `GET /historial` en que quedó este turno; nulo si no se persistió (design.md D13 de asistente-rediseno-v3) |

`opciones` sólo viaja cuando `estado = necesita_aclaracion`: bloquea el turno esperando una elección. No hay un campo equivalente para después de una respuesta o un rechazo — el asistente ya no sugiere próximos pasos fuera de la pantalla de bienvenida (ARS-140, ARS-149, design.md D12 de asistente-rediseno-v3); el catálogo de `GET /capacidades` sigue siendo la única fuente de ejemplos clicables.

`estado` usa etiquetas propias del contrato y no el nombre del enum del backend: renombrar un valor interno no puede romper a los clientes en silencio.

**`vinculos[]`** trae `{ fila, columna, tipo, id }` y **no una URL**: el backend dice qué clase de recurso identifica cada celda y con qué identificador se abre, y la ruta la resuelve el cliente, que es donde viven las rutas. Un cliente que no reconoce un `tipo` **no pinta nada** — así, el día que se ofrezcan vínculos a otro módulo, un cliente viejo no muestra un enlace roto.

Que una fila esté en `filas[]` **no implica** que traiga vínculo. Las filas las filtra el motor con las policies de RLS del asistente; la pantalla la autoriza el módulo dueño del recurso, con su propia regla. **No son la misma regla y divergen hoy**: el ámbito departamental es una lista fija de códigos de rol en el módulo y `identity.roles.scope` en las funciones del asistente; los roles del módulo salen del token y están acotados al rol seleccionado en la sesión, y los del asistente son las asignaciones vigentes leídas en vivo; el permiso es un claim de un lado y la matriz en vivo del otro. Por eso el vínculo se le pregunta al módulo dueño —`IDesignacionesQueries.UbicarPedidosAsync` para el trámite— y no se deduce de que la fila haya llegado: deducirlo produce un botón que responde 403 al apretarlo, que es lo que el invariante #7 prohíbe.

**`Idempotency-Key` obligatoria.** Cada turno cuesta dos o tres llamadas al modelo, así que un doble submit se factura completo dos veces. Se resuelve **en memoria con expiración corta y acotada por actor** — no se reusa ni se copia `designaciones.idempotencia_comandos`, que guarda el cuerpo completo de la respuesta HTTP, que es exactamente lo que este módulo decidió no persistir.

**`409 Conflict`**: únicamente cuando `reemplaza` no nombra el último turno vigente del hilo del actor (target de reemplazo inválido o hilo vencido). No cambia nada — ni el hilo, ni el historial, ni el cupo —, así que un reintento con un objetivo válido es seguro.

#### `GET /api/asistente/capacidades`

Devuelve `cubre[]` con sus conteos, `tablas`, `columnas`, `ejemplos[]`, `noPuede[]`, `alcance`, `presentacion`, `mantenimiento` y `cupo` (asistente-modo-mantenimiento / asistente-cupo-visible).

`mantenimiento: { activo, razon }` refleja el estado GLOBAL del kill switch, sin bypass — así el banner es consistente para todo el mundo aunque un admin no esté bloqueado por él.

`cupo: { restante, bloqueado, motivo, vuelveA }` es el cupo diario de ESTE actor, con el bypass de mantenimiento del admin ya aplicado: un actor con `asistente.administrar` no se ve a sí mismo como bloqueado por mantenimiento. `motivo` es uno de `presupuesto_propio` | `tope_organizacional` | `mantenimiento`, nulo si no está bloqueado. `restante` vale `2147483647` (`int.MaxValue`) cuando el cupo está desactivado (0). `vuelveA` sólo se conoce para `presupuesto_propio`.

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

Las transiciones de pedidos (`enviar`, `reenviar`, `aceptar`, `rechazar`, `devolver`, `priorizar`, `despriorizar`) requieren `Idempotency-Key: <uuid>`. La identidad lógica de la clave incluye actor, ruta, recurso y payload durante 24 horas: el replay idéntico retorna la misma respuesta y una reutilización incompatible retorna `409 idempotency-key-reused`. La exclusión concurrente garantiza una sola transición y un solo evento de historial.

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

Query: `periodo` (`dia` | `semana` | `mes`, default `dia`) o el rango explícito `desde`/`hasta`. Exige `asistente.administrar`. Devuelve `{ porUsuario[], porRol[], organizacion }`, cada uno con `{ clave, nombreParaMostrar?, turnos, porEstado, llamadasAlModelo, tokensDeEntrada, tokensDeSalida, tokensDeCache, latenciaPromedioMs, latenciaP95Ms, proveedores[], costoEstimado, esEstimado: true, turnosSinPrecio }`.

Se agrega **sólo** desde `asistente.registro_operativo` — nunca `registro_analitico` (TD-012): no hay ningún campo con el texto de una pregunta. `nombreParaMostrar` se resuelve vía `IConsultasIdentity.ListarUsuariosAsync` (design.md D12), nunca vía `usuarios.ver`: un admin con sólo `asistente.administrar` ve nombres igual. `costoEstimado` sale de `CalculadoraDeCosto` contra `asistente.tabla_de_precios`, con el precio vigente en el momento en que cada fila ocurrió; `turnosSinPrecio` cuenta las filas sin ningún precio vigente para su proveedor/modelo — **nunca** se costean en cero. Un actor con más de un rol de sistema vigente suma su uso a TODOS esos roles en `porRol`.

#### `PUT /api/asistente/administracion/presupuestos/roles/{rol}` y `/presupuestos/usuarios/{actorId}`

Pedido: `{ cupo }` (turnos por día; `0` desactiva). Exigen `asistente.administrar`. El primero edita el default de un código de rol de sistema; el segundo, el override de un actor puntual, que **siempre** gana sobre el default de su rol (design.md D2/D3, tareas 3.4/9.5), más chico o más grande. Ambos escriben, antes de devolver `204`, una fila en `asistente.auditoria_administracion` con el par antes/después.

#### `PUT /api/asistente/administracion/tope-organizacional`

Pedido: `{ topeMensualUsd }` (`0` desactiva). Exige `asistente.administrar`. Igual disciplina de auditoría que los dos anteriores.

## Versioning

V1 implícito hasta que sea necesario versionar. Cuando se necesite: prefijo `/api/v2/{modulo}/`. NO romper V1 sin período de coexistencia.

## Auto-discovery

Swagger UI disponible en `/swagger` (solo Development). Cada controller debe tener atributos `[ProducesResponseType]` para que el schema sea preciso.
