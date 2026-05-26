# Contribuir a Ars Docendi

## Setup desarrollo

Si es la primera vez que cloneás el repo:

```bash
./scripts/setup.sh
```

Esto levanta Postgres en docker, instala deps Node (raíz + frontend), restaura y buildea backend .NET. Más detalle en [README.md](README.md).

## Pre-commit

Al correr `pnpm install` en la raíz, [husky](https://typicode.github.io/husky/) se instala automáticamente. A partir de ahí, cada `git commit` dispara `lint-staged` que:

- Para archivos `.cs` (backend): `dotnet format backend/ArsDocendi.slnx --include <files>`
- Para archivos `.ts/.tsx/.js/.jsx` (frontend): `eslint --fix` + `prettier --write`
- Para `.md/.json/.yml/.yaml/.css/.html`: `prettier --write`

**Si el pre-commit falla**: arreglar el formato/lint indicado, hacer `git add` de los archivos modificados, volver a commitear. NO bypass con `--no-verify` salvo acuerdo explícito del equipo.

Para correr manualmente:

```bash
pnpm format         # formatea todo el repo
pnpm format:check   # verifica sin modificar
```

## Ramas (Gitflow)

### Permanentes

- **`main`** — Producción. Solo recibe merges desde `develop` (releases) o `hotfix/*` (parches urgentes).
- **`develop`** — Integración. Default branch del repo: todos los PRs de features apuntan acá.

### Temporales

- **`feature/<descripcion-corta>`** — Trabajo nuevo. Sale de `develop`, mergea a `develop` vía PR.
- **`hotfix/<descripcion-corta>`** — Parche urgente sobre producción. Sale de `main`, mergea a `main` **y** a `develop` (dos PRs) para que el fix no se pierda en el próximo release.

### Naming

- Prefijo obligatorio: `feature/` o `hotfix/`.
- Nombre en kebab-case, descripción corta de qué hace la branch.
- Ejemplos: `feature/login-azure-ad`, `feature/reserva-aulas-listado`, `hotfix/swagger-403-en-prod`.

## Flujo de trabajo

### Feature nueva (con Claude Code skills)

El flujo recomendado usa las skills del proyecto:

1. **Plan** — `/plan-feature <descripción>` genera `docs/product/specs/<slug>.md` + `docs/plans/active/<slug>.md`.
2. **Review humano del plan** — el equipo aprueba o pide cambios en los archivos generados.
3. **Implementación** — `/add-feature <slug>` ejecuta el plan respetando spec, layers y BR-\*.
4. **PR** — la skill abre el PR a `develop` (ver `docs/workflows/open-pr.md`).
5. **Review humano del PR** — opcional complemento con `/pr-review <NUMBER>`.
6. **Merge** — el workflow `close-plan-on-merge.yml` mueve el plan a `completed/` automáticamente.

### Feature nueva (sin Claude Code)

```bash
git checkout develop && git pull
git checkout -b feature/mi-feature

# Crear spec manual: docs/product/specs/mi-feature.md (desde _template.md)
# Crear plan manual:  docs/plans/active/mi-feature.md (desde _template.md)
# ... implementar ...

git push -u origin feature/mi-feature
gh pr create --base develop
```

### Hotfix sobre producción

```bash
git checkout main && git pull
git checkout -b hotfix/descripcion
# ... fix, commitear ...
git push -u origin hotfix/descripcion
```

Abrir **dos** PRs: uno a `main`, otro a `develop`. Ambos deben mergearse.

### Bug (no hotfix)

Usar `/fix-bug <descripción>` para flujo red-green-refactor. Detalle en [docs/workflows/fix-bug.md](docs/workflows/fix-bug.md).

### Release (promoción a producción)

PR de `develop` → `main` cuando se decide cortar release. Sin release branch intermedia.

## Code review

Requerimos al menos **1 review humano** antes de mergear cualquier PR a `develop` o `main`.

### Complemento con `/pr-review`

La skill `/pr-review <NUMBER>` aplica un check estructurado en 7 ejes (correctness, regressions, security, error handling, tests, maintainability, docs) y postea inline comments + summary. Útil como pre-pase antes del review humano para que el human reviewer encuentre la PR ya saneada.

NO reemplaza el review humano — lo complementa.

### Checklist mínimo del reviewer

- [ ] El PR tiene spec + plan asociados (si es feature) o `_bug-template.md` (si es bug escalado).
- [ ] CI verde (backend + frontend + format-check).
- [ ] Tests cubren BR-\* aplicables (si la feature las introduce/modifica).
- [ ] Docs actualizadas (dependency-graph, api-contracts, data-model, domains/) si hubo cambios estructurales.
- [ ] No hay fake UI (stubs visualmente completos sin lógica).
- [ ] Sin secrets en código.
- [ ] Respeta layers y dependency-graph.

## Requisitos para mergear un PR

1. CI verde (backend + frontend + format-check).
2. 1 aprobación humana de otro miembro del equipo.
3. Branch al día respecto de la base (puede requerir rebase / merge previo).
4. Conversaciones de review resueltas.
5. Si el PR cambia BR-\* aplicables, los tests correspondientes están actualizados.

## Compliance reglamentario

Toda regla de negocio que provenga de **normativa institucional** (estatutos, regímenes, normativas departamentales) debe registrarse como `BR-<modulo>-NNN` en `docs/business-rules/<modulo>.md` con:

- **Statement** — la regla en una oración.
- **Fuente normativa** — cita exacta de la normativa (documento, artículo).
- **Test mapping** — al menos un test que verifica la regla.

Ver [docs/business-rules/\_template.md](docs/business-rules/_template.md) para el formato.

**Cuándo crear una BR**: cuando el código implementa una decisión NO obvia que viene de fuera del equipo (reglamento, política institucional). Si es una decisión técnica interna, va en `docs/quality/golden-principles.md` o en `domains/<x>.md`, no en business-rules.

## Convenciones de código

Detalle en [docs/quality/golden-principles.md](docs/quality/golden-principles.md). Resumen:

- **Backend (.NET)**: Controller → Service → Repository. Sin saltar capas. Cross-module SOLO via `Modules.<X>.Contracts`. Cada módulo expone `/api/<x>/ping`.
- **Frontend (React + Vite)**: features aisladas en `src/features/<x>/`. Lo común sube a `src/shared/`. React Query para data del servidor. Un solo `axios` instance.
- **Archivos chicos**: ~300 líneas como cap soft.
- **Logging**: Serilog (backend), structured. Nunca `Console.WriteLine` ni `console.log` en código productivo.
- **Naming**: PascalCase (componentes, clases .NET), camelCase (funciones, variables, hooks), kebab-case (filenames, branches, slugs).

## Commits

- Mensaje en imperativo, idealmente con prefijo convencional:
  - `feat(<modulo>): ...` — feature nueva
  - `fix(<modulo>): ...` — bug fix
  - `refactor(<modulo>): ...` — refactor sin cambio funcional
  - `test(<modulo>): ...` — solo tests
  - `docs: ...` — solo documentación
  - `chore: ...` — tooling, configs, CI
  - `ci: ...` — workflows GitHub Actions
- Un commit = un cambio lógico. Evitar commits gigantes que mezclan refactor + feature + bug fix.
- Idioma: consistente dentro del mismo commit (español o inglés según preferencia del equipo).

### Sin atribución AI

Por convención del proyecto, **no agregar** `Co-Authored-By: Claude` o similar a los commits. Las herramientas usadas (Claude Code u otras) son herramientas — el commit lo firma quien commitea.

## Recursos

- [README.md](README.md) — Setup + comandos
- [CLAUDE.md](CLAUDE.md) — Contexto del proyecto, invariantes, skills
- [docs/](docs/) — System of record completo
- [docs/workflows/](docs/workflows/) — Playbooks operacionales
