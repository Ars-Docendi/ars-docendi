---
name: ci-fix
description: Arreglar un CI fallido en un PR existente. Lee logs via gh, identifica la causa, propone fix mínimo, push al mismo branch. NO usar para fixes funcionales (eso es /fix-bug).
argument-hint: [<PR-number>]
---

# CI fix

Arreglar un CI fallido en un PR abierto. Fix mínimo, push al mismo branch.

## Cuándo usar

- Un PR abierto tiene jobs de CI fallidos en GitHub Actions.
- El fix no requiere replantear la feature (eso sería `/fix-bug` o volver a planning).

## Pre-requisitos

- Acceso a `gh` CLI autenticado.
- Branch del PR check-outed localmente.

## Steps

### 1. Inspeccionar el run fallido

```bash
gh pr checks <NUMBER>                      # lista de checks
gh run list --branch <BRANCH> --limit 5    # últimos runs
gh run view <RUN_ID> --log-failed          # logs solo de los steps fallidos
```

### 2. Reproducir localmente

Según el tipo de falla:

- **Build backend**: `dotnet build backend/ArsDocendi.slnx`
- **Test backend**: `dotnet test backend/ArsDocendi.slnx`
- **Format**: `pnpm format:check`
- **Frontend build**: `pnpm --filter frontend build`
- **Frontend lint**: `pnpm --filter frontend lint`

Si no reproducís localmente, el problema puede ser env de CI (versión de Node, falta de variable, etc.) — investigar el workflow.

### 3. Diagnosticar la causa

| Síntoma                            | Causa probable                                                                          |
| ---------------------------------- | --------------------------------------------------------------------------------------- |
| `Cannot find module 'X'`           | Falta `pnpm install` o `node_modules` desactualizado en CI                              |
| `error CS0246: Type 'X' not found` | Falta referencia .NET, falta `dotnet restore`                                           |
| `prettier --check` falla           | Archivos no formateados — correr `pnpm format`                                          |
| `eslint` falla                     | Reglas violadas — correr `pnpm --filter frontend lint --fix`                            |
| Test específico falla              | Bug real introducido — escalar a `/fix-bug` si es no trivial                            |
| Path filter equivocado             | Job corrió cuando no debía o no corrió cuando debía — fix en `.github/workflows/ci.yml` |

### 4. Aplicar el fix mínimo

- **NO refactor** mientras arreglas CI.
- **NO mezclar** con otros cambios funcionales.
- Si el fix es trivial (formato, lint): aplicar y push.
- Si requiere lógica nueva: considerar si vale `/fix-bug` separado.

### 5. Verificar localmente

Antes de push, asegurar que la suite que estaba roja ahora pasa local.

### 6. Push al mismo branch

```bash
git add <archivos>
git commit -m "ci: fix <descripción corta de la causa>"
git push origin <BRANCH>
```

### 7. Esperar nuevo run de CI

```bash
gh pr checks <NUMBER> --watch
```

Si vuelve a fallar: volver a step 1 con los nuevos logs (puede ser otra causa).

## Reglas

- **Un fix por causa**: no acumular fixes de causas distintas en un commit.
- **No bypass del CI**: no usar `[skip ci]` o deshabilitar checks para evadir el problema.
- **Si el CI está mal configurado**: arreglar el `.github/workflows/*.yml`, no el código que falsamente falla.
- **Si requiere un revert**: hacerlo explícito (`git revert <sha>`) con justificación en el commit.

## Arguments

`$ARGUMENTS` — número del PR (opcional; si no se provee, usar el del branch actual).
