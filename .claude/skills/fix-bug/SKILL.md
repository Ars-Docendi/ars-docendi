---
name: fix-bug
description: Arreglar un bug usando red-green-refactor. Usar cuando el usuario reporta algo roto, una regresión, comportamiento incorrecto o un test fallido — no una feature nueva.
argument-hint: [<descripción del bug, error, o síntoma>]
---

# Fix bug (red-green)

**Source of truth:** [docs/workflows/fix-bug.md](../../../docs/workflows/fix-bug.md). Leerlo.

## Orden

1 (localizar) → 2 (escalation check) → 3 (red test) → 4 (green fix) → 5 (refactor, opcional) → 6 (verify) → 7 (docs si surfaces cambiaron) → 8 (prevent recurrence) → 9 (open PR).

## Escalation en step 2

| Scope                                            | Acción                                                              |
| ------------------------------------------------ | ------------------------------------------------------------------- |
| Un módulo, sin cambio de Contracts               | Continuar este workflow                                             |
| Toca `Modules.<X>.Contracts` o múltiples módulos | Llenar `docs/product/specs/_bug-template.md` + nota en `backlog.md` |
| Revela brecha arquitectural                      | Escalar a `/add-feature` completo                                   |

## Hard rules

- **Red-green obligatorio**: test que falla primero, después fix mínimo.
- **No drive-by refactors** durante el fix.
- **Sin features colaterales** en un fix de bug.
- Si el fix puede prevenir recurrencia, agregar regla a `docs/quality/golden-principles.md`.
- Si el bug viola una BR-\*, anotar la regresión en `docs/business-rules/<modulo>.md`.

## Comandos clave

- Backend test: `dotnet test backend/ArsDocendi.slnx`
- Test específico: `dotnet test --filter "FullyQualifiedName~<NombreTest>"`

## Arguments

`$ARGUMENTS` — descripción del bug, mensaje de error, o síntoma.
