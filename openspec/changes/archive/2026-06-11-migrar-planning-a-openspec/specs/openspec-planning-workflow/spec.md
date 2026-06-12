## ADDED Requirements

### Requirement: Planning del proyecto vía OpenSpec

El proyecto SHALL usar OpenSpec como única capa de planning. Todo cambio sustancial se planifica con el ciclo `/opsx:explore` → `/opsx:propose` → `/opsx:apply` → `/opsx:archive`, y sus artefactos (proposal, design, specs, tasks) MUST vivir en `openspec/changes/<id>/`.

#### Scenario: Planificar una feature nueva

- **WHEN** un developer quiere planificar una feature o cambio sustancial
- **THEN** ejecuta `/opsx:propose "<descripción>"` y el CLI crea `openspec/changes/<id>/` con los artefactos generados

#### Scenario: Las skills de planning anteriores ya no se usan

- **WHEN** un developer intenta planificar con `/plan-feature` o `/complete-plan`
- **THEN** esas skills ya no existen y el flujo correcto es `/opsx:propose` y `/opsx:archive`

### Requirement: openspec/ como única fuente de verdad

Las specs vigentes SHALL vivir en `openspec/specs/<capability>/spec.md` y los changes en `openspec/changes/`. `docs/product/specs/` y `docs/plans/` MUST dejar de ser fuente de verdad de planning.

#### Scenario: Ubicar la spec de una capability

- **WHEN** alguien busca el comportamiento esperado de una capability
- **THEN** lo encuentra en `openspec/specs/<capability>/spec.md`, no en `docs/product/specs/`

### Requirement: Inyección de contexto e invariantes del proyecto

`openspec/config.yaml` SHALL proveer el `context` del proyecto y las `rules` por artefacto, de modo que cada artefacto generado respete las invariantes del proyecto.

#### Scenario: Generar un artefacto con contexto inyectado

- **WHEN** se ejecuta `openspec instructions <artifact> --change <id> --json`
- **THEN** la respuesta incluye el `context` del proyecto y las `rules` del artefacto como restricciones, sin copiarse al output del artefacto
