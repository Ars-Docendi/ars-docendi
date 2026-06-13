## Context

El meta-layer del repo (docs + skills + onboarding) está dimensionado para un producto
maduro mientras el producto es esqueleto. El conocimiento del agente está replicado en cuatro
registros, y hay skills que describen infraestructura inexistente (infra = TBD). El equipo
decidió mantener OpenSpec como columna vertebral del planning y recortar todo lo de alrededor.

Restricciones:

- **No tocar el glue de OpenSpec** (`.claude/commands/opsx/*`, `.claude/skills/openspec-*`):
  es generado por la CLI y se regenera con `openspec update`. Editarlo a mano es pelearse con
  la herramienta que se decidió conservar.
- **No tocar código** (`backend/`, `frontend/`), las 13 invariantes, ni los docs de
  `architecture/`, `business-rules/`, `product/`. Son los guardrails que se protegen.
- El TFI se defiende ante un tribunal: el recorte debe quedar documentado (este change) para
  defender la decisión.

## Goals / Non-Goals

**Goals:**

- Una sola fuente por tipo de conocimiento (eliminar el registro `docs/workflows/` duplicado).
- Quitar skills que no pueden correr hoy (dependen de infra TBD).
- Reducir la prosa de onboarding (ONBOARDING.md 33KB) a un puntero.
- Dejar un registro verificable de la estructura buscada (la capability `estructura-scaffolding-repo`).

**Non-Goals:**

- Cambiar el flujo de OpenSpec o su glue.
- Tocar código de producto, invariantes, o docs de arquitectura/negocio/producto.
- Eliminar skills core de implementación (`add-feature`, `create-module`, `modify-module`,
  `fix-bug`) ni los guides auto-activables (`dotnet-modules-guide`, `react-features-guide`).

## Decisions

### D1: Colapsar `docs/workflows/<x>.md` dentro del `SKILL.md` de su skill

Cada playbook que espeja 1:1 una skill se funde en el `SKILL.md` correspondiente y el playbook
standalone se elimina. Pares a fundir: `add-feature`, `add-tests`, `create-module`,
`modify-module`, `fix-bug`, `pr-review`, `evaluate`, `test-gap-monitor`, `init-project`,
`architecture-proposal`, `ci-fix`.

- **Alternativa descartada**: mantener la separación skill (trigger) / workflow (detalle).
  Se descarta porque duplica el mantenimiento sin beneficio a esta escala de equipo.
- **Conservar**: `docs/workflows/open-pr.md` (referencia canónica sin skill dueña) y un
  `docs/workflows/README.md` recortado como índice de los workflows restantes.
- Si la fusión empuja un `SKILL.md` muy por encima del cap blando (~300 líneas), se prioriza
  resumir el detalle, no copiarlo verbatim.

### D2: Skills dependientes de infra TBD → eliminar (no mover a holding)

`check-deploy`, `debug-production`, `infra-logs-monitor` se eliminan de `.claude/skills/` (y el
workflow `check-deploy`). No se usa un directorio "holding": el historial de git preserva el
contenido y se recrean cuando exista infra. Se registra la deuda en `docs/quality/tech-debt.md`.

- **Alternativa descartada**: dejarlas con una nota "no funcional aún". Se descarta: contradice
  la invariante #7 (no fake UI / nada que aparente estar hecho) aplicada al meta-layer.

### D3: Skills borderline read-only → conservar dos, diferir dos

- **Conservar** `architecture-drift-check` y `evaluate`: atan a invariantes y grading vigentes,
  son útiles ya hoy sobre el esqueleto.
- **Diferir** (eliminar + nota en tech-debt) `security-audit` y `test-gap-monitor`: escanean
  superficie (deps, auth boundaries, cobertura BR-\*) que casi no existe; su valor llega cuando
  haya código real. Se recrean entonces.

### D4: Onboarding → puntero + consolidación

`ONBOARDING.md` se recorta a una orientación corta (quién lee qué, dónde está la columna
vertebral). Lo esencial vigente migra a `README.md`. Se audita el solape
`CLAUDE.md` ↔ `README.md` ↔ `CONTRIBUTING.md`: invariantes + tabla de skills en CLAUDE.md,
setup en README, gitflow/PR en CONTRIBUTING. Cada hecho en un solo lugar.

### D5: Reconciliación de links como gate de cierre

Tras borrar/fundir, se hace un barrido (`grep`) de cada path eliminado/movido en `CLAUDE.md`,
`README.md`, `CONTRIBUTING.md`, `ONBOARDING.md` y `docs/**`. Cero referencias colgadas es
condición de done.

## Risks / Trade-offs

- **[Perder detalle al fundir workflows en skills]** → Mitigación: fundir contenido, no borrarlo;
  revisar diffs por par; el historial de git conserva el original si hace falta restaurar.
- **[Romper un link que el agente o un dev usa]** → Mitigación: D5 (barrido de links como gate)
  - verificación con `openspec validate --strict` y conteo before/after.
- **[Eliminar una skill que resultaba útil antes de lo previsto]** → Mitigación: están en git;
  recrearlas es `git show`/`git restore` del archivo. Deuda anotada en tech-debt.
- **[Tocar el glue por accidente]** → Mitigación: D2/D5 excluyen explícitamente
  `opsx/*` y `openspec-*`; la spec lo fija como requirement verificable.

## Migration Plan

1. Ejecutar las tasks en `feature/right-size-agent-scaffolding`.
2. PR a `develop`; CI corre `openspec validate --strict`.
3. Post-merge: `/opsx:archive right-size-agent-scaffolding` (mergea la delta spec a
   `openspec/specs/estructura-scaffolding-repo/`).
4. **Rollback**: `git revert` del PR. El glue intacto garantiza que OpenSpec sigue operativo
   idéntico antes y después.

## Open Questions

- Ninguna bloqueante. Si al fundir, algún `SKILL.md` excede mucho el cap de líneas, decidir
  caso por caso entre resumir agresivo o dejar el detalle largo (preferencia: resumir).
