## ADDED Requirements

### Requirement: Ciclo de vida de un change

Un change SHALL vivir en `openspec/changes/<id>/` mientras está activo y MUST moverse a `openspec/changes/archive/` al completarse, integrando sus delta specs en `openspec/specs/`.

#### Scenario: Archivar un change completado

- **WHEN** la implementación está completa y se ejecuta `/opsx:archive <id>`
- **THEN** el change se mueve a `openspec/changes/archive/` y sus delta specs se integran en `openspec/specs/`

### Requirement: Retiro de close-plan-on-merge

El cierre de un cambio SHALL hacerse con `openspec archive` y NO con `scripts/close-plan-on-merge.ts` ni el workflow `.github/workflows/close-plan-on-merge.yml`.

#### Scenario: Cierre post-merge de una feature

- **WHEN** se mergea el PR de una feature
- **THEN** el cierre del change se realiza con `openspec archive`, y la automatización anterior de cierre de planes ya no interviene
