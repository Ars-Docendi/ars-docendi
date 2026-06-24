## Why

Los tres workflows de deploy (`deploy-staging.yml`, `deploy-prod.yml`, `pr-env-deploy.yml`) buildean imágenes, las pushean al registro y corren el spin-up **incondicionalmente** en cada `push` a `develop`/`main` o en cada `pull_request`, sin mirar qué cambió. Eso provoca redeploys completos para cambios que **no afectan el artefacto desplegable** (docs, specs de OpenSpec, `*.md`).

Caso concreto que motiva el change: el merge del [PR #12](https://github.com/Ars-Docendi/ars-docendi/pull/12) sólo tocó `openspec/changes/**/spec.md`, pero igual disparó `deploy-staging` (build de imágenes frontend+backend + spin-up). Eso desperdicia tiempo de runner self-hosted, ensucia el historial de deploys y arriesga un redeploy de un ambiente real sin ningún cambio de código detrás.

## What Changes

- Los workflows de deploy SÓLO redespliegan cuando el cambio toca paths que afectan el artefacto desplegable: **`backend/**`, `frontend/**`, `infra/**`, `database/**`** (más los manifiestos de workspace que alteran el build: `package.json`, `pnpm-lock.yaml`, `pnpm-workspace.yaml`). Un cambio doc-only / openspec-only **no** redeploya.
- **`deploy-staging.yml`** y **`deploy-prod.yml`** (trigger `push` a `develop`/`main`): se agrega filtrado por paths para que el deploy quede gateado por los cambios relevantes.
- **`pr-env-deploy.yml`** (trigger `pull_request`, ya doble-gateado por label de maintainer + environment con required reviewers): se agrega el mismo filtro de paths como defensa en profundidad y por consistencia.
- El concern de **base de datos** se cubre con un trigger `database/**` propio: el repo tiene un directorio `database/` con migraciones SQL versionadas (`database/audit/001_audit_schema.sql`, `database/identity/001_identity_users.sql`, etc.). Un cambio de schema/migración SÍ es desplegable y debe redeployar. (Además, lo que viva en `backend/src/Modules.*/Infrastructure/` queda cubierto por `backend/**` y la provisión/seed en `infra/scripts/` por `infra/**`.)
- `ci.yml` **no** se modifica: ya implementa correctamente el patrón de detección de cambios con `dorny/paths-filter`. Sirve de referencia de consistencia.
- Documentación: se actualiza la descripción del pipeline de deploy donde corresponda (`infra/README.md` y/o `docs/architecture/infrastructure.md`) reflejando el gating por paths.

## Capabilities

### New Capabilities

- `deploy-condicional-por-paths`: los pipelines de CD (staging, prod, pr-N efímero) sólo ejecutan build+push+spin-up cuando el conjunto de cambios incluye paths que afectan el artefacto desplegable (`backend/**`, `frontend/**`, `infra/**`, `database/**` y manifiestos de workspace); en caso contrario el deploy se omite sin marcar fallo.

### Modified Capabilities

<!-- Ninguna: no hay specs vigentes en openspec/specs/ que describan los pipelines de deploy. El único spec consolidado es estructura-scaffolding-repo, que no cubre el comportamiento de los workflows de CD. -->

## Impact

- **Archivos afectados**: `.github/workflows/deploy-staging.yml`, `.github/workflows/deploy-prod.yml`, `.github/workflows/pr-env-deploy.yml`. Docs: `infra/README.md` y/o `docs/architecture/infrastructure.md`.
- **Trigger cubierto**: `database/**` contiene migraciones SQL versionadas (`audit/`, `identity/`), por eso entra como path desplegable propio.
- **Sin impacto en código de aplicación** (backend/frontend), en el grafo de dependencias ni en APIs públicas de módulos. No toca normativa institucional (sin `BR-*`).
- **Riesgo principal**: un filtro mal calibrado podría omitir un deploy necesario (falso negativo) o, si se usa branch protection sobre el check de deploy, un workflow omitido podría no crear el check y bloquear merges. El diseño debe elegir el mecanismo (filtro a nivel `on.push.paths` vs. job `changes` + `if`) sopesando este tradeoff. Ver `design.md`.
- **Rollback**: revertir los workflows a su versión previa (deploy incondicional) es un único `git revert` del PR; no hay estado persistente que migrar.
