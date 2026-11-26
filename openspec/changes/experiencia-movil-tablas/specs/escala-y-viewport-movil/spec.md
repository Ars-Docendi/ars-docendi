## ADDED Requirements

### Requirement: No UI zoom on phones and tablets

The UI zoom (`--ui-zoom`) SHALL be 1 below 1024 px so the page uses the full CSS width of the device, and 1.125 from 1024 px. Shell and stage rules that divide by `--ui-zoom` MUST keep working at both values.

#### Scenario: 390 px phone

- **GIVEN** a 390 px wide viewport
- **WHEN** any authenticated page is opened
- **THEN** the available layout width is 390 CSS px

### Requirement: Dynamic viewport height

Full-height layouts (Centro de ayuda, login, términos, filter popovers, table max-heights) SHALL use `dvh` instead of `vh`, so the mobile browser address bar does not hide content.

#### Scenario: Address bar visible

- **GIVEN** a mobile browser with a visible address bar
- **WHEN** the login page is opened
- **THEN** its content fits without being cut at the bottom

### Requirement: Inputs do not trigger browser zoom

Below 560 px, text inputs, selects and textareas SHALL use a font size of at least 16 px.

#### Scenario: Focus a field on iOS

- **GIVEN** an iPhone viewport
- **WHEN** the user focuses the search field
- **THEN** the browser does not zoom the page

### Requirement: Navigation drawer highlight clear of the border

In the navigation drawer, the hover and active item background MUST NOT touch the drawer's border.

#### Scenario: Active item in the drawer

- **GIVEN** the open navigation drawer
- **WHEN** the user looks at the highlighted item
- **THEN** a visible gap separates it from the drawer border
