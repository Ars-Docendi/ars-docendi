# Workflows

El detalle paso a paso de cada workflow vive ahora en su skill (`.claude/skills/<x>/SKILL.md`) — **una sola fuente**. Esta carpeta conserva únicamente:

- [open-pr.md](./open-pr.md) — referencia canónica de `gh pr create`, linkeada desde varias skills.
- este índice.

## ¿Qué skill uso?

| Situación                                          | Skill / Comando                  | Artefactos creados                                                 |
| -------------------------------------------------- | -------------------------------- | ------------------------------------------------------------------ |
| Proyecto recién iniciado, sin docs de producto     | `/init-project`                  | `docs/product/{brief,vision,design-principles}.md` (una vez)       |
| Docs de producto OK, arquitectura todavía template | `/architecture-proposal`         | `docs/architecture/*` + `domains/<dominio>.md` (una vez)           |
| Explorar una idea antes de proponerla              | `/opsx:explore`                  | —                                                                  |
| Nueva feature o capability                         | `/opsx:propose` → `/add-feature` | Change OpenSpec + código + doc updates                             |
| Implementar tasks de un change apply-ready         | `/opsx:apply`                    | Código + checkboxes en `tasks.md`                                  |
| Después de mergear PR de feature                   | `/opsx:archive <id>`             | Archiva change en `openspec/changes/archive/` + mergea delta specs |
| Bug, un módulo, sin cambio de Contracts            | `/fix-bug`                       | Red-green test + fix                                               |
| Bug que toca Contracts o múltiples módulos         | `/fix-bug` + `/opsx:propose`     | Change OpenSpec + fix                                              |
| Agregar tests (BR-\* o smoke técnico)              | `/add-tests`                     | Opcional `docs/business-rules/` + tests + traceability             |
| Nuevo módulo .NET (Modules.X + .Contracts)         | `/create-module`                 | Module scaffold + domain doc                                       |
| Cambiar módulo existente                           | `/modify-module`                 | Análisis de impacto en Contracts                                   |
| Evaluar trabajo completado                         | `/evaluate`                      | Report + scorecard update                                          |
| PR necesita review estructurado                    | `/pr-review`                     | Inline comments + summary                                          |
| CI falló en PR existente                           | `/ci-fix`                        | `gh` inspect → fix mínimo → push mismo branch                      |
| Docs de arquitectura vs código real                | `/architecture-drift-check`      | Findings read-only                                                 |
| Abrir el PR (cualquier flujo)                      | [open-pr.md](./open-pr.md)       | `gh pr create` canónico                                            |

> Las skills de operaciones (`/check-deploy`, `/debug-production`, `/infra-logs-monitor`) y de auditoría (`/security-audit`, `/test-gap-monitor`) se retiraron hasta que haya infra y código real para correrlas — ver [tech-debt.md](../quality/tech-debt.md) (TD-002, TD-003).
