# Design

## Decision: a versioned list next to the baselines, outside the seal

`backend/eval/lineas-de-base/inestables.json`: `{ "<axis>": { "<item id>": "<reason>" } }`.

Alternatives considered:

- **Flag in the dataset JSON** (`inestable_en_vivo`). Rejected: the dataset is hashed into the baseline seal (`Sello.Dataset`), so adding the flag would change the hash and the gate would refuse to compare against the baselines frozen on 2026-11-07, forcing a funded live re-freeze for a metadata change. Excluding the flag from the hash would work but needs canonicalization code per item type.
- **Per-turn flag on dialogue items.** Rejected with the above: the baseline key is per turn (`dia-001-seguimiento#2`), and a list keyed by that same id needs no extra turn-index mapping.

The list is not part of the seal: tolerating an item does not change what was measured, only how a flip is judged. Its diff is reviewed like the baselines.

## Behavior

- `GateDeRegresion.Comparar(reporte, linea, inestables)`: an item that passed and now fails, and is listed, goes to `Tolerados` instead of `Regresiones`; `Pasa` ignores it. Improvements and new items are unchanged.
- Validation runs before the seal check: every listed id must exist in the axis baseline `Items`, else `InvalidOperationException`. The runner also rejects unknown axes and empty reasons. All map to exit 64 (usage), never 4, so a rotten list is distinguishable from a regression.
- Reporting: the verdict line adds "Oscilaciones conocidas (... N tolerada(s))", the axis report gets an "Oscilaciones conocidas" section, and the run prints the total tolerated.
- Baseline keys are used as ids, so ids are unique per axis; the file is keyed by axis to validate against the right baseline.
