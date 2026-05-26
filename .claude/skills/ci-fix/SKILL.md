---
name: ci-fix
description: Arreglar un CI fallido en un PR existente. Lee logs via gh, identifica la causa, propone fix mínimo, push al mismo branch. NO usar para fixes funcionales (eso es /fix-bug).
argument-hint: [<PR-number>]
---

# CI fix

**Source of truth:** [docs/workflows/ci-fix.md](../../../docs/workflows/ci-fix.md). Leerlo.

## Cuándo usar

- Un PR abierto tiene jobs de CI fallidos en GitHub Actions.
- El fix no requiere replantear la feature.

## Flujo

1. `gh pr checks <NUMBER>` — listar checks fallidos.
2. `gh run view <RUN_ID> --log-failed` — logs solo de steps fallidos.
3. Reproducir localmente según el tipo:
   - Backend: `dotnet build backend/ArsDocendi.slnx` o `dotnet test`
   - Frontend: `pnpm --filter frontend build` o `lint`
   - Format: `pnpm format:check`
4. Aplicar fix mínimo.
5. Verificar local.
6. Push al mismo branch.
7. `gh pr checks <NUMBER> --watch`.

## Hard rules

- **Un fix por causa**: no acumular fixes de causas distintas.
- **No bypass del CI** (`[skip ci]` o disable checks).
- Si el CI está mal configurado, arreglar el `.github/workflows/*.yml`, no falsear el código.
- **No refactor** mientras arreglas CI.

## Arguments

`$ARGUMENTS` — número del PR (opcional; si no se provee, usar el del branch actual).
