## Purpose

Defines the dialog shell shared by the screens of the app, the masked date and time fields and the "Nueva solicitud" form that uses them, so that every dialog behaves the same way for keyboard, pointer and assistive technology users.

## ADDED Requirements

### Requirement: Dialog shell

Every dialog built on the shell SHALL be a labelled modal dialog. On open it MUST focus the first field, or the least destructive button when it has no fields. Tab and Shift+Tab MUST stay inside it. On close the focus MUST return to the element that opened it, or to the page title if that element no longer exists.

#### Scenario: Focus on open and on close

- **GIVEN** a button that opens a dialog with fields
- **WHEN** the dialog opens and is later closed
- **THEN** the first field has the focus while open and the button has it again afterwards

### Requirement: Dialog dismissal

Esc MUST act only on the topmost dialog. While a mutation is pending, Esc, the close button and the overlay MUST NOT dismiss it. The overlay MUST dismiss only when both the mouse press and the release land on it. With unsaved changes, dismissing MUST ask inside the same dialog "Tenés cambios sin guardar." with "Seguir editando" focused and "Descartar cambios".

#### Scenario: Unsaved changes

- **GIVEN** a form dialog with edited fields
- **WHEN** the user presses Esc, the close button or the overlay
- **THEN** the dialog stays open and asks to keep editing or discard, and Esc on that question keeps editing

#### Scenario: Pending submit

- **GIVEN** a dialog whose mutation is in flight
- **WHEN** the user presses Esc, the close button or the overlay
- **THEN** the dialog stays open

#### Scenario: Selecting text out of the dialog

- **GIVEN** an open dialog
- **WHEN** the mouse is pressed inside the dialog and released on the overlay
- **THEN** the dialog does not close

### Requirement: Dialog sizes and small screens

Confirmation dialogs SHALL be 480 CSS px wide and form dialogs `min(600px, 100vw - 32px)`, both computed against the app zoom, with a maximum height of 90vh and a scrolling body. Below 560 px every dialog SHALL be a full-screen sheet with stacked full-width buttons, the primary one on top.

#### Scenario: Narrow screen

- **GIVEN** a viewport narrower than 560 px
- **WHEN** a dialog opens
- **THEN** it fills the screen and its footer buttons are stacked

### Requirement: Masked date and time fields

The date field SHALL accept and show `dd/mm/aaaa`, with slashes inserted while typing and a pasted ISO date converted. The time field SHALL accept and show 24-hour `hh:mm`. Both MUST convert to ISO values (`aaaa-mm-dd`, `hh:mm`) and MUST reject impossible values. The date field MUST offer a button labelled "Abrir calendario" that opens a native date picker when the browser supports it and keeps working when it does not.

#### Scenario: Typing

- **GIVEN** an empty date field
- **WHEN** the user types `25122026`
- **THEN** the field shows `25/12/2026`

#### Scenario: Impossible date

- **GIVEN** the value `31/02/2026`
- **WHEN** it is validated
- **THEN** it is rejected as not a valid date

### Requirement: New request form

"Nueva solicitud de reserva de aula" SHALL group its fields in sections (Examen, Fecha y horario, Capacidad), validate each field with its own message, and on a failed submit focus the first invalid field. It MUST send ISO dates and times to the API. Every opening MUST start with an empty form and without errors from a previous opening. A failure to load the subjects MUST offer a real "Reintentar" button.

#### Scenario: Fresh form on every opening

- **GIVEN** a user who typed in the form and discarded it
- **WHEN** she opens "Nueva solicitud" again
- **THEN** every field is empty

#### Scenario: Failed submit

- **GIVEN** a form with several invalid fields
- **WHEN** the user submits
- **THEN** each invalid field shows its message and the first one has the focus
