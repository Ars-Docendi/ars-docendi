# Proposal

## Why

When the assistant runs on the shared Ollama service (`ollama` provider, the pc-prod GPU behind `ia.<DOMINIO>`), Ollama unloads the model after `OLLAMA_KEEP_ALIVE` of idleness, on a restart, or when another tenant of the shared GPU loads a different model. The first question then pays a cold load of several seconds or more with no explanation, and looks like a broken assistant. The chat should say that the model is «en reposo» and let the user start the load before writing the question.

## What Changes

- The backend learns whether the model the turn would run on (the resolved identity, including a runtime selection from `asistente-modelo-en-caliente`) is loaded in Ollama, using `GET /api/ps`, behind a short server-side cache with single-flight, so the per-credential budget of 30 requests/min is not spent on status checks.
- New endpoint `GET /api/asistente/modelo/disponibilidad` (permission `asistente.consultar`) returns `no_aplica` | `listo` | `en_reposo` | `activando` | `desconocido`. Providers other than `ollama` always get `no_aplica`.
- New endpoint `POST /api/asistente/modelo/activacion` (permission `asistente.consultar`) starts an Ollama preload (`POST /api/generate` with the model, no prompt, and the same `num_ctx` as the turns). It is idempotent and single-flight: concurrent clicks from any number of users produce one load. It answers `202` with the current state and never consumes the user's daily quota.
- The chat shows an inline notice «El modelo está en reposo. La primera respuesta puede demorar unos segundos.» with the button «Activar modelo»; while loading, «Activando el modelo…» with the button disabled; when loaded, «El modelo está listo.», which then disappears; on failure, «No se pudo activar el modelo. Probá de nuevo.» No system or push notification.
- Infra: the Traefik router of `infra/ollama/compose.yml` admits `GET /api/ps` (today only `/api/version`, `/api/tags`, `/api/chat`, `/api/generate` pass). Until an operator applies it in an agreed window, the endpoint answers `desconocido` and the notice never appears.
- Docs: API contracts, the assistant domain doc, the Ollama operations doc, `modelo-local.md` (Ollama section), and the assistant design spec (new state and copy).

## Capabilities

### New Capabilities

- `asistente-disponibilidad-del-modelo`: whether the assistant's own-hardware model is loaded, how any assistant user can ask to load it, and how the chat reports «en reposo», «activando», «listo» and failure inline.

### Modified Capabilities

None. The `ollama` provider spec still lives in the unarchived change `asistente-proveedor-ollama`; this change adds a separate capability instead of a delta against an in-flight one.

## Impact

- **Modules:** `Modules.Asistente` only (Infrastructure: a status/preload client next to `ProveedorOllama`; Api: two actions; frontend `features/asistente`). No cross-module calls, no `Contracts` change, no new edges in the dependency graph, no schema or migration.
- **APIs:** two new endpoints under `/api/asistente/modelo/`. No existing response changes; `capacidades` is untouched.
- **Shared service:** at most one `GET /api/ps` per cache window per environment and one preload per cooldown window per environment, within the 30/min credential budget shared with turns.
- **Infra:** one Traefik rule change on pc-prod, applied by an operator in an agreed window (recreating Ollama cuts requests in flight).
- **Help center:** no article covers the assistant (`frontend/src/features/ayuda/contenido/index.ts` excludes it explicitly), so rule 9 has no article to update.
- **Rollback:** revert the backend and frontend; the Traefik rule can stay (read-only route) or be reverted independently. With the rule absent, the feature degrades to «no notice» by design.
