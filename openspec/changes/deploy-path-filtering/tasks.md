## 1. Filtrado en deploy-staging

- [x] 1.1 Agregar la clave `paths:` al trigger `on.push` de `.github/workflows/deploy-staging.yml` con el conjunto desplegable (`backend/**`, `frontend/**`, `infra/**`, `database/**`, `package.json`, `pnpm-lock.yaml`, `pnpm-workspace.yaml`), conservando `branches: [develop]`

## 2. Filtrado en deploy-prod

- [x] 2.1 Agregar la misma clave `paths:` al trigger `on.push` de `.github/workflows/deploy-prod.yml`, conservando `branches: [main]`

## 3. Filtrado en pr-env-deploy

- [x] 3.1 Agregar `paths:` al trigger `on.pull_request` de `.github/workflows/pr-env-deploy.yml` con el conjunto desplegable, conservando `types: [opened, synchronize, reopened, labeled]` y sin tocar los jobs `gate`/`deploy` ni los gates de maintainer

## 4. Documentación

- [x] 4.1 Documentar el gating por paths del pipeline de deploy en `infra/README.md` (y en `docs/architecture/infrastructure.md` si describe el pipeline), listando el conjunto canónico de paths desplegables (`backend/**`, `frontend/**`, `infra/**`, `database/**` + manifiestos de workspace)

## 5. Verificación

- [x] 5.1 Correr `pnpm exec openspec validate --all --strict` y confirmar que el change valida (4/4 verde)
- [x] 5.2 Verificar con `actionlint` (o revisión manual del YAML) que los tres workflows siguen siendo válidos sintácticamente tras agregar `paths:`
- [x] 5.3 Confirmar el comportamiento esperado: un PR/merge solo-docs no dispara deploy; un cambio en `backend/**`, `frontend/**` o `infra/**` sí lo dispara
