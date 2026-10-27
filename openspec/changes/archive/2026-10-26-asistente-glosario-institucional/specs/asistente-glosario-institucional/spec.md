## Purpose

Gives the SQL-generation model the Department's vocabulary in the fixed part of its prompt, so that a word like "en Decanato" or "aprobado" resolves to the column and stored value it means instead of a guess, and defines what the glossary may reference and how that is verified against the database.

## ADDED Requirements

### Requirement: The glossary does not change the default prompt

The glossary SHALL be rendered only when `GlosarioEnElPrefijo` is `true`, and that option MUST default to `false`. With the default, the fixed part of the generation prompt MUST be identical, byte for byte, to the one produced before this capability existed, for every provider and for both read roles.

#### Scenario: Default configuration leaves the prefix untouched

- **GIVEN** `GlosarioEnElPrefijo` is not set
- **WHEN** the fixed part of the generation prompt is built for the basic role and for the personal-data role
- **THEN** each prefix is identical to the one built before this change, and the cassettes recorded with it are still served

#### Scenario: The option is documented with its real default

- **GIVEN** the options table in the module README
- **WHEN** the documented default of `GlosarioEnElPrefijo` is compared with the code
- **THEN** both say `false`

### Requirement: The glossary is a versioned, structured file

The system SHALL read the institutional terms from a versioned file embedded in the module. Each entry MUST have a term, a one-line explanation in Spanish and at least one reference, and MAY have synonyms and a short SQL hint. Each reference MUST name a column as `schema.table.column` and MAY name the exact stored values it corresponds to.

#### Scenario: A term maps to a stored value, not to its label

- **GIVEN** the entry for the term "aprobado"
- **WHEN** its references are read
- **THEN** it names `designaciones.pedidos.estado` with the value `en_lote`, and no value `aprobado`

#### Scenario: An entry without references is rejected

- **GIVEN** an entry whose reference list is empty
- **WHEN** the glossary is loaded
- **THEN** loading fails with a message naming the term

#### Scenario: Terms are unique

- **GIVEN** two entries with the same term, ignoring case and accents
- **WHEN** the glossary is loaded
- **THEN** loading fails with a message naming the term

### Requirement: The glossary block sits in the fixed part, after the schema and before the examples

With `GlosarioEnElPrefijo` on, the fixed part of the generation prompt SHALL be, in this order: the instructions, the closed-catalog values, the schema, the block «GLOSARIO INSTITUCIONAL», and, when `EjemplosEnElPrefijo` is also on, the examples block. The block MUST contain one line per term, in file order, and MUST NOT depend on the question, the actor, the date or the database contents.

#### Scenario: The block is identical in every call

- **GIVEN** `GlosarioEnElPrefijo` on
- **WHEN** the block is built twice, in two processes
- **THEN** the two blocks are byte-for-byte equal

#### Scenario: Turning the glossary on does not move the examples

- **GIVEN** `GlosarioEnElPrefijo` and `EjemplosEnElPrefijo` both on
- **WHEN** the fixed part is built
- **THEN** the glossary block appears after the schema and the examples block starts after the last glossary line, unchanged from how it renders with the glossary off

#### Scenario: Each term line names its columns and values

- **GIVEN** the term "finalizado"
- **WHEN** its line is rendered
- **THEN** it names `designaciones.pedidos.estado` with the three stored values `en_lote`, `rechazado` and `cancelado`

### Requirement: The block is the same for both read roles

The glossary block SHALL be byte-for-byte identical in the prefix of the basic role and in the prefix of the personal-data role. No entry MAY reference a column the basic role cannot read, nor a column classified `sensible-valor` or `sensible-texto`.

#### Scenario: Same block in both prefixes

- **GIVEN** `GlosarioEnElPrefijo` on
- **WHEN** both prefixes are built
- **THEN** the glossary block of one equals the glossary block of the other

#### Scenario: A reference to a sensitive column fails the suite

- **GIVEN** an entry that references a column classified `sensible-valor`
- **WHEN** the glossary tests run
- **THEN** a test fails naming the term and the column

### Requirement: Every column the glossary names exists and is readable

Every column named by a reference, and every column or table named in a term's explanation or SQL hint, SHALL exist in the migrated database. Every column named by a reference MUST be granted to the basic read role in the privilege manifest.

#### Scenario: A column that does not exist fails the suite

- **GIVEN** an entry that references `designaciones.pedidos.estado_inexistente`
- **WHEN** the glossary tests run against the migrated database
- **THEN** a test fails naming the term and the column

#### Scenario: A column of a module that is not granted fails the suite

- **GIVEN** an entry that references a column of a schema absent from the privilege manifest
- **WHEN** the glossary tests run
- **THEN** a test fails naming the term and the column

### Requirement: Every value the glossary names exists

Every stored value named by a reference SHALL be verified mechanically: against the rows of the catalog table when the column belongs to a closed catalog, against the column's `CHECK` constraint when it is constrained text, or against the column declared by the reference when the column has neither. A value that cannot be verified by one of these means MUST NOT be in the file.

#### Scenario: A catalog value is checked against its rows

- **GIVEN** the reference `designaciones.cargos.codigo` with the value `adjunto`
- **WHEN** the glossary tests run
- **THEN** the value is found in the rows of `designaciones.cargos`

#### Scenario: A constrained-text value is checked against the CHECK

- **GIVEN** the reference `designaciones.pedidos.estado` with the value `aprobado`
- **WHEN** the glossary tests run
- **THEN** a test fails because `aprobado` is not in the `CHECK` of that column

#### Scenario: A column without CHECK is checked against its declared source

- **GIVEN** the reference `designaciones.pedidos.propietario_actual` with the value `jefe_catedra`, declared as verified against `identity.roles.code`
- **WHEN** the glossary tests run
- **THEN** the value is found in `identity.roles`

#### Scenario: A value with no way to be verified fails the suite

- **GIVEN** a reference with a value on a column that has no catalog, no `CHECK` and no declared source
- **WHEN** the glossary tests run
- **THEN** a test fails naming the term and the column

### Requirement: Composite terms stay verifiable

A term that is not a conjunction of equalities and value lists MAY carry a SQL hint. Every quoted literal in the hint MUST appear among the values of that term's references, and every column named in the hint MUST be one of that term's referenced columns.

#### Scenario: The total workload is a sum of three referenced columns

- **GIVEN** the term "carga horaria total"
- **WHEN** its entry is read
- **THEN** it references `horas`, `horas_investigacion` and `horas_externas` of `designaciones.designaciones`, and its hint adds the three treating each null as zero

#### Scenario: A hint that names an unreferenced column fails the suite

- **GIVEN** an entry whose hint uses a column that none of its references names
- **WHEN** the glossary tests run
- **THEN** a test fails naming the term and the column

### Requirement: Only vocabulary that maps to readable data enters

The glossary MUST NOT contain a term for which no readable column or value exists, and MUST NOT contain vocabulary of a module whose tables the assistant cannot read. It MUST NOT contain the dedication values as terms.

#### Scenario: A term with no column is not in the file

- **GIVEN** the glossary file
- **WHEN** it is searched for the terms "interino", "suplente", "carácter", "exclusiva", "semiexclusiva", "concurso" and "licencia"
- **THEN** none of them is a term or a synonym of any entry

#### Scenario: Tareas and Aulas vocabulary is not in the file

- **GIVEN** the glossary file
- **WHEN** its references are read
- **THEN** none names the schemas `tareas` or `aulas`

### Requirement: Dedications are a closed catalog read from the database, under the same option

With `GlosarioEnElPrefijo` on, the prefix SHALL list the values of `designaciones.dedicaciones.nombre` and `designaciones.dedicaciones.codigo`, read from the database like the other closed catalogs. With the option off, the list of closed-catalog columns MUST be exactly the one that existed before this change. The set of tables that may be enumerated MUST stay declared in a test and not detected.

#### Scenario: The dedications are in the prefix only with the option on

- **GIVEN** the migrated database
- **WHEN** the prefix is built with `GlosarioEnElPrefijo` on and off
- **THEN** it lists «Categoría 1» to «Categoría 6» only with the option on

#### Scenario: Adding an undeclared table fails the suite

- **GIVEN** a closed-catalog column of a table that the guard test does not admit
- **WHEN** the pure prefix tests run
- **THEN** the guard test fails

### Requirement: The change is measured before it is promoted

The option SHALL be added to the RTX 3070 profile only if, comparing item by item against the local baseline, the capacidad and robustez axes improve and the diálogo and social axes do not worsen. The evaluator MUST NOT be run with the baseline-freezing flag for this comparison.

#### Scenario: An ambiguous result leaves the option off

- **GIVEN** a local evaluator run with the option on where one axis improves and another worsens
- **WHEN** the promotion decision is made
- **THEN** the option stays off in every profile and the result is documented

#### Scenario: The control run reproduces the baseline

- **GIVEN** a local evaluator run with the option off
- **WHEN** it is compared item by item with the local baseline
- **THEN** no item changes
