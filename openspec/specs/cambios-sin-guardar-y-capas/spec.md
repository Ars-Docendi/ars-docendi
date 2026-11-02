# cambios-sin-guardar-y-capas Specification

## Purpose

Definir la protección de cambios sin guardar, el comportamiento de las capas superpuestas y los campos de fecha y hora.

## Requirements

### Requirement: Unsaved changes guard

Pages with editable state SHALL block leaving with unsaved changes through the router and `beforeunload`, asking "Tenés cambios sin guardar." with "Seguir editando" focused and "Descartar cambios". Navigation that only changes the query of the same page MUST NOT block. A successful save MUST NOT ask.

#### Scenario: Leaving a dirty form

- **GIVEN** the pedido form with edited fields
- **WHEN** the user follows a link to another section
- **THEN** the dialog SHALL appear and the navigation SHALL wait for the answer

#### Scenario: Discard

- **WHEN** the user chooses "Descartar cambios"
- **THEN** the navigation SHALL continue

#### Scenario: Changing the active role

- **GIVEN** a page with unsaved changes
- **WHEN** the user picks another role in "Cambiar rol"
- **THEN** the same confirmation SHALL appear and the active role SHALL NOT change until the user discards the changes
- **AND** after discarding, no second confirmation SHALL appear

#### Scenario: Landing after changing the active role

- **GIVEN** a user with more than one role on a role-gated page such as `/aulas`
- **WHEN** the user picks a different role in "Cambiar rol"
- **THEN** the app SHALL land on the home of the new role, replacing the current history entry, with that role's sidebar and notifications
- **AND** this SHALL hold even though the session reloads and the shell is replaced by "Cargando sesión…" while the role changes
- **AND** picking the role already active on login SHALL NOT navigate, and a failed change SHALL show the error in the dialog without navigating

#### Scenario: Opening a notification over a dirty page

- **GIVEN** the notifications panel open over a page with unsaved changes
- **WHEN** the user opens a notification and then chooses "Seguir editando"
- **THEN** the panel SHALL stay open and the page SHALL not change
- **AND** the panel SHALL close only when the navigation actually happens

### Requirement: Overlay behavior

Every modal and the mobile menu drawer SHALL trap focus, focus the first field or the least destructive button on open, return focus to the opener on close, and react to Esc only on the top-most overlay. A form modal with data SHALL ask for confirmation before closing by Esc, overlay or X, and SHALL NOT close while a mutation is pending. Browser Back with an overlay open SHALL close the top-most overlay and keep the page.

#### Scenario: Stacked dialogs

- **GIVEN** a confirmation opened over a form modal
- **WHEN** the user presses Esc
- **THEN** only the confirmation SHALL close

#### Scenario: Initial focus survives a repeated mount

- **GIVEN** a modal or the notifications panel mounted twice in a row (React StrictMode in development)
- **THEN** the initial focus SHALL stay inside the overlay and SHALL NOT return to the opener until it really closes

#### Scenario: Browser Back

- **GIVEN** an open modal
- **WHEN** the user presses the browser Back button
- **THEN** the modal SHALL close and the URL SHALL not change

### Requirement: Date and time fields

Forms SHALL use `CampoFecha` and `CampoHora` (values stay ISO) instead of native date/time inputs or the library `DatePicker`.

#### Scenario: Typing a date

- **WHEN** the user types a valid date in a modal field
- **THEN** the form SHALL receive an ISO `yyyy-mm-dd` value
