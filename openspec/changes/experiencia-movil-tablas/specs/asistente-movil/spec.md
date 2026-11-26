## ADDED Requirements

### Requirement: Full-screen assistant above the on-screen keyboard

Below 560 px the assistant modal SHALL fill the screen without margin or radius and respect the device safe-area insets. Its height SHALL follow the visible viewport, so the question input stays pinned at the bottom and visible while the on-screen keyboard is open; only the thread scrolls. Every control in the modal SHALL have a touch target of at least 44 px. The top-bar launcher SHALL keep its visible size but offer a 44 px touch target.

#### Scenario: Typing with the keyboard open

- **GIVEN** the assistant open on a phone
- **WHEN** the user focuses the question input and the on-screen keyboard appears
- **THEN** the input remains visible directly above the keyboard
- **AND** the page behind the modal does not scroll

### Requirement: Conversation rail as a drawer on phones

Below 560 px the conversation rail MUST NOT be a column beside the conversation. A «Conversaciones» button in the conversation header SHALL open it as a drawer over the conversation, with «Nueva conversación», a «Cerrar conversaciones» control, the search field and the full list. Opening a conversation or starting a new one SHALL close the drawer. Escape SHALL close only the drawer and keep the assistant open. Focus SHALL move into the drawer when it opens and return to the «Conversaciones» button when it closes; while it is open the conversation behind it MUST NOT receive focus. The undo notice SHALL stay visible above the drawer. The collapsed-rail preference does not apply on phones.

#### Scenario: Resume a conversation from the drawer

- **GIVEN** the assistant open on a phone with two saved conversations
- **WHEN** the user taps «Conversaciones» and chooses a conversation
- **THEN** that conversation is resumed and the drawer closes
- **AND** the question input is reachable

#### Scenario: Escape closes only the drawer

- **GIVEN** the drawer open over the assistant
- **WHEN** the user presses Escape
- **THEN** the drawer closes, the assistant stays open and focus returns to «Conversaciones»

### Requirement: Result tables never widen the page

Result tables, inline and expanded, SHALL render inside the accessible scroll region with a sticky first column and a right-edge shadow while columns remain, so a wide result scrolls inside its region and never makes the modal or the page scroll sideways. Below 560 px the expanded table SHALL place its title on its own line with the actions wrapping below it. The mentions popover SHALL fit the composer width, with options of at least 44 px whose career and code wrap below the name.

#### Scenario: A seven-column result on a 390 px phone

- **GIVEN** an answer with a seven-column result
- **WHEN** it renders on a 390 px phone
- **THEN** the first column stays visible while the user scrolls the table sideways
- **AND** the conversation does not scroll sideways
