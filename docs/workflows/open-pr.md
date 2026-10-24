# Workflow: Open PR

Procedimiento canonical para abrir un Pull Request con `gh`. Referenciado desde otros workflows.

## Pre-requisitos

- Branch local con los cambios commiteados.
- `gh` CLI autenticado (`gh auth status`).
- Pre-commit pasó (lint, format).

## Steps

### 1. Push a remote

```bash
git push -u origin <BRANCH_NAME>
```

### 2. Validar pre-condiciones

Antes de `gh pr create`:

- `git status` limpio (nada por commitear).
- Checks de [AGENTS.md → Verificación](../../AGENTS.md#verificación) según lo tocado, en verde.

### 3. Construir título y body

**Título**: corto (~70 chars max), imperativo, prefijo según convencional commits.

Ejemplos:

- `feat(designaciones): agregar workflow de aprobación por Coordinador`
- `fix(aulas): corregir validación de capacidad en reservas`
- `docs: completar plan de feature exportar-designaciones`
- `chore: actualizar dependencias frontend`
- `test(portal): cubrir BR-portal-002 con tests unitarios`

**Body** (template):

```markdown
## Summary

- _(bullet con qué cambió y por qué)_
- _(bullet con módulo/path afectados)_

## Change OpenSpec

- Change: `openspec/changes/<id>/` (proposal/specs/tasks) (si aplica)

## Roles afectados

- [x] Jefe de Cátedra
- [ ] Coordinador de Carrera
- [ ] ...

## BR-\* aplicables

- `BR-<modulo>-NNN` — link a `docs/business-rules/<modulo>.md`

## Test plan

- [ ] `dotnet test backend/ArsDocendi.slnx` verde local
- [ ] `pnpm --filter frontend build` verde
- [ ] Manual spot-check de <flujo> con rol <X>
- [ ] BR-\* cubiertas tienen test verde

## Documentación tocada

- [ ] `docs/architecture/dependency-graph.md` actualizado si hubo cambio de edges
- [ ] `docs/architecture/api-contracts.md` actualizado si hubo cambio de endpoints
- [ ] `docs/business-rules/<modulo>.md` actualizado si introduce/modifica BR
- [ ] Change OpenSpec en `openspec/changes/<id>/` con tasks actualizadas si aplica

## Breaking changes

- _(listar cambios en Contracts que afecten consumidores; "ninguno" si aplica)_
```

### 4. Crear PR

```bash
gh pr create \
  --base develop \
  --head <BRANCH_NAME> \
  --title "<TÍTULO>" \
  --body "$(cat <<'EOF'
<BODY de arriba>
EOF
)"
```

(Si es hotfix, base = `main` y crear PR separado para `develop` después.)

### 5. Asignar reviewers / labels (opcional)

```bash
gh pr edit <NUMBER> --add-reviewer <usuario>
gh pr edit <NUMBER> --add-label "module:designaciones,type:feature"
```

### 6. Esperar CI

```bash
gh pr checks <NUMBER> --watch
```

Si falla, usar la skill `/ci-fix` documentada en [el índice de workflows](./README.md).

## Reglas

- **Base correcta**: `develop` para features/bugs comunes; `main` solo para hotfixes (más un segundo PR del mismo fix a `develop`, ver CONTRIBUTING).
- **Branch nombre**: seguir la convención de CONTRIBUTING.
- **Sin force-push** en branches con review en curso salvo coordinado con reviewers.
- **PR description completa**: la sección "Documentación tocada" es checklist real, no decorativa.
- **Sin atribución AI**: ver [CONTRIBUTING](../../CONTRIBUTING.md#sin-atribución-ai).
