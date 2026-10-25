## Context

The model port, `IProveedorDeModelo`, names no provider. Four adapters exist on this branch, selected once per process by the `switch` in `ModuleExtensions.ConstruirProveedor` from `IOptions<OpcionesAsistente>.Proveedor`:

| Key         | Class                | Talks to                                                                                                                                                                                                                   | Configured by                                                                                                                                                                                    |
| ----------- | -------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `simulado`  | `ProveedorSimulado`  | nothing (deterministic text)                                                                                                                                                                                               | default                                                                                                                                                                                          |
| `anthropic` | `ProveedorAnthropic` | Anthropic Messages API through the SDK, over the named `HttpClient`                                                                                                                                                        | `Asistente__ClaveDelProveedor`, `Asistente__Modelo`                                                                                                                                              |
| `local`     | `ProveedorLocal`     | an own llama.cpp/vLLM server, OpenAI-compatible `/v1/chat/completions`, with streaming, `enable_thinking`, structured output and cache accounting                                                                          | `Asistente__UrlDelProveedorLocal` (+ optional `Asistente__ClaveDelProveedor`)                                                                                                                    |
| `ollama`    | `ProveedorOllama`    | the shared inference service, **native** `/api/chat` only (Traefik passes just `/api/chat`, `/api/generate`, `/api/version`, `/api/tags`), with `think`, `num_ctx`, `truncate:false`, `format`, `prompt_eval_cached_count` | `Inferencia__Url`, `Inferencia__Usuario`, `Inferencia__Contrasena`, optional `Inferencia__AccessClientId`/`AccessClientSecret` (`OpcionesInferencia`, inside `Modules.Asistente/Configuracion/`) |

All four write `Nombre = "{clave}/{modelo}"` (`simulado` writes `"simulado"`), the shape `CalculadoraDeCosto` and `tabla_de_precios` already key on. `ProveedorOllama` reuses the static helpers `ProveedorLocal.VisibleHastaAhora` and `SinRazonamiento`, so `ProveedorLocal` cannot simply be deleted.

Registration today (`ModuleExtensions.AddAsistenteModule`):

- Raw adapters are **keyed singletons** (`AdaptadorDeGeneracion`, `AdaptadorDeRedaccion`) so the container disposes them. `ProveedorBase` and `BaseDeRedaccion` wrap them as singletons; the evaluator's `envolverProveedor` hook wraps the raw adapter **inside** the ceiling and breaker. `ModeloDeRedaccion` empty means the redaction base is the same instance.
- `Encadenar` builds, per scope, `ProveedorConTechoDeLlamadas` (scoped `ContadorDeLlamadasDelTurno`) → optional `ProveedorConCompuerta` (singleton `CompuertaDelModelo`, only when `MaximoDeLlamadasConcurrentes > 0`) → `ProveedorConBreaker` (singleton `BreakerDelProveedor`, per-call timeout) → adapter. The gate sits **outside** the breaker on purpose so queue time is not a provider failure.
- The shared transport is the named client `ClienteDelProveedor`: `GrabadorDeCassettes` (only if `DirectorioDeCassettes` is set) → `ReintentoDeTransporte` → network. It is the module's only retry authority.
- Failure vocabulary (`Application/Modelo/FallasDelProveedor.cs`): `ProveedorNoDisponible` (breaker open, no call), `TimeoutDelProveedor`, `ProveedorSaturado` (gate full, **or** `ProveedorOllama` mapping 429/503 from the shared service; deliberately not counted by the breaker). Everything else non-cancellation is `HttpRequestException`, which the breaker counts.
- The module already has these patterns this change reuses: per-request reads of shared state (`DisponibilidadDelModuloReal`, no cache), versioned prices (`asistente.tabla_de_precios`, per-token columns, nothing writes it today), and audited administration (`AdministracionAsistenteController`, policy `Permisos.AsistenteAdministrar`, `IAuditoriaDeAdministracion.RegistrarAsync(actor, accion, antes, despues)`).

Migration route (`database/README.md`, "Integración de incrementales del Asistente", and `MigradorAsistente`): `database/asistente/*.sql` has no EF history. `MigradorAsistente.Recursos` lists the embedded scripts in order (`RecursoVersion`, `007`, is last by construction), each exposed by a static wrapper (`RegistrosAsistente`, `AdministracionAsistente`, ...). Files `001`-`008` exist; `008` has no wrapper of its own (`AdministracionAsistente.RecursoSqlVigenteUnico`). The architecture guard allows only `ALTER TABLE <own table> ADD COLUMN IF NOT EXISTS` and ratified CHECK replacements; it forbids `DROP`, `RENAME` and `ALTER COLUMN ... TYPE`. The privilege and sensitivity manifests classify only what is granted to the read-only roles; the whole `asistente` schema is denied wholesale (`manifiesto-privilegios.json`, schema `asistente`, `estado: denegado`), so new `asistente` tables need no manifest entry and are unreadable by construction.

## Goals / Non-Goals

**Goals:**

- Switch model at runtime without a restart, effective from the next turn, consistent across Host instances.
- Support Anthropic, OpenAI, Google Gemini and Mistral behind one adapter, and keep the two existing local servers selectable.
- Store one key per company safely, and never let a key leave the server.
- Keep the module startable, and `/api/asistente/ping` answering, with incomplete configuration.
- Never let a runtime switch send real `sensible-valor` data to a third party.

**Non-Goals:** see "Out of Scope" in the proposal.

## Decisions

### D1 — Cloud clients over Microsoft.Extensions.AI `IChatClient`; local servers keep their adapters

The NuGet package `Anthropic` 12.42.1, already referenced by `Modules.Asistente`, exposes `AsIChatClient()` and depends on `Microsoft.Extensions.AI.Abstractions`. `Microsoft.Extensions.AI.OpenAI` adds the OpenAI client and works against any OpenAI-compatible endpoint: OpenAI, Mistral and Gemini's OpenAI-compatible endpoint. One abstraction covers the four cloud companies.

It is **not** used for the two local servers:

- The shared service cannot be reached through an OpenAI-compatible client. Traefik denies `/v1/*`; opening it would change the surface of a shared service for the sake of one adapter (`asistente-proveedor-ollama`, D1).
- `IChatClient` has no first-class carrier for what the port already asks of local adapters: `SolicitudAlModelo.AlRecibirTexto` streaming, `EsquemaDeSalidaJson`, `think` / `enable_thinking` per `EsfuerzoDelModelo`, `num_ctx`, and cache accounting from `prompt_eval_cached_count` / `timings.cache_n`.

**Alternatives considered:**

- _`OllamaSharp` (`IChatClient` over the native API) for the shared service._ Rejected for now: another package, and the `think`, `num_ctx` and `truncate` knobs would have to ride `RawRepresentationFactory`. Revisit only if `ProveedorOllama` becomes a maintenance burden.
- _LiteLLM proxy._ Rejected: an extra service to run and secure, and keys would live outside the application, which breaks D4.
- _A hand-written adapter per cloud company._ Rejected: each company costs a class, a switch arm, cassette-key work and failure-translation tests; it does not scale to four companies.

### D2 — `ProveedorChat` for cloud companies; `Nombre = "{empresa}/{modelo}"`; the registry gains a resolver

`ProveedorChat : IProveedorDeModelo` wraps an `IChatClient` and exposes `Nombre` as `"{empresa}/{modelo}"` with `empresa` in `anthropic`, `openai`, `google`, `mistral`. The existing adapters keep their own `Clave` as `empresa` (`ollama`, `local`), so every stored `registro_operativo.proveedor` and price key stays valid.

`ProveedorChat` replaces the hand-written role of `ProveedorAnthropic` only after **wire-level parity** is shown: a fake `HttpMessageHandler` captures the outgoing Anthropic request body from both adapters for the same `SolicitudAlModelo` (`system` carrying `cache_control`, `output_config.effort`, token ceilings) and the translated `RespuestaDelModelo`. Parity is not shown through the cassette corpus because TD-030 leaves all 109 cassettes irreproducible until they are re-recorded. Until parity holds, `ProveedorAnthropic` stays and serves `anthropic` (see D10).

The `switch` in `ConstruirProveedor` is not replaced: it stays the fallback registry (environment selection). A new `IResolutorDelProveedor` (scoped) resolves the adapter per turn (D3) and, when a stored selection exists, builds it from the catalog row and the company key instead of from the environment.

`ArquitecturaAsistenteTests.El_SDK_del_proveedor_se_nombra_en_un_solo_archivo` is updated: the SDK namespace may be named in `ProveedorAnthropic.cs` (until retired) and in the one new file that builds `IChatClient` instances; nothing else.

### D3 — Selection read per turn, no process cache, env fallback, chain rebuilt per turn

The active selection is read from `asistente.modelo_vigente` at the start of each turn, the same pattern as `modo_mantenimiento` / `DisponibilidadDelModuloReal`. Several Host instances must agree, and a cache would make "effective from the next turn" false on every instance but the one that handled the write.

A turn resolves its adapter once, at start. An in-flight turn therefore finishes on the model it began with. When no row exists the resolver returns exactly what registration builds today (`AdaptadorDeGeneracion` / `AdaptadorDeRedaccion` through `ProveedorBase` and `BaseDeRedaccion`), preserving the invariant that the module starts and `/api/asistente/ping` answers with incomplete configuration. Nothing reads the database at registration time.

Lifetimes. Singletons that capture a fixed adapter cannot follow a selection, so the two `Encadenar` registrations (`IProveedorDeModelo` and the keyed `ProveedorDeRedaccion`), already scoped today, keep their scope but call the resolver. A selection-built adapter is created per scope and disposed with it (the `AnthropicClient` inside `ProveedorAnthropic` and `IChatClient` are `IDisposable`); `HttpClient` instances come from `IHttpClientFactory`, so per-turn construction does not leak sockets. The env-built singletons keep being disposed by the container.

The resolver builds the chain with `Encadenar`, with one change: the concurrency gate (`ProveedorConCompuerta`) is added only when the resolved adapter is a **local kind** (`ollama`, `local`), whether the adapter comes from a stored selection or from the environment: an environment running `anthropic` or `simulado` is no longer wrapped by the gate even when `MaximoDeLlamadasConcurrentes > 0`. The gate protects one shared GPU (`MaximoDeLlamadasConcurrentes: 1` in `compose.asistente-ollama.yml`); applied to a cloud company it would serialize every cloud call behind a limit that does not exist there. The per-turn ceiling and the breaker apply to every kind. A stored selection whose adapter cannot be built (no stored key, an unreadable ciphertext, no usable master key, a local server no longer configured) does not fail the turn with an exception: the resolver substitutes `ProveedorNoArmable`, which fails every call as an `HttpRequestException`, so the turn degrades and the breaker of that name records it until the key or the server configuration is fixed or the selection is reverted. `CacheDeConsultasGeneradas` (a singleton keyed by question, role and date) is cleared when a selection write succeeds, so a query generated by the previous model is not served after a switch.

**Alternatives considered:** a short-TTL cache (rejected: reintroduces cross-instance disagreement); `IOptionsMonitor` with a reload signal (rejected: signals do not cross Host instances).

### D4 — One key per company, encrypted with AES-GCM

Keys are stored in `asistente.credencial_de_proveedor` encrypted with `System.Security.Cryptography.AesGcm`. Only cloud companies have a row: the local servers' credentials are server configuration (D6).

- The master key comes from `Asistente__ClaveMaestra` (32 bytes, base64). An optional `Asistente__ClaveMaestraAnterior` supports rotation: a value encrypted under the previous key is decrypted and lazily re-encrypted under the current one on read.
- Associated data binds each ciphertext to `asistente:{empresa}`, so a ciphertext copied to another company's row fails authentication.
- Stored format: `v1:{kid}:{base64(iv|tag|ciphertext)}`. The key id (`kid`) names which master key encrypted the value, which avoids a "try the current key, then the previous one" decrypt loop and its ambiguity.
- Only a last-4 hint (`pista`) is ever returned. The key never appears in a response, log line, exception message or `auditoria_administracion` row.
- With no master key configured, saving a key returns 503 and the local servers keep working.

`ClaveDelProveedor` (environment) remains the fallback key for `anthropic` when no stored selection exists; a stored selection never reads it.

**Alternative considered:** ASP.NET Data Protection. Rejected: it needs a persisted key ring (an EF package or a mounted volume), which is more infrastructure than one master secret.

### D5 — The catalog is defined by the repository

`asistente.catalogo_de_modelos(empresa, modelo)` is seeded by the SQL file. Adding a model is a pull request. The UI lists cloud models grouped by company; the local panel lists the configured local servers with their catalog models. Live listing from provider APIs is out of scope: it would make the screen depend on third-party availability and expose models the pipeline was never evaluated against. The shared service's `/api/tags` is reachable, but listing from it has the same objection.

Seed: Anthropic, OpenAI, Google and Mistral entries; `ollama/qwen3.5:9b` (the model `compose.asistente-ollama.yml` and the evaluation target use); `local/qwen3-8b` (the default of `compose.asistente-local.yml`). Identifiers are confirmed against evaluation results during task 1.3.

**Alternative considered:** administrator-editable catalog. Rejected: an unevaluated model could be selected in production without review.

### D6 — Local servers are a closed set derived from server configuration; credentials stay server-side

The "Modelo local" option maps to **both** existing local adapters, as a server choice inside the local panel:

- **Servicio de inferencia compartido** → `ProveedorOllama` (`empresa = ollama`). Offered only when `OpcionesInferencia.Habilitada` (`Inferencia__Url` set). Credentials come from `OpcionesInferencia.Autenticar`: BasicAuth always, plus the Cloudflare Access service token pair when configured (staging and previews; production reaches the service over the local Docker network). They are applied per request, never as client default headers, so they cannot travel to another provider through the shared transport.
- **Servidor propio (llama.cpp / vLLM)** → `ProveedorLocal` (`empresa = local`). Offered only when `Asistente__UrlDelProveedorLocal` is set; `Asistente__ClaveDelProveedor` is its optional bearer.

The administrator chooses a server **identifier** (`ollama` or `local`) and a catalog model; no URL, host or credential crosses the API. This is the SSRF guard: the set of reachable hosts is exactly what deployment already configured, so the earlier `Asistente__HostsLocales` allowlist is dropped as a redundant second setting. The administrator test and the apply step reject an identifier whose server is not configured, without any outbound call.

The shared service has its own failure surface that the test endpoint must map (see slice 3): 401/403 (BasicAuth or Access rejected the credential), 404 (model not pulled), 429/503 (saturated, `ProveedorSaturado`, not a connectivity failure). Saturation does not open the breaker, so a "test connection" reporting `saturado` must not be shown as an error state of the provider.

`infra/compose/compose.base.yml` already forwards `Inferencia__*` from `INFERENCIA_*`; this change adds `Asistente__ClaveMaestra` the same way and documents the mapping in `docs/architecture/infrastructure.md`.

**Alternative considered:** free-text server URL with per-server credentials in the UI. Rejected: SSRF exposure and a second place secrets could live. **Alternative considered:** keep an `Asistente__HostsLocales` allowlist of arbitrary hosts. Rejected for now: nothing in the deployment needs a third local server, and every extra host is an extra credential to route.

### D7 — A runtime selection overrides `ModeloDeRedaccion`, and decides masking from the resolved adapter

When a runtime selection exists, the environment value `ModeloDeRedaccion` is ignored, because it may belong to another company. The selected model performs both generation and redaction. There is no redaction-model field in the UI.

`OpcionesAsistente.RedaccionSinEnmascararVigente` (`RedaccionSinEnmascarar` and `ProveedorEnHardwarePropio` and no cassette directory) is read by `CarrilSql` (`CarrilSql.cs:585`) and `AnuncioDeLaRedaccion`, and today looks only at the environment's `Proveedor`. With runtime selection that is unsafe: an environment running `ollama` with `RedaccionSinEnmascarar=true` would keep sending real `sensible-valor` values (documents) in the redaction prompt after an administrator selects a cloud company. The decision therefore moves to the resolved adapter: a selection is "own hardware" only for `ollama` and `local`; for any cloud company masking is on regardless of the option. `AnuncioDeLaRedaccion` keeps describing the startup (environment) state and the "Modelo" tab shows an "En uso" note when masking is active because of the selection.

**Alternative considered:** a second selector for redaction. Rejected as out of scope and as a source of invalid cross-company combinations.

### D8 — Circuit-breaker state is keyed by adapter `Nombre`

`BreakerDelProveedor` becomes a per-`Nombre` registry (still one singleton). Switching from a failing provider to another does not inherit an open breaker, and switching back does not reset one that is genuinely open. `Encadenar` and `DisponibilidadDelModeloReal` (the `ProveedorCaido` pre-check) ask for the breaker of the resolved name. `CompuertaDelModelo` is not keyed: it models one GPU, not a provider name, and applies only to local kinds (D3).

**Alternative considered:** reset the single breaker on every switch. Rejected: it would hide a real outage after a quick toggle.

### D9 — Prices are versioned on write

Writing a price closes the current `tabla_de_precios` row (`vigente_hasta`) and inserts a new version in the same transaction. The table (`006_asistente_administracion.sql`) stores **per-token** prices (`precio_por_token_entrada`, `_salida`, `_cache`, `numeric(18,12)`); the screen takes USD per million tokens and the endpoint converts, so there is no float rounding between the form and the table. When the cache price is omitted, cached tokens are priced like input tokens (the cache price defaults to the input price), never as zero. The date is a calendar day taken as 00:00 UTC; a date earlier than the current version's start, a negative, missing, out-of-range or over-precise amount, or a model outside the catalog is a 400 with a `precio-*` / `vigencia-anterior-a-la-version-actual` / `modelo-fuera-del-catalogo` code. `GET .../modelo` carries `precios[]` with the version in force of every priced catalog model; a model absent from it is "not priced". Historical usage keeps being priced with the version in force when each row was recorded. `proveedor` and `modelo` are the two halves of `Nombre`; local models without a price keep reporting "not priced", never zero.

### D10 — Effort and caching map per company, validated by a spike first

Anthropic `cache_control` on `PrefijoEstable` (`ProveedorAnthropic.cs:136`) is preserved, and `EsfuerzoDelModelo` maps to each provider's own parameter (`Effort` on Anthropic's `OutputConfig`; reasoning effort for OpenAI and Gemini; Mistral is sent no `reasoning_effort` at all, because the spike proved the wire shape only and a 4xx on an unsupported field would take the whole company down for a knob). Gemini's OpenAI-compatible base path is `/v1beta/openai/`, whereas `ProveedorLocal` hard-codes `/v1/chat/completions` against its own server.

The first task is a spike that verifies, through Microsoft.Extensions.AI: injection of the module's named `HttpClient` (so `GrabadorDeCassettes` and `ReintentoDeTransporte` still wrap every call), `cache_control` on the stable prefix, the effort mapping per company, the Gemini path, translation of provider failures to `HttpRequestException` **with `StatusCode` preserved** (the breaker and the failure causes of `asistente-metricas-de-calidad` read it), and `SeQuedoSinTokens` from the finish reason. If the spike fails any point for a company, that company keeps a hand-written adapter behind the same interface; the remaining companies proceed on `ProveedorChat`.

Cassette recording and replay work against the Anthropic wire shape (`ClaveDeCassette` reads `system`, `messages`, `model`, `output_config`); recording through OpenAI-compatible companies is not claimed (see TD-030 and `asistente-proveedor-ollama`, which leaves the same gap).

#### Spike results (2026-10-24)

Packages: `Microsoft.Extensions.AI.OpenAI` 10.5.1 (brings `OpenAI` 2.10.0, `System.ClientModel` 1.10.0) over `Microsoft.Extensions.AI.Abstractions` 10.5.1, the version `Anthropic` 12.42.1 already resolves. Evidence: `EspigaChatClientTests` (fake `HttpMessageHandler`, no network, no keys). Mistral and Gemini are the OpenAI-compatible client with a different base URL, so they share the OpenAI column and differ only in point (d). Pass for them is wire-level only: no real endpoint was called, so server-side acceptance of a field (for example `reasoning_effort` on a given Mistral model) is unverified until the first real call.

| Point                                                      | anthropic                                                                                                                                                                                                                                                                                                                                                            | openai                                                                                                                                                                                                                                                                  | mistral (OpenAI-compatible)                                            | gemini (OpenAI-compatible)                                                                                               |
| ---------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------ |
| (a) Injected `HttpClient` wrapped by the module's handlers | **pass**: `new AnthropicClient { HttpClient = http, MaxRetries = 0 }.AsIChatClient(model, maxTokens)`; an outer handler saw the call and a real `ReintentoDeTransporte` retried a 500 below it                                                                                                                                                                       | **pass**: `OpenAIClientOptions { Transport = new HttpClientPipelineTransport(http), RetryPolicy = new ClientRetryPolicy(0) }`; same chain observed. The SDK retry policy must be zeroed or it multiplies attempts (12 to 36 per turn), as with Anthropic's `MaxRetries` | pass (same client)                                                     | pass (same client)                                                                                                       |
| (b) `cache_control` on `PrefijoEstable`                    | **pass, only via raw types**: `AdditionalProperties["cache_control"]` / `["CacheControl"]` on system text are ignored. Works through `TextContent.RawRepresentation = TextBlockParam { CacheControl }`, or `ChatOptions.RawRepresentationFactory` returning a `MessageCreateParams` with `System` and empty `Messages` (filling `Messages` duplicates the user turn) | n/a (automatic prefix caching, no request field)                                                                                                                                                                                                                        | n/a                                                                    | n/a                                                                                                                      |
| (c) `EsfuerzoDelModelo` mapping                            | **pass, only via `RawRepresentationFactory`**: `OutputConfig { Effort }` produces `output_config.effort` exactly as `ProveedorAnthropic`. `ChatOptions.Reasoning` does NOT: it selects extended thinking (`thinking.budget_tokens`, inflated `max_tokens`) and must not be used. `Minimo` is omitted by returning no `OutputConfig`                                  | **pass**: `ChatOptions.Reasoning.Effort` produces `reasoning_effort` low/medium/high; `MaxOutputTokens` produces `max_completion_tokens`; no `Reasoning` omits the field (`Minimo`)                                                                                     | pass (wire-level)                                                      | pass (wire-level)                                                                                                        |
| (d) Base path                                              | n/a                                                                                                                                                                                                                                                                                                                                                                  | **pass**: `https://api.openai.com/v1/chat/completions`                                                                                                                                                                                                                  | **pass**: `https://api.mistral.ai/v1/chat/completions`                 | **pass**: `https://generativelanguage.googleapis.com/v1beta/openai/chat/completions` (base URL needs the trailing slash) |
| (e) Usage and finish reason to `RespuestaDelModelo`        | **pass with one adjustment**: `FinishReason` `Length` (`max_tokens`) / `Stop`; `OutputTokenCount`; `CachedInputTokenCount` = cache read. MEAI `InputTokenCount` sums input + cache read + cache creation, while `ProveedorAnthropic` reports input + cache read, so `ProveedorChat` subtracts `AdditionalCounts["CacheCreationInputTokens"]`                         | **pass**: `Length` / `Stop`; `CachedInputTokenCount` from `prompt_tokens_details.cached_tokens`; `InputTokenCount` = `prompt_tokens` (already includes cached)                                                                                                          | pass; absent `prompt_tokens_details` reads as no cache tokens (tested) | pass (same as Mistral)                                                                                                   |
| (f) Failure shape                                          | **pass with translation**: `AnthropicApiException` subtypes carry `StatusCode` (401 `AnthropicUnauthorizedException`, 429 `AnthropicRateLimitException`, 5xx `Anthropic5xxException`); not an `HttpRequestException`                                                                                                                                                 | **pass with translation**: `ClientResultException.Status` (401, 429, 500); not an `HttpRequestException`                                                                                                                                                                | pass (same)                                                            | pass (same)                                                                                                              |

Conclusions:

- No company needs to keep a hand-written adapter: every point passes, so all four cloud companies proceed on `ProveedorChat`. The fallback rule was not triggered.
- Two points pass only through provider-specific raw types, so `ProveedorChat` is not provider-agnostic in its request building: a per-company request builder is required (Anthropic via `RawRepresentationFactory` with `MessageCreateParams`; OpenAI-compatible via `ChatOptions.Reasoning`). Anthropic request building must never use `ChatOptions.Reasoning`.
- `ProveedorChat` owns failure translation (point f): it catches `AnthropicApiException` and `ClientResultException`, and throws `HttpRequestException(message, null, statusCode)` with the status preserved, or lets caller-token cancellation through. Neither SDK throws `HttpRequestException` itself.
- The SDK type names (`Anthropic.*`, `OpenAI.*`, `System.ClientModel`) leave the single-file rule of `ArquitecturaAsistenteTests`, handled by task 2.11.

### D11 — Administration outcomes and the test call

- `POST .../modelo/prueba` answers **200** with `{ exito, latenciaMs, codigo }` in both cases: a failed test is data for the screen, not an HTTP error. `PUT .../modelo` and `POST .../credenciales/{empresa}/reemplazo` re-test on the server and answer **422** with the same `codigo` (in the problem details extension) when that test fails, leaving everything unchanged. Invalid requests (unknown company, model outside the catalog, local server not configured, local credential supplied, key too short, company without a key to replace) are **400** with a `codigo`. A stored key that must be read or written with no usable master key is **503** `clave-maestra-no-disponible`.
- The test call is built through the same adapter path a turn would use but **bypasses the per-name breaker**: a test must neither trip nor reset the breaker of a model that is serving turns. For local servers it goes through the gate of the shared GPU **without waiting**, so a busy service is `saturado`.
- `DELETE .../modelo` with no stored selection is a no-op: 204, no audit row and no cache clear.
- Audit actions: `modelo.seleccionar`, `modelo.restablecer`, `credencial.crear`, `credencial.reemplazar`, `precio.cargar`. `credencial.*` rows carry company and hint only.

## Risks / Trade-offs

- **Master key loss** makes stored keys unrecoverable. Mitigation: documented operating procedure; keys can be re-entered from the screen.
- **A per-turn database read** adds one small query per turn. Accepted; same cost class as the maintenance check.
- **Provider quirks behind a common abstraction** (reasoning parameters, caching) may leak. Mitigated by D10's spike and per-company fallback.
- **An administrator can select a model that has no price**: the UI warns and usage reports it as not priced; it does not block selection.
- **Test calls cost money** for cloud companies. "Probar conexión" issues one minimal real call; the UI says so. The shared service call is free but occupies its single slot (`NUM_PARALLEL=1`, shared by every environment); the test goes through the same gate and reports saturation rather than waiting.
- **Switching the shared GPU away and back** does not reload the model: `num_ctx` is fixed per configuration (`ContextoDelModelo`), so the selection never changes it.
- **Key confinement is a convention until tested.** A static confinement scan (like the one guarding other secrets) fails the build if a key-bearing type reaches a log, response or audit writer.

## Migration Plan

1. Ship the storage, crypto and catalog (new SQL `009`, inert until used).
2. Ship `ProveedorChat`, the resolver and the keyed breaker behind the env fallback; with no stored selection, behavior is unchanged.
3. Ship the endpoints, then the tab.
4. Roll back by DELETE of the selection (returns to server configuration) or by not setting `Asistente__ClaveMaestra`.

## Open Questions

- Resolved 2026-10-24: the spike confirmed every company on `IChatClient` (D10); no company kept a hand-written adapter. `ProveedorAnthropic` still serves the environment's `anthropic` selection.
- The exact Mistral and Gemini model identifiers to seed; confirmed during task 1.3 against the evaluation results.

Resolved 2026-10-24: `Asistente__HostsLocales` is dropped (D6). The screen only sends a server identifier, so there is no URL to restrict.
