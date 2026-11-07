# Design

## Context

- `ProveedorOllama` (`Modules.Asistente/Infrastructure`) talks to Ollama's native API (`POST /api/chat`) with `OpcionesInferencia` credentials: Traefik BasicAuth always, Cloudflare Access Service Token outside prod. It sends `options.num_ctx = Asistente:ContextoDelModelo` on every call because a different `num_ctx` makes Ollama reload the model (`docs/operations/ollama-compartido.md` §11). It does not send `keep_alive`; the server uses `OLLAMA_KEEP_ALIVE=24h`.
- The turn chain is built per scope by `ResolutorDelProveedor`: ceiling → optional `ProveedorConCompuerta` (concurrency gate, 1 per environment with the Ollama profile) → `ProveedorConBreaker` → raw adapter. The identity can come from the environment or from a stored runtime selection (`asistente-modelo-en-caliente`), so "is the provider `ollama`?" must be asked to `IdentidadAsync`, not to `OpcionesAsistente.Proveedor`.
- Existing status surfaces do not fit: `GET /api/inferencia/estado` (`ClienteInferencia`, `/api/version`) needs `sistema.estado.ver` and only says "reachable"; `GET /api/asistente/administracion/servidor-local` (`TelemetriaDelServidorLocal`) reads llama-server `/metrics` and needs `asistente.administrar`. `GET /api/asistente/capacidades` is per-actor and GRANT-derived, cached 5 min by the client (`useAccesoAlAsistente`), so volatile state does not belong there.
- Traefik admits only `GET /api/version`, `GET /api/tags`, `POST /api/chat`, `POST /api/generate` (`infra/ollama/compose.yml`, router `ollama-privada`); everything else is 404. Rate limits: `ollama-tasa-ambiente` 30/min per credential (shared with turns, which make up to 4 calls each), `ollama-concurrencia` 4 in flight.
- Ollama (docs via context7, `/ollama/ollama` `docs/api.md`): `GET /api/ps` lists loaded models with `name`, `model`, `expires_at`, `size_vram` (newer versions also report `context_length`); `POST /api/generate` with only `model` loads it and returns `done: true` with an empty `response` and a `load_duration`. `keep_alive` in a request overrides the server default for that model.
- Cold-start causes on the shared GPU: idle beyond keep-alive, Ollama recreated (every Traefik label change recreates it), and another tenant loading a different model (one model loaded at a time with the 12 GB budget).

## Goals / Non-Goals

**Goals:**

- Tell the user, before they ask, that the first answer will be slow, and let them move the wait earlier.
- Spend at most a few requests per minute of the 30/min credential budget regardless of how many users have the assistant open.
- Make the notice impossible to show for `local` and cloud providers, and silent when we cannot know.

**Non-Goals:**

- Keeping the model warm permanently (a scheduled pinger or `keep_alive: -1`): the shared GPU belongs to other tenants too; the operator's keep-alive policy stays authoritative.
- Prioritizing or reserving the shared GPU, or showing load progress percentages.
- Warming a distinct redaction model (`Asistente:ModeloDeRedaccion`). With one model loaded at a time, a different redaction model swaps on every turn anyway; the notice covers the generation model only. **Decision (final):** only the generation model is detected and activated; `ModeloDeRedaccion` is never set in our environments, so no redaction-model handling exists.
- A Centro de ayuda article: the assistant has none (`ayuda/contenido/index.ts` excludes it).

## Decisions

### D1. Detect «loaded» with `GET /api/ps`, behind a Traefik route change

`GET /api/ps` answers without touching the GPU and names exactly what is resident. The adapter compares the resolved model name (normalized: a name without a tag means `:latest`) with each entry's `model`/`name`; when the entry carries `context_length`, it must equal `ContextoDelModelo` (when `> 0`), otherwise it is `en_reposo` because the first turn would reload it. Task 1.1 verifies the field name against the pinned image (0.35.0); if absent, the comparison is by name only. **Implementation note:** 1.1 could not be run in the implementation session (no local Ollama may be used, and pc-prod is off limits), so the adapter treats `context_length` as optional by construction (name-only comparison when it is absent or not a number) and the check stays a manual step (task 7.2).

Alternatives:

- _Infer from turns_ (last successful call + known keep-alive, or `load_duration` of the last turn): needs no infra change, but cannot see restarts or another tenant evicting the model, which are the common causes on a shared GPU. Rejected as primary; kept as a cache refresh (D2).
- _Probe with `POST /api/generate`_: the probe itself loads the model, so it cannot tell, and it spends GPU time for every open assistant.
- _`GET /api/tags`_: lists installed, not loaded, models.

Consequence: `infra/ollama/compose.yml` adds `Path(`/api/ps`)` to the GET branch of the router rule. It is read-only and exposes only loaded model names to holders of a valid credential. Until an operator applies it in pc-prod (recreating Ollama, agreed window), `/api/ps` answers 404 → `desconocido` → no notice. The feature ships safely before the infra change.

### D2. When to check: on opening, cached server-side for 30 s with single-flight

The client queries the availability when the assistant modal mounts (React Query, `staleTime` equal to the server cache, no refetch per keystroke, no background polling unless `activando`). The server keeps one per-environment entry (a singleton `DisponibilidadDelModelo` keyed by resolved model name) with `TiempoDeVidaDisponibilidadSegundos` (default 30). An expired entry is refreshed by one in-flight `Task` shared by all callers (single-flight via `Lazy<Task>`/`SemaphoreSlim` swap, same pattern as `ResolutorDelProveedor.UnaVez`). Worst case: 2 `/api/ps` per minute per environment.

Turns also feed the cache: a successful `ollama` model call marks the model `listo` (a small hook in `ProveedorOllama` after a 2xx, or in the decorator that already observes outcomes), so the common "someone just used it" path needs no `/api/ps`. A failure never marks `en_reposo`; only `/api/ps` does.

The `/api/ps` request uses its own named `HttpClient` with a 3 s timeout and no retries (like `TelemetriaDelServidorLocal.Cliente`), not the provider client with its retry policy.

### D3. Activation endpoint: `POST /api/asistente/modelo/activacion`, single-flight + cooldown

- Authorization: `[Authorize(Policy = Permisos.AsistenteConsultar)]`, like the rest of `AsistenteController`. Anyone who can ask can activate, because a question would load the model anyway; the button only moves the wait.
- Body to Ollama: `POST /api/generate` with `{ "model": <resolved>, "options": { "num_ctx": ContextoDelModelo } }` (omit `num_ctx` when 0, exactly as `ProveedorOllama.Cuerpo` does), `stream: false`, no `prompt`, no `keep_alive` (the server's 24 h policy governs; overriding it on a shared GPU is the operator's decision). `/api/generate` rather than `/api/chat` with empty messages because it is the documented preload form and already admitted by Traefik.
- Single-flight: one in-flight load `Task` per environment and model; concurrent requests join it. The endpoint returns `202 Accepted` with the availability DTO (`activando`) immediately and the load runs detached from the request (bounded by `TiempoMaximoDeActivacionSegundos`, default 120, with the host's stopping token), so Cloudflare's ~100 s origin timeout and user navigation do not cancel it.
- Cooldown: after a load ends, success or failure, a new load does not start for `EnfriamientoDeActivacionSegundos` (default 30); requests in that window return the current state. With single-flight this bounds upstream loads to 2/min per environment no matter how many users or clicks, which is what the 30/min budget needs. A per-user ASP.NET rate limiter was considered and rejected: the backend has none today, and the scarce resource is the shared upstream budget, which the global cooldown already protects; extra clicks cost an in-memory read.
- Not part of the turn chain: no ceiling, no quota charge, no `RegistroDeConsumo` row, no breaker transition, no concurrency-gate slot. Ollama serializes loads itself, and a turn sent meanwhile queues behind the same load in Ollama. Trade-off: the gate does not see the preload; acceptable because a preload is one request per cooldown window.
- `202` also when already `listo` or `no_aplica` (returns that state without contacting Ollama), so the client has a single code path. 403 comes from the policy.

### D4. How the client learns «listo»: poll the availability endpoint with backoff

While the state is `activando`, `useDisponibilidadDelModelo` sets `refetchInterval` to 2 s, 3 s, 5 s, then 5 s until 120 s total; polls are served from the in-process load state (D2), so they never reach Ollama. On `listo` the notice shows «El modelo está listo.» for 4 s and unmounts. On `en_reposo` with `ultimaActivacionFallida: true`, or the client limit elapsing, it shows the failure copy with the button enabled.

Alternative: SSE. The turn stream (`consultas/flujo`) is the only SSE in the module and is per turn; adding a long-lived stream for a once-a-day event adds connection handling and proxy timeout concerns for no user-visible gain. Rejected.

### D5. DTO and states

`GET /api/asistente/modelo/disponibilidad` → `{ "estado": "no_aplica" | "listo" | "en_reposo" | "activando" | "desconocido", "ultimaActivacionFallida": boolean }`. No model name, URL, timestamps from Ollama, or other tenants' models. The client renders the notice only for `en_reposo`/`activando`, plus the transient post-activation `listo` and failure copies; `desconocido` renders nothing (never promise or nag about what we cannot see).

### D6. Questions while activating, maintenance, and the turn

A question sent while `activando` or `en_reposo` is accepted and waits as today; nothing blocks the composer. When a turn returns answered, the client hides the notice (the answer proves the model is loaded) and the server cache is already `listo` (D2). In maintenance mode (`capacidades.mantenimiento`), the notice is not rendered; the backend still answers the endpoints because maintenance is a turn concern.

### D7. Logging, no audit

Each activation logs one structured entry: model name, outcome (`cargado`, `rechazado` with status category, `tiempo_agotado`), `load_duration` when Ollama reports it, and elapsed time. No actor id, no credentials, no URL, no body (consistent with `ClienteInferencia` logging only `HttpRequestError`). It is not an audit event: it changes no data and grants nothing; `asistente-auditoria-de-administracion` covers administrative mutations only.

### D8. Placement and boundaries

All in `Modules.Asistente`: `Infrastructure/CargaDelModelo.cs` (named so because `IDisponibilidadDelModelo` already means the turn-blocking reasons) (cache, single-flight, cooldown, `/api/ps` and preload calls through `OpcionesInferencia.Ruta`/`Autenticar`), a thin service method resolving the identity through `ResolutorDelProveedor`, and two actions in a new `ModeloDelAsistenteController` (route `api/asistente/modelo`) to keep `AsistenteController` under the ~300-line cap. Frontend: `api/asistenteApi.ts` (two calls), `hooks/useDisponibilidadDelModelo.ts`, `components/AvisoDeModeloEnReposo.tsx` rendered by `PanelAsistente` above the composer. No `Contracts`, no Shared, no schema.

## Risks / Trade-offs

- **Infra dependency** → Without the Traefik rule the feature is silent (`desconocido`). Rollout order: backend/frontend first, rule in the next agreed pc-prod window.
- **`context_length` may be missing in the pinned Ollama** → Name-only match can say `listo` while the first turn reloads (if another environment loaded it with the server default). Mitigated by every environment sending the same `num_ctx` and the server's `OLLAMA_CONTEXT_LENGTH` matching it (§11 of the ops doc).
- **Staleness up to 30 s** → The model can be evicted right after a `listo`; the user then waits as today. Acceptable: the notice is a hint, not a guarantee.
- **Preload competes with other tenants** → Loading our model can evict another tenant's model. Bounded by the cooldown and by the fact that the user's question would do the same moments later.
- **Multiple backend instances** → Single-flight and cooldown are per process. Today there is one backend per environment; Ollama itself coalesces concurrent loads of the same model, so a second instance would add at most one duplicate `/api/generate`.
- **Budget** → Worst case per environment: 2 `/api/ps` + 2 loads per minute = 4 of 30, only while someone has the assistant open.
