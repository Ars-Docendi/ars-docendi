# Tasks

## 1. Verify the Ollama contract

- [ ] 1.1 Against the pinned Ollama image (0.35.0, `infra/ollama` `.env.example` digest) on a local Ollama, NOT pc-prod: confirm the `GET /api/ps` fields (`model`, `name`, and whether `context_length` exists), that `POST /api/generate` with only `model` + `options.num_ctx` loads and returns `done: true`, and that a later `/api/chat` with the same `num_ctx` does not reload (`load_duration` near zero). Record the result in design.md D1. _Not run during implementation (no local Ollama may be used here); the adapter does not depend on `context_length` being present. Do it with 7.2._

## 2. Backend: availability (red first)

- [x] 2.1 Red: integration tests in `backend/tests/ArsDocendi.IntegrationTests/Asistente/` with a fake HTTP handler (same style as `ProveedorOllamaTests`/`TelemetriaDelServidorLocalTests`): `en_reposo` when `/api/ps` omits the model; `listo` when listed with matching context; `en_reposo` on mismatched context; `desconocido` on 404/401/429/timeout with no URL or body in the DTO; `no_aplica` for `local`, `anthropic` and `simulado` with zero outbound requests; a stored `ollama` selection over a cloud environment is honored.
- [x] 2.2 Red: 20 concurrent availability requests with an expired cache produce exactly one `/api/ps`; a request inside the cache lifetime produces none; a successful turn call marks `listo` without `/api/ps`.
- [x] 2.3 Green: `Infrastructure/CargaDelModelo.cs` (singleton cache keyed by model, single-flight refresh, named `HttpClient` with 3 s timeout and no retries), options `TiempoDeVidaDisponibilidadSegundos` (default 30) validated in `ValidadorDeOpcionesAsistente`, and the hook that marks `listo` after a successful `ollama` call.
- [x] 2.4 Green: `Api/ModeloDelAsistenteController.cs` with `GET api/asistente/modelo/disponibilidad` under `Permisos.AsistenteConsultar`, resolving the identity through `ResolutorDelProveedor`. Test: a user without `asistente.consultar` gets 403 and no outbound request.

## 3. Backend: activation (red first)

- [x] 3.1 Red: ten concurrent `POST api/asistente/modelo/activacion` produce exactly one `/api/generate`, all answering 202 `activando`; the body names the model, carries `num_ctx` equal to `ContextoDelModelo` (absent when 0), and has no `prompt` nor `keep_alive`; `listo` and `no_aplica` produce no outbound request; a request inside the cooldown starts no load.
- [x] 3.2 Red: a failed or timed-out load leaves `en_reposo` with `ultimaActivacionFallida: true`; the log entry has model and outcome and no actor id, URL or body; the actor's remaining quota and the breaker state are unchanged after an activation.
- [ ] 3.3 Red: a question sent while activating is accepted and answered as usual (existing turn test fixture with a delayed fake Ollama). _Not written: no code path couples a turn to the activation state (the activation lives outside the turn chain), so the only way to cover it is an end-to-end run, which is 7.2._
- [x] 3.4 Green: single-flight load detached from the request (host stopping token, `TiempoMaximoDeActivacionSegundos` default 120), cooldown `EnfriamientoDeActivacionSegundos` default 30, outside the turn chain (no ceiling, quota, consumption row, breaker or gate).

## 4. Frontend (red first)

- [x] 4.1 Red: Vitest for `AvisoDeModeloEnReposo` in `PanelAsistente`: shown with exact copy and «Activar modelo» only for `en_reposo`; absent for `no_aplica`, `listo`, `desconocido` and in maintenance; pressing the button shows «Activando el modelo…» with the button disabled; `listo` shows «El modelo está listo.» and then unmounts; failure flag or client time limit shows «No se pudo activar el modelo. Probá de nuevo.» with the button enabled; an answered turn hides the notice; the text is in a polite live region.
- [x] 4.2 Green: `api/asistenteApi.ts` (`obtenerDisponibilidadDelModelo`, `activarModelo`), `types.ts`, `hooks/useDisponibilidadDelModelo.ts` (query on mount, `staleTime` 30 s, `refetchInterval` 2/3/5 s only while `activando`, 120 s client limit), `components/AvisoDeModeloEnReposo.tsx` with `@ars-docendi/ui` components and no new dependency.

## 5. Infra

- [x] 5.1 Add `Path(`/api/ps`)` to the GET branch of `traefik.http.routers.ollama-privada.rule` in `infra/ollama/compose.yml`; update the route list in `docs/operations/ollama-compartido.md` (§9 labels and §11 table) and state that applying it recreates Ollama and needs an agreed window. If `infra/tests` assert the router rule, update them; do not add a test that only restates the label.
- [ ] 5.2 **Operator, not done here:** apply the router rule on pc-prod (`oc up` recreates Ollama and cuts requests in flight) in a window agreed with the team that operates the GPU. Until then `/api/ps` answers 404 and the notice never appears. Nothing in this change touches pc-prod. No `infra/tests` assert the router rule, so none was added.

## 6. Documentation

- [x] 6.1 `docs/architecture/api-contracts.md` and `docs/architecture/domains/asistente.md`: the two endpoints, permission, DTO and states.
- [x] 6.2 `docs/architecture/modelo-local.md` (Ollama section): cold start causes, detection via `/api/ps`, preload with the same `num_ctx`, budget math. `backend/src/Modules.Asistente/README.md`: the three new options.
- [x] 6.3 `docs/product/designs/asistente-conversacional-design-spec.md`: new state «Modelo en reposo» in «Estados a diseñar» and the four strings in «Copy». Note that the Centro de ayuda has no assistant article (`ayuda/contenido/index.ts` excludes it), so rule 9 has nothing else to update.

## 7. Verification

- [x] 7.1 `dotnet test backend/ArsDocendi.slnx`; `pnpm --filter frontend test:run`, `pnpm --filter frontend lint`, `pnpm --filter frontend build`; `node --test infra/tests/*.test.mjs`; `pnpm format:check`, `node scripts/docs/check-links.mjs`, `pnpm exec openspec validate --all`.
- [ ] 7.2 Local e2e against a local Ollama (e.g. the RTX 3070 box with Ollama and a small model, `Asistente__Proveedor=ollama`, short `OLLAMA_KEEP_ALIVE`): wait for unload, open the assistant, see the notice, press «Activar modelo» from two browsers at once, confirm one load in Ollama logs, see «El modelo está listo.», then ask a question and confirm no reload.
- [ ] 7.3 Manual e2e against the real shared Ollama in pc-prod (staging credential) ONLY inside a window agreed with the team that operates the GPU, after 5.1 is applied there: same steps as 7.2, plus checking that `/api/ps` 404 before the rule yields no notice. Do not run anything against pc-prod outside that window.
