## MODIFIED Requirements

### Requirement: Dashboard de salud operativa

The system SHALL offer users holding `sistema.estado.ver` a view of the current state of the backend modules, the assistant and PostgreSQL, presented as the «Estado» tab of the «Sistema» section. The view MUST check, independently of each other, the ping of each business module (`aulas`, `tareas`, `designaciones`, `portal`), the ping of the assistant (`asistente`) and the PostgreSQL connectivity check, and MUST distinguish each module or assistant ping from the database check. A module or assistant ping MUST time out after 5 seconds; its response time MUST be measured by the client. The PostgreSQL check MUST be measured by the server and report its duration.

The system status API (`GET /api/administracion/sistema/estado`, `sistema.estado.ver`) MUST additionally report whether the assistant's maintenance mode is active, as one of active, inactive or unknown, without requiring `asistente.consultar` or `asistente.administrar`, and MUST NOT include the maintenance reason, the actor who toggled it or any other detail. A failure to read the maintenance state MUST yield "unknown" and MUST NOT turn the PostgreSQL result into a failure.

Every result MUST carry the time of its check. A failed check MUST be shown as unavailable without hiding the state of the other components and without exposing internal details, exceptions, hosts, credentials or connection strings. A pending check MUST be represented differently from a healthy or a failed result. If the assistant ping fails, the assistant MUST be shown as unavailable regardless of its maintenance state.

#### Scenario: Todos los componentes responden

- **GIVEN** a user with `sistema.estado.ver`, successful pings of the four modules and of the assistant, a successful PostgreSQL check and maintenance inactive
- **WHEN** they open or refresh the Estado tab
- **THEN** they see every module, the assistant and PostgreSQL as available, each with its response time and check time

#### Scenario: Falla un ping de módulo

- **GIVEN** a user with `sistema.estado.ver` and one module ping that fails or exceeds 5 seconds
- **WHEN** the Estado tab is refreshed
- **THEN** that module appears as unavailable and the results of the other components remain visible

#### Scenario: PostgreSQL no está disponible

- **GIVEN** a user with `sistema.estado.ver` and a failed connectivity check
- **WHEN** the Estado tab is refreshed
- **THEN** PostgreSQL appears as unavailable with a safe message, without connection information or internal exception

#### Scenario: Maintenance state reaches a user without assistant permissions

- **GIVEN** a user holding only `sistema.estado.ver` and the assistant's maintenance mode active
- **WHEN** the system status API is queried
- **THEN** it reports maintenance as active and does not include the reason or the toggling actor
- **AND** the request succeeds without `asistente.consultar` or `asistente.administrar`

#### Scenario: Maintenance state cannot be read

- **GIVEN** a user with `sistema.estado.ver`, PostgreSQL reachable, and the assistant's maintenance query failing
- **WHEN** the system status API is queried
- **THEN** PostgreSQL is reported as available and maintenance is reported as unknown

#### Scenario: The assistant ping fails during maintenance

- **GIVEN** maintenance mode active and the assistant ping failing
- **WHEN** the Estado tab is refreshed
- **THEN** the assistant appears as unavailable, not as in maintenance

#### Scenario: Consulta del dashboard sin permiso

- **GIVEN** an authenticated user without `sistema.estado.ver`
- **WHEN** they open the «Sistema» section or query the status API directly
- **THEN** the frontend shows no Estado tab and the backend denies the query

### Requirement: Consulta paginada y de solo lectura de auditoría

The system SHALL allow users holding `auditoria.ver` to consult audit events through a read-only API and the «Auditoría» tab of the «Sistema» section. The feed MUST cover the events of `audit.change_log` and of the assistant's administration log as one list (see "Unified audit feed across sources"). Each event MUST present at a glance its time, actor, action, module and a summary of the change; the object, the table (`schema.table`), the row key and the request id MUST be available as event context when the source records them.

The actor MUST be shown by a readable name when it resolves to an account: the linked person's name when there is one, else the account's display name. An event with no actor and no request context MUST be shown as «Proceso automático». An event with no actor but with a request context, or with an actor that does not resolve to an account, MUST be shown as «Actor no identificado»; the system MUST NOT attribute either case to a person. UPN, email or any other personal attribute of the actor MUST NOT be returned or shown.

The action MUST be labelled «Alta» (insert), «Cambio» (update) or «Eliminación» (delete); an update MUST NOT be presented as a deletion. The module MUST be shown with a readable label and MUST keep a fallback for unknown schemas.

The query MUST order events from newest to oldest with a stable total order and paginate on the server with a default page size of 50 and a maximum of 100. It MUST support filters by date range, action, module, table, row key and free-text search, applied before counting and paginating. It MUST reject inverted ranges, page sizes outside the allowed limits and over-long filter values with a stable validation error, without running an unbounded query. Before → after values MUST be exposed only for approved safe fields: personal or secret fields MUST be masked, unclassified fields MUST NOT expose their value, and the raw `old_row`/`new_row` snapshots, the assistant log's raw before/after documents and `client_ip` MUST NOT be returned or shown. The query MUST NOT modify or delete events.

#### Scenario: Consultar eventos recientes

- **GIVEN** a user with `auditoria.ver` and recorded events
- **WHEN** they open the Auditoría tab
- **THEN** they receive a bounded page of the last 7 days ordered newest first, with time, actor name or fallback, action label, module and summary visible
- **AND** the object, table, row key and request id are available in the event detail

#### Scenario: Identificar al actor

- **GIVEN** an event whose actor refers to an account in `identity.users`
- **WHEN** the event is presented
- **THEN** the linked person's name is shown if it exists, or the account's display name as fallback
- **AND** no UPN, email or other personal data of the account is exposed

#### Scenario: Automatic process

- **GIVEN** a `change_log` event with no `changed_by` and no `request_id`
- **WHEN** the event is presented
- **THEN** the actor is shown as «Proceso automático» with a neutral avatar

#### Scenario: Actor no identificado

- **GIVEN** a `change_log` event with no `changed_by` but with a `request_id`
- **WHEN** the event is presented
- **THEN** the actor is shown as «Actor no identificado» and is not attributed to a person or to an automatic process

#### Scenario: Etiquetar la acción y el módulo

- **GIVEN** events of known and unknown schemas with actions insert, update and delete
- **WHEN** each event is presented
- **THEN** the action is shown as «Alta», «Cambio» or «Eliminación» and the module with a label derived from its source
- **AND** unknown schemas keep a fallback label
- **AND** an update is never presented as a deletion

#### Scenario: Inspeccionar un cambio con PII

- **GIVEN** an event whose data contains personal, secret or unclassified fields
- **WHEN** the authorized user inspects its summary or detail
- **THEN** they see the object and the readable names of the changed fields, with values only for approved fields
- **AND** sensitive fields appear masked, unclassified fields expose no value, and no raw snapshot or `client_ip` is presented

#### Scenario: Filtrar eventos

- **GIVEN** a user with `auditoria.ver` and valid filters of date range, action, module, table, row key or search text
- **WHEN** they query the audit feed
- **THEN** the API returns only events matching every filter, paginated on the server, with a total that uses the same filters

#### Scenario: Rango o paginación inválidos

- **GIVEN** an inverted date range or a page size above the maximum
- **WHEN** the user sends the query
- **THEN** the API rejects it with a stable validation error and runs no unbounded query

#### Scenario: Consultar auditoría sin permiso

- **GIVEN** an authenticated user without `auditoria.ver`
- **WHEN** they open the «Sistema» section or query the audit API directly
- **THEN** the frontend shows no Auditoría tab and the backend denies the query without revealing events

#### Scenario: No hay eventos para los filtros

- **GIVEN** a user with `auditoria.ver` and valid filters without matches
- **WHEN** the query finishes
- **THEN** the tab shows «No hay registros para estos filtros.», distinct from the loading and error states

## ADDED Requirements

### Requirement: Unified Sistema section with permission-gated tabs

The frontend SHALL expose system administration as one section at route `/sistema`, with the heading «Sistema», the subtitle «Estado de los servicios, uso del asistente y registros de auditoría.», a secondary «Actualizar» button and a tab list in the fixed order «Estado», «Asistente», «Auditoría». Each tab MUST be rendered only when the session holds its permission — `sistema.estado.ver`, `asistente.administrar` and `auditoria.ver` respectively — independently of the other two. «Actualizar» MUST refetch only the active tab and MUST read «Actualizando…» and be disabled while that refetch runs.

The active tab MUST be reflected in the URL hash (`#estado`, `#asistente`, `#auditoria`). On entry, the initial tab MUST be the hash's tab when it is valid and permitted, and otherwise the first permitted tab in the fixed order; the URL MUST then be corrected to the tab actually shown. A user holding none of the three permissions MUST get the existing no-permission route behavior. The old routes MUST redirect: `/auditoria` to `/sistema#auditoria` and `/asistente/administracion` to `/sistema#asistente`, preserving no other state. The navigation MUST show a single entry «Sistema» in the «Sistema» group when the session holds any of the three permissions, and MUST NOT show the former «Dashboard del sistema», «Registros de auditoría» or «Uso del asistente» entries.

Each tab MUST carry a status dot with an accessible text equivalent: «Estado» green when every checked component is available and red when any is unavailable; «Asistente» green when maintenance is inactive and amber when it is active; «Auditoría» neutral. A dot whose state is pending or unknown MUST be neutral. Hiding a tab MUST NOT replace backend authorization: every endpoint keeps its own permission.

#### Scenario: All three permissions

- **GIVEN** a user holding `sistema.estado.ver`, `asistente.administrar` and `auditoria.ver`
- **WHEN** they open `/sistema` with no hash
- **THEN** they see the tabs «Estado», «Asistente» and «Auditoría» in that order with «Estado» active and the URL hash set to `#estado`

#### Scenario: Only one permission

- **GIVEN** a user holding only `auditoria.ver`
- **WHEN** they open `/sistema`
- **THEN** only the «Auditoría» tab is rendered and active, and no Estado or Asistente content or request is made

#### Scenario: Two of three permissions

- **GIVEN** a user holding `asistente.administrar` and `auditoria.ver` but not `sistema.estado.ver`
- **WHEN** they open `/sistema`
- **THEN** the tabs «Asistente» and «Auditoría» are rendered, «Asistente» is active, and no health probe is sent

#### Scenario: Deep link to a permitted tab

- **GIVEN** a user holding `auditoria.ver` and `sistema.estado.ver`
- **WHEN** they open `/sistema#auditoria`
- **THEN** the «Auditoría» tab is active

#### Scenario: Unpermitted or invalid hash

- **GIVEN** a user holding only `sistema.estado.ver`
- **WHEN** they open `/sistema#asistente` or `/sistema#desconocido`
- **THEN** the «Estado» tab is active, the hash is corrected to `#estado`, and no assistant content is rendered

#### Scenario: No permission at all

- **GIVEN** an authenticated user holding none of the three permissions
- **WHEN** they navigate directly to `/sistema`
- **THEN** the frontend applies the existing no-permission redirect without rendering the section
- **AND** no «Sistema» navigation entry is shown to them

#### Scenario: Old routes redirect

- **GIVEN** a user holding `auditoria.ver` and `asistente.administrar`
- **WHEN** they open `/auditoria` or `/asistente/administracion`
- **THEN** they land on `/sistema#auditoria` or `/sistema#asistente` respectively

#### Scenario: Single navigation entry

- **GIVEN** a user holding any one of the three permissions
- **WHEN** the sidebar is rendered
- **THEN** it shows one «Sistema» entry in the «Sistema» group and none of the three former entries

#### Scenario: Refresh the active tab

- **GIVEN** the «Auditoría» tab active
- **WHEN** the user presses «Actualizar»
- **THEN** only the audit query is refetched, the button reads «Actualizando…» while it runs, and the Estado probes are not re-sent

#### Scenario: Tab status dots

- **GIVEN** a user holding all three permissions, one module unavailable and maintenance active
- **WHEN** the section has finished its checks
- **THEN** the «Estado» dot is red, the «Asistente» dot is amber and the «Auditoría» dot is neutral, each with an accessible text equivalent

### Requirement: Estado summary banner and component cards

The Estado tab SHALL show a summary banner and one card per checked component, grouped under «Módulos» (Aulas, Tareas, Designaciones, Portal, Asistente) and «Infraestructura» (PostgreSQL). The banner MUST take exactly one of three forms once every check has finished: «Todos los componentes disponibles» with «N de N responden con normalidad.»; a count of unavailable components («1 componente no disponible» / «N componentes no disponibles») naming them, followed by «El resto funciona con normalidad.» when any remain available; or, when every component is available and the assistant is in maintenance, «Todo disponible, asistente en mantenimiento» with «Los módulos responden. El asistente no acepta consultas hasta que se desactive el mantenimiento.». Unavailability MUST take precedence over maintenance. The banner MUST show «Última comprobación» with the date and time of the latest check.

Each card MUST show the component name; a pill «Disponible», «No disponible» or «Mantenimiento»; the response time as «N ms» with «tiempo de respuesta», or «sin respuesta» when it failed; and a note: «Responde normalmente» for an available module, «Recibe consultas» for the available assistant, «Consultas pausadas por un administrador» for the assistant in maintenance, «No respondió en 5 s» for a ping that timed out, and «Comprobado por separado» for PostgreSQL. A failed card MUST offer «Reintentar», which re-checks only that component. The assistant card MUST offer «Ver uso →», switching to the Asistente tab, only when the session holds `asistente.administrar`.

#### Scenario: Everything available

- **GIVEN** six successful checks and maintenance inactive
- **WHEN** the Estado tab finishes checking
- **THEN** the banner reads «Todos los componentes disponibles» and «6 de 6 responden con normalidad.»

#### Scenario: Some components unavailable

- **GIVEN** the Aulas and Tareas pings timing out and every other check successful
- **WHEN** the Estado tab finishes checking
- **THEN** the banner reads «2 componentes no disponibles», names Aulas and Tareas, and says «El resto funciona con normalidad.»
- **AND** both cards show «No disponible», «sin respuesta», «No respondió en 5 s» and «Reintentar»

#### Scenario: Assistant in maintenance

- **GIVEN** every check successful and maintenance active
- **WHEN** the Estado tab finishes checking
- **THEN** the banner reads «Todo disponible, asistente en mantenimiento» with its explanation
- **AND** the assistant card shows «Mantenimiento» and «Consultas pausadas por un administrador»

#### Scenario: Retry one component

- **GIVEN** the Tareas card failed
- **WHEN** the user presses its «Reintentar»
- **THEN** only the Tareas ping is re-sent and the other cards keep their results

#### Scenario: See assistant usage from its card

- **GIVEN** a user holding `sistema.estado.ver` and `asistente.administrar`
- **WHEN** they press «Ver uso →» on the assistant card
- **THEN** the Asistente tab becomes active and the hash becomes `#asistente`

#### Scenario: No usage link without the permission

- **GIVEN** a user holding `sistema.estado.ver` but not `asistente.administrar`
- **WHEN** the assistant card is rendered
- **THEN** it shows no «Ver uso →» link

### Requirement: Recent changes on the Estado tab

When the session holds both `sistema.estado.ver` and `auditoria.ver`, the Estado tab SHALL show a «Cambios recientes» box below the cards with the four most recent events of the unified audit feed, without period filter. Each row MUST show when it happened («Hoy HH:mm», «Ayer HH:mm» or «d/m HH:mm» in the institution's time zone), the summary, the actor, the action label and a chevron. Activating a row MUST switch to the Auditoría tab with the period «Todo» and that event's detail panel open; the header link «Ver todo en Auditoría →» MUST switch to the Auditoría tab with its default filters. Without `auditoria.ver` the box MUST NOT be rendered and the audit API MUST NOT be called. With no events the box MUST say so instead of showing an empty list.

#### Scenario: Show the last four changes

- **GIVEN** a user holding `sistema.estado.ver` and `auditoria.ver` and more than four audit events
- **WHEN** the Estado tab loads
- **THEN** «Cambios recientes» lists the four newest events, newest first, with time, summary, actor and action

#### Scenario: Open a recent change

- **GIVEN** the «Cambios recientes» box listing an event
- **WHEN** the user activates that row
- **THEN** the Auditoría tab becomes active with the period «Todo», the event's row highlighted and its detail panel open

#### Scenario: Recent changes hidden without audit permission

- **GIVEN** a user holding `sistema.estado.ver` but not `auditoria.ver`
- **WHEN** the Estado tab loads
- **THEN** no «Cambios recientes» box is rendered and no audit request is sent

### Requirement: Embedded assistant usage tab

The Asistente tab SHALL render the assistant's usage and administration panel — the same content, data and actions as the former «Uso del asistente» page — embedded in the section, without its own page header. It MUST be reachable only with `asistente.administrar`; the assistant's endpoints keep their own authorization. «Actualizar» on this tab MUST refetch the panel's data without discarding the period the user selected.

The panel SHALL follow the visual and interaction design of the «Uso del asistente» reference (Claude Design canvas): a period selector rendered as a pressed-button group (not a full-width `<select>`); the organization-wide totals for the selected period (sessions, model calls, estimated cost, p95 latency) summarized as KPI cards, separate from the per-user/per-role detail; the organization's current monthly cap and its estimated spend-to-date for the calendar month, shown as a fifth card with a spend-vs-cap bar; the per-user and per-role detail rendered as two tabs (with a count badge each) over one sortable table with a name/role search field, each row showing its persisted daily quota (distinguishing a role's default from a user's own override) and edited inline in that row (no free-text role code or user UUID field). Elements of the reference design that depend on data or operations the backend does not expose (a live cloud/local provider switch with local-server telemetry, and a per-day usage trend chart) remain out of scope for this requirement until that data is available.

#### Scenario: Embedded panel

- **GIVEN** a user holding `asistente.administrar`
- **WHEN** they open `/sistema#asistente`
- **THEN** they see the usage panel, the maintenance banner and the organizational KPIs, and no second page heading «Uso del asistente»

#### Scenario: Refresh keeps the selected period

- **GIVEN** the Asistente tab with a non-default usage period selected
- **WHEN** the user presses «Actualizar»
- **THEN** the usage data is refetched for the same period

#### Scenario: Period is a button group, not a full-width select

- **GIVEN** a user holding `asistente.administrar` on the Asistente tab
- **WHEN** they look at the period control
- **THEN** it renders as a group of pressed buttons («Hoy», «Última semana», «Último mes»), not a `<select>` element

#### Scenario: Daily quota is edited in the row, never as a typed code or UUID

- **GIVEN** the per-user or per-role detail table
- **WHEN** the admin edits a row's daily quota
- **THEN** the edit happens inline in that row, identified by the row itself, with no field asking for a role code or a user id to be typed

#### Scenario: The organization cap card shows the persisted cap and a spend-vs-cap bar

- **GIVEN** a user holding `asistente.administrar` on the Asistente tab, with an organization monthly cap already set above zero
- **WHEN** they look at the fifth KPI card
- **THEN** it shows the persisted cap value, the calendar month's estimated spend-to-date, and a bar showing the percentage of the cap already spent

#### Scenario: Editing the cap or a row's quota starts from the persisted value

- **GIVEN** a user holding `asistente.administrar` who did not edit the cap or any quota earlier in this session
- **WHEN** they open the cap's or a row's inline editor
- **THEN** the editor's starting value is the value already persisted in the backend, not a placeholder that only reflects a save made earlier in the same session

### Requirement: Unified audit feed across sources

The audit API SHALL return, as one feed, the events of `audit.change_log` and of the assistant's administration log (budget edits, organization cap edits and maintenance toggles). Assistant events MUST carry the module «Asistente»; the action «Alta» when a value is created where none existed and «Cambio» otherwise; an object label naming what was configured; field-level before → after changes with readable field names subject to the same masking rule as every other event; and their actor resolved like any other actor. Every event id MUST be unique across sources and stable across requests, so a single event can be addressed by the detail panel and by «Cambios recientes». The merged order MUST be newest first with a deterministic tie-break across sources, and every filter MUST apply to both sources before counting and paginating; the total MUST be the sum of both sources' matching counts.

The backend MUST read the assistant's log only through the assistant module's public contract, never by querying its schema directly. If that read fails, the API MUST still return the `audit.change_log` events for the query, MUST mark the response as partial naming the missing source, and MUST compute the total from the sources that answered; the tab MUST tell the user the assistant's records could not be loaded. A module filter that selects only the assistant MUST NOT query `audit.change_log`, and one that excludes it MUST NOT query the assistant log.

#### Scenario: One feed across sources

- **GIVEN** a `change_log` event at 10:00 and an assistant budget edit at 10:05
- **WHEN** a user with `auditoria.ver` queries the feed without filters
- **THEN** the budget edit appears first with module «Asistente», action «Cambio» and its before → after quota, followed by the `change_log` event, and the total counts both

#### Scenario: Pagination is exact across sources

- **GIVEN** 120 events interleaved in time between both sources
- **WHEN** the user requests pages 1, 2 and 3 with a page size of 50
- **THEN** the three pages together contain every event exactly once, in the same order as a single sorted list

#### Scenario: Assistant source fails

- **GIVEN** the assistant's log cannot be read
- **WHEN** a user with `auditoria.ver` queries the feed
- **THEN** the response contains the matching `change_log` events, is marked partial naming the assistant source, and its total counts only `change_log`
- **AND** the tab shows a notice that the assistant's records could not be loaded

#### Scenario: Module filter selects one source

- **GIVEN** the module filter «Asistente»
- **WHEN** the feed is queried
- **THEN** only assistant events are returned and `audit.change_log` is not queried

#### Scenario: Same masking for assistant events

- **GIVEN** a maintenance activation whose reason is free text
- **WHEN** its event is presented
- **THEN** the maintenance state change is shown with values and the reason field is masked

### Requirement: Audit search matches labels, never values

The audit API SHALL accept one free-text search parameter that matches, case-insensitively and accent-insensitively, the actor's displayed name, the module label, the object label, the table name, the row key and the readable labels of the changed fields. Since the object label can name the affected person (an identity event's humanized subject), the search MUST also find an `identity.users`, `identity.personas` or `identity.user_roles` event by that person's **current** displayed name — resolved the same way and with the same name expression as the label — even though that name is never a column in those tables' own snapshot; an event whose object label falls back to its generic form because the event itself changed that person's name fields MUST NOT be a hit for that name, consistent with the label. The search MUST NOT match any before or after value of any field, masked or not, nor the raw snapshots, so that search can never reveal a masked value by probing. The search text MUST be bounded in length and MUST be applied to both sources before counting and paginating.

#### Scenario: Search by actor name without accents

- **GIVEN** events by «Lucía Fernández»
- **WHEN** the user searches «lucia fernandez»
- **THEN** those events are returned

#### Scenario: Search by field label

- **GIVEN** an update that changed the field labelled «Estado»
- **WHEN** the user searches «estado»
- **THEN** that event is returned

#### Scenario: Masked value is never a search hit

- **GIVEN** an event whose masked field `documento` changed to «30111222» and no label, table or row key containing that text
- **WHEN** the user searches «30111222»
- **THEN** that event is not returned

#### Scenario: Safe value is not a search hit either

- **GIVEN** an event whose safe field «Estado» changed to «aprobado» and no label, table or row key containing «aprobado»
- **WHEN** the user searches «aprobado»
- **THEN** that event is not returned

#### Scenario: Search by the affected person's name

- **GIVEN** a person currently named «Gustavo Ruiz» with an `identity.users` account, an `identity.personas` row and an `identity.user_roles` assignment naming them in their object label, plus one earlier event that itself renamed them to «Gustavo Ruiz»
- **WHEN** the user searches «gustavo ruiz» (accents and case insensitive)
- **THEN** the account, persona and role-assignment events are returned, and the renaming event is not

### Requirement: Audit summaries show values only when every shown value is safe

The summary of an audit event SHALL include concrete before → after values only when every value it shows belongs to an approved safe field, for example «Cupo diario de Lucía Fernández: 30 → 60»; otherwise it MUST be a generic summary naming the object and the changed fields without values. A masked or unclassified value MUST NOT appear in the summary, the object label, the search matching or the detail panel.

Boolean values of safe fields MUST be presented as «Sí» and «No», never as `true`/`false`, in the summary and in the detail panel. Events on identity records whose subject is a person — user accounts, persons and role assignments — MUST name that person in the object label, for example «Cuenta de usuario de Paula Gómez», «Persona Paula Gómez» or «Roles de Paula Gómez», with role assignments naming the role, for example «Rol Docente asignado a Paula Gómez». The person's name MUST be resolved from current identity records exactly like an actor name, never read from the event's snapshot; when the event itself changed that person's name fields, or the person cannot be resolved, the object label MUST fall back to the generic label without a name.

#### Scenario: Safe single-field change

- **GIVEN** an assistant per-user quota changed from 30 to 60 for «Lucía Fernández»
- **WHEN** the event is presented
- **THEN** its summary reads «Cupo diario de Lucía Fernández: 30 → 60»

#### Scenario: Change touching a masked field

- **GIVEN** an update that changed the safe field `estado` and the personal field `documento`
- **WHEN** the event is presented
- **THEN** the summary names the object and both field labels without any value, and neither the summary nor the object label contains the document number

#### Scenario: Boolean values read as Sí and No

- **GIVEN** a user account of «Paula Gómez» reactivated, changing only the safe field `activo` from false to true
- **WHEN** the event is presented
- **THEN** its object label reads «Cuenta de usuario de Paula Gómez», its summary reads «Cuenta de usuario de Paula Gómez: Activo No → Sí», and the detail panel shows «No» → «Sí»

#### Scenario: Role assignment names the person and the role

- **GIVEN** the role «Docente» assigned to «Julieta Acosta»
- **WHEN** the event is presented
- **THEN** its summary reads «Rol Docente asignado a Julieta Acosta», and removing that assignment reads «Rol Docente quitado a Julieta Acosta»

#### Scenario: Renaming a person does not leak the name through the label

- **GIVEN** an update to a person that changed its `nombre` or `apellido`
- **WHEN** the event is presented
- **THEN** the object label is the generic «Persona» without a name, and neither the summary nor the label contains the old or the new name

### Requirement: Auditoría tab filters and day-grouped list

The Auditoría tab SHALL offer: a search field «Buscar por usuario, objeto o cambio»; a period control with «Hoy», «7 días» (default), «30 días» and «Todo», computed in the America/Argentina/Buenos_Aires time zone; «Acción» chips «Todas», «Altas», «Cambios», «Eliminaciones»; «Módulo» chips «Todos» plus one chip per module that has audited data — «Identidad», «Designaciones», «Portal», «Asistente»; and a «Más filtros» toggle, with a badge counting its active filters, revealing «Desde» and «Hasta» (date and time), «Tabla» (placeholder «p. ej. identity.users») and «Clave de fila» (placeholder «p. ej. 1042»). «Desde» or «Hasta» MUST override the period. Modules without audited data (Aulas, Tareas) MUST NOT get a chip. Every filter and the page MUST be reflected in the URL query string so a view can be shared and restored.

The tab MUST show «N registros · solo lectura» with the total, and «Limpiar filtros» only when the filters differ from the default. The list MUST be grouped by day in the institution's time zone with headers «Hoy · <weekday> <d> de <month>», «Ayer · <weekday> <d> de <month>» or «<Weekday> <d> de <month>», under a column header «Hora | Cambio · usuario · módulo | Acción». Each row MUST show the time (HH:mm:ss), an avatar with the actor's initials (neutral for «Proceso automático» and «Actor no identificado»), the summary, «actor · módulo · objeto» and the action chip «Alta», «Cambio» or «Eliminación». Pagination MUST read «<first>–<last> de <total>» with previous and next controls.

#### Scenario: Default view

- **GIVEN** a user with `auditoria.ver` and events over the last 60 days
- **WHEN** they open the Auditoría tab with no query string
- **THEN** «7 días», «Todas» and «Todos» are selected, only events since the start of the day six days ago are listed, grouped by day, and «Limpiar filtros» is hidden

#### Scenario: Filters in the URL

- **GIVEN** the user selects «30 días», «Cambios» and «Portal» and searches «perfil»
- **WHEN** another user opens the resulting URL
- **THEN** the same filters are selected and the same results are listed

#### Scenario: Custom range overrides the period

- **GIVEN** the period «7 días» selected
- **WHEN** the user sets «Desde» in «Más filtros»
- **THEN** the query uses the custom range, the «Más filtros» badge counts it, and «Limpiar filtros» appears

#### Scenario: Clear filters

- **GIVEN** non-default filters applied
- **WHEN** the user presses «Limpiar filtros»
- **THEN** every filter returns to its default and the button disappears

#### Scenario: No chip for modules without audited data

- **GIVEN** the Auditoría tab rendered
- **WHEN** the «Módulo» chips are shown
- **THEN** they are «Todos», «Identidad», «Designaciones», «Portal» and «Asistente», with no «Aulas» or «Tareas»

#### Scenario: Pagination label

- **GIVEN** 312 matching events and a page size of 50
- **WHEN** the user moves to the second page
- **THEN** the pagination reads «51–100 de 312» and «312 registros · solo lectura» is shown

#### Scenario: Empty result

- **GIVEN** filters with no matching events
- **WHEN** the query finishes
- **THEN** the tab shows «No hay registros para estos filtros.» and no pagination

### Requirement: Inline audit event detail panel

Activating an audit row SHALL open an inline side panel next to the list — the list narrows to make room; the panel MUST NOT overlay the list — and highlight that row. Activating the same row again, the «×» control («Cerrar detalle»), or the Escape key while focus is inside the panel MUST close it and return focus to the row that opened it. The panel MUST show the action chip, the summary and «actor · d/m/aaaa hh:mm:ss»; a «Qué cambió» section with one block per field showing its readable name and before → after, with the before value struck through, the after value highlighted and «—» for an absent value; a masked field MUST show a lock icon and «Enmascarado por política» instead of values; and a «Datos técnicos» section with «Tabla» (`schema.tabla`), «Clave de fila» and «Solicitud», showing «—» when the source does not record one. A field whose before AND after are both absent or `null` MUST be omitted from «Qué cambió» entirely — including a masked field with neither side recorded — since it carries no information. Every safe date/timestamp value shown in «Qué cambió» (before/after) or in a value-bearing summary MUST be presented in the institution's time zone (`America/Argentina/Buenos_Aires`): `d/m/aaaa HH:mm:ss` (24h) for a timestamp, `d/m/aaaa` for a date-only value — never the raw ISO/UTC text. The open event MUST be reflected in the URL. The panel MUST close when its event is no longer in the current result after a filter, page or refresh change.

#### Scenario: Open and close the detail

- **GIVEN** the audit list with results
- **WHEN** the user activates a row, then activates it again
- **THEN** the panel opens beside the narrowed list with that row highlighted, and then closes

#### Scenario: Escape closes the detail and returns focus to the row

- **GIVEN** the detail panel open for a row
- **WHEN** the user presses Escape while focus is inside the panel
- **THEN** the panel closes and focus returns to the row that opened it

#### Scenario: Masked field in the detail

- **GIVEN** an event that changed the safe field `estado` and the masked field `documento`
- **WHEN** its detail panel is open
- **THEN** «Estado» shows its before value struck through and its after value highlighted, and the document field shows the lock icon and «Enmascarado por política» with no value

#### Scenario: Assistant event technical data

- **GIVEN** an assistant maintenance event, which records no request id
- **WHEN** its detail panel is open
- **THEN** «Solicitud» shows «—»

#### Scenario: Event leaves the result

- **GIVEN** the detail panel open for an event of today
- **WHEN** the user changes the action filter so that event no longer matches
- **THEN** the panel closes

#### Scenario: Both-null field is omitted and a safe date shows in local time

- **GIVEN** an INSERT into `identity.user_roles` whose snapshot has `deleted_at` absent (`null`) and `created_at` at `2026-09-27T01:52:30+00:00` UTC
- **WHEN** its detail panel is open
- **THEN** «Qué cambió» does not show a «Fecha de baja» block at all, and «Fecha de creación» shows «— → 26/9/2026 22:52:30»
