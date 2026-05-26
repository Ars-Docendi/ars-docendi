# Workflow: Complete plan after merge

Post-merge cleanup que mueve un plan activo a `completed/` y actualiza frontmatter.

## Cuándo usar

- Un PR de feature acaba de ser mergeado a `develop`.
- Existe un archivo en `docs/plans/active/<feature>.md` correspondiente.

## Modos de invocación

### Automático (vía GitHub Action)

`.github/workflows/close-plan-on-merge.yml` se dispara al mergear PR a `develop`. Ejecuta `scripts/close-plan-on-merge.ts` que:

1. Detecta si el PR mergeado tocó algún plan en `active/`.
2. Si sí: mueve a `completed/`, actualiza frontmatter (`status: completed`, `completed_at: <fecha>`), abre PR de docs con auto-merge.
3. Si no o si toca varios: salta y deja al humano hacerlo manual con `/complete-plan`.

### Manual (`/complete-plan <feature-kebab>`)

Cuando la automatización no aplica o el equipo prefiere control manual.

## Steps (manual)

### 1. Verificar pre-condiciones

- `docs/plans/active/<feature>.md` existe.
- El PR de la feature está mergeado a `develop`.

### 2. Actualizar frontmatter del plan

```yaml
---
status: completed # antes: active o in-review
completed_at: YYYY-MM-DD
pr: <URL del PR mergeado>
---
```

### 3. Llenar sección "Completion"

Al final del plan, agregar:

```markdown
## Completion

- **Fecha**: YYYY-MM-DD
- **PR**: <URL>
- **Outcome**: <resumen de lo entregado>
- **Variaciones del plan original**: <qué cambió vs lo planeado y por qué>
- **Follow-ups**: <link a items en backlog o tech-debt si aplica>
```

### 4. Mover archivo

```bash
git mv docs/plans/active/<feature>.md docs/plans/completed/<feature>.md
```

### 5. Actualizar spec correspondiente (si aplica)

En `docs/product/specs/<feature>.md`:

```yaml
---
status: completed
---
```

### 6. Regenerar índices

```bash
pnpm exec tsx scripts/generate-indexes.ts
```

(Los `_index.md` están gitignored, no se commitean.)

### 7. Commit + PR

```bash
git checkout -b docs/complete-<feature>
git add docs/plans/ docs/product/specs/<feature>.md
git commit -m "docs: complete plan for <feature>"
git push -u origin docs/complete-<feature>
gh pr create --title "docs: complete plan for <feature>" --body "Post-merge cleanup for #<original-pr>."
```

(Si la automatización del workflow ya creó el PR, esto es no-op.)
