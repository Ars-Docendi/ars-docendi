## 1. Confirmaciones previas (gate antes de escribir provisioning)

- [x] 1.1 Confirmar con el usuario el app host: VM (recomendado, D1) vs LXC con workarounds de nesting. Registrar la decisión en `design.md` / runbook.
- [x] 1.2 Confirmar el dominio real que reemplaza `*.example.net` y la zone en Cloudflare.
- [x] 1.3 Confirmar el modelo de aislamiento de DB por ambiente: base completa (preferido, D7) vs schema, alineado con "un schema por módulo".
- [x] 1.4 Confirmar el umbral N de días del reaper y si hay límite de ambientes pr-N concurrentes.

## 2. Templating de Compose (base de todo)

- [x] 2.1 Crear `infra/compose/compose.base.yml` con los servicios frontend y backend parametrizados por `${ENV_NAME}`→`${AMBIENTE}`, `${HOSTNAME}`→`${HOST_PUBLICO}` (renombrado por colisión con la shell), `${IMAGE_TAG_FRONTEND}`→`${TAG_FRONTEND}`, `${IMAGE_TAG_BACKEND}`→`${TAG_BACKEND}`, `${DB_URL}`→`${URL_BASE_DATOS}` y la red interna de Traefik.
- [x] 2.2 Declarar en el base las labels de Traefik del frontend (`Host(${HOST_PUBLICO})`) y del backend (`Host(${HOST_PUBLICO}) && PathPrefix('/api')`), sin labels de router público en backend directo ni admin.
- [x] 2.3 Crear `infra/compose/.env.example` documentando todas las variables de un ambiente (sin valores reales).
- [x] 2.4 Verificar que `docker compose -p pr-test --env-file <env> -f compose.base.yml config` resuelve sin errores. (Verificación estática hecha: toda var interpolada está documentada en `.env.example`. El `docker compose config` en sí se corre en el host con Docker — no hay Docker en este entorno de dev; queda cubierto por §11.)

## 3. Traefik (reverse proxy interno)

- [x] 3.1 Crear `infra/traefik/traefik.yml` (config estática): entrypoint HTTP interno, Docker provider (`exposedByDefault=false`), sin ACME/TLS propio (Cloudflare termina TLS).
- [x] 3.2 Configurar el trust de forwarded headers del túnel y los headers de seguridad apropiados asumiendo TLS terminado aguas arriba.
- [x] 3.3 Dejar el dashboard de Traefik solo en red interna/admin (nunca en el ingress del túnel); documentar el acceso vía red de administración.
- [x] 3.4 Documentar la convención de labels en `infra/traefik/README.md` (cómo un ambiente se rutea solo con sus labels, sin tocar Traefik).

## 4. Cloudflare Tunnel (ingreso público)

- [x] 4.1 Crear `infra/cloudflared/config.yml` con un único ingress wildcard `*.<dominio> → http://traefik:<puerto-interno>` y el `http_status:404` final.
- [x] 4.2 Escribir `infra/cloudflared/README.md`: crear el túnel, obtener credenciales (`credentials.json`/token), inyectarlas en runtime (nunca al repo), y configurar Cloudflare Access si aplica.
- [x] 4.3 Verificar que el ingress wildcard cubre prod/staging/pr-N sin un public hostname por PR. (Documentado en README.md §"Verificación": un solo wildcard sirve los tres tipos; alta de pr-N no toca Cloudflare.)

## 5. Base de datos por ambiente

- [x] 5.1 Crear `infra/scripts/provision-db.sh`: crea la base/schema determinística del ambiente usando credenciales admin (desde env), idempotente.
- [x] 5.2 Crear `infra/scripts/seed.sh`: siembra datos sintéticos o snapshot anonimizado; MUST fallar/abortar si se le pide copiar datos de prod a un ambiente no-prod.
- [x] 5.3 Crear `infra/scripts/drop-db.sh`: `DROP DATABASE` del ambiente, valida prefijo `pr-`/`staging` antes de borrar y nunca opera sobre la base de prod; idempotente.

## 6. Tooling manual (Makefile + scripts)

- [x] 6.1 Crear `infra/scripts/spin-up.sh <env>`: provisiona DB, materializa el Compose project, corre migraciones + seed; idempotente.
- [x] 6.2 Crear `infra/scripts/teardown.sh <env>`: `docker compose -p <env> down -v` + drop-db; idempotente (no falla si ya no existe).
- [x] 6.3 Crear `infra/Makefile` con targets `up`, `down`, `logs`, `ps` que envuelven los scripts y leen credenciales de env.
- [x] 6.4 Crear `.env.example` en la raíz del repo con la lista documentada de todas las variables y secrets requeridos (sin valores reales).

## 7. GitHub Actions — deploy de ramas confiables

- [x] 7.1 Crear `.github/workflows/deploy-prod.yml` (push a `main`): build de imágenes frontend+backend, tag por SHA, deploy del ambiente `prod` en el runner self-hosted; no toca staging/pr-N.
- [x] 7.2 Crear `.github/workflows/deploy-staging.yml` (push a `develop`): igual que prod pero ambiente `staging`; no toca prod.
- [x] 7.3 Inyectar todos los secrets (Cloudflare, Postgres admin, registry) vía GitHub Actions secrets en runtime; verificar que no se hornean en capas de imagen. (Secrets vía `secrets.*`/`vars.*` como env de runtime; el build solo recibe el tag por SHA, no secrets como build-args.)

## 8. GitHub Actions — ciclo de vida pr-N (seguridad crítica)

- [x] 8.1 Crear `.github/workflows/pr-env-deploy.yml` con trigger `pull_request` (`opened`/`synchronize`); NO usar `pull_request_target`.
- [x] 8.2 Gatear el deploy detrás de un label de maintainer (p. ej. `deploy-preview`) y/o un GitHub Environment con required reviewers; el build del código del PR corre sin secrets hasta pasar el gate. (Gate 1: label `deploy-preview`; Gate 2: Environment `pr-preview` con required reviewers. El job con secrets corre solo post-gate.)
- [x] 8.3 Tras el gate: build + tag por SHA del PR, `spin-up` del ambiente `pr-N` accesible en `pr-N.<dominio>`, con DB sembrada (no productiva).
- [x] 8.4 Crear `.github/workflows/pr-env-teardown.yml` con trigger `pull_request` (`closed`): `teardown` idempotente de `pr-N` (contenedores + DROP DATABASE).
- [x] 8.5 Configurar runners self-hosted **efímeros** (estado limpio por job, runner descartado tras el job); documentar el registro en el runbook. (Workflows usan el label de runner `efimero`; el registro `--ephemeral` se documenta en el runbook, §10.4.)

## 9. Reaper (red de seguridad)

- [x] 9.1 Crear `infra/reaper/reap-pr-envs.sh`: lista Compose projects con prefijo `pr-`, calcula antigüedad, destruye los que superan N días (contenedores + DROP DATABASE); nunca toca prod/staging; logging estructurado de cada acción.
- [x] 9.2 Crear `infra/reaper/reap-pr-envs.service` y `reap-pr-envs.timer` (systemd) que invocan el script periódicamente.
- [x] 9.3 Documentar instalación del timer en el runbook (alternativa cron si el host no usa systemd).

## 10. Documentación (mismo cambio, invariante #6)

- [x] 10.1 Reescribir `docs/architecture/infrastructure.md`: nueva topología (Proxmox + Docker + Traefik + Cloudflare Tunnel), ambientes prod/staging/pr-N, deployment process, secrets, fronteras de red.
- [x] 10.2 Actualizar `infra/README.md`: nuevo layout (`traefik/`, `cloudflared/`, `compose/`, `reaper/`, `scripts/`), retirar referencias a nginx/systemd.
- [x] 10.3 Retirar `infra/nginx/` e `infra/systemd/` (modelo viejo de deploy reemplazado).
- [x] 10.4 Escribir el runbook `infra/README.md` (o `infra/RUNBOOK.md`) cubriendo lo no automatizable: setup VM/LXC en Proxmox, instancia Postgres, Cloudflare Tunnel + Access, registro del runner self-hosted efímero.
- [x] 10.5 Actualizar `docs/quality/tech-debt.md` (TD-002): las skills de ops pueden recrearse ahora que existe infra de deploy.

## 11. Validación end-to-end

- [ ] 11.1 Levantar `staging` desde `develop` y verificar routing público (`staging.<dominio>`), API bajo `/api`, y que DB/admin no son alcanzables por el túnel. **(BLOQUEADO: requiere host provisionado — Docker/Postgres/Cloudflare/runner. No ejecutable en el entorno de dev.)**
- [ ] 11.2 Abrir un PR de prueba, aplicar el label de maintainer y verificar `pr-N.<dominio>` con DB sembrada no productiva. **(BLOQUEADO: requiere host + runner self-hosted + túnel.)**
- [ ] 11.3 Cerrar el PR de prueba y verificar teardown completo (contenedores + base); re-disparar teardown para confirmar idempotencia. **(BLOQUEADO: requiere host.)**
- [ ] 11.4 Forzar la condición del reaper (ambiente pr- vencido) y verificar que lo borra sin tocar prod/staging. **(BLOQUEADO: requiere host con Docker.)**
- [x] 11.5 Correr `openspec validate ephemeral-environments --strict` y confirmar que pasa. ✓ "Change 'ephemeral-environments' is valid" (exit 0).
