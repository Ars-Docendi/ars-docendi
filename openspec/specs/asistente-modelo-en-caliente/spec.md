# asistente-modelo-en-caliente Specification

## Purpose

Lets an administrator change the Asistente's model provider and model at runtime from the Sistema screen, choose among several cloud companies and the local servers the deployment already configures (the shared inference service and an own server), and keep one encrypted API key per cloud company, without a restart and without a key ever leaving the server.

## Requirements

### Requirement: The active model is chosen at runtime and read on every turn

The system SHALL resolve the model adapter at the start of every turn from the stored runtime selection, without a process-level cache, so that every Host instance applies a change from the next turn on. A turn SHALL finish on the adapter it resolved at its start.

#### Scenario: A change applies from the next turn without a restart

- **GIVEN** the stored selection is company A with model X
- **WHEN** an administrator applies company B with model Y and a new turn starts
- **THEN** the new turn uses company B and model Y
- **AND** the Host was not restarted

#### Scenario: An in-flight turn finishes on the previous model

- **GIVEN** a turn has started on company A with model X
- **WHEN** an administrator applies company B with model Y before that turn ends
- **THEN** the in-flight turn completes on company A with model X

#### Scenario: Two Host instances agree on the selection

- **GIVEN** two Host instances sharing one database
- **WHEN** an administrator applies a new selection through one of them
- **THEN** the next turn on the other instance uses the new selection

### Requirement: Server configuration is the fallback when no selection exists

The system SHALL use the environment configuration (`Asistente__Proveedor`, `Asistente__Modelo`, and related settings) whenever no runtime selection is stored, and SHALL NOT read the database during service registration. The module MUST start and `GET /api/asistente/ping` MUST answer even when the provider configuration is incomplete.

#### Scenario: No stored selection uses the environment

- **GIVEN** no row exists in the selection table
- **WHEN** a turn starts
- **THEN** the adapter is built from the environment configuration

#### Scenario: Incomplete configuration does not stop the module

- **GIVEN** a configuration that cannot build any real provider and no stored selection
- **WHEN** the Host starts
- **THEN** it starts successfully
- **AND** `GET /api/asistente/ping` answers `{ module, status: "ok" }`

#### Scenario: Reverting returns to server configuration

- **GIVEN** a stored selection
- **WHEN** an administrator reverts to server configuration
- **THEN** the selection is removed
- **AND** the next turn uses the environment configuration

### Requirement: A runtime selection governs both generation and redaction

When a runtime selection exists, the system SHALL ignore the environment's redaction-model setting and SHALL use the selected model for both generation and redaction.

#### Scenario: The environment redaction model is ignored

- **GIVEN** an environment redaction model belonging to another company and a stored selection
- **WHEN** a turn redacts its answer
- **THEN** the redaction call uses the selected company and model

### Requirement: A cloud selection never unmasks redaction

The system SHALL decide whether redaction runs without masking `sensible-valor` columns from the adapter resolved for the turn, and SHALL mask them whenever that adapter is a cloud company, regardless of the environment's unmasking option and of the environment's provider.

#### Scenario: Selecting a cloud company masks again

- **GIVEN** an environment on the shared inference service with the unmasking option on, so redaction carries real values
- **WHEN** an administrator selects a cloud company and a new turn redacts its answer
- **THEN** the redaction prompt carries masked markers and no real `sensible-valor` value

#### Scenario: Selecting own hardware keeps the environment decision

- **GIVEN** the unmasking option on and no cassette directory
- **WHEN** an administrator selects the shared inference service or the own server
- **THEN** redaction follows the environment option as it does without a selection

### Requirement: Every adapter is named company slash model and breaker state follows the name

The system SHALL name each adapter `"{empresa}/{modelo}"` and SHALL hold circuit-breaker state per adapter name.

#### Scenario: Switching does not inherit an open breaker

- **GIVEN** the breaker for the current adapter name is open
- **WHEN** an administrator selects a different company and model
- **THEN** turns on the new adapter are attempted normally

#### Scenario: A breaker opened for one name stays open for that name

- **GIVEN** the breaker for adapter A opened and the selection moved to adapter B and back to A before it closed
- **WHEN** a turn starts on adapter A
- **THEN** the breaker for A is still open

### Requirement: The concurrency gate protects local servers only

The system SHALL apply the concurrency gate only to adapters running on own hardware (the shared inference service and the own server), and SHALL NOT apply it to a cloud company or to an environment-configured `anthropic` or `simulado` adapter, while the per-turn call ceiling and the circuit breaker apply to every adapter.

#### Scenario: A cloud selection is not serialized by the GPU gate

- **GIVEN** an environment with the concurrency gate set to one call
- **WHEN** an administrator selects a cloud company and two turns call it at once
- **THEN** both calls proceed without waiting on the gate

#### Scenario: An unbuildable selection degrades instead of failing the turn

- **GIVEN** a stored selection whose key is missing or unreadable, or whose master key is unavailable
- **WHEN** a turn calls the model
- **THEN** the call fails as a provider failure and the turn degrades
- **AND** no key or provider text appears in the error or the log

#### Scenario: A saturated shared service does not open the breaker

- **GIVEN** the shared inference service answers 429 or 503
- **WHEN** a turn calls it
- **THEN** the turn degrades as saturated
- **AND** the breaker for that adapter name does not count the failure

### Requirement: The model catalog is defined by the repository

The system SHALL offer only models present in a seeded catalog table, grouped by company, and SHALL reject any selection of a model not in the catalog, except that for a configured local server the model the environment configures for that server (`Asistente__Modelo` when `Asistente__Proveedor` names it) SHALL also be offered, first in its group, and accepted without being written to the catalog. The system MUST NOT list models from provider APIs.

#### Scenario: The environment model of a configured local server is offered and accepted

- **GIVEN** `Asistente__Proveedor=local`, `Asistente__Modelo=qwen3.5:9b-q4_K_M` and a configured own server whose catalog lacks that model
- **WHEN** an administrator reads the screen state, and tests or applies `local` with that model
- **THEN** the `local` group lists it first, followed by its catalog models, the test and the apply succeed, and the catalog is unchanged; the same model for another server is still rejected

#### Scenario: A model outside the catalog is rejected

- **GIVEN** an administrator
- **WHEN** the administrator applies a company and model not in the catalog
- **THEN** the request is rejected and the selection is unchanged

#### Scenario: The catalog is grouped by company with key status

- **GIVEN** a stored key for company A and none for company B
- **WHEN** an administrator reads the model selection
- **THEN** models are grouped by company
- **AND** company A shows a last-4 hint and company B shows the key as missing

### Requirement: One API key per company is stored encrypted and never exposed

The system SHALL keep at most one API key per company, encrypted at rest with AES-GCM under a master key from `Asistente__ClaveMaestra`, with associated data binding the ciphertext to its company. The system SHALL support rotation through `Asistente__ClaveMaestraAnterior` and a key identifier in the stored format, re-encrypting under the current key on read. The system MUST NOT return a key in any response and MUST NOT write a key to a log, an error message or the administration audit; only a last-4 hint may be shown.

#### Scenario: A stored key round-trips

- **GIVEN** a key saved for a company
- **WHEN** a turn resolves that company's adapter
- **THEN** the decrypted key equals the one saved

#### Scenario: A tampered ciphertext is rejected

- **GIVEN** a stored ciphertext altered by one byte
- **WHEN** the system decrypts it
- **THEN** decryption fails and no key is produced

#### Scenario: A ciphertext moved to another company is rejected

- **GIVEN** the ciphertext of company A copied into company B's row
- **WHEN** the system decrypts company B's key
- **THEN** decryption fails because the associated data does not match

#### Scenario: Rotation re-encrypts lazily

- **GIVEN** a key encrypted under the previous master key
- **WHEN** the key is read with both master keys configured
- **THEN** it decrypts correctly
- **AND** it is stored again under the current master key

#### Scenario: Reverting without a stored selection is a no-op

- **GIVEN** no stored selection
- **WHEN** an administrator reverts to server configuration
- **THEN** the response is 204, nothing changes and no audit row is written

#### Scenario: No master key still allows the local model

- **GIVEN** no `Asistente__ClaveMaestra` configured
- **WHEN** an administrator tries to save a key
- **THEN** the system responds 503
- **AND** the local model remains selectable and usable

#### Scenario: A key never appears in responses, logs or audit

- **GIVEN** any administration action that handles a key
- **WHEN** its response, logs, error text and audit row are inspected
- **THEN** none contains the key, and only the hint is present

### Requirement: Connection testing persists nothing

The system SHALL provide an administrator-only test that makes one minimal real call to the given company and model, optionally with a not-yet-saved key, and SHALL return either the latency or exactly one of the codes `clave-invalida`, `inalcanzable`, `sin-credito`, `modelo-inexistente` or `saturado`. A failed test SHALL be answered with HTTP 200 and `exito: false` carrying the code, because it is a result and not a request error. The test SHALL NOT persist the key, the selection or any other state, and SHALL NOT count against or reset the circuit breaker of any adapter name. For a local server the test SHALL go through the concurrency gate without waiting.

#### Scenario: A successful test returns latency

- **GIVEN** a valid company, model and key
- **WHEN** an administrator runs the test
- **THEN** the response carries a latency and nothing is stored

#### Scenario: Each failure maps to one code

- **GIVEN** a provider that rejects the key or credential, is unreachable, reports no credit, does not know the model, or is saturated
- **WHEN** an administrator runs the test
- **THEN** the response carries `clave-invalida`, `inalcanzable`, `sin-credito`, `modelo-inexistente` or `saturado` respectively

#### Scenario: The shared service rejecting the server credential is reported without exposing it

- **GIVEN** the shared inference service answers 401 or 403 to the credential configured on the server
- **WHEN** an administrator runs the test on it
- **THEN** the response carries `clave-invalida`
- **AND** no credential text appears in the response

### Requirement: The model screen state carries the catalog, key status and prices

The system SHALL answer `GET /api/asistente/administracion/modelo` with what is in use and its origin, the configured local servers (identifiers only), the catalog by company with key status, whether encryption is available, and `precios[]` with the price in force of every priced catalog model. A model absent from `precios[]` SHALL be treated as not priced.

#### Scenario: An unpriced model is absent from the prices

- **GIVEN** a catalog model without a `tabla_de_precios` row
- **WHEN** an administrator reads the model screen state
- **THEN** `precios[]` has no entry for it

### Requirement: Applying a selection re-tests on the server and saves atomically

The system SHALL re-run the connection test on the server when a selection is applied, SHALL save a new key only when that test succeeds, and SHALL set the selection only after that. Every apply SHALL be audited before the response.

#### Scenario: A failing re-test changes nothing

- **GIVEN** an apply request whose re-test fails
- **WHEN** the request is processed
- **THEN** the selection and stored keys are unchanged
- **AND** the response is 422 and carries the failure code

#### Scenario: A successful apply records an audit row first

- **GIVEN** an apply request whose re-test succeeds
- **WHEN** the request is processed
- **THEN** an audit row naming the administrator, company and model exists before the response is returned

### Requirement: Replacing a key validates the new key first

The system SHALL replace a company's key through a dedicated operation, independent of model selection, that validates the new key against the provider and replaces the stored key only on success. On failure the response SHALL be 422 with the failure code and the previous key SHALL remain stored and in use.

#### Scenario: A failed replacement keeps the old key

- **GIVEN** a stored key for a company
- **WHEN** an administrator submits a replacement key that the provider rejects
- **THEN** the stored key and its hint are unchanged and turns keep using it

#### Scenario: A successful replacement updates the hint

- **GIVEN** a stored key for a company
- **WHEN** an administrator submits a replacement the provider accepts
- **THEN** the stored key is replaced and the hint shows the new last four characters

### Requirement: Local servers are a closed set derived from server configuration

The system SHALL offer as local servers only those the deployment configures: the shared inference service when `Inferencia__Url` is set, and the own server when `Asistente__UrlDelProveedorLocal` is set. The administrator SHALL choose a server by identifier (`ollama` or `local`) and a catalog model, and no endpoint SHALL accept a URL, host or local credential. The system SHALL NOT make any outbound call for a server that is not configured. Credentials a local server needs SHALL come from server configuration and MUST NOT be entered through the screen.

#### Scenario: An unconfigured local server is rejected without a call

- **GIVEN** a deployment without `Inferencia__Url`
- **WHEN** an administrator tests or applies the `ollama` server
- **THEN** the request is rejected
- **AND** no outbound request is made

#### Scenario: A configured local server uses server-side credentials

- **GIVEN** the shared inference service configured with BasicAuth credentials and, outside production, a Cloudflare Access service token
- **WHEN** a turn runs on it
- **THEN** the server-side credentials are sent on that request only
- **AND** no credential field exists in the screen

#### Scenario: A free-form address is not accepted

- **GIVEN** an administrator
- **WHEN** the administrator submits a request carrying a URL or host instead of a server identifier
- **THEN** the request is rejected and nothing is stored

### Requirement: Administration routes require the administration permission and are audited

Every route introduced by this capability SHALL require `asistente.administrar` and SHALL write its audit row before responding.

#### Scenario: A caller without the permission is rejected

- **GIVEN** an authenticated caller without `asistente.administrar`
- **WHEN** the caller invokes any model, credential or price route
- **THEN** the system responds 403 and changes nothing

### Requirement: Prices are written as new versions

The system SHALL let an administrator write a price for a company and model (USD per million tokens, effective from a calendar date taken as 00:00 UTC) as a new version, pricing cached tokens like input tokens when no cache price is given and rejecting negative, missing, out-of-range or over-precise amounts, a model outside the catalog and a date earlier than the current version's start with a 400 and a code: the current `tabla_de_precios` row is closed and a new row opened in one transaction, and earlier usage SHALL keep being priced with the version in force when it was recorded.

#### Scenario: A new version closes the previous one

- **GIVEN** a model with a current price
- **WHEN** an administrator writes a new price effective from a date
- **THEN** the previous row is closed at that date and exactly one current row exists

#### Scenario: Earlier usage keeps its original price

- **GIVEN** usage recorded before a price change
- **WHEN** estimated cost is computed after the change
- **THEN** that usage is priced with the earlier version

### Requirement: The Modelo tab is gated by permission and guards the apply step

The Sistema screen SHALL show a "Modelo" tab (hash `#modelo`) only to holders of `asistente.administrar`. The "Aplicar cambio" action SHALL stay disabled until a connection test for the current selection has passed, and SHALL state that in-flight turns finish on the current model. The cloud model list SHALL be keyboard operable.

#### Scenario: The tab is hidden without the permission

- **GIVEN** a user without `asistente.administrar`
- **WHEN** the user opens the Sistema screen
- **THEN** no "Modelo" tab is shown

#### Scenario: The tab follows the Asistente tab

- **GIVEN** a user with `asistente.administrar`
- **WHEN** the user opens the Sistema screen
- **THEN** the "Modelo" tab appears after "Asistente" and before any later tab

#### Scenario: Apply is disabled until the test passes

- **GIVEN** an administrator who changed the selection
- **WHEN** no successful test exists for that selection
- **THEN** "Aplicar cambio" is disabled
- **AND** it becomes enabled after a successful "Probar conexión"

#### Scenario: The listbox works with the keyboard

- **GIVEN** the cloud model list is focused
- **WHEN** the user presses arrow keys, Enter and Esc
- **THEN** the highlight moves, Enter selects, and Esc closes the list
