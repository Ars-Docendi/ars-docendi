## ADDED Requirements

### Requirement: Not-answerable items declare acceptable refusal reasons

Every `no_contestable` item of the capability dataset SHALL declare a non-empty list of acceptable refusal reasons, each from the closed set `fuera_de_tema`, `otro_sistema`, `muy_general`, `no_cubierto`. Items of any other category MUST NOT declare it. The dataset SHALL include at least one item per reason, and SHALL include the bare-term question «python».

#### Scenario: Every reason is represented

- **WHEN** the capability dataset is loaded
- **THEN** each of the four reasons is acceptable for at least one `no_contestable` item

#### Scenario: A bare term is covered

- **WHEN** the capability dataset is loaded
- **THEN** it contains a `no_contestable` item whose question is «python»

#### Scenario: An answerable item cannot declare reasons

- **GIVEN** a dataset where a `consulta_simple` item declares acceptable reasons
- **WHEN** it is loaded
- **THEN** it is rejected

#### Scenario: An out-of-set reason is rejected

- **GIVEN** a dataset where a `no_contestable` item declares the reason `clima`
- **WHEN** it is loaded
- **THEN** it is rejected

### Requirement: Reason agreement is reported apart from the score

The capability report SHALL include an informational section with, for the `no_contestable` items where the turn abstained with a model-declared reason, how many declared an acceptable reason, and which items did not. Reason agreement MUST NOT change any item's outcome, the scores under any penalty, nor the per-item regression gate.

#### Scenario: A wrong reason still counts as a correct abstention

- **GIVEN** a `no_contestable` item that accepts only `otro_sistema`, and a turn that abstains with `no_cubierto`
- **WHEN** the item is scored
- **THEN** it counts as a correct abstention and appears as a reason disagreement in the informational section

#### Scenario: The gate ignores reasons

- **GIVEN** a baseline and a run that differ only in declared reasons
- **WHEN** the regression gate compares them
- **THEN** it reports no regression
