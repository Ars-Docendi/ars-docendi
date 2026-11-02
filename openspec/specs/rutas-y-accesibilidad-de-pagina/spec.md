# rutas-y-accesibilidad-de-pagina Specification

## Purpose

Definir las rutas de error y acceso denegado, el retorno tras el ingreso y la accesibilidad de título, foco y tablas.

## Requirements

### Requirement: Not found and no access

Inside the shell an unknown route SHALL show "Página no encontrada" and a route the active profile cannot open SHALL show "No tenés acceso a esta página", both with "Ir al inicio". A render crash SHALL show an in-shell error page. Unauthenticated users SHALL still go to the login.

#### Scenario: Unknown route

- **WHEN** an authenticated user opens `/no-existe`
- **THEN** the shell SHALL render "Página no encontrada" with "Ir al inicio"

#### Scenario: Missing permission

- **GIVEN** a user without `periodos.administrar`
- **WHEN** they open `/designaciones/periodos`
- **THEN** the shell SHALL render "No tenés acceso a esta página" and MUST NOT redirect silently

### Requirement: Post-login return

After signing in the app SHALL return to the originally requested path, query and hash, only when it is a same-origin relative path.

#### Scenario: Return with query

- **GIVEN** an unauthenticated visit to `/tareas?estado=todas#x`
- **WHEN** the user signs in
- **THEN** the app SHALL open `/tareas?estado=todas#x`

### Requirement: Title, focus, announcement and skip link

Each page SHALL set `document.title` to `<página> · Ars Docendi`. On every route change except query-only ones, focus SHALL move to the page `h1` (no visible outline) and the title SHALL be announced in a polite live region. A "Saltar al contenido" link SHALL be the first focusable element and visible on focus.

#### Scenario: Navigating

- **WHEN** the user navigates to another page
- **THEN** the `h1` SHALL have focus and the live region SHALL contain its text

### Requirement: Accessible tabs and rows

Tab bars SHALL use roving tabindex, ArrowLeft/ArrowRight/Home/End moving focus and activating, and a real `tabpanel` labelled by its tab. Clickable rows SHALL contain a real link in their primary cell and show a visible focus ring.

#### Scenario: Keyboard on tabs

- **WHEN** focus is on a tab and the user presses End
- **THEN** the last tab SHALL be selected and focused and its panel shown
