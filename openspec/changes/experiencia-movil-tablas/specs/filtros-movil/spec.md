## ADDED Requirements

### Requirement: Filter sheet on phones

Below 560 px, list pages SHALL replace the per-column header filters with one "Filtros (n)" button, where n is the number of applied filters. The button SHALL open a bottom sheet containing every filter group of that page, a "Limpiar" button and an apply button labelled "Ver N resultados" whose N is computed live from the draft selection. Applying SHALL update the existing URL filter state, so reload, back navigation and shared links behave as on desktop. Desktop filters MUST be unchanged.

#### Scenario: Filter from the sheet

- **GIVEN** a phone viewport on Personas with 40 people
- **WHEN** the user opens "Filtros", selects a role and sees "Ver 7 resultados"
- **AND** taps that button
- **THEN** the sheet closes and the card list shows those 7 people
- **AND** the URL carries the filter

#### Scenario: Discard the draft

- **GIVEN** an open sheet with a changed draft
- **WHEN** the user closes it without applying
- **THEN** the applied filters and the list do not change

### Requirement: Applied filters as removable chips

Applied filters SHALL be shown as chips above the list, each removable with a control of at least 44 px high, plus a "Limpiar filtros" action when any is applied.

#### Scenario: Remove one chip

- **GIVEN** two applied filters
- **WHEN** the user removes one chip
- **THEN** only that filter is cleared and the list updates

### Requirement: Sort and search on phones

Below 560 px, sorting SHALL be a native `<select>` bound to the existing sort state, replacing clickable column headers. The search input SHALL be 44 px high with 16 px text and `inputmode="search"`. The audit log adds no sort select: it has no sortable columns.

#### Scenario: Change sort

- **GIVEN** a phone viewport on Mis pedidos
- **WHEN** the user picks another option in the sort select
- **THEN** the cards are reordered like the table would be
