## ADDED Requirements

### Requirement: The capacity axis covers the comisiones domain

The capacity dataset SHALL include items answerable from the comisiones tables and items about comisiones that the schema cannot answer.

The items that cannot be answered MUST declare their acceptable refusal reasons. Free aulas, which depend on an aula catalog that does not exist, MUST be one of them.

An item whose truth changed because comisiones became readable MUST be reclassified in the same change, with the reason written.

#### Scenario: Answerable and non-answerable comisiones items exist

- **WHEN** the dataset is grouped by category
- **THEN** comisiones items appear among the answerable categories and among the non-answerable ones

#### Scenario: Free aulas must abstain

- **GIVEN** an item that asks which aulas are free
- **WHEN** it is loaded
- **THEN** it is non-answerable and declares its acceptable refusal reasons

#### Scenario: A reclassified item has its reason

- **GIVEN** a pre-existing item whose answerability changed with this change
- **WHEN** the dataset diff is read
- **THEN** the item is reclassified and the reason is written in the commit or the baseline notes

### Requirement: The fixture makes comisiones items unpassable without reading

The synthetic fixture MUST seed comisiones with declared cardinalities, several períodos and a docente without a matching person, and MUST grant the permission to the actors of the comisiones items.

#### Scenario: The fixture grants the permission

- **GIVEN** the evaluation fixture
- **WHEN** the permissions of the evaluation actors are read
- **THEN** the actors of the comisiones items hold `comisiones.ver`
