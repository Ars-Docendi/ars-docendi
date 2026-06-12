## 1. Retiro del sistema viejo (Fase 2)

- [x] 1.1 Retirar la skill `/plan-feature` (`.claude/skills/plan-feature/`) y su playbook en `docs/workflows/`
- [x] 1.2 Retirar la skill `/complete-plan` (`.claude/skills/complete-plan/`) y su playbook en `docs/workflows/`
- [x] 1.3 Retirar plantillas viejas: `docs/plans/active/_template.md`, `docs/product/specs/_template.md`, `docs/product/specs/_bug-template.md`
- [x] 1.4 Reapuntar `/add-feature` (D3 híbrido): precondición = change OpenSpec aprobado; delegar la ejecución de tasks a `/opsx:apply`; conservar los gates del proyecto (architecture check, security pass, `/evaluate`, apertura de PR)
- [x] 1.5 Eliminar `.github/workflows/close-plan-on-merge.yml` y `scripts/close-plan-on-merge.ts`
- [x] 1.6 Recortar `scripts/generate-indexes.ts` para que deje de indexar `docs/plans/` y `docs/product/specs/`
- [x] 1.7 Agregar `openspec validate --strict` al CI (`.github/workflows/ci.yml`) (D6)
- [x] 1.8 Actualizar scripts de `package.json` que referencien `generate-indexes` o `close-plan`

## 2. Gobernanza y docs (Fase 3)

- [x] 2.1 `CLAUDE.md`: actualizar tabla de navegación (Planes/specs → `openspec/`; conservar producto, UX y business-rules), tabla de skills (planning → `/opsx:*`) y estructura del repo
- [x] 2.2 `CLAUDE.md`: reformular invariante #5 (spec + plan → change OpenSpec aprobado) y #10 (plan lifecycle → change lifecycle con `openspec archive`)
- [x] 2.3 Actualizar `ONBOARDING.md` con el flujo de planning OpenSpec (`/opsx:*`) y la cheat sheet
- [x] 2.4 Actualizar `README.md` y `CONTRIBUTING.md` (referencias a planning, planes y `generate-indexes`)
- [x] 2.5 Actualizar `docs/workflows/*`: reescribir `add-feature.md` reflejando el híbrido D3 (gates del proyecto + ejecución vía `/opsx:apply`), actualizar el índice `README.md`
- [x] 2.6 Documentar en `CONTRIBUTING.md` (y `CLAUDE.md`) la política del glue OpenSpec (D7, Modelo A): el glue va versionado; al actualizar el devDep correr `openspec update` + commitear en el mismo PR; no correr `openspec init` global. Opcional: guard en CI que falle si `openspec update` produce diff (glue desincronizado de la CLI)

## 3. Verificación y cierre

- [x] 3.1 Verificar que no quedan referencias colgadas a `/plan-feature`, `/complete-plan`, `close-plan-on-merge` ni `docs/plans/active`
- [x] 3.2 `openspec validate --strict` en verde + smoke del flujo completo (`/opsx:propose` → `/add-feature` con ejecución vía `/opsx:apply` → `/opsx:archive`) en un change de prueba
- [ ] 3.3 Archivar este change con `/opsx:archive migrar-planning-a-openspec` una vez mergeado
