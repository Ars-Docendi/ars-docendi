---
name: add-tests
description: Agregar tests faltantes a un módulo o feature. Dos lanes: business (BR-* con cita reglamentaria) o technical (smoke, render, integration). Mantiene traceability test ↔ requisito.
argument-hint: [<modulo-o-feature> [--lane business|technical]]
---

# Add tests

**Source of truth:** [docs/workflows/add-tests.md](../../../docs/workflows/add-tests.md). Leerlo.

## Lanes

| Lane          | Cuándo                     | Output                                                                        |
| ------------- | -------------------------- | ----------------------------------------------------------------------------- |
| **Business**  | Tests de BR-\*             | Test verifica una BR específica con cita en `docs/business-rules/<modulo>.md` |
| **Technical** | Smoke, render, integration | Tests cubren caminos felices + casos básicos                                  |

## Reglas

- Lane business: cada BR-\* sin test en "Test mapping" es candidato.
- Lane technical: priorizar módulos críticos (Designaciones > otros).
- **No coverage por coverage**: priorizar BR-\* y caminos críticos.
- Tests nuevos pasan + tests existentes siguen pasando.
- Agregar entry a `Test mapping` del BR-\* correspondiente cuando se cubre.

## Comandos clave

- `dotnet test backend/ArsDocendi.slnx` — todos los tests backend
- Frontend test runner: TBD (no configurado todavía — gap conocido)

## Si un BR no es testeable

- Documentar por qué.
- Marcar con `_test-deferred_` y mover a verificación manual.
- Agregar a `docs/quality/tech-debt.md`.

## Arguments

`$ARGUMENTS` — módulo o feature objetivo. Opcional `--lane business|technical`.
