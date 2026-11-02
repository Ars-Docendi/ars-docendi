## ADDED Requirements

### Requirement: Unsaved changes guard

Pages with editable state SHALL block leaving with unsaved changes through the router and `beforeunload`, asking "Tenés cambios sin guardar." with "Seguir editando" focused and "Descartar cambios". Navigation that only changes the query of the same page MUST NOT block. A successful save MUST NOT ask.

#### Scenario: Leaving a dirty form

- **GIVEN** the pedido form with edited fields
- **WHEN** the user follows a link to another section
- **THEN** the dialog SHALL appear and the navigation SHALL wait for the answer

#### Scenario: Discard

- **WHEN** the user chooses "Descartar cambios"
- **THEN** the navigation SHALL continue

### Requirement: Overlay behavior

Every modal and the mobile menu drawer SHALL trap focus, focus the first field or the least destructive button on open, return focus to the opener on close, and react to Esc only on the top-most overlay. A form modal with data SHALL ask for confirmation before closing by Esc, overlay or X, and SHALL NOT close while a mutation is pending. Browser Back with an overlay open SHALL close the top-most overlay and keep the page.

#### Scenario: Stacked dialogs

- **GIVEN** a confirmation opened over a form modal
- **WHEN** the user presses Esc
- **THEN** only the confirmation SHALL close

#### Scenario: Browser Back

- **GIVEN** an open modal
- **WHEN** the user presses the browser Back button
- **THEN** the modal SHALL close and the URL SHALL not change

### Requirement: Date and time fields

Forms SHALL use `CampoFecha` and `CampoHora` (values stay ISO) instead of native date/time inputs or the library `DatePicker`.

#### Scenario: Typing a date

- **WHEN** the user types a valid date in a modal field
- **THEN** the form SHALL receive an ISO `yyyy-mm-dd` value
