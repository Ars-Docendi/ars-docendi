## 1. Eliminar skills dependientes de infra TBD (D2)

- [x] 1.1 Eliminar `.claude/skills/check-deploy/`, `.claude/skills/debug-production/` y `.claude/skills/infra-logs-monitor/`
- [x] 1.2 Eliminar `docs/workflows/check-deploy.md` (si existe) y cualquier otro playbook de estas skills — no existía ninguno
- [x] 1.3 Quitar las filas de `check-deploy`, `debug-production`, `infra-logs-monitor` de la tabla de skills en `CLAUDE.md`
- [x] 1.4 Anotar en `docs/quality/tech-debt.md` que estas 3 skills se recrean cuando `infrastructure.md` deje de ser TBD (TD-002)

## 2. Diferir skills borderline (D3)

- [x] 2.1 Eliminar `.claude/skills/security-audit/` y `.claude/skills/test-gap-monitor/` + sus playbooks en `docs/workflows/` (solo test-gap-monitor.md existía)
- [x] 2.2 Quitar sus filas de la tabla de skills en `CLAUDE.md`
- [x] 2.3 Anotar en `docs/quality/tech-debt.md` que se recrean cuando haya código real que escanear (deps/auth para security-audit; cobertura BR-\* para test-gap-monitor) (TD-003)
- [x] 2.4 Confirmar que `architecture-drift-check` y `evaluate` se conservan (no se tocan sus skills)

## 3. Colapsar `docs/workflows/` dentro de las skills (D1)

- [x] 3.1 Fundir el detalle de cada `docs/workflows/<x>.md` en su `.claude/skills/<x>/SKILL.md` para los pares: `add-feature`, `add-tests`, `create-module`, `modify-module`, `fix-bug`, `pr-review`, `evaluate`, `init-project`, `architecture-proposal`, `ci-fix` (fix de links + drift `/plan-feature`→`/opsx:propose`)
- [x] 3.2 Eliminar cada `docs/workflows/<x>.md` ya fundido
- [x] 3.3 Conservar `docs/workflows/open-pr.md` intacto (referencia canónica sin skill dueña)
- [x] 3.4 Recortar `docs/workflows/README.md` para que indexe solo los workflows restantes (apunta a skills)
- [x] 3.5 Actualizar la sección "Workflows clave" de `CLAUDE.md` para reflejar la fuente única (skill, no playbook)

## 4. Recortar y consolidar onboarding (D4)

- [x] 4.1 Recortar `ONBOARDING.md` (565→~210 líneas): setup→README, walkthroughs→skills, preservar §2 (BR bootstrapping + doc-status, contenido único), fix stale (12→13 invariantes, skills retiradas, "13 playbooks")
- [x] 4.2 Mover lo esencial vigente de `ONBOARDING.md` a `README.md` — README ya cubría setup; ONBOARDING ahora apunta ahí (sin duplicar)
- [x] 4.3 De-duplicar solape `CLAUDE.md` ↔ `README.md` ↔ `CONTRIBUTING.md` — ya complementarios (setup en README, invariantes+skills en CLAUDE, gitflow en CONTRIBUTING); sin solape real que recortar

## 5. Reconciliar links y verificar (D5)

- [x] 5.1 `grep` de cada path eliminado/movido en `CLAUDE.md`, `README.md`, `CONTRIBUTING.md`, `ONBOARDING.md` y `docs/**`; corregidas 6 referencias colgadas (CLAUDE, CONTRIBUTING, docs/architecture/README, docs/product/designs/README, infra/README x2). Re-grep: cero hits
- [x] 5.2 Glue intacto: `.claude/commands/opsx/*` (5) y `.claude/skills/openspec-*` (5) presentes y sin editar en toda la sesión
- [x] 5.3 `backend/` y `frontend/` sin cambios — la sesión solo editó `.md` en docs/.claude/CLAUDE/README/CONTRIBUTING/ONBOARDING/openspec
- [x] 5.4 `openspec validate --strict` → valid; `openspec list` → resuelve
- [x] 5.5 Conteos before→after: docs 39→28 (−11); docs/workflows 13→2 (open-pr + README); skills 24→19 (−5); ONBOARDING.md 565→216 líneas
