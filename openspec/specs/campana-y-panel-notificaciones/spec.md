# campana-y-panel-notificaciones Specification

## Purpose

Definir la campana de notificaciones internas y su panel: contador, estados, apertura y marcado como leídas.

## Requirements

### Requirement: Bell with unread count

The shell SHALL show a notifications bell with the number of unread notifications: on desktop in the sidebar foot between "Centro de ayuda" and the user card, on narrow screens in the top bar. The count MUST be hidden at 0 and shown as `99+` above 99. Its query key MUST include the active role, and it SHALL refresh every 60 seconds and on window focus when older than 30 seconds.

#### Scenario: Count above 99

- **GIVEN** 150 unread notifications
- **THEN** the bell SHALL show `99+` and its accessible name SHALL be "Notificaciones, 150 notificaciones sin leer"
- **AND** with none the name is "Notificaciones, ninguna sin leer" and with one "Notificaciones, 1 notificación sin leer"

#### Scenario: Collapsed sidebar

- **GIVEN** a collapsed sidebar and unread notifications
- **THEN** the bell SHALL show a corner badge and a "Notificaciones" tooltip instead of the label and the pill

#### Scenario: Role change

- **WHEN** the user changes the active role
- **THEN** the count SHALL be fetched again for the new role

### Requirement: Panel

Clicking the bell SHALL open a dialog panel beside the sidebar, 500px wide at most and never wider than the screen, titled "Notificaciones", with a close button, the tabs "No leídas" and "Leídas" and the notifications grouped as "Hoy", "Esta semana" (week starting Monday) and "Anteriores", 20 per page loaded on scroll. Lists SHALL be fetched only while the panel is open on that tab.

#### Scenario: Closing

- **WHEN** the user presses Esc, presses outside the panel or uses the close button
- **THEN** the panel closes and focus returns to the bell

#### Scenario: Tab keys

- **GIVEN** the focus on a tab
- **WHEN** the user presses an arrow key, Home or End
- **THEN** the focus and the selection move between the tabs

#### Scenario: Tabs

- **WHEN** the panel opens
- **THEN** "No leídas" is selected and only that list is fetched; selecting "Leídas" fetches the other

### Requirement: Panel states

The panel SHALL show a skeleton while loading, an empty state per tab, an error state with a "Reintentar" button when the first request fails, a "No pudimos actualizar la lista." notice when a refetch fails over existing data, and a "No pudimos cargar más notificaciones." notice with "Reintentar" when the next page fails.

#### Scenario: Empty states

- **GIVEN** no notifications in the tab
- **THEN** "No leídas" SHALL show "No tenés notificaciones sin leer" / "Las nuevas notificaciones van a aparecer acá."
- **AND** "Leídas" SHALL show "Todavía no tenés notificaciones leídas" / "Cuando abras una notificación, la vas a encontrar acá."

#### Scenario: Error without data

- **GIVEN** the first request fails
- **THEN** the panel SHALL show "No pudimos cargar las notificaciones" / "Revisá tu conexión e intentá nuevamente." and "Reintentar" SHALL request it again

### Requirement: Opening a notification

Clicking a notification SHALL mark it read when unread, close the panel, navigate to the entity (`/aulas/solicitudes/:id`, `/designaciones/pedidos/:id`, `/tareas/:id`) and briefly highlight it. Each item SHALL show its icon, title, message, a link text and a screen-reader description of its read state.

#### Scenario: Navigate

- **GIVEN** an unread notice of a designation request
- **WHEN** the user clicks it
- **THEN** it is marked read, the panel closes and the app shows `/designaciones/pedidos/:id`

### Requirement: Bulk selection

On the unread tab with loaded items, "Marcar como leídas" SHALL start a selection mode where items are checkboxes, with a footer "Marcar como leídas (N)" and "Seleccionar todas" / "Deseleccionar todas", and a "Cancelar" action. Selecting all SHALL send `{ todas: true, hasta }` with the newest loaded creation instant; otherwise the chosen ids are sent in chunks of at most 100.

#### Scenario: Select all

- **WHEN** the user selects all and confirms
- **THEN** one request `{ todas: true, hasta }` is sent and the count becomes 0 when the server confirms

### Requirement: Optimistic read marking with rollback

Marking as read SHALL update the count and lists immediately. If the request fails the previous state MUST be restored and a toast SHALL say "No pudimos marcar la notificación como leída" (one) or "No pudimos marcar las notificaciones como leídas" (several). After success or failure the count and lists SHALL be refetched.

#### Scenario: Rollback

- **GIVEN** an unread notice and a failing server
- **WHEN** the user opens it
- **THEN** the count returns to its previous value and the toast appears

### Requirement: Honest dialog copy

The "Cancelar reserva" dialog SHALL state that the teacher is notified ("se le notifica (in-app)"). The designaciones confirmation dialog for Devolver SHALL read "El pedido vuelve ${vuelveA} para que lo corrija y lo reenvíe. Se notifica al destinatario (in-app)." and its confirm button SHALL read "Devolver".

#### Scenario: Return copy

- **WHEN** the dialog to return a request opens
- **THEN** it SHALL NOT mention "Borrador"
