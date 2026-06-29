## Why

Hoy `docs/architecture/infrastructure.md` describe un deploy de un solo ambiente sobre una VM con nginx + systemd + Kestrel, todo TBD. No tenemos forma de validar un PR en un ambiente real antes de mergear, ni un staging vivo, y el modelo planeado no escala a ambientes por-PR.

Necesitamos una plataforma de deploy estilo Bunnyshell sobre un único nodo Proxmox que dé tres clases de ambiente público: **prod** (desde `main`), **staging** (desde `develop`) y **pr-N** efímeros (uno por pull request abierto, destruido al cerrar el PR). Esto permite revisar cada PR contra un ambiente real con su propia base de datos, sin tocar producción, y sin trabajo manual de routing por ambiente.

## What Changes

- **BREAKING (planning)**: Se reemplaza la topología planeada de `infrastructure.md` (nginx + certbot + systemd + Kestrel en una VM) por **Docker Compose + Traefik + Cloudflare Tunnel** sobre un host Docker en Proxmox. `infrastructure.md` e `infra/README.md` se reescriben en el mismo cambio; los samples `infra/nginx/` y `infra/systemd/` se retiran (la app corre en contenedores, no como systemd unit).
- **Modelo de ambientes**: prod / staging / pr-N como **un Compose project por ambiente** (`docker compose -p pr-123 ...`). Cada ambiente es un hostname bajo el dominio wildcard `*.example.net`.
- **Routing por labels**: Traefik como reverse proxy interno con el provider de Docker; un ambiente nuevo no requiere cambios en Traefik ni en Cloudflare (todo por labels en el contenedor).
- **Ingreso público**: Cloudflare Tunnel con un único ingress wildcard (`*.example.net → cloudflared → Traefik → contenedor por Host header`). Cloudflare termina TLS; Traefik corre en HTTP interno. **Bases de datos y puertos de administración nunca se exponen por el túnel.**
- **Base de datos compartida**: una instancia Postgres en su propio contenedor, con aislamiento por base/schema por ambiente. Los ambientes pr-N y staging se siembran con datos sintéticos o snapshots anonimizados — **nunca** copia de datos productivos reales.
- **CI/CD (GitHub Actions, self-hosted runners)**: deploy `main`→prod y `develop`→staging; en PR open/synchronize build + deploy `pr-N` **gated** detrás de aprobación/label de un maintainer; en PR close teardown de `pr-N` (contenedores **y** su base/schema). Runners **efímeros**, sin patrones `pull_request_target` que filtren secrets a PRs de forks.
- **Reaper**: script + systemd timer (o cron) que borra ambientes pr-N más viejos que N días, para que un webhook de close perdido no deje ambientes colgados para siempre.
- **Tooling de operación**: Makefile + scripts helper para spin-up/teardown manual, `.env.example`, y un README/runbook para lo que no se puede automatizar desde el repo (setup de la VM/LXC en Proxmox, instancia Postgres, Cloudflare Tunnel + Access, registro del runner self-hosted).

## Capabilities

### New Capabilities

- `plataforma-ambientes-efimeros`: Modelo de ambientes (prod/staging/pr-N) sobre Docker Compose, routing por labels con Traefik, ingreso wildcard vía Cloudflare Tunnel, templating de Compose por ambiente, base de datos compartida con aislamiento por ambiente, reaper de ambientes huérfanos, y las fronteras de seguridad de red (qué se expone y qué no).
- `pipeline-deploy-ci`: Workflows de GitHub Actions sobre runners self-hosted efímeros para deploy de prod/staging y el ciclo de vida completo de los ambientes pr-N (build, deploy gated por maintainer, teardown), con el manejo de secrets y el sembrado de datos no productivos.

### Modified Capabilities

<!-- Ninguna capability con requisitos vigentes cambia: la única spec existente (estructura-scaffolding-repo) describe el scaffolding del repo, no el deploy. -->

## Impact

- **Docs**: reescritura de `docs/architecture/infrastructure.md` (topología, deployment process, reverse proxy/TLS, secrets, ambientes); actualización de `infra/README.md`; nota en `docs/quality/tech-debt.md` (TD-002 sobre skills de ops). El grafo de dependencias de módulos (`dependency-graph.md`) **no** cambia: esto es infra, no toca el código de los módulos backend.
- **Repo (artefactos nuevos)**: `infra/traefik/` (config estática + dinámica), `infra/cloudflared/` (config del túnel + ingress), `infra/compose/` (base + overrides/templating por ambiente), `infra/reaper/` (script + timer), `infra/scripts/` + `Makefile`, `.env.example`. Se retiran `infra/nginx/` e `infra/systemd/`.
- **CI**: nuevos workflows en `.github/workflows/` (deploy-prod, deploy-staging, pr-env-deploy, pr-env-teardown). El `ci.yml` existente (build/test/format) no cambia.
- **Secrets requeridos** (GitHub Actions secrets, nunca en el repo): credenciales del Cloudflare Tunnel, host/credenciales del runner, credenciales admin de Postgres para crear/borrar bases por ambiente, registry creds si se usa registry. Documentados en `.env.example` y el runbook.
- **Sistemas externos**: cuenta Cloudflare (Tunnel + Access), nodo Proxmox, instancia GitHub del repo (self-hosted runner + branch protection / required reviews para el gate de maintainer).
- **Rollback**: el cambio es aditivo a nivel runtime hasta que se apunte el DNS productivo al túnel. Rollback = no migrar prod al nuevo stack y mantener el deploy actual; los ambientes pr-N/staging son descartables por diseño.
