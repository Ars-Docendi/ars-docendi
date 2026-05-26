---
name: pr-review
description: Review estructurado de un Pull Request. Usa gh para postear inline comments + un summary. Cubre 7 ejes (correctness, regressions, security, error handling, tests, maintainability, docs).
argument-hint: [<PR-number>]
---

# PR review

**Source of truth:** [docs/workflows/pr-review.md](../../../docs/workflows/pr-review.md). Leerlo.

## Ejes del review

1. **Correctness** — ¿hace lo que dice el spec/plan?
2. **Regressions** — ¿rompe funcionalidad existente?
3. **Security** — ¿autorización por rol? ¿sin secrets?
4. **Error handling** — ¿maneja errores razonables?
5. **Tests** — ¿significativos? ¿cubre BR-\* aplicables? ¿red-green si es bug?
6. **Maintainability** — ¿respeta golden-principles? ¿layer rules?
7. **Documentación** — ¿spec/plan/BR-\*/dependency-graph actualizados?

## Flujo

1. `gh pr view <NUMBER>` + `gh pr diff <NUMBER>` — recopilar contexto.
2. Buscar spec/plan/bug-spec asociado.
3. Aplicar los 7 ejes; identificar issues con severidad (`blocker`, `high`, `medium`, `low`, `nit`).
4. Postear inline comments en archivos/líneas.
5. Postear single summary top-level con tabla de issues + recomendación.

## Reglas

- **Inline > top-level** para issues concretas.
- **Severidad explícita** siempre.
- **No drive-by suggestions** que cambian scope.
- Validar contra spec/plan, no contra preferencias personales.
- Si el PR debería tener spec/plan y no tiene, eso es `[high]`.

## Arguments

`$ARGUMENTS` — número del PR.
