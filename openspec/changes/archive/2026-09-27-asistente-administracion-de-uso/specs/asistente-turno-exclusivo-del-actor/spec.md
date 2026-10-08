## Purpose

Ensures only one turn per authenticated actor runs at a time, protecting the shared Anthropic rate limit today and the single-GPU local model provider soon, and guarantees the lock is always released even when the turn fails, times out, or the caller cancels.

## ADDED Requirements

### Requirement: A second concurrent turn from the same actor is rejected

The system SHALL allow at most one in-flight turn per authenticated actor at any moment. A second turn submitted by the same actor while the first has not yet completed SHALL be rejected immediately with the existing `ServicioDegradado` outcome and a friendly, distinct explanation — never queued, and never left waiting for the first turn to finish.

#### Scenario: Second turn from the same actor is rejected while the first is in flight

- **GIVEN** an actor whose turn is currently being processed
- **WHEN** that same actor submits a second turn before the first completes
- **THEN** the second turn resolves immediately as the degraded outcome with a message distinct from quota exhaustion and maintenance mode, and the first turn continues unaffected

#### Scenario: Different actors are never blocked by each other

- **GIVEN** two different actors, each with no turn currently in flight
- **WHEN** both submit a turn at the same moment
- **THEN** neither is rejected for concurrency reasons

### Requirement: The lock is released under every ending, including failure and timeout

The system SHALL release the actor's in-flight lock when the turn completes successfully, when it is abstained or needs clarification, when it degrades, when the caller cancels, when an unhandled exception occurs, and when the turn's end-to-end budget (150 s) expires — with no path that leaves the lock held after the turn has ended by any means.

#### Scenario: A turn that throws releases the lock

- **GIVEN** an actor whose turn is in flight
- **WHEN** that turn ends in an unhandled exception
- **THEN** the actor's next turn is accepted immediately, not rejected for concurrency

#### Scenario: A turn that exceeds its budget releases the lock

- **GIVEN** an actor whose turn has been running for 150 seconds and is cut off by the turn budget
- **WHEN** the actor submits a new turn right after
- **THEN** the new turn is accepted, not rejected for concurrency

#### Scenario: A cancelled request releases the lock

- **GIVEN** an actor whose turn is in flight
- **WHEN** the caller cancels the underlying HTTP request (e.g. closes the tab)
- **THEN** the actor's lock is released and a subsequent turn from that actor is accepted

### Requirement: The lock holds correctly across multiple backend instances

The system SHALL enforce the one-turn-per-actor rule correctly even when more than one instance of the backend is running concurrently (e.g. during a rolling redeploy), without relying on any in-process-only state that a second instance cannot see.

#### Scenario: Two instances see the same lock

- **GIVEN** two backend instances running behind the same load balancer
- **WHEN** the same actor's two concurrent turns land on different instances
- **THEN** exactly one of the two is accepted and the other is rejected for concurrency, regardless of which instance received which request
