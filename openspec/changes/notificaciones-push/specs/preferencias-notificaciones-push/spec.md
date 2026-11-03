## ADDED Requirements

### Requirement: Preferences view in the notifications panel

The notifications panel header SHALL have a settings button ("Preferencias de notificaciones") that opens a view inside the panel, with a back button that returns to the list. The view SHALL contain: (a) a switch "Notificaciones en este dispositivo" with a state text, (b) one switch per event type grouped by module, and (c) a link "¿Cómo activarlas en el celular?" to the help article. Every control MUST be a real, labelled, keyboard-operable control and the view MUST work at 390 px width.

#### Scenario: Open and return

- **WHEN** the user presses the settings button
- **THEN** the panel SHALL show the preferences view
- **AND** pressing back SHALL show the list again

### Requirement: Device switch states

The device switch SHALL reflect one of: `activas` (permission granted and subscription registered), `desactivadas`, `bloqueadas en el navegador` (permission denied; the text SHALL explain how to unblock it and the switch SHALL be disabled), `no disponibles` (the browser has no Web Push or the backend has it disabled; the text SHALL say so) and `iPhone sin instalar` (iOS without `PushManager`: the view SHALL explain that the app must be added to the Home Screen first and link the help article). Turning the switch on SHALL request the permission, subscribe and register the subscription; turning it off SHALL delete the subscription on the server and unsubscribe the browser.

#### Scenario: Blocked permission

- **GIVEN** `Notification.permission` is `denied`
- **THEN** the state text SHALL say the notifications are blocked and how to unblock them
- **AND** the device switch SHALL be disabled

#### Scenario: iPhone not installed

- **GIVEN** an iPhone browser without `PushManager`
- **THEN** the view SHALL NOT be empty: it SHALL explain "Agregar a pantalla de inicio" and link the help article

### Requirement: Per-module switches

The view SHALL show one switch per module, each with a short description of what it notifies: Reserva de aulas ("Asignaciones, cambios, rechazos y cancelaciones de tus reservas."), Designaciones and Tareas. A module switch covers every event type of that module. They MUST be disabled while the device switch is off. Only the modules the active role's permissions can receive SHALL be shown. A switch change SHALL persist through `PUT` and roll back with an error message when it fails.

#### Scenario: Device off

- **GIVEN** the device switch is off
- **THEN** every module switch SHALL be disabled

#### Scenario: Role without Aulas

- **GIVEN** a session without `aulas.solicitar` and `aulas.aprobar`
- **THEN** the Aulas switch SHALL NOT be shown

### Requirement: Subscription sync and logout

When the session is available the app SHALL register the service worker and, if the browser already holds a push subscription and the permission is granted, re-send it to the server on load and whenever the session user changes, at most once per user and subscription however many times the shell mounts. On logout the app SHALL ask the server to delete the subscription of that endpoint before the session ends, and a failure MUST NOT block the logout.

#### Scenario: Another user on the same phone

- **GIVEN** user A subscribed and logged out, and user B logs in on the same browser
- **THEN** the app SHALL re-register the existing browser subscription for user B

### Requirement: Activation hint

While push is not active and available the panel SHALL show at most one small hint inviting the user to activate it, with a dismiss button; once dismissed it MUST NOT reappear on that browser. It MUST NOT be shown when push is active, blocked or unavailable.

#### Scenario: Dismissed hint

- **WHEN** the user dismisses the hint and reopens the panel
- **THEN** the hint SHALL NOT be shown

### Requirement: Service worker and manifest

The app SHALL serve `/sw.js` and `/manifest.webmanifest` (name, short name, start URL, `display: standalone`, 192 and 512 px PNG icons, a maskable icon and an `apple-touch-icon`). The service worker MUST show the notification with the received title and body, tag it with the notice id, and on click focus an open window or open the URL, navigating to the deep link. Because a standalone web app on iOS can cold-start at its start page and lose a message posted to it, the worker MUST also persist the tapped target `{ url, id, en }` in Cache Storage before focusing or opening, and the app MUST consume (read and delete) it on mount and when it becomes visible, navigating once with replace when it is less than 2 minutes old and an in-app path, and ignoring it otherwise. The web server MUST send `Cache-Control: no-cache` for the worker and the manifest.

#### Scenario: Click on a push

- **WHEN** the user taps a notification with `url: /tareas/123`
- **THEN** the app SHALL open or focus on that route

#### Scenario: Cold start on iOS

- **GIVEN** the app was closed and starts at its start page after the tap
- **WHEN** the app mounts and finds a pending target of less than 2 minutes
- **THEN** it SHALL navigate to that route once and delete the target

#### Scenario: Stale target

- **WHEN** the pending target is older than 2 minutes or not an in-app path
- **THEN** it SHALL be deleted and ignored

### Requirement: Help article

The Centro de ayuda SHALL include the article "Activar las notificaciones en el celular" (iPhone/iPad, Android, choosing types, troubleshooting), registered in the help index, visible to every authenticated user, and linked from the preferences view.

#### Scenario: From the preferences view

- **WHEN** the user follows "¿Cómo activarlas en el celular?"
- **THEN** the article SHALL open
