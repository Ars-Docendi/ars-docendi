# asistente-metricas-de-calidad Specification

## Purpose

Gives administrators a view of how users rate the Asistente's answers and where it visibly fails, per model and over time, without ever linking a rating to a person or exposing a free-text comment.

## Requirements

### Requirement: Aggregate quality metrics are available to administrators

The system SHALL expose a read endpoint, gated by `asistente.administrar`, that for a period of 7, 30 or 90 days and an optional model returns: the negative rate (negative votes divided by total votes), negatives by reason (the four reasons and "no reason given"), detectable failures by cause, the share of answers with problems (negative, replaced or a provider or query failure, counted once per turn), vote coverage (votes divided by answered turns), a per-model breakdown, a series covering every day of the period (every week for 90 days) and the comparison with the previous period of equal length. Turns denied by policy (quota, organization cap, maintenance, revoked access, concurrent turn) SHALL be excluded from every rate and SHALL be returned as a separate count by policy cause, the trend versus the previous period SHALL be omitted when either period has fewer than 20 votes, and the day of a vote SHALL be the day of its turn. A day or week without turns SHALL appear in the series as a point with a null rate, never as 0%, rates whose denominator is zero SHALL be null, and the response SHALL list every model seen in the period regardless of the model filter.

#### Scenario: Rates are computed from votes and turns

- **GIVEN** a period with 10 votes of which 3 are negative
- **WHEN** an administrator requests the metrics
- **THEN** the negative rate is 30%

#### Scenario: Policy denials do not count against a model

- **GIVEN** 10 turns of which 2 were refused because the user's quota ran out and 1 failed because the provider timed out
- **WHEN** an administrator requests the metrics
- **THEN** the detectable failure rate is computed over 8 turns
- **AND** the quota refusals appear in no failure bar
- **AND** the policy denials count reports 2 under the quota cause

#### Scenario: The trend is omitted with too few votes

- **GIVEN** a previous period with 12 votes
- **WHEN** an administrator requests the metrics
- **THEN** the negative rate is returned without a trend versus the previous period

#### Scenario: A vote counts on the day of its turn

- **GIVEN** a turn recorded on one day and a vote cast on the next day
- **WHEN** the per-day series is computed
- **THEN** the vote is counted on the day of the turn

#### Scenario: A turn with several problems counts once

- **GIVEN** a turn that was rated negative and was also replaced
- **WHEN** the share of answers with problems is computed
- **THEN** that turn contributes one to the numerator

#### Scenario: Days without votes are gaps in the series

- **GIVEN** a 30-day period with votes on a single day
- **WHEN** an administrator requests the metrics
- **THEN** the series has one point for each of the 30 days, in order
- **AND** only the day with votes has a rate, and every other point has a null rate

#### Scenario: Rates without a denominator are null

- **GIVEN** a period with no votes
- **WHEN** an administrator requests the metrics
- **THEN** the negative rate is null and the screen shows a dash, not 0%

#### Scenario: Coverage is votes over answered turns

- **GIVEN** 20 answered turns and 5 votes
- **WHEN** an administrator requests the metrics
- **THEN** vote coverage is 25%

#### Scenario: The trend uses a previous period of equal length

- **GIVEN** a 7-day period
- **WHEN** the metrics are returned
- **THEN** the trend compares it with the 7 days immediately before it

#### Scenario: A period beyond retention is rejected

- **GIVEN** an administrator
- **WHEN** the administrator requests a period longer than 90 days
- **THEN** the system rejects the request

#### Scenario: A caller without the permission is rejected

- **GIVEN** an authenticated caller without `asistente.administrar`
- **WHEN** the caller requests the metrics
- **THEN** the system responds 403 and returns no data

### Requirement: Metrics can be compared per model

The system SHALL record the model (`empresa/modelo`) on each analytic record and SHALL break every metric down by it, with turn counts shown so that volume is visible.

#### Scenario: Metrics are filtered by model

- **GIVEN** turns recorded under two models
- **WHEN** an administrator requests the metrics filtered to one model
- **THEN** only that model's turns and votes are counted

#### Scenario: The model is recorded on each turn

- **GIVEN** a turn answered by a given adapter
- **WHEN** its analytic record is written
- **THEN** the record carries that adapter's `empresa/modelo` name

### Requirement: Every failure path records a cause

The system SHALL record on the analytic record of each failing or empty turn exactly one cause code from the closed set `proveedor_no_disponible`, `proveedor_saturado`, `timeout_proveedor`, `credencial_rechazada`, `modelo_no_encontrado`, `error_del_proveedor`, `techo_de_llamadas`, `presupuesto_del_turno`, `generacion_cortada`, `respuesta_ininteligible`, `validador_rechazo`, `plan_invalido`, `sin_privilegio_de_datos`, `error_del_motor`, `timeout_de_sentencia`, `excepcion`, `resultado_vacio`, `cupo_agotado`, `tope_organizacional`, `mantenimiento`, `acceso_revocado` and `turno_concurrente`, without changing the turn's existing outcome state. A model's own declination of a question SHALL NOT be recorded as a cause.

#### Scenario: A provider timeout records its cause

- **GIVEN** a turn whose model call times out
- **WHEN** the turn ends degraded
- **THEN** its analytic record carries `timeout_proveedor` and the same outcome state as before

#### Scenario: A saturated shared service is not an outage

- **GIVEN** the shared inference service answers 503 because its queue is full
- **WHEN** the turn ends degraded
- **THEN** its analytic record carries `proveedor_saturado`
- **AND** the provider's circuit breaker remains closed

#### Scenario: A rejected credential is distinguished from an unreachable provider

- **GIVEN** a provider that answers 401 or 403 to the configured credential
- **WHEN** the turn ends degraded
- **THEN** its cause is `credencial_rechazada`, and an answer of 404 for the model yields `modelo_no_encontrado`

#### Scenario: A statement timeout is distinguished from an engine error

- **GIVEN** a generated query the engine cancels with SQLSTATE `57014`
- **WHEN** the turn ends without an answer
- **THEN** its cause is `timeout_de_sentencia` and any other engine rejection yields `error_del_motor`

#### Scenario: An empty result is distinguishable

- **GIVEN** a turn whose query returns no rows
- **WHEN** the turn is recorded
- **THEN** its state is still answered
- **AND** its cause is `resultado_vacio`

#### Scenario: A declination by the model is not a failure

- **GIVEN** a turn the model declines as out of topic
- **WHEN** the turn is recorded
- **THEN** no cause is recorded on its analytic record

#### Scenario: An unclassified failure is visible

- **GIVEN** a failing turn with no cause recorded
- **WHEN** failures by cause are computed
- **THEN** it appears under the `sin_clasificar` bucket instead of being dropped

### Requirement: A replaced answer is recorded as implicit dissatisfaction

The system SHALL set a replaced flag on the analytic record of an answered turn whose answer the user replaces by editing and resending, once the replacement completes, and SHALL count that turn among the answers with problems. When the replaced turn's analytic record is not known, the flag SHALL be left unset.

#### Scenario: Replacing a turn sets the flag

- **GIVEN** an answered turn
- **WHEN** the user edits and resends the question, replacing it
- **THEN** the replaced turn's analytic record is flagged as replaced

#### Scenario: A replacement that fails sets nothing

- **GIVEN** an answered turn
- **WHEN** the user's replacement ends in an unhandled failure and the previous turn is restored
- **THEN** the original turn's record is not flagged as replaced

### Requirement: Metrics never link a rating to a person or to the operational record

The system SHALL compute quality metrics from `registro_analitico` joined only with `retroalimentacion_turno`, and MUST NOT join either to an actor or, per row, to `registro_operativo`. No endpoint SHALL return the free-text comment or the precise time of a vote.

#### Scenario: The comment is never returned

- **GIVEN** a vote carrying a comment
- **WHEN** any quality endpoint is called with any filter
- **THEN** the comment text appears in no response

#### Scenario: The aggregate reader is structurally confined

- **GIVEN** the source of the quality readers
- **WHEN** the architecture test inspects it
- **THEN** it reads only the analytic and feedback tables and references no actor column, no operational table, no comment column and no vote timestamp

### Requirement: The problem list is anonymous, permissioned and audited

The system SHALL expose a paginated list of problem answers, filterable by type (negative rating, failure, replacement), period and model, requiring the new permission `asistente.revisar_calidad`, which SHALL be seeded to no role. Each row SHALL show the anonymous question text, the reason or cause, the type, the model, the day and the state, and SHALL NOT show the actor or the comment. Every successful read SHALL write an audit row.

#### Scenario: The review permission alone opens the list but not the aggregates

- **GIVEN** a caller with `asistente.revisar_calidad` but without `asistente.administrar`
- **WHEN** the caller requests the problem list and then the aggregates
- **THEN** the list is returned and the aggregates respond 403

#### Scenario: Holding only the administration permission is not enough

- **GIVEN** a caller with `asistente.administrar` but without `asistente.revisar_calidad`
- **WHEN** the caller requests the problem list
- **THEN** the system responds 403

#### Scenario: A read is audited

- **GIVEN** a caller with `asistente.revisar_calidad`
- **WHEN** the caller reads a page of the problem list
- **THEN** one audit row records the caller, the moment and the filters used, written before the response is returned

#### Scenario: A refused read is not audited

- **GIVEN** a caller without `asistente.revisar_calidad`
- **WHEN** the caller requests the problem list
- **THEN** the system responds 403 and writes no audit row

#### Scenario: Rows carry no identity

- **GIVEN** a returned page of the problem list
- **WHEN** its rows are inspected
- **THEN** no row contains an actor identifier or a comment

#### Scenario: The permission is assigned to no role by default

- **GIVEN** a freshly migrated database
- **WHEN** role permissions are listed
- **THEN** `asistente.revisar_calidad` is assigned to no role

### Requirement: The Calidad tab emphasizes negative answers and respects permissions

The Sistema screen SHALL show a "Calidad" tab between "Modelo" and "Auditoría" to holders of `asistente.administrar`, whose first KPI, "Respuestas negativas", is wider and red-accented and shows the trend against the previous period, with an amber note when rising. The tab status dot SHALL turn to warning when the 30-day negative rate rises against the previous 30 days, whatever period is selected in the tab. The "Respuestas con problemas" table SHALL be replaced by an information notice for viewers without `asistente.revisar_calidad`.

#### Scenario: The negative KPI comes first and is emphasized

- **GIVEN** an administrator opening the tab
- **WHEN** the KPI row renders
- **THEN** "Respuestas negativas" is the first KPI and carries the emphasis styling

#### Scenario: A rising rate is flagged

- **GIVEN** a negative rate higher than in the previous period
- **WHEN** the tab renders
- **THEN** the KPI shows an amber note and the tab status dot is in warning

#### Scenario: Without the review permission the table is replaced

- **GIVEN** an administrator without `asistente.revisar_calidad`
- **WHEN** the tab renders
- **THEN** an information notice replaces the problem table and no problem-list request is made

#### Scenario: The chart has a non-visual alternative

- **GIVEN** the per-day chart
- **WHEN** it renders
- **THEN** it has an accessible label and an equivalent table alternative
- **AND** days without votes read "sin valoraciones" in the table and the label, and draw no bar

#### Scenario: A breakdown without entries shows no percentage

- **GIVEN** a period with no negative votes
- **WHEN** "Por qué no sirvió" renders
- **THEN** each reason shows its count of 0 with a dash, never NaN
