# API contracts

Documentación de las superficies HTTP públicas del backend. Cada módulo expone su API bajo `/api/{modulo}/`.

## Base URL

- Desarrollo local: `http://localhost:5000`
- Swagger UI: `http://localhost:5000/swagger` (solo en Development)
- Producción: TBD (depende de la VM y reverse proxy — ver [`infrastructure.md`](./infrastructure.md))

## Autenticación

Todas las rutas (excepto `/api/{modulo}/ping`) requieren **JWT Bearer token** emitido por Azure AD (tenant UNLaM).

- Header: `Authorization: Bearer <token>`
- Validación: el Host valida firma + issuer + audience contra Azure AD.
- Autorización: por rol via `[Authorize(Roles = "JefeDeCatedra,...")]` en controllers/actions.

## Forma de error estándar

Todos los endpoints retornan errores en formato consistente:

```json
{
  "type": "https://ars-docendi.unlam.edu.ar/errors/<error-code>",
  "title": "Mensaje corto y accionable para el usuario",
  "status": 400,
  "detail": "Detalle técnico opcional (omitir info sensible)",
  "instance": "/api/<modulo>/<endpoint>",
  "traceId": "<correlation-id>"
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

## Idempotencia

Endpoints `POST/PUT/PATCH` que modifican estado de aprobación o reservas deben aceptar header `Idempotency-Key: <uuid>` y retornar la misma respuesta para keys repetidos en ventana de 24h.

## Versioning

V1 implícito hasta que sea necesario versionar. Cuando se necesite: prefijo `/api/v2/{modulo}/`. NO romper V1 sin período de coexistencia.

## Auto-discovery

Swagger UI disponible en `/swagger` (solo Development). Cada controller debe tener atributos `[ProducesResponseType]` para que el schema sea preciso.
