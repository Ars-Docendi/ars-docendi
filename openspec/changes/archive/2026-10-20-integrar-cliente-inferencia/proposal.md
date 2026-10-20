# Proposal

## Why

El change `servicio-ollama-compartido` deja Ollama accesible para prod (localmente por la red Docker `traefik`) y para staging/pr-N (por `ia.<DOMINIO>` tras Cloudflare Access + BasicAuth), pero hoy sólo se puede consumir a mano con archivos de cabeceras y contraseñas por teclado. Para que una funcionalidad futura (chatbot) use la IA como cualquier otra dependencia, el backend necesita una única forma de conectarse cuya diferencia entre ambientes sea sólo configuración, y el equipo necesita comprobar en cada despliegue que esa conexión funciona.

## What Changes

- Nuevo proyecto de infraestructura `ArsDocendi.Inferencia` (análogo a `ArsDocendi.Storage`) con opciones `Inferencia__*` y un cliente HTTP tipado registrado con `IHttpClientFactory` que agrega BasicAuth siempre y las cabeceras `CF-Access-Client-Id/Secret` sólo cuando están configuradas.
- La inferencia queda **deshabilitada** cuando `Inferencia__Url` está vacía (desarrollo local, tests, ambientes sin secretos); con URL configurada y credenciales incompletas, el arranque falla fuera de Development.
- Nuevo endpoint `GET /api/inferencia/estado` (permiso `sistema.estado.ver`) que consulta `/api/version` del servicio con timeout corto y devuelve `deshabilitado | disponible | no_disponible`, `comprobadoEn` y `duracionMs`, sin exponer URL, credenciales ni excepciones.
- El dashboard de sistema agrega la fila "Inferencia (IA)" con el estado nuevo `deshabilitado`.
- `compose.base.yml` agrega `Inferencia__*` al backend; `deploy-prod.yml`, `deploy-staging.yml` y `pr-env-deploy.yml` las completan desde secrets de cada GitHub Environment:
  - `prod`: URL `http://traefik:11435`, usuario `prod`, sin Service Token.
  - `staging`: URL `https://ia.<DOMINIO>`, usuario `staging`, Service Token.
  - `pr-preview`: URL `https://ia.<DOMINIO>`, usuario `preview`, mismo Service Token.
- Documentación: matriz de secrets, contratos API, grafo de dependencias, design spec del dashboard y alta del usuario `preview` en el runbook de Ollama.

Fuera de alcance: chatbot, endpoints de chat/generación, streaming, prompts, selección de modelo, cuotas por usuario y un proyecto `*.Contracts` (no hay consumidor cross-module todavía).

## Capabilities

### New Capabilities

- `cliente-inferencia`: conexión configurable por ambiente del backend al servicio de inferencia compartido y comprobación segura de su estado.

### Modified Capabilities

- `administracion-sistema`: el dashboard de salud operativa incluye el estado de la inferencia, con el estado `deshabilitado` diferenciado de una falla.

## Impact

- **Backend:** nuevo `backend/src/ArsDocendi.Inferencia/`; `ArsDocendi.Host` lo referencia y registra (`AddInferencia`). Sin cambios en módulos de dominio, Contracts ni schemas de base de datos. Tests de integración nuevos en `backend/tests/ArsDocendi.IntegrationTests/Inferencia/`.
- **Frontend:** `features/sistema` (`sistemaApi.ts`, `DashboardPage.tsx`, CSS y tests).
- **Infra/CI:** `infra/compose/compose.base.yml`, `.github/workflows/{deploy-prod,deploy-staging,pr-env-deploy}.yml`. `spin-up.sh` no cambia: Compose toma las variables del entorno del proceso.
- **Grafo de dependencias:** se agrega `ArsDocendi.Host → ArsDocendi.Inferencia` y `ArsDocendi.Inferencia → ArsDocendi.Shared`; sigue siendo DAG.
- **Operación:** el operador crea los secrets en GitHub Environments y el usuario `preview` en el `htpasswd` de pc-prod.
- **Normativa institucional:** no aplica (sin BR nuevas).

**Rollback:** vaciar `INFERENCIA_URL` (o quitar los secrets) y redeployar deshabilita la inferencia sin afectar el resto; revertir el PR elimina el proyecto, el endpoint y la fila del dashboard. No hay migraciones que revertir.
