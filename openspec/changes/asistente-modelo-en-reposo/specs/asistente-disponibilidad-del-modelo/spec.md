## Purpose

Tell assistant users when the model served by the shared inference service is unloaded («en reposo»), let any of them start loading it before asking, and report the outcome inline in the chat without spending the shared service's request budget on status checks.

## ADDED Requirements

### Requirement: The system reports whether the assistant's model is loaded

The system SHALL expose the availability of the model that a turn of the requesting actor would run on, resolved the same way a turn resolves it (server configuration or the stored runtime selection). The state SHALL be one of `no_aplica`, `listo`, `en_reposo`, `activando` or `desconocido`. When the resolved provider is not the shared inference service (`ollama`), the state MUST be `no_aplica`, and the system MUST NOT contact any inference service to compute it. With `ollama`, the state SHALL be `listo` only when the service lists that model as loaded with the context size the turns use, `en_reposo` when it does not, `activando` while a load requested through this capability is in progress, and `desconocido` when the service cannot be asked (unreachable, timeout, rejected credentials, or the route not admitted). The endpoint MUST require `asistente.consultar` and MUST NOT reveal URLs, credentials, other loaded models or error bodies.

#### Scenario: Ollama with the model unloaded

- **GIVEN** the resolved provider is `ollama` and the service does not list the configured model as loaded
- **WHEN** a user with `asistente.consultar` asks for the availability
- **THEN** the answer is `en_reposo`

#### Scenario: Ollama with the model loaded with the turns' context size

- **GIVEN** the resolved provider is `ollama` and the service lists the configured model as loaded with the context size of `Asistente:ContextoDelModelo`
- **WHEN** a user with `asistente.consultar` asks for the availability
- **THEN** the answer is `listo`

#### Scenario: Loaded with a different context size counts as at rest

- **GIVEN** the service lists the configured model as loaded with a context size different from the turns' one
- **WHEN** the availability is asked
- **THEN** the answer is `en_reposo`, because the first turn would reload the model anyway

#### Scenario: A runtime selection on Ollama is honored

- **GIVEN** the server configuration names a cloud provider and the stored runtime selection names an `ollama` model
- **WHEN** the availability is asked
- **THEN** the state is computed for the selected `ollama` model and not reported as `no_aplica`

#### Scenario: Local and cloud providers never apply

- **GIVEN** the resolved provider is `local`, `anthropic`, any other cloud company, or `simulado`
- **WHEN** the availability is asked
- **THEN** the answer is `no_aplica` and no request reaches any inference service

#### Scenario: The service cannot be asked

- **GIVEN** the resolved provider is `ollama` and the service answers 404, 401, 429, 5xx or does not answer in time
- **WHEN** the availability is asked
- **THEN** the answer is `desconocido`, and the response contains no URL, credential, status code or error body

#### Scenario: A user without the permission

- **GIVEN** an authenticated user without `asistente.consultar`
- **WHEN** they ask for the availability or request an activation
- **THEN** the system answers 403 and contacts no inference service

### Requirement: Availability checks stay within the shared service's request budget

The system SHALL answer availability requests from a per-environment server-side cache whose lifetime is configurable and SHALL NOT ask the service more than once per cache lifetime, however many users or requests arrive. Concurrent requests that find the cache expired MUST share a single request to the service. A successful model call of a turn or of an activation SHALL refresh the cache to `listo` without an extra request to the service. While an activation is in progress, the state MUST come from that activation and MUST NOT trigger requests to the service.

#### Scenario: Many users open the assistant at once

- **GIVEN** an expired cache and 20 concurrent availability requests
- **WHEN** they are served
- **THEN** the service receives exactly one listing request and all 20 get the same state

#### Scenario: Requests within the cache lifetime

- **GIVEN** an availability answered from the service 5 seconds ago and a cache lifetime of 30 seconds
- **WHEN** another availability request arrives
- **THEN** it is answered from the cache with no request to the service

#### Scenario: A turn proves the model is loaded

- **GIVEN** a cached `en_reposo`
- **WHEN** a turn's call to the `ollama` model succeeds
- **THEN** the next availability request answers `listo` without asking the service

### Requirement: Any assistant user can ask to load the model once

The system SHALL let any user with `asistente.consultar` request that the shared service load the resolved `ollama` model. The load MUST use the same model name and the same context size the turns send, MUST NOT send a prompt, and MUST NOT override the service's own keep-alive policy. The request SHALL be idempotent and single-flight per environment: while a load is in progress, further requests from any user MUST join it and MUST NOT start another. After a load ends, a new load MUST NOT start within a configurable cooldown; requests in that window answer the current state. An activation MUST NOT consume the user's daily quota, MUST NOT count as a turn, and MUST NOT open or close the turn breaker. With a provider other than `ollama`, the request MUST NOT contact any service and SHALL answer `no_aplica`. The request SHALL answer immediately with the current state; it MUST NOT hold the connection until the load ends.

#### Scenario: Ten concurrent clicks load the model once

- **GIVEN** the model is `en_reposo`
- **WHEN** ten users with `asistente.consultar` request an activation at the same time
- **THEN** the service receives exactly one load request, and every response says `activando`

#### Scenario: The load uses the turns' context size

- **GIVEN** `Asistente:ContextoDelModelo` is 16384
- **WHEN** an activation reaches the service
- **THEN** the load request names the configured model, carries `num_ctx` 16384, and carries neither a prompt nor a keep-alive value

#### Scenario: Activating an already loaded model

- **GIVEN** the availability is `listo`
- **WHEN** a user requests an activation
- **THEN** no load request reaches the service and the answer is `listo`

#### Scenario: Activation does not spend quota

- **GIVEN** a user with one question left in their daily quota
- **WHEN** they request an activation and the load completes
- **THEN** their remaining quota is still one question

#### Scenario: Activation with a non-Ollama provider

- **GIVEN** the resolved provider is `local`
- **WHEN** a user with `asistente.consultar` requests an activation
- **THEN** the answer is `no_aplica` and no request reaches any inference service

#### Scenario: A question sent while activating

- **GIVEN** an activation in progress
- **WHEN** the user sends a question
- **THEN** the turn is accepted and waits for the model as any turn does today, with no extra error

### Requirement: A failed activation is reported without details

When the load fails or does not finish within its own time limit, the system SHALL return to `en_reposo` and SHALL flag the last activation as failed until the next successful check or activation. The failure SHALL be logged with the model name, the outcome category and the duration, and MUST NOT log the actor's identity, credentials, URLs or response bodies.

#### Scenario: The service rejects the load

- **GIVEN** an activation in progress
- **WHEN** the service answers the load with an error or the load exceeds its time limit
- **THEN** the availability is `en_reposo` with the last activation flagged as failed, and the log entry names the model and the outcome but not the user

### Requirement: The chat shows the at-rest notice inline

The assistant conversation SHALL show, inline above the question field and only while the availability is `en_reposo` and the assistant accepts questions (not in maintenance), the notice «El modelo está en reposo. La primera respuesta puede demorar unos segundos.» with a button «Activar modelo». The client SHALL ask for the availability when the assistant opens and MUST NOT ask on every keystroke. While `activando`, the notice SHALL read «Activando el modelo…» with the button disabled, and the client SHALL poll the availability with increasing intervals until it leaves `activando` or a client time limit elapses. When it becomes `listo` after an activation, the notice SHALL read «El modelo está listo.» and then disappear on its own. When the activation failed, the notice SHALL read «No se pudo activar el modelo. Probá de nuevo.» with the button enabled again. The notice MUST be announced through a polite live region, MUST NOT use a toast, a browser notification or a push notification, and MUST NOT appear for `no_aplica`, `listo` (outside the post-activation confirmation) or `desconocido`.

#### Scenario: Notice shown only with Ollama at rest

- **GIVEN** a user with `asistente.consultar` and the availability `en_reposo`
- **WHEN** they open the assistant
- **THEN** the conversation shows «El modelo está en reposo. La primera respuesta puede demorar unos segundos.» and the button «Activar modelo»

#### Scenario: The notice never appears for other states

- **GIVEN** the availability is `no_aplica`, `listo` or `desconocido`
- **WHEN** the user opens the assistant
- **THEN** no at-rest notice and no «Activar modelo» button are shown

#### Scenario: Activating and becoming ready

- **GIVEN** the at-rest notice is shown
- **WHEN** the user presses «Activar modelo» and the availability later becomes `listo`
- **THEN** the notice reads «Activando el modelo…» with the button disabled, then «El modelo está listo.», and then disappears without any system notification

#### Scenario: Activation failure in the chat

- **GIVEN** the user pressed «Activar modelo»
- **WHEN** the availability returns to `en_reposo` with the last activation flagged as failed, or the client time limit elapses
- **THEN** the notice reads «No se pudo activar el modelo. Probá de nuevo.» and «Activar modelo» is enabled again

#### Scenario: A question answered removes the notice

- **GIVEN** the at-rest notice is shown
- **WHEN** the user sends a question and it is answered
- **THEN** the notice disappears, because the answer proves the model is loaded

#### Scenario: Maintenance hides the notice

- **GIVEN** the availability is `en_reposo` and the assistant is in maintenance mode
- **WHEN** the user opens the assistant
- **THEN** only the maintenance state is shown, without the at-rest notice
