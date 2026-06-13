## Context

Los pipelines de CD viven en `.github/workflows/`:

- `deploy-staging.yml` — trigger `push` a `develop`. Build+push de imágenes frontend+backend a `ghcr.io` (tag por SHA) + `infra/scripts/spin-up.sh staging`. Runner `[self-hosted, arsdocendi, confiable]`.
- `deploy-prod.yml` — idéntico pero trigger `push` a `main`, ambiente `prod`.
- `pr-env-deploy.yml` — trigger `pull_request` (`opened`, `synchronize`, `reopened`, `labeled`). Doble gate de maintainer (label `deploy-preview` + environment `pr-preview` con required reviewers). Runner `[self-hosted, arsdocendi, efimero]`.

Hoy ninguno filtra por paths: corren siempre. `ci.yml` sí filtra, vía un job `changes` con `dorny/paths-filter@v3` que expone outputs (`backend`, `frontend`, `docs`, `openspec`) consumidos por `if:` en los jobs downstream.

El artefacto desplegable depende de: código backend (`backend/**`), código frontend (`frontend/**`), infraestructura/compose/scripts de seed (`infra/**`), migraciones SQL de schema (`database/**`, que contiene `audit/` e `identity/` con archivos `NNN_*.sql`), y los manifiestos de workspace que alteran el build (`package.json`, `pnpm-lock.yaml`, `pnpm-workspace.yaml`). Los cambios en `docs/**`, `openspec/**`, `*.md`, `.claude/**` no afectan el binario/imagen desplegada.

## Goals / Non-Goals

**Goals:**

- Que `deploy-staging`, `deploy-prod` y `pr-env-deploy` sólo ejecuten build+push+spin-up cuando el cambio toca paths que afectan el artefacto desplegable.
- Mantener el comportamiento de gating existente de `pr-env-deploy` (label + required reviewers) intacto; el filtro de paths es defensa adicional, no lo reemplaza.
- Consistencia con la convención de paths ya establecida en `ci.yml`.

**Non-Goals:**

- No se modifica `ci.yml` (ya filtra correctamente).
- No se extrae el detect-changes a un workflow reusable compartido (posible mejora futura, fuera de alcance).
- No se cambia la lógica de build, los tags por SHA, los runners ni los secrets.
- No se agrega un trigger `database/`: ese concern ya está cubierto por `backend/**` + `infra/**`.

## Decisions

### Decisión 1: Mecanismo de filtrado — `on.<event>.paths` a nivel workflow

Se filtra con la clave nativa `paths:` en el trigger de cada workflow (no con un job `changes` + `if`).

- `deploy-staging.yml` / `deploy-prod.yml`: `on.push.paths`.
- `pr-env-deploy.yml`: `on.pull_request.paths`.

**Por qué sobre la alternativa (job `changes` + `if`, como `ci.yml`):** el filtro a nivel trigger es más simple, no gasta runner cuando no aplica, y para estos workflows de deploy no hay branch protection que requiera el check (los deploys corren post-merge sobre ramas confiables o post-gate de maintainer, no son required status checks de PR). El costo del patrón `changes+if` (un runner hosted sólo para detectar, en cada push) no se justifica acá. Se acepta el tradeoff de que un push sin paths relevantes no produzca un check de deploy visible.

**Conjunto de paths (idéntico en los tres workflows):**

```
paths:
  - 'backend/**'
  - 'frontend/**'
  - 'infra/**'
  - 'database/**'
  - 'package.json'
  - 'pnpm-lock.yaml'
  - 'pnpm-workspace.yaml'
```

### Decisión 2: `pr-env-deploy` mantiene el doble gate y suma el filtro de paths

El filtro `on.pull_request.paths` se evalúa sobre el conjunto de archivos cambiados del PR. Un PR doc-only no dispara el workflow aunque reciba el label `deploy-preview`. Esto es deseable (defensa en profundidad) y no debilita los gates existentes: si el PR sí toca paths relevantes, el flujo gate→deploy sigue igual.

### Decisión 3: `database/**` es un trigger desplegable propio

El repo tiene un directorio `database/` con migraciones SQL versionadas por schema: `database/audit/001_audit_schema.sql`, `database/identity/001_identity_users.sql` … `005_identity_user_roles.sql`. Un cambio en una migración altera el schema desplegado y DEBE redeployar, así que `database/**` entra como path desplegable de primer nivel en los tres workflows. Adicionalmente, lo que viva bajo `backend/src/Modules.*/Infrastructure/` queda cubierto por `backend/**` y la provisión/seed (`provision-db.sh`, `seed.sh`, `seed-data/sintetico.sql`) por `infra/**`.

## Risks / Trade-offs

- **[Falso negativo: un deploy necesario se omite]** → El conjunto de paths cubre las cuatro raíces desplegables (`backend`, `frontend`, `infra`, `database`) + los manifiestos de build. Si en el futuro se agrega una raíz desplegable nueva, hay que sumarla a los tres workflows. Mitigación: la capability lista el conjunto canónico y la doc del pipeline lo referencia; un cambio de estructura del repo debe actualizar ambos.

- **[Check de deploy ausente en pushes filtrados]** → Con `on.push.paths`, un push doc-only a `develop`/`main` no genera ningún run de deploy ni check. Aceptado: estos workflows no son required status checks. Si en el futuro se quisiera un check siempre-presente (verde/skipped), habría que migrar al patrón `changes+if` de `ci.yml`.

- **[Cambios mixtos]** → Un push que toca `docs/` Y `backend/` matchea el filtro y deploya (correcto: hubo cambio de código). No hay riesgo de omitir un cambio real escondido detrás de docs.

- **[Rollback]** → Revertir el PR restaura el deploy incondicional. No hay estado persistente ni migración de datos involucrada.
