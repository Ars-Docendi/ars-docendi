# deploy-condicional-por-paths

## Purpose

Los workflows de deploy (staging, prod y ambientes efímeros de PR) sólo deben ejecutar build, push de imágenes y spin-up cuando el push o PR toca paths que realmente afectan el artefacto desplegado. Cambios que sólo tocan documentación o planning (`docs/**`, `openspec/**`, `*.md`) no deben redeployar. Esto evita despliegues innecesarios, ahorra minutos de CI y reduce el riesgo de interrumpir ambientes por cambios no funcionales, sin debilitar los gates de maintainer existentes.

## Requirements

### Requirement: Deploy de staging gateado por paths desplegables

El workflow `deploy-staging.yml` (trigger `push` a `develop`) SHALL ejecutar build+push de imágenes y spin-up SOLO cuando el conjunto de archivos del push incluya al menos un path desplegable: `backend/**`, `frontend/**`, `infra/**`, `database/**`, `package.json`, `pnpm-lock.yaml` o `pnpm-workspace.yaml`. Si el push no toca ninguno de esos paths, el deploy NO SHALL ejecutarse.

#### Scenario: Push solo-docs no redeploya staging

- **WHEN** se mergea a `develop` un cambio que sólo toca `openspec/**`, `docs/**` o archivos `*.md`
- **THEN** el workflow `deploy-staging` no se dispara y no corre build ni spin-up

#### Scenario: Push de backend redeploya staging

- **WHEN** se mergea a `develop` un cambio que toca `backend/**` (incluyendo `backend/src/Modules.*/Infrastructure/`)
- **THEN** el workflow `deploy-staging` se dispara y corre build+push+spin-up

#### Scenario: Push de infra redeploya staging

- **WHEN** se mergea a `develop` un cambio que toca `infra/**` (compose, scripts de provisión o seed)
- **THEN** el workflow `deploy-staging` se dispara y corre build+push+spin-up

#### Scenario: Cambio mixto redeploya staging

- **WHEN** un push a `develop` toca a la vez `docs/**` y `frontend/**`
- **THEN** el workflow `deploy-staging` se dispara (el path desplegable manda)

### Requirement: Deploy de prod gateado por paths desplegables

El workflow `deploy-prod.yml` (trigger `push` a `main`) SHALL aplicar el mismo filtro de paths desplegables que staging: ejecuta build+push+spin-up SOLO si el push toca `backend/**`, `frontend/**`, `infra/**`, `database/**`, `package.json`, `pnpm-lock.yaml` o `pnpm-workspace.yaml`.

#### Scenario: Push solo-docs no redeploya prod

- **WHEN** se mergea a `main` un cambio que sólo toca `docs/**`, `openspec/**` o `*.md`
- **THEN** el workflow `deploy-prod` no se dispara

#### Scenario: Push de código redeploya prod

- **WHEN** se mergea a `main` un cambio que toca `backend/**` o `frontend/**`
- **THEN** el workflow `deploy-prod` se dispara y corre build+push+spin-up

### Requirement: Deploy de ambiente efímero gateado por paths sin debilitar los gates de maintainer

El workflow `pr-env-deploy.yml` (trigger `pull_request`) SHALL aplicar el filtro de paths desplegables (`on.pull_request.paths`) además de los gates existentes (label `deploy-preview` + environment `pr-preview` con required reviewers). El filtro de paths SHALL evaluarse sobre los archivos cambiados del PR y NO SHALL reemplazar ni debilitar el doble gate de maintainer.

#### Scenario: PR solo-docs no levanta ambiente efímero

- **WHEN** un PR sólo cambia `docs/**` u `openspec/**`, aun con el label `deploy-preview` aplicado
- **THEN** el workflow `pr-env-deploy` no dispara el deploy

#### Scenario: PR con código y gates aprobados levanta ambiente

- **WHEN** un PR toca `backend/**` o `frontend/**`, tiene el label `deploy-preview` y la aprobación del environment `pr-preview`
- **THEN** el workflow corre el gate y luego el deploy del ambiente efímero

#### Scenario: PR con código pero sin label no levanta ambiente

- **WHEN** un PR toca paths desplegables pero no tiene el label `deploy-preview`
- **THEN** el deploy no corre (el gate de maintainer sigue vigente)

### Requirement: Las migraciones de base de datos disparan deploy vía database/\*\*

El conjunto de paths desplegables SHALL incluir `database/**` como trigger propio, porque el repo versiona las migraciones SQL de schema bajo `database/` (p.ej. `database/audit/`, `database/identity/`). Un cambio en una migración altera el schema desplegado y SHALL redeployar. La provisión/seed que viva bajo `infra/scripts/` queda cubierta por `infra/**`.

#### Scenario: Cambio de migración SQL redeploya

- **WHEN** un push toca `database/identity/005_identity_user_roles.sql` u otra migración bajo `database/**`
- **THEN** el filtro `database/**` matchea y el deploy correspondiente se dispara

#### Scenario: Cambio de seed/provisión redeploya

- **WHEN** un push toca `infra/scripts/seed.sh` o `infra/scripts/seed-data/sintetico.sql`
- **THEN** el filtro `infra/**` matchea y el deploy correspondiente se dispara
