## ADDED Requirements

### Requirement: Help center without sidebar on phones

At 900 px or less, the Centro de ayuda SHALL hide the sidebar. Navigation SHALL be home, module, article, using the existing back link. The page MUST NOT scroll horizontally at 360 px.

#### Scenario: Open the help on a phone

- **GIVEN** a 390 px viewport
- **WHEN** the user opens the Centro de ayuda
- **THEN** no sidebar is shown and the first screen shows the search and the modules
- **AND** the page has no horizontal scrollbar

### Requirement: Search on top of the content

On phones the help search SHALL appear at the top of the content, 44 px high with 16 px text.

#### Scenario: Search from the home

- **GIVEN** the help home on a phone
- **WHEN** the user types a term
- **THEN** matching articles are listed under the field

### Requirement: Collapsible page index on articles

On phones, articles SHALL show an "En esta página" index built with native `details`/`summary`, listing the article's steps as links to their anchors, instead of the expanded step tree. Captures SHALL keep opening in the existing image viewer.

#### Scenario: Jump to a step

- **GIVEN** an article on a phone
- **WHEN** the user expands "En esta página" and taps a step
- **THEN** the page scrolls to that step
