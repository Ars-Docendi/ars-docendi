# Design

## Context

Ollama corre en pc-prod detrás del entrypoint privado `ollama` de Traefik (`:11435`), con allowlist de rutas, rate limit y BasicAuth (change `servicio-ollama-compartido`). Prod alcanza `http://traefik:11435` porque su backend ya está en la red Docker `traefik` (`red-publica` en `compose.base.yml`). Staging y pr-N corren en vm-dev y llegan por `https://ia.<DOMINIO>`, donde Cloudflare Access exige Service Token e IP de egreso.

Precedentes observados:

- `ArsDocendi.Storage` es infraestructura con I/O fuera de `Modules.*` y de `Shared`: opciones por sección (`Almacenamiento__*`), `ModuleExtensions` registrado en `Program.cs`, controller propio y validación de credenciales que sólo tolera faltantes en Development.
- El dashboard de sistema (`features/sistema`) combina sondas independientes: pings de módulos y `GET /api/administracion/sistema/estado` (PostgreSQL, timeout 3 s, respuesta `{ estado, comprobadoEn, duracionMs }`).
- Los secretos por ambiente fluyen como GitHub Environment secrets → `env:` del job → entorno del proceso de `spin-up.sh` → interpolación de `compose.base.yml` → `Seccion__Clave` de ASP.NET.

## Goals / Non-Goals

**Goals:** una sola forma de conectar el backend con la IA en los tres tipos de ambiente; deshabilitar sin configuración; verificar la conexión desde el dashboard tras cada deploy; credenciales sólo en GitHub Environments.

**Non-Goals:** chatbot, endpoints de chat/generación, streaming, prompts, modelos, cuotas, proyecto `*.Contracts`, cambios a Traefik/Cloudflare/Ollama.

## Decisions

### 1. Proyecto `ArsDocendi.Inferencia`, sin Contracts todavía

La I/O HTTP no puede vivir en `Shared` (sólo admite identity/audit) ni pertenece a un módulo de dominio. Se crea `backend/src/ArsDocendi.Inferencia` siguiendo a `ArsDocendi.Storage`: referencia `ArsDocendi.Shared` (para `Permisos`) y `Microsoft.AspNetCore.App`; `ArsDocendi.Host` lo referencia y llama `AddInferencia(configuration)`.

No se crea `ArsDocendi.Inferencia.Contracts`: hoy el único consumidor es su propio controller. El change del chatbot agregará el contrato con la operación que realmente necesite. Al no ser `Modules.*`, no expone `/ping` (igual que Storage); su sonda es `/api/inferencia/estado`.

Estructura:

```text
ArsDocendi.Inferencia/
  InferenciaOptions.cs        Seccion = "Inferencia"; Url, Usuario, Contrasena, AccessClientId, AccessClientSecret; Habilitada => Url no vacía
  ModuleExtensions.cs         AddInferencia: options + validación + AddHttpClient<ClienteInferencia>
  Infrastructure/ClienteInferencia.cs   ComprobarAsync(ct) -> bool (GET /api/version)
  Api/InferenciaController.cs           GET /api/inferencia/estado
```

Capas: el controller usa el cliente directamente; no hay repositorio ni reglas de negocio que justifiquen un servicio intermedio.

### 2. Cliente tipado y cabeceras condicionales

`AddHttpClient<ClienteInferencia>` configura, sólo si la inferencia está habilitada, `BaseAddress = Url`, `Authorization: Basic base64(usuario:contraseña)` y, si hay Service Token, `CF-Access-Client-Id` y `CF-Access-Client-Secret` como cabeceras por defecto. Sin `DelegatingHandler` propio: las cabeceras son estáticas por proceso.

`ClienteInferencia.ComprobarAsync` hace `GET api/version` con un `CancellationTokenSource` enlazado de 3 s y devuelve `true` sólo ante 2xx. `HttpRequestException`, `TaskCanceledException` por timeout y no-2xx devuelven `false` y se registran con Serilog en `Warning` con el código de estado o el tipo de excepción, nunca la URL con credenciales ni cabeceras. La cancelación del request entrante se propaga y no se registra como falla.

`HttpClient.Timeout` queda en el default; el chatbot definirá su política de streaming.

### 3. Validación al arranque

`AddOptions<InferenciaOptions>().Bind(...).Validate(...).ValidateOnStart()` con una regla: si `Url` no está vacía, deben estar `Usuario` y `Contrasena`, y `AccessClientId`/`AccessClientSecret` deben estar ambos o ninguno; `Url` debe ser absoluta http/https. En Development se omite la validación para no exigir secretos locales, igual que Storage. Los mensajes nombran la opción, nunca su valor.

### 4. Endpoint de estado

`GET /api/inferencia/estado`, `[Authorize(Policy = Permisos.SistemaEstadoVer)]`, devuelve `EstadoInferenciaDto { estado, comprobadoEn, duracionMs }` con `estado ∈ { "deshabilitado", "disponible", "no_disponible" }`, mismo formato que `EstadoBaseDatosDto`. Si está deshabilitada responde sin contactar el servicio (`duracionMs = 0`). Requerir permiso evita que tráfico anónimo consuma el rate limit compartido de Traefik/Cloudflare.

### 5. Frontend

`sistemaApi.ts` agrega `comprobarInferencia()` al `Promise.all` de `consultarEstadoSistema`, con el mismo patrón y timeout que `comprobarBaseDatos`; `EstadoComponente` suma `"deshabilitado"`. `EstadoSistema` agrega `inferencia: ComprobacionComponente`. `DashboardPage` agrega la fila "Inferencia (IA)" y la etiqueta/clase `sistema-estado--deshabilitado` (neutral, distinta de no disponible), y actualiza la nota explicativa. Se actualiza `administracion-sistema-design-spec.md`.

### 6. Configuración y secretos

`compose.base.yml` agrega al backend:

```yaml
Inferencia__Url: ${INFERENCIA_URL:-}
Inferencia__Usuario: ${INFERENCIA_USUARIO:-}
Inferencia__Contrasena: ${INFERENCIA_CONTRASENA:-}
Inferencia__AccessClientId: ${INFERENCIA_ACCESS_CLIENT_ID:-}
Inferencia__AccessClientSecret: ${INFERENCIA_ACCESS_CLIENT_SECRET:-}
```

Los workflows definen en el `env:` del job de deploy:

| Workflow             | Environment  | `INFERENCIA_URL`                 | `INFERENCIA_USUARIO` | Contraseña (secret)             | Access (secrets)                                                 |
| -------------------- | ------------ | -------------------------------- | -------------------- | ------------------------------- | ---------------------------------------------------------------- |
| `deploy-prod.yml`    | `prod`       | `http://traefik:11435`           | `prod`               | `INFERENCIA_CONTRASENA_PROD`    | —                                                                |
| `deploy-staging.yml` | `staging`    | `https://ia.${{ vars.DOMINIO }}` | `staging`            | `INFERENCIA_CONTRASENA_STAGING` | `INFERENCIA_ACCESS_CLIENT_ID`, `INFERENCIA_ACCESS_CLIENT_SECRET` |
| `pr-env-deploy.yml`  | `pr-preview` | `https://ia.${{ vars.DOMINIO }}` | `preview`            | `INFERENCIA_CONTRASENA_PREVIEW` | `INFERENCIA_ACCESS_CLIENT_ID`, `INFERENCIA_ACCESS_CLIENT_SECRET` |

Los secrets de Access se definen a nivel Environment (`staging` y `pr-preview`), no repositorio, para que el job de prod no los tenga disponibles. Compose prioriza el entorno del proceso sobre el `--env-file` temporal de `spin-up.sh`, así que el script no cambia y los secretos no se escriben en ese archivo. Usuario `preview` propio para revocar previews sin cortar staging.

### 7. Tests

Integración en `ArsDocendi.IntegrationTests/Inferencia/`, con una fábrica que sobreescribe configuración `Inferencia:*` y reemplaza el primary handler del cliente tipado (`services.Configure<HttpClientFactoryOptions>(typeof(ClienteInferencia).Name, ...)`) por un handler stub que registra requests:

- sin URL → `deshabilitado`, cero requests;
- configuración tipo prod → BasicAuth `prod`, sin `CF-Access-*`;
- configuración tipo staging → BasicAuth + ambas cabeceras;
- stub 401/500/timeout → `no_disponible`, cuerpo sin URL/usuario;
- usuario sin permiso → 403 y cero requests;
- arranque en Production con URL sin contraseña → falla de validación.

Frontend: tests de `sistemaApi` y `DashboardPage` para la fila nueva y el estado `deshabilitado`.

## Risks / Trade-offs

- [Service Token compartido staging/pr-N] → revocarlo corta ambos; se acepta porque Access identifica a vm-dev, y BasicAuth separa ambientes.
- [PR con código no revisado recibe credenciales de IA] → usuario `preview` propio, rate limit y allowlist de rutas en Traefik; revocación independiente.
- [Secret faltante en un Environment] → falla de arranque explícita fuera de Development en lugar de un error silencioso en runtime; se documenta en la matriz de secrets.
- [Estado depende de red externa en staging/pr-N] → timeout 3 s y sonda independiente; no bloquea el resto del dashboard.

## Migration Plan

1. Operador: alta del usuario `preview` en `htpasswd` (rotación con `OLLAMA_AUTH_REVISION`) y secrets en los tres Environments.
2. Merge a develop: staging despliega y el dashboard debe mostrar "Disponible".
3. Release a main: prod despliega con la ruta local.

Rollback: quitar `INFERENCIA_URL` del workflow (o revertir el PR) y redeployar; la inferencia queda deshabilitada sin migraciones.

## Open Questions

Ninguna bloqueante. El contrato que consumirá el chatbot se decide en su propio change.
