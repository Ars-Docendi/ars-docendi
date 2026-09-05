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

| Superficie       | Rutas principales                                                                                 | Autorización                                                        |
| ---------------- | ------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------- | --------------------------------------- |
| Usuarios         | `GET/POST /api/administracion/usuarios`, `GET/PUT /{id}`, `POST /{id}/activar                     | desactivar`                                                         | `usuarios.ver` / `usuarios.administrar` |
| Docentes         | `GET/POST /api/administracion/docentes`, `GET/PUT /{personaId}`, cambios de estado y `/catalogos` | `usuarios.ver` / `usuarios.administrar`                             |
| Roles y permisos | `/api/administracion/roles`, `/permisos`, `/roles/{id}/permisos`                                  | `roles.ver`, `roles.administrar`, `roles.gestionar_membresia`       |
| Períodos         | `/api/designaciones/periodos` y comandos activar/desactivar                                       | `periodos.administrar`                                              |
| Catálogos        | `GET /api/designaciones/catalogos`                                                                | `designaciones.ver`                                                 |
| Pedidos          | `/api/designaciones/pedidos`, detalle, envío, reenvío y revisión                                  | permisos de consulta, gestión o revisión; siempre acotados al actor |
| Sesión dev       | `GET /api/desarrollo/identidades`                                                                 | sólo ambiente no productivo con opt-in                              |

Todos los DTOs usan JSON `camelCase`, UUIDs canónicos y fechas ISO. Las respuestas de pedidos incluyen historial y `accionesPermitidas`; el frontend no vuelve a ejecutar la autorización ni la máquina de estados.

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

| Método | Path    | Rol mínimo | Descripción                           |
| ------ | ------- | ---------- | ------------------------------------- |
| GET    | `/ping` | (anónimo)  | Health check del módulo               |
| ...    | ...     | ...        | _(a documentar en specs por feature)_ |

### Tareas (`/api/tareas/`)

| Método | Path    | Rol mínimo | Descripción                           |
| ------ | ------- | ---------- | ------------------------------------- |
| GET    | `/ping` | (anónimo)  | Health check del módulo               |
| ...    | ...     | ...        | _(a documentar en specs por feature)_ |

### Asistente (`/api/asistente/`)

| Método | Path           | Permiso               | Descripción                                  |
| ------ | -------------- | --------------------- | -------------------------------------------- |
| GET    | `/ping`        | (anónimo)             | Health check del módulo                      |
| POST   | `/consultas`   | `asistente.consultar` | Un turno. Exige `Idempotency-Key`            |
| GET    | `/capacidades` | `asistente.consultar` | Qué puede hacer el asistente para este actor |

Es el único ping declarado `[AllowAnonymous]` en el código. Los otros cuatro responden anónimos porque el Host no tiene una política global que exija autenticación, no porque lo declaren; si algún día se agrega esa política, dejan de responder. Hay un test que lo demuestra en `PingAsistenteTests`.

El ping vive en un controller **propio y sin constructor**, y eso no es prolijidad: mientras compartió controller con el turno, construirlo exigía resolver las cadenas de solo lectura del asistente —cuya fábrica falla si el ambiente no las configuró— y el ping devolvía 500 sin base. Un ping que necesita configuración de base deja de poder distinguir «el módulo está cargado» de «la base responde». Hay un guard de arquitectura que lo fija.

#### `POST /api/asistente/consultas`

Pedido: `{ mensaje, hilo? }`. **No lleva actor**: sale de la identidad de la sesión, porque un identificador tomado del cuerpo sería un selector de alcance controlado por el cliente.

Respuesta:

| Campo                  | Qué                                                                            |
| ---------------------- | ------------------------------------------------------------------------------ |
| `estado`               | `respondida` · `no_contestable` · `necesita_aclaracion` · `servicio_degradado` |
| `respuesta`            | El texto que lee el usuario                                                    |
| `hilo`                 | Para mandarlo en el turno siguiente                                            |
| `preguntaInterpretada` | Solo si difiere del mensaje                                                    |
| `razonamiento`         | Cómo se interpretó la pregunta, tal como lo devolvió la generación             |
| `opciones[]`           | El menú de una aclaración. **Bloquean** el turno                               |
| `sugerencias[]`        | Qué otra cosa probar. **No** bloquean nada                                     |
| `columnas[]`           | Nombre y marca de sensibilidad                                                 |
| `filas[]`              | Los valores reales, incluidos los que no viajaron al modelo                    |
| `truncado`             | Booleano, **nunca** un conteo                                                  |
| `sql`                  | Solo con `asistente.ver_consulta`                                              |
| `metricas`             | Llamadas al modelo y categoría                                                 |

`opciones` y `sugerencias` son campos distintos a propósito, y colapsarlos borraría el tercer estado: las opciones esperan una elección para poder seguir, las sugerencias no esperan nada.

`estado` usa etiquetas propias del contrato y no el nombre del enum del backend: renombrar un valor interno no puede romper a los clientes en silencio.

**`Idempotency-Key` obligatoria.** Cada turno cuesta dos o tres llamadas al modelo, así que un doble submit se factura completo dos veces. Se resuelve **en memoria con expiración corta y acotada por actor** — no se reusa ni se copia `designaciones.idempotencia_comandos`, que guarda el cuerpo completo de la respuesta HTTP, que es exactamente lo que este módulo decidió no persistir.

#### `GET /api/asistente/capacidades`

Devuelve `cubre[]` con sus conteos, `tablas`, `columnas`, `ejemplos[]`, `noPuede[]`, `alcance` y `presentacion`.

`presentacion` es la única parte del catálogo que mira el **rol** del actor y no sus GRANT: es la línea que le dice por qué cosas suele venir a preguntar. Sale del código de su único rol vigente; con varios roles, con ninguno, o con uno que el backend no reconoce, devuelve un texto genérico que no promete nada de más. Vive en el backend porque el cliente no tiene catálogo de roles y no debe crecer uno: `identity.roles` no es cerrado —Secretaría crea roles desde la aplicación— así que una lista embebida en el cliente se desactualizaría sola. El rol **no** influye en `alcance`, en los conteos, en los ejemplos ni en qué conexión de lectura se usa.

Se deriva de los **GRANT efectivos** del rol con el que el actor consulta y **nunca del payload del prompt**: el prefijo trae el esquema entero, columnas personales incluidas, así que un catálogo derivado de ahí ofrecería preguntas sobre columnas que el rol no puede leer.

Cada ejemplo se valida con `EXPLAIN` contra los privilegios del actor antes de ofrecerse. Cuesta cero tokens, así que sigue respondiendo con el proveedor caído.

## Idempotencia

Las transiciones de pedidos (`enviar`, `reenviar`, `aceptar`, `rechazar`, `devolver`, `priorizar`, `despriorizar`) requieren `Idempotency-Key: <uuid>`. La identidad lógica de la clave incluye actor, ruta, recurso y payload durante 24 horas: el replay idéntico retorna la misma respuesta y una reutilización incompatible retorna `409 idempotency-key-reused`. La exclusión concurrente garantiza una sola transición y un solo evento de historial.

## Versioning

V1 implícito hasta que sea necesario versionar. Cuando se necesite: prefijo `/api/v2/{modulo}/`. NO romper V1 sin período de coexistencia.

## Auto-discovery

Swagger UI disponible en `/swagger` (solo Development). Cada controller debe tener atributos `[ProducesResponseType]` para que el schema sea preciso.
