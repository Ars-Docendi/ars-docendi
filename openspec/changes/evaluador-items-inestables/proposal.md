# Proposal

## Why

The evaluator now runs live (no cassettes). Five items oscillate between runs with no code change (4 live runs on 2026-11-07): `cap-009`, `cap-020`, `dia-001-seguimiento#2`, `dia-003-pivote-duro#1`, `dia-005-referencia-al-resultado#2`. The per-item regression lock turns each oscillation into a false exit 4, so a real regression cannot be told apart from noise.

## What Changes

- A versioned `backend/eval/lineas-de-base/inestables.json` lists, per axis, the items known to oscillate, each with a reason.
- A listed item flipping pass to fail is reported as a known oscillation (console, axis report, run summary count) and does not exit 4.
- Every other item keeps the strict lock. An id in the list that is not in the axis baseline fails the run loudly.
- The list is outside the baseline seal, so the freshly frozen baselines stay valid.

## Capabilities

### New Capabilities

### Modified Capabilities

- `asistente-gate-de-regresion`: adds the tolerated-oscillation requirement.

## Impact

- `ArsDocendi.Evaluacion.Nucleo` (`GateDeRegresion`, new `ItemsInestables`), `backend/eval` runner, `backend/eval/lineas-de-base/README.md`.
- No dataset, baseline, schema or API change.
