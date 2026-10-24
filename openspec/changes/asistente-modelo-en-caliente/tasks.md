## 0. Prerequisites

- [x] 0.1 Confirm the branch contains `asistente-proveedor-local` and `asistente-proveedor-ollama` (`ProveedorLocal`, `ProveedorOllama`, `OpcionesInferencia`, `ProveedorConCompuerta`, `FallasDelProveedor`); this change assumes them. Both are on the head of PR #40
- [ ] 0.2 Re-derive the next free `database/asistente` number against the target base immediately before writing the SQL (today: `008_asistente_presupuesto_usuario_vigente_unico.sql` is the last, so this change uses `009`; `asistente-metricas-de-calidad` uses `010`). If another change lands a number first, renumber both together and keep `RecursoVersion` last in `MigradorAsistente.Recursos`
- [ ] 0.3 `.env.example` is updated only after any merge conflict in it is resolved (task 6.6); never read or edit real `.env*` files

## 1. Slice 1 — Storage, crypto and catalog

- [x] 1.1 `database/asistente/009_asistente_modelo.sql` (new file, never edit a published one) — `credencial_de_proveedor(empresa PK, clave_cifrada, pista, validada_en, actor_id, actualizado_en)`, `modelo_vigente(id smallint PK CHECK (id = 1), empresa, modelo, actor_id, actualizado_en)`, `catalogo_de_modelos(empresa, modelo, PK(empresa, modelo))`; `IF NOT EXISTS` throughout, comments on every table and column, `COMMENT` explaining that no row in `modelo_vigente` means server configuration
- [x] 1.2 Static wrapper class `ModeloAsistente` (`RecursoSql`, `AplicarAsync`) following `AdministracionAsistente`, embedded resource entry, and registration in `MigradorAsistente.Recursos` after `AdministracionAsistente.RecursoSqlVigenteUnico` and before `RecursoVersion`
- [x] 1.3 Seed `catalogo_de_modelos` (idempotent `INSERT ... ON CONFLICT DO NOTHING`): initial Anthropic, OpenAI, Google Gemini and Mistral entries (OpenAI, Gemini and Mistral ids are provisional: no evaluation results exist yet; confirm or correct them in a follow-up SQL before slice 3), `ollama/qwen3.5:9b` (`compose.asistente-ollama.yml`) and `local/qwen3-8b` (`compose.asistente-local.yml`)
- [x] 1.4 Confirm the manifests need no change: the `asistente` schema is `denegado` wholesale in `database/asistente/manifiesto-privilegios.json` and neither manifest lists schema-internal tables; add no entries
- [x] 1.5 `CifradoDeCredenciales` (AES-GCM, `v1:{kid}:{base64(iv|tag|ciphertext)}`, AAD `asistente:{empresa}`, current and previous master key, lazy re-encryption on read), reading `Asistente__ClaveMaestra` / `Asistente__ClaveMaestraAnterior` through `OpcionesAsistente`, with validation (32 bytes, base64) added to `ValidadorDeOpcionesAsistente` only for the case "a key operation is requested", so it never runs at module registration and an unset key never fails startup
- [x] 1.6 Repository for credentials, selection and catalog (reads and writes; key text never returned, only `pista`)
- [x] 1.7 Test: migration applies twice without error; DDL passes the `ArquitecturaAsistenteTests` rules (no `DROP`, `RENAME`, `ALTER COLUMN ... TYPE`)
- [x] 1.8 Test: `ManifiestoPrivilegiosTests` / `PrivilegiosLecturaTests` still pass and show neither read-only role can read the three new tables (extend `PrivilegiosLecturaTests` with them)
- [x] 1.9 Test: AES-GCM round trip; a tampered ciphertext fails; a ciphertext moved to another company's AAD fails
- [x] 1.10 Test: rotation — a value encrypted under the previous key (by `kid`) decrypts and is re-encrypted under the current key on read
- [x] 1.11 Test: with no master key configured, the module starts and `/api/asistente/ping` answers; saving a key reports the unavailable-key condition
- [x] 1.12 Update `ArquitecturaAsistenteTests`: add the new reader/writer files to the `CadenaDuena` allowlist `PuedenUsarLaCadenaDelDueno` if they need the owner connection, and raise `SuperficiePublicaDeApplication` only by the exact count of intentionally public new types

## 2. Slice 2 — Spike, `ProveedorChat`, resolver, keyed breaker

- [ ] 2.1 Spike (timeboxed, results recorded in this change's design.md under D10): through Microsoft.Extensions.AI verify (a) injection of the named `ClienteDelProveedor` `HttpClient` so `GrabadorDeCassettes` and `ReintentoDeTransporte` still wrap every call, (b) Anthropic `cache_control` on `PrefijoEstable`, (c) `EsfuerzoDelModelo` mapping per company, (d) Gemini through `/v1beta/openai/`, (e) provider failures surface as `HttpRequestException` with `StatusCode` preserved, (f) `SeQuedoSinTokens` from the finish reason. For any failed point, mark that company "keeps hand-written adapter" and continue
- [ ] 2.2 Add `Microsoft.Extensions.AI.OpenAI` to `Modules.Asistente`; pin versions consistent with `Anthropic` 12.42.1 / `Microsoft.Extensions.AI.Abstractions`
- [ ] 2.3 `ProveedorChat : IProveedorDeModelo` with `Nombre = "{empresa}/{modelo}"`, one attempt per call, failures translated to `HttpRequestException` (status preserved) or caller-token cancellation (the only shapes `ProveedorConBreaker` counts), no raw provider detail in messages
- [ ] 2.4 Per-company client factory for the four cloud companies (Anthropic via `AsIChatClient()`; OpenAI, Mistral and Gemini via OpenAI-compatible endpoints). The local kinds are not built here: `ProveedorOllama` and `ProveedorLocal` are built by the existing `ArmarOllama` / `ArmarLocal` paths, reusing `OpcionesInferencia.Autenticar` and `UrlDelProveedorLocal`
- [ ] 2.5 `IResolutorDelProveedor` (scoped): read `modelo_vigente` once per turn, decrypt the company key for cloud kinds, build the adapter for that turn only, fall back to the environment-built singletons (`ProveedorBase`, `BaseDeRedaccion`) when no row exists; no process cache of the selection. The two scoped `Encadenar` registrations (`IProveedorDeModelo`, keyed `ProveedorDeRedaccion`) call it. Preserve `envolverProveedor` for the evaluator on both paths and keep the raw adapter inside the ceiling and breaker
- [ ] 2.6 Apply the concurrency gate (`ProveedorConCompuerta`) only when the resolved kind is `ollama` or `local`; cloud kinds get ceiling plus breaker only
- [ ] 2.7 Turn `BreakerDelProveedor` into a per-`Nombre` registry; `Encadenar` and `DisponibilidadDelModeloReal` read the breaker of the resolved name
- [ ] 2.8 Ignore `ModeloDeRedaccion` when a runtime selection exists
- [ ] 2.9 Decide `RedaccionSinEnmascararVigente` from the resolved adapter: with a runtime selection, own hardware means only `ollama` / `local`; any cloud company masks regardless of `RedaccionSinEnmascarar`. Update the read in `CarrilSql` and keep `AnuncioDeLaRedaccion` describing the environment state
- [ ] 2.10 Clear `CacheDeConsultasGeneradas` when a selection write succeeds
- [ ] 2.11 Update `ArquitecturaAsistenteTests.El_SDK_del_proveedor_se_nombra_en_un_solo_archivo` to allow exactly `ProveedorAnthropic.cs` (until retired) plus the one new file that builds `IChatClient`
- [ ] 2.12 Test: wire-level parity — for the same `SolicitudAlModelo`, a fake handler captures the same Anthropic request body (system with `cache_control`, `output_config.effort`, token ceilings) from `ProveedorAnthropic` and `ProveedorChat`, and the translated `RespuestaDelModelo` carries the same text, token counts, cache tokens and `SeQuedoSinTokens`. Not through cassettes (TD-030)
- [ ] 2.13 Test: switching the stored selection changes the adapter used by the next turn without restarting the Host
- [ ] 2.14 Test: a turn that began before the switch finishes on the old model
- [ ] 2.15 Test: with no stored selection the environment configuration is used unchanged, for each of `simulado`, `anthropic`, `local` and `ollama`
- [ ] 2.16 Test: with incomplete configuration the Host starts and `/api/asistente/ping` answers
- [ ] 2.17 Test: an open breaker for one `Nombre` does not affect another `Nombre`, and `DisponibilidadDelModeloReal` reports `ProveedorCaido` only for the resolved name
- [ ] 2.18 Test: the selected model performs both generation and redaction when a selection exists
- [ ] 2.19 Test: with `RedaccionSinEnmascarar=true` and environment `ollama`, selecting a cloud company makes the redaction prompt carry masked `sensible-valor` markers; selecting `ollama` again unmasks
- [ ] 2.20 Test: the concurrency gate wraps `ollama` and `local` selections and does not wrap a cloud selection, even with `MaximoDeLlamadasConcurrentes > 0`
- [ ] 2.21 Test: a saturated shared service (`ProveedorSaturado` from 429/503) does not open the breaker for `ollama/...`

## 3. Slice 3 — Administration endpoints

- [ ] 3.1 `GET api/asistente/administracion/modelo` — current selection, origin (server configuration or screen), local servers configured (identifier only, never a URL), catalog grouped by company with key status (hint or missing); never the key
- [ ] 3.2 `POST .../modelo/prueba` `{empresa, modelo, claveNueva?}` — one minimal real call; returns latency or one of `clave-invalida`, `inalcanzable`, `sin-credito`, `modelo-inexistente`, `saturado`; persists nothing. For `ollama`: 401/403 → `clave-invalida`, 404 → `modelo-inexistente`, 429/503 (`ProveedorSaturado`) → `saturado`, transport failure → `inalcanzable`
- [ ] 3.3 `PUT .../modelo` `{empresa, modelo, claveNueva?}` — server re-tests, saves a new key if given, sets the selection; audit row written before responding
- [ ] 3.4 `DELETE .../modelo` — return to server configuration; audited
- [ ] 3.5 `POST .../credenciales/{empresa}/reemplazo` `{clave}` — cloud companies only; validate against the provider first, replace only on success, otherwise leave the old key untouched; independent of model selection; audited
- [ ] 3.6 Local-server check on every local selection or test: the identifier must be `ollama` with `Inferencia__Url` set, or `local` with `Asistente__UrlDelProveedorLocal` set; otherwise reject without an outbound call. No endpoint accepts a URL, host or local credential
- [ ] 3.7 Audit actions for selection, revert, key replacement and key creation through `IAuditoriaDeAdministracion`, recording `empresa`, `modelo` and the hint only
- [ ] 3.8 Test: every route returns 403 without `asistente.administrar`; no route collides with `uso`, `presupuestos`, `servidor-local`, `tope-organizacional`, `mantenimiento`
- [ ] 3.9 Test: a failed replacement keeps the old key decryptable and in use
- [ ] 3.10 Test: PUT with a failing test result does not change the selection
- [ ] 3.11 Test: an unconfigured local server identifier is rejected without any outbound call
- [ ] 3.12 Test: each error code of the test endpoint is produced by a stubbed provider response of the matching kind, including `saturado` from a 503 of the shared service
- [ ] 3.13 Static confinement test: no response, log call, exception message or audit row can carry key text, `Inferencia__Contrasena` or the Access secret (scan modeled on the existing confinement scans)
- [ ] 3.14 Test: saving a key with no master key configured returns 503 and leaves the local servers usable

## 4. Slice 4 — Frontend "Modelo" tab

- [ ] 4.1 `frontend/src/features/sistema/hooks/useSeccionSistema.ts` — add `modelo` to `IdPestanaSistema`, `ORDEN` (Estado, Asistente, Modelo, then Calidad and Auditoría) and `PERMISO_POR_PESTANA` (`asistente.administrar`); `pages/SistemaPage.tsx` — `NOMBRES_PESTANAS`, the tab content, the "Actualizar" prefix (`prefijoActivo`, a `modelo` query-key prefix) and the status-dot tone (neutral). `app/shell/nav.ts` needs no change: the Sistema item already accepts any of `sistema.estado.ver`, `asistente.administrar`, `auditoria.ver`
- [ ] 4.2 API hooks for the endpoints in slice 3 (React Query, the single axios instance)
- [ ] 4.3 "En uso" banner reusing the `adoc-asistente-admin-mantenimiento` styles from `features/asistente/asistente.css` (or the Sistema equivalents), with "Volver a la configuración del servidor", and a note when masking is active because a cloud model is selected
- [ ] 4.4 Two radio cards, "Modelo local" and "API en la nube"
- [ ] 4.5 Local panel: server select listing only the configured local servers by name ("Servicio de inferencia compartido", "Servidor propio"), model select from the catalog, help tooltip explaining credentials live in server configuration. No URL or credential field
- [ ] 4.6 Cloud panel: custom listbox grouped by company, group header shows key status (saved hint in green, missing in amber), check mark on the selected option, "En uso" pill; keyboard operable (arrows, Enter, Esc, click outside)
- [ ] 4.7 Key field "Clave de {empresa}" appears when the company has no key and is saved on apply
- [ ] 4.8 "Probar conexión" then "Aplicar cambio" (disabled until the test passes), with inline confirmation stating in-flight turns finish on the current model; `saturado` is shown as "El servicio está ocupado, probá en unos segundos", not as a failure
- [ ] 4.9 Key replacement panel: current key stays visible ("Sigue en uso hasta que confirmes el reemplazo"), "Confirmar reemplazo" validates first, failure keeps the old key
- [ ] 4.10 "Cambios del modelo" list reusing `sistema-cambios-*` styles from `sistema.css`, linking to Auditoría
- [ ] 4.11 Reuse existing classes and `@ars-docendi/ui` components; no placeholder controls
- [ ] 4.12 Vitest (`SistemaPage.test.tsx`, `routes.test.tsx`): tab hidden without the permission and shown with it, after "Asistente"
- [ ] 4.13 Vitest: listbox keyboard behavior (arrows, Enter, Esc, click outside)
- [ ] 4.14 Vitest: "Aplicar cambio" disabled until the connection test passes, and disabled again when the selection changes
- [ ] 4.15 Vitest: replacement flow keeps the old key visible on failure and updates the hint on success
- [ ] 4.16 Update the design spec for the Sistema screen (`docs/product/designs/administracion-sistema-design-spec.md` and `docs/product/designs/asistente-conversacional-design-spec.md`) where they describe the Sistema tabs (AGENTS.md rule 9)

## 5. Slice 5 — Price write path and form

- [ ] 5.1 `POST api/asistente/administracion/precios` `{empresa, modelo, entradaPorMillon, salidaPorMillon, cachePorMillon?, vigenteDesde}` — convert to the per-token columns of `tabla_de_precios`, close the current row and insert a new version in one transaction; audited
- [ ] 5.2 Validation: non-negative amounts, `vigenteDesde` not before the current version's start, `empresa/modelo` in the catalog
- [ ] 5.3 Frontend: "Sin precio cargado" warning with "Cargar precio" form (input, output and cache USD per million tokens plus effective date; provider pricing page or the LiteLLM public price list shown as suggestions only). Offered for local models too, which are "not priced" until a price is loaded
- [ ] 5.4 Frontend: when priced, show "Precio vigente desde …" and "Cargar nueva versión"
- [ ] 5.5 Test: writing a price closes the previous version and opens exactly one current version
- [ ] 5.6 Test: usage computed for rows before the change still uses the old version
- [ ] 5.7 Test: a model without a price is reported as not priced and the price endpoint returns 403 without the permission
- [ ] 5.8 Vitest: price form states (missing, present, new version)

## 6. Slice 6 — Documentation and verification

- [ ] 6.1 `docs/architecture/domains/asistente.md` — runtime selection, resolver, keyed breaker, gate scope, masking rule
- [ ] 6.2 `docs/architecture/api-contracts.md` — new routes and error codes
- [ ] 6.3 `docs/architecture/data-model.md` — three new tables, price write path
- [ ] 6.4 `docs/architecture/infrastructure.md` — `Asistente__ClaveMaestra`, `Asistente__ClaveMaestraAnterior`, how local servers derive from `Inferencia__*` and `Asistente__UrlDelProveedorLocal`, master key generation and rotation procedure; cross-reference `docs/operations/ollama-compartido.md` and `docs/architecture/modelo-local.md`
- [ ] 6.5 `backend/src/Modules.Asistente/README.md`, `backend/eval/README.md`, `docs/quality/tech-debt.md` (TD-008: switching provider or model is now a screen action)
- [ ] 6.6 `.env.example` and `infra/compose/compose.base.yml` — add `Asistente__ClaveMaestra` the way `Inferencia__*` is forwarded; update only after any merge conflict in `.env.example` is resolved
- [ ] 6.7 Run the proportional verification: `dotnet test backend/ArsDocendi.slnx`, `pnpm --filter frontend test:run`, `pnpm --filter frontend lint`, `pnpm --filter frontend build`, `pnpm format:check`, `pnpm exec openspec validate --all --strict`; record any check the environment could not complete
