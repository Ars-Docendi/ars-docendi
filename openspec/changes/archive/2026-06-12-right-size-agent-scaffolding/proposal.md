## Why

El repo está en etapa esqueleto (backend = stubs de `GET /ping`, contexts vacíos, DTOs
stub; frontend mínimo), pero la capa de scaffolding para agentes ya está construida por
completo: `docs/` (39 archivos, ~2.450 líneas), `.claude/` (24 skills + 5 comandos opsx),
y ~60KB de prosa de onboarding (CLAUDE.md 19KB + ONBOARDING.md 33KB + CONTRIBUTING.md 8KB).
Es **proceso escrito antes que la práctica que describe**: OpenSpec corrió exactamente una
vez —para instalarse a sí mismo (`openspec/changes/` solo contiene
`migrar-planning-a-openspec`; `archive/` está vacío)—.

El mismo conocimiento está replicado en **cuatro registros** —`CLAUDE.md`, `ONBOARDING.md`,
`docs/workflows/` y `.claude/skills/`—. Para un equipo de TFI de 1–2 personas que defiende
el trabajo ante un tribunal, esa replicación es impuesto puro: cada edición es una edición
en cuatro lugares, y un aparato grande de orquestación de agentes alrededor de un producto
esqueleto es un pasivo a defender, no un activo.

Esta corrección NO toca OpenSpec: el equipo decidió mantenerlo como **columna vertebral**
del planning. Se recorta todo lo de alrededor para volver a poner el meta-layer en sintonía
con el producto, no por delante de él.

## What Changes

- **Colapsar un registro entero**: el detalle ejecutable de cada `docs/workflows/<x>.md`
  que espeja 1:1 una skill se mueve a su `.claude/skills/<x>/SKILL.md`, y el playbook
  standalone se elimina. Se conserva `docs/workflows/open-pr.md` (referencia canónica sin
  skill dueña) y un `docs/workflows/README.md` recortado como índice.
- **Recortar prosa de onboarding**: `ONBOARDING.md` (33KB) pasa a ser un puntero corto de
  orientación; lo esencial vigente se mueve a `README.md`. Se de-duplica el solape entre
  `CLAUDE.md` ↔ `README.md` ↔ `CONTRIBUTING.md` (cada hecho vive en un solo lugar).
- **BREAKING (para el flujo de agentes)**: se eliminan las skills `check-deploy`,
  `debug-production` e `infra-logs-monitor` —describen infraestructura que aún es TBD
  (`docs/architecture/infrastructure.md`)—. Se registra en `docs/quality/tech-debt.md` que
  deben recrearse cuando exista infra real. Se quitan sus filas de la tabla de skills de
  `CLAUDE.md`.
- **Diferir skills borderline**: se difieren `security-audit` y `test-gap-monitor` (escanean
  superficie que casi no existe todavía); se conservan `architecture-drift-check` y
  `evaluate` (atan a invariantes vigentes).
- **Reconciliar todos los links**: ningún doc apunta a un archivo movido/eliminado.

**Fuera de alcance / no se toca**: el glue de OpenSpec (`.claude/commands/opsx/*` y
`.claude/skills/openspec-*`, regenerado por `openspec update`); `backend/` y `frontend/`;
las 13 invariantes; y los docs de `architecture/`, `business-rules/` y `product/`.

## Capabilities

### New Capabilities

- `estructura-scaffolding-repo`: define la estructura intencionada del meta-layer del repo
  —una sola fuente por tipo de conocimiento (invariantes en CLAUDE.md, planning en OpenSpec,
  detalle operacional en las skills), qué skills existen y la regla de no mantener skills
  que describan sistemas inexistentes—. Sirve como contrato verificable para futuros cambios
  del scaffolding y como registro del recorte para la defensa del TFI.

### Modified Capabilities

<!-- openspec/specs/ está vacío (solo .gitkeep): no hay capabilities vigentes que modificar. -->

## Impact

- **Archivos eliminados**: ~11 playbooks de `docs/workflows/` + 3 directorios de skills
  (`check-deploy`, `debug-production`, `infra-logs-monitor`) + el workflow `check-deploy`.
  Skills diferidas (movidas a holding o eliminadas con nota): `security-audit`,
  `test-gap-monitor`.
- **Archivos recortados**: `ONBOARDING.md` (33KB → puntero), `CLAUDE.md` (tabla de skills +
  sección "Workflows clave"), `README.md` (absorbe esencial), `CONTRIBUTING.md` (de-dup),
  `docs/quality/tech-debt.md` (nota de recreación).
- **Sin impacto en código**: no se modifica `backend/` ni `frontend/`; el grafo de
  dependencias y los Contracts quedan intactos.
- **Sin impacto cross-module**: ningún módulo ni consumidor de Contracts se ve afectado.
- **Rollback**: cambio 100% en archivos de docs/skills versionados; revertir es `git revert`
  del PR. El glue de OpenSpec no se toca, así que `openspec validate --strict` y `/opsx:*`
  siguen funcionando idénticos antes y después.
