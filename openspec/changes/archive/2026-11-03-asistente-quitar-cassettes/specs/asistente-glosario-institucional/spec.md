## MODIFIED Requirements

### Requirement: The glossary does not change the default prompt

The glossary SHALL be rendered only when `GlosarioEnElPrefijo` is `true`, and that option MUST default to `false`. With the default, the fixed part of the generation prompt MUST be identical, byte for byte, to the one produced before this capability existed, for every provider and for both read roles.

#### Scenario: Default configuration leaves the prefix untouched

- **GIVEN** `GlosarioEnElPrefijo` is not set
- **WHEN** the fixed part of the generation prompt is built for the basic role and for the personal-data role
- **THEN** each prefix is identical to the one built before this change

#### Scenario: The option is documented with its real default

- **GIVEN** the options table in the module README
- **WHEN** the documented default of `GlosarioEnElPrefijo` is compared with the code
- **THEN** both say `false`
