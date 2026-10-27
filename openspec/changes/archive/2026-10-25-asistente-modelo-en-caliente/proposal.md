## Why

Today the Asistente's model provider, model and API key are frozen at first resolution. `ModuleExtensions.cs` registers the raw adapter (`AdaptadorDeGeneracion`, plus `AdaptadorDeRedaccion` when `ModeloDeRedaccion` is set) and `ProveedorBase` as singletons built from `IOptions<OpcionesAsistente>`, and the decorator chain (`Encadenar`: turn ceiling, optional concurrency gate, breaker and timeout) is built once per scope around them. Moving between a local model and a cloud API therefore means editing environment variables and restarting the Host. Administrators cannot do this themselves, cannot choose among several cloud companies, and cannot keep a separate key per company.

Four adapters exist on this branch and the registry is the `switch` in `ModuleExtensions.ConstruirProveedor`: `ProveedorSimulado` (`simulado`), `ProveedorAnthropic` (`anthropic`, SDK over the module's named `HttpClient`), `ProveedorLocal` (`local`, hand-written OpenAI-compatible client for an own llama.cpp/vLLM server, `Asistente__UrlDelProveedorLocal`) and `ProveedorOllama` (`ollama`, native `/api/chat` against the shared inference service configured by `Inferencia__*`). The two local adapters are already merged here (`asistente-proveedor-local`, `asistente-proveedor-ollama`).

This change lets an administrator switch the model at runtime from the Sistema screen, choose among Anthropic, OpenAI, Google Gemini and Mistral (plus the local servers the deployment already has), and keep one encrypted key per company. Environment configuration remains the fallback whenever no runtime selection exists.

## What Changes

- **One cloud chat adapter**, `ProveedorChat : IProveedorDeModelo`, built over Microsoft.Extensions.AI `IChatClient`. It serves Anthropic, OpenAI, Google Gemini and Mistral, and replaces the hand-written Anthropic adapter's role only after wire-level parity is shown. `Nombre` is `"{empresa}/{modelo}"`, the format `ProveedorAnthropic`, `ProveedorLocal` and `ProveedorOllama` already write. `ProveedorOllama` and `ProveedorLocal` **stay**: the shared inference service only exposes Ollama's native API through Traefik, and both adapters carry port features (streaming, `think`, `num_ctx`, structured output, cache accounting) that `IChatClient` does not carry.
- **Per-turn model resolution.** The active selection is read from the database at the start of every turn, with no process cache, so several Host instances agree. In-flight turns finish on the model they started with. With no stored selection the module falls back to `IOptions<OpcionesAsistente>` exactly as today. The decorator chain is rebuilt per turn around the resolved adapter; the concurrency gate applies only to local kinds.
- **A runtime selection of a cloud model can never unmask redaction.** `OpcionesAsistente.RedaccionSinEnmascararVigente` is decided from the environment's `Proveedor`; under runtime selection it is decided from the resolved adapter, so an environment that runs the shared GPU with `RedaccionSinEnmascarar=true` masks again the moment a cloud company is selected.
- **One API key per company, encrypted at rest** with AES-GCM and a master key from `Asistente__ClaveMaestra`. Only a last-4 hint is ever shown. Keys never appear in responses, logs, errors or the administration audit.
- **A repository-defined model catalog**, seeded in a table and grouped by company. Adding a model is a pull request.
- **Local servers are a closed set derived from server configuration**, never a URL typed in the screen: the shared inference service (`Inferencia__Url`, empresa `ollama`) and the own server (`Asistente__UrlDelProveedorLocal`, empresa `local`), each offered only if configured. Credentials stay server configuration (`Inferencia__Usuario`/`Contrasena`, optional Cloudflare Access service token `Inferencia__AccessClientId`/`AccessClientSecret`, `Asistente__ClaveDelProveedor` for the own server) and never appear in the UI.
- **A price write path** for `asistente.tabla_de_precios`, which is already versioned but has no writer today.
- **New endpoints** under `api/asistente/administracion/` (policy `Permisos.AsistenteAdministrar`, every write audited before responding): read selection, test connection, apply, revert to server configuration, replace a company key, write a price.
- **A new "Modelo" tab** in `SistemaPage` (hash `#modelo`, gated by `asistente.administrar`, placed after the existing "Asistente" tab), following the prototype at https://claude.ai/artifact/3ehTaUJJdL5JwgW5n7F8an: an "En uso" banner, two radio cards ("Modelo local" / "API en la nube"), a grouped listbox of cloud models showing per-company key status, "Probar conexión" then "Aplicar cambio", a separate key-replacement panel, a "Sin precio cargado" warning with a price form, and a "Cambios del modelo" list. "Modelo local" lists the configured local servers (shared inference service, own server).
- **Circuit-breaker state keyed by adapter `Nombre`**, so switching does not inherit the previous provider's open breaker. `DisponibilidadDelModeloReal` (the `ProveedorCaido` pre-check) reads the breaker of the resolved name.
- **Documentation in the same diff** (AGENTS.md rule 6): `docs/architecture/domains/asistente.md`, `api-contracts.md`, `data-model.md`, `infrastructure.md`, `docs/architecture/modelo-local.md` and `docs/operations/ollama-compartido.md` (cross-reference only), `backend/src/Modules.Asistente/README.md`, `.env.example`, `infra/compose/compose.base.yml`, `backend/eval/README.md`, `docs/quality/tech-debt.md` (TD-008), and the design spec for the screen if one exists.

## Capabilities

### New Capabilities

- `asistente-modelo-en-caliente`: runtime model selection, per-company encrypted credentials, the repository-defined catalog, the closed set of local servers derived from server configuration, and the price write path.

### Modified Capabilities

- `asistente-auditoria-de-administracion`: the set of audited administrative actions grows to include model selection, key replacement and price writes (with the key itself never recorded).
- `asistente-panel-de-uso`: the versioned price table gains an administrator write path; the estimated-cost requirement is extended so that a model without a price is reported as not priced and can be priced from the screen.

## Impact

- **Affected module**: `Modules.Asistente` (`ModuleExtensions`, `Configuracion/OpcionesAsistente.cs`, `Infrastructure/`, `Application/Administracion/`, `Api/AdministracionAsistenteController`, `Application/CarrilSql/CarrilSql.cs` for the masking decision), the SQL under `database/asistente/`, `frontend/src/features/sistema/`, and `ArquitecturaAsistenteTests`.
- **Dependency graph**: no new module edges. New NuGet package: `Microsoft.Extensions.AI.OpenAI` (`Microsoft.Extensions.AI.Abstractions` already arrives through `Anthropic` 12.42.1, which exposes `AsIChatClient()`). `ArsDocendi.Inferencia` is not a project on this branch: the shared-service client (`OpcionesInferencia`, `ClienteInferencia`) lives inside `Modules.Asistente` since `asistente-proveedor-ollama`.
- **Public API**: no change to `Modules.Asistente.Contracts`. New administration routes only (existing ones: `uso`, `presupuestos`, `servidor-local`, `tope-organizacional`, `mantenimiento`).
- **Database**: new numbered file `database/asistente/009_asistente_modelo.sql` (008 is taken by `008_asistente_presupuesto_usuario_vigente_unico.sql`; `asistente-metricas-de-calidad` takes 010), with a static wrapper class and an entry in `MigradorAsistente.Recursos` before `RecursoVersion`. Three new tables (`credencial_de_proveedor`, `modelo_vigente`, `catalogo_de_modelos`). The manifests need no entries: `manifiesto-privilegios.json` declares the whole `asistente` schema `denegado` to both read-only roles and both manifests list only granted columns; the existing privilege tests prove the new tables are unreadable. No published SQL file is edited. This change must land before `asistente-metricas-de-calidad` or the two numbers are swapped together; neither depends on the other's schema.
- **Configuration**: `Asistente__ClaveMaestra` (32 bytes, base64) and optional `Asistente__ClaveMaestraAnterior`. No new local-host setting: local servers come from `Inferencia__*` and `Asistente__UrlDelProveedorLocal`. Existing `Asistente__Proveedor`, `Asistente__Modelo`, `Asistente__ClaveDelProveedor` and `Asistente__UrlDelProveedorLocal` keep working as the fallback.
- **Artifact language**: English, per the Language Domain Contract (the project's local preferences override `openspec/config.yaml` for newly generated artifacts); existing Spanish code and product documents stay as they are.
- **Institutional business rules**: not applicable; no `BR-*` is introduced.
- **Rollback**: DELETE of the selection returns the module to server configuration with no data loss; the new tables are additive and can be left unused.

## Out of Scope

- Per-agent model choice (generation, redaction and rewriting all use the one selected model).
- Audience-specific models, per-key budgets.
- OpenRouter or a LiteLLM proxy.
- A redaction-model field in the UI (D7).
- Live model listing from provider APIs (D5).
- Replacing the module's retry authority (`ReintentoDeTransporte`) or the breaker design beyond keying it by `Nombre`.
