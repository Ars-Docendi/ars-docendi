# Workflows

Playbooks **operacionales** paso a paso para agentes y humanos. Las skills en `.claude/skills/` apuntan acá para el detalle completo.

## ¿Qué workflow uso?

| Situación                                                      | Workflow                                                                                      | Artefactos creados                                                                     |
| -------------------------------------------------------------- | --------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------- |
| Proyecto recién iniciado, sin docs de producto                 | [init-project.md](./init-project.md)                                                          | `docs/product/{brief,vision,design-principles}.md` (una vez)                           |
| Docs de producto OK, arquitectura todavía template             | [architecture-proposal.md](./architecture-proposal.md)                                        | `docs/architecture/*` + `domains/<dominio>.md` por cada bounded context (una vez)      |
| Nueva feature o capability                                     | [add-feature.md](./add-feature.md)                                                            | Change OpenSpec + código + doc updates                                                 |
| Después de mergear PR de feature                               | `/opsx:archive <id>`                                                                          | Archiva change en `openspec/changes/archive/` y mergea delta specs a `openspec/specs/` |
| Bug, un módulo, sin cambio de Contracts                        | [fix-bug.md](./fix-bug.md)                                                                    | Red-green test + fix                                                                   |
| Agregar tests (BR-\* o smoke técnico)                          | [add-tests.md](./add-tests.md)                                                                | Opcional `docs/business-rules/` + tests + traceability                                 |
| Bug que toca Contracts o múltiples módulos                     | [fix-bug.md](./fix-bug.md) + change OpenSpec (bug escalado)                                   | Change OpenSpec + fix                                                                  |
| Bug revela brecha arquitectural                                | Escalar a [add-feature.md](./add-feature.md) vía `/opsx:propose`                              | Change OpenSpec completo                                                               |
| Nuevo módulo .NET (Modules.X + .Contracts)                     | [create-module.md](./create-module.md)                                                        | Module scaffold + domain doc                                                           |
| Cambiar módulo existente                                       | [modify-module.md](./modify-module.md)                                                        | Análisis de impacto en Contracts                                                       |
| Evaluar trabajo completado                                     | [evaluate.md](./evaluate.md)                                                                  | Report + scorecard update                                                              |
| Tareas scheduled de security / arquitectura / infra / test-gap | Ops tasks (read-only, JSON findings); test-gap → [test-gap-monitor.md](./test-gap-monitor.md) | Findings; test-gap → QA issues capped para `/add-tests`                                |
| PR necesita review estructurado                                | [pr-review.md](./pr-review.md)                                                                | Inline comments + summary                                                              |
| CI falló en PR existente                                       | [ci-fix.md](./ci-fix.md)                                                                      | `gh` inspect → fix mínimo → push mismo branch                                          |

## Índice de workflows

| Workflow                                               | Cuándo usar                                                                    |
| ------------------------------------------------------ | ------------------------------------------------------------------------------ |
| [init-project.md](./init-project.md)                   | Bootstrap único: llenar `docs/product/*`                                       |
| [architecture-proposal.md](./architecture-proposal.md) | Bootstrap único (post init-project): llenar `docs/architecture/*` + `domains/` |
| [add-feature.md](./add-feature.md)                     | Nueva capability: change OpenSpec → execute → security → QA → archive          |
| [fix-bug.md](./fix-bug.md)                             | Bug/regression: red test → green fix → refactor                                |
| [add-tests.md](./add-tests.md)                         | QA harness: business (BR-\*) o smoke técnico                                   |
| [create-module.md](./create-module.md)                 | Nuevo módulo .NET (contract-first)                                             |
| [modify-module.md](./modify-module.md)                 | Cambiar módulo existente; assess impacto Contracts                             |
| [evaluate.md](./evaluate.md)                           | Grade trabajo contra spec + grading-criteria                                   |
| [pr-review.md](./pr-review.md)                         | PR review: classify diff → inline comments → single summary                    |
| [ci-fix.md](./ci-fix.md)                               | CI fallido en PR abierto: logs → fix → push                                    |
| [test-gap-monitor.md](./test-gap-monitor.md)           | Read-only: tests faltantes → JSON scheduled                                    |
| [open-pr.md](./open-pr.md)                             | **Referencia** (linkeada desde otros): canonical `gh pr create`                |

## Skills Claude Code que invocan estos workflows

| Skill / Comando             | Trigger                    | Propósito                                                 |
| --------------------------- | -------------------------- | --------------------------------------------------------- |
| `/init-project`             | Humano (una vez)           | Bootstrap `docs/product/*`                                |
| `/architecture-proposal`    | Humano (post init-project) | Bootstrap `docs/architecture/*`                           |
| `/opsx:propose`             | Humano o Claude            | Crear change OpenSpec (proposal+design+specs+tasks)       |
| `/opsx:apply`               | Humano o Claude            | Implementar tasks de un change apply-ready                |
| `/opsx:archive`             | Post-merge                 | Archivar change; mergear delta specs a `openspec/specs/`  |
| `/opsx:explore`             | Humano o Claude            | Explorar idea antes de proponer formalmente               |
| `/opsx:sync`                | Humano                     | Sincronizar glue con la CLI de OpenSpec                   |
| `/add-feature`              | Humano o Claude            | Flujo completo feature; gated en change OpenSpec aprobado |
| `/fix-bug`                  | Humano o Claude            | Red-green bug fix con check de escalación                 |
| `/add-tests`                | Humano o QA                | Agregar tests: BR-\* o smoke                              |
| `/create-module`            | Humano o Claude            | Scaffold módulo .NET                                      |
| `/modify-module`            | Humano o Claude            | Cambiar módulo + chequear impacto                         |
| `/evaluate`                 | Humano o Claude            | Evaluación read-only contra spec/criteria                 |
| `/security-audit`           | Humano                     | Read-only security pass                                   |
| `/architecture-drift-check` | Humano                     | Read-only drift vs docs/grafo                             |
| `/infra-logs-monitor`       | Humano (post-deploy)       | Read-only infra/logs vs `infrastructure.md`               |
| `/check-deploy`             | Humano (post-deploy)       | Verificar health del despliegue                           |
| `/debug-production`         | Humano                     | Investigar issues en producción                           |
| `/pr-review`                | Humano                     | Review estructurado de PR                                 |
| `/ci-fix`                   | Humano                     | Arreglar CI fallido en PR                                 |
| `/test-gap-monitor`         | Humano                     | Tests faltantes (read-only)                               |
