## ADDED Requirements

### Requirement: Una sola fuente por tipo de conocimiento

El meta-layer del repo SHALL mantener exactamente una fuente de verdad por cada tipo de
conocimiento del agente, sin replicar el mismo contenido en múltiples registros:

- **Invariantes y convenciones de arquitectura** → `CLAUDE.md`.
- **Planning (proposals, specs, changes)** → `openspec/` (la columna vertebral).
- **Detalle operacional de cada skill** → el `SKILL.md` de esa skill en `.claude/skills/`.
- **Setup y comandos de desarrollo** → `README.md`.
- **Gitflow y flujo de PRs** → `CONTRIBUTING.md`.

El directorio `docs/workflows/` NO SHALL contener un playbook que duplique el detalle ya
presente en el `SKILL.md` de su skill. Las excepciones permitidas son referencias canónicas
sin skill dueña (p. ej. `open-pr.md`) y un `README.md` índice.

#### Scenario: Un hecho operacional vive en un solo lugar

- **WHEN** un mantenedor busca cómo ejecutar un workflow que tiene skill dueña (p. ej. `fix-bug`)
- **THEN** el detalle ejecutable está únicamente en `.claude/skills/fix-bug/SKILL.md`
- **AND** no existe un `docs/workflows/fix-bug.md` que repita ese detalle

#### Scenario: Editar un workflow es una edición de un solo archivo

- **WHEN** cambia el procedimiento de un workflow con skill dueña
- **THEN** la actualización se aplica en un único archivo (el `SKILL.md` correspondiente)
- **AND** ningún otro registro queda desincronizado

### Requirement: No mantener skills que describan sistemas inexistentes

El repo NO SHALL conservar skills cuyo funcionamiento dependa de infraestructura o superficie
de código que todavía no existe. Cuando la dependencia se materialice, la skill puede
recrearse; mientras tanto su ausencia SHALL quedar registrada en `docs/quality/tech-debt.md`.

#### Scenario: Skill dependiente de infra TBD se difiere

- **WHEN** una skill (p. ej. `check-deploy`, `debug-production`, `infra-logs-monitor`) requiere
  infraestructura marcada como TBD en `docs/architecture/infrastructure.md`
- **THEN** la skill no está presente en `.claude/skills/`
- **AND** `docs/quality/tech-debt.md` registra que debe recrearse al definirse la infra

#### Scenario: La tabla de skills refleja lo que existe

- **WHEN** se lee la tabla de skills de `CLAUDE.md`
- **THEN** cada fila corresponde a un directorio real en `.claude/skills/`
- **AND** no hay filas para skills eliminadas o diferidas

### Requirement: El glue de OpenSpec no se edita a mano

El glue generado por OpenSpec MUST tratarse como artefacto derivado de la CLI: no se edita ni
elimina a mano, y solo cambia al correr `openspec update` (D7, Modelo A). Aplica a los comandos
`opsx` y a las skills `openspec-` en `.claude/`. Cualquier recorte del scaffolding SHALL
dejar ese glue intacto.

#### Scenario: Un recorte de scaffolding preserva el glue

- **WHEN** se ejecuta un cambio que recorta el meta-layer
- **THEN** `.claude/commands/opsx/*` y `.claude/skills/openspec-*` quedan sin modificar
- **AND** `openspec validate --strict` y los comandos `/opsx:*` siguen funcionando igual
