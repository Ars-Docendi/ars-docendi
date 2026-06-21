# Quality scorecard

Registro vivo de evaluaciones. Actualizar después de milestones importantes o cuando se corre `/evaluate`.

## Historial

| Fecha      | Feature / plan                     | Func (30%) | Code (25%) | UX (20%) | Orig (15%) | Doc (10%) | Composite | Notas                                                                                                                                                                                                                                                                                                                                                                                                                                |
| ---------- | ---------------------------------- | ---------- | ---------- | -------- | ---------- | --------- | --------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 2026-06-20 | proyecto-docente-pedidos (SCRUM-7) | 4          | 5          | 4        | 4          | 4         | 4.25      | Prototipo frontend-only mock (sin backend, por diseño). 34 tests verdes (BR-001..004 + 008 TDD red-green), lint/build/`openspec validate --strict` OK, `// TODO(backend)` confinado a `api/`. Gaps honestos: spot-check **visual** no verificable (extensión browser no conectada; cubierto por test de integración + build); citas normativas BR pendientes con cliente; Demo multi-rol + circuito de revisión diferidos a SCRUM-8. |
| YYYY-MM-DD | _(name)_                           | /5         | /5         | /5       | /5         | /5        | _(calc)_  |                                                                                                                                                                                                                                                                                                                                                                                                                                      |

**Leyenda:** Func = Funcionalidad/Compliance, Code = Calidad de código, UX = Diseño/UX, Orig = Originalidad/craft, Doc = Documentación (ver [`grading-criteria.md`](./grading-criteria.md)).

_(Agregar filas más arriba — orden cronológico inverso, lo más nuevo primero.)_
