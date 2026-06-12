## Context

La topología de deploy planeada en `docs/architecture/infrastructure.md` (nginx + certbot + systemd + Kestrel sobre una VM, todo TBD) no soporta ambientes por-PR ni un staging vivo. Este cambio define una plataforma de deploy estilo Bunnyshell sobre un **único nodo Proxmox**, que produce tres clases de ambiente público para una app web (frontend + backend separados): `prod` (desde `main`), `staging` (desde `develop`) y `pr-N` efímeros.

Infra existente / planeada en el nodo (no se rediseña acá):

- **LXC 01**: AdGuard Home + Tailscale subnet router (DNS de LAN + acceso remoto fuera de banda). No se toca salvo pedido explícito.
- **Postgres** en su propio contenedor, compartido por prod, staging y todos los pr-N.
- **App host** corriendo Docker, donde viven todos los contenedores de ambientes.
- **GitHub Actions** con runners self-hosted.

Decisiones ya tomadas por el usuario (no se re-debaten, ver proposal): Docker Compose con un Compose project por ambiente; Traefik como reverse proxy interno con routing por labels; Cloudflare Tunnel apuntando solo a Traefik con un único ingress wildcard; un origin público por ambiente (frontend, API bajo `/api`); DB y puertos de admin nunca expuestos; prod/staging/pr-N todos públicos.

El dominio `*.example.net` es un **placeholder**; el dominio real se parametriza por variable de entorno y se resuelve al implementar.

## Goals / Non-Goals

**Goals:**

- Artefactos versionados en el repo (config de Traefik, cloudflared, templates de Compose, workflows de CI, reaper, scripts/Makefile, `.env.example`) que materialicen cualquier ambiente cambiando solo parámetros.
- Alta/baja de un ambiente sin tocar config de Traefik ni de Cloudflare (todo por labels + ingress wildcard).
- Ciclo de vida completo de pr-N por CI: build → deploy gated → teardown, con reaper de respaldo.
- Postura de seguridad explícita: runners efímeros, sin fuga de secrets a forks, sin datos productivos en ambientes no-prod, DB/admin nunca públicos.
- Un runbook para lo que no se puede automatizar desde el repo (Proxmox, Postgres, Cloudflare, runner).

**Non-Goals:**

- Kubernetes o cualquier orquestador distinto de Docker Compose.
- Multi-nodo / alta disponibilidad. Es un único nodo Proxmox.
- Public hostname de Cloudflare por PR (explícitamente prohibido; se usa wildcard).
- Rediseñar el LXC de AdGuard/Tailscale.
- Cambiar el código de los módulos backend. Esto es infra; el grafo de dependencias de módulos no se modifica.
- Definir el pipeline de build de las imágenes de la app más allá de "se taggean por SHA" (el Dockerfile de la app es trabajo adyacente, no de este cambio salvo lo mínimo para deployar).

## Decisions

### D1 — App host: VM, no LXC (a confirmar con el usuario)

Docker dentro de un LXC sobre Proxmox es frágil (nesting, cgroups, keyctl). El runbook y los pasos de provisioning **targetean una VM** para el app host. **Flag explícito**: confirmar antes de escribir los pasos definitivos; si el usuario prefiere LXC, se documentan los workarounds de nesting. Alternativa considerada: LXC con `nesting=1` + `keyctl=1` — rechazada por defecto por fragilidad, pero viable si el usuario lo pide.

### D2 — Un Compose project por ambiente, nombrado determinísticamente

`docker compose -p <id>` con `id ∈ {prod, staging, pr-<N>}`. El nombre del project deriva del ambiente, así el spin-up/teardown/reaper operan sobre un ambiente sin tocar otros. Alternativa: un solo Compose con todos los servicios — rechazada (acopla ciclos de vida y rompe el aislamiento).

### D3 — Templating de Compose: base + override por variables de entorno

Un `compose.base.yml` con la definición de servicios (frontend, backend) parametrizada por variables (`${ENV_NAME}`, `${HOSTNAME}`, `${IMAGE_TAG}`, `${DB_URL}`), más un mecanismo de override por ambiente. Se prefiere **interpolación de variables de entorno de Compose + un archivo `.env` por ambiente generado en runtime** sobre alternativas más pesadas (Helm-like, envsubst de YAML completo) por simplicidad y porque Compose ya soporta `--env-file`. Las labels de Traefik se declaran en el base usando las mismas variables, de modo que el Host se fija por ambiente sin duplicar YAML. Alternativa considerada: un `docker-compose.override.yml` por ambiente versionado — rechazada porque duplicaría la definición de servicios.

### D4 — Traefik con Docker provider, TLS terminado aguas arriba

Traefik corre en el app host, escucha en un entrypoint HTTP interno (no expone 443 con certificados propios) y descubre contenedores por el Docker provider leyendo labels. Cloudflare termina TLS; cloudflared reenvía HTTP a Traefik. Traefik se configura para confiar en los forwarded headers del túnel y **no** gestiona ACME/Let's Encrypt para estos hostnames (lo hacía el diseño viejo con nginx+certbot). El dashboard de Traefik y el socket de Docker quedan en red interna, nunca en el ingress del túnel. Alternativa: Traefik con ACME propio — rechazada, Cloudflare ya termina TLS y emitir certs duplicaría y expondría superficie.

### D5 — Routing por Host header vía labels

Cada contenedor frontend declara `traefik.http.routers.<env>.rule=Host(\`${HOSTNAME}\`)`. La API pública se rutea por `Host(...) && PathPrefix(\`/api\`)`al backend del mismo ambiente. Así un ambiente nuevo se rutea solo con sus labels; Traefik y Cloudflare quedan estáticos. El backend directo (sin`/api`) y los puertos de admin no llevan label de router público.

### D6 — Cloudflare Tunnel: un ingress wildcard

`cloudflared` corre con un único ingress rule `*.example.net → http://traefik:<puerto-interno>` (más el `service: http_status:404` final). El routing fino lo hace Traefik por Host. Esto cumple "cero cambios en Cloudflare por PR". Las credenciales del túnel (`credentials.json` / token) viven fuera del repo, inyectadas en runtime. Alternativa: ingress por hostname concreto — rechazada explícitamente por el usuario.

### D7 — DB compartida con aislamiento por ambiente, datos no productivos

Una instancia Postgres; cada ambiente recibe su **propia base** (preferido sobre schema por simplicidad de drop completo en teardown) nombrada determinísticamente (p. ej. `arsdocendi_pr_123`). El deploy aprovisiona la base y corre migraciones + seed sintético/anonimizado. El teardown hace `DROP DATABASE`. prod usa su base real; staging y pr-N **nunca** reciben copia de prod. Las credenciales admin de Postgres (para CREATE/DROP DATABASE) son un secret de CI, distintas del usuario de la app. Alternativa: una instancia Postgres por ambiente — rechazada (desperdicio de recursos en un solo nodo).

### D8 — CI: separar build (no confiable) de deploy (confiable), runners efímeros

- `deploy-prod` (push a `main`) y `deploy-staging` (push a `develop`): build + deploy directo; corren con secrets porque la rama es confiable.
- `pr-env-deploy` (PR `opened`/`synchronize`): el build del código del PR corre **sin secrets**; el deploy del ambiente está **gated** por un label de maintainer (p. ej. `deploy-preview`) y/o environment con required reviewers. **No** se usa `pull_request_target`; se usa `pull_request` + gating por label + GitHub Environments con protección. Esto evita la fuga de secrets a PRs de forks.
- `pr-env-teardown` (PR `closed`): destruye contenedores + DROP DATABASE; idempotente.
- Runners **efímeros**: cada job parte de estado limpio y el runner se descarta tras el job (p. ej. runner por job vía ephemeral registration), de modo que código de un PR no observe credenciales/workspace de otro.

Alternativa considerada para el gate: `pull_request_target` con checkout del SHA del PR — rechazada por ser el patrón clásico de fuga de secrets a forks.

### D9 — Reaper como red de seguridad

Script idempotente + systemd timer (preferido sobre cron por logging vía journald y dependencias) que lista Compose projects con prefijo `pr-`, consulta su antigüedad (label de creación o metadata) y destruye los que superan N días (contenedores + DROP DATABASE). Nunca toca `prod`/`staging`. Cubre el caso de webhook de `closed` perdido. Logging estructurado de cada acción.

### D10 — Layout en el repo

```
infra/
├── traefik/         # traefik.yml (estático) + dynamic/ (si hace falta) + doc de labels
├── cloudflared/     # config.yml (ingress wildcard) + README de credenciales
├── compose/         # compose.base.yml + env templates + .env.example
├── reaper/          # reap-pr-envs.sh + .service + .timer
├── scripts/         # spin-up.sh / teardown.sh / provision-db.sh / seed.sh
├── Makefile         # targets manuales (up/down/logs)
└── README.md        # runbook (Proxmox, Postgres, Cloudflare, runner)
.github/workflows/   # deploy-prod, deploy-staging, pr-env-deploy, pr-env-teardown
```

Se **retiran** `infra/nginx/` e `infra/systemd/` (modelo viejo). Se reescribe `docs/architecture/infrastructure.md` y `infra/README.md`.

## Risks / Trade-offs

- **Docker en LXC frágil (D1)** → targetear VM por defecto; documentar workarounds solo si el usuario elige LXC.
- **Único nodo = SPOF** → aceptado; está fuera de alcance HA. El reaper y el aislamiento por ambiente limitan el blast radius entre ambientes, no fallas de hardware.
- **Fuga de secrets vía PRs de forks** → mitigado con `pull_request` (no `pull_request_target`), build sin secrets, gate por label de maintainer + GitHub Environment protegido, y runners efímeros.
- **Webhook de `closed` perdido deja ambientes colgados** → mitigado por el reaper con timer.
- **DROP DATABASE en teardown es destructivo** → mitigado porque solo aplica a bases `pr-*`/`staging` con datos sintéticos; el reaper y el teardown validan el prefijo antes de borrar y nunca operan sobre la base de prod.
- **Agotamiento de recursos del nodo con muchos PRs abiertos** → mitigado por el reaper (umbral N días) y, opcionalmente, un límite de ambientes concurrentes; recursos por contenedor acotables vía Compose.
- **Traefik confiando en headers de un upstream** → mitigado porque el único upstream es cloudflared en la red interna; Traefik no se expone directo a internet.
- **Datos anonimizados desactualizados** → el snapshot/seed es responsabilidad de mantener; documentar el proceso en el runbook.

## Migration Plan

1. Implementar artefactos en el repo (Traefik, cloudflared, compose, workflows, reaper, scripts, docs) — no afecta runtime todavía.
2. Provisionar el app host (VM Proxmox, pendiente confirmación D1), el contenedor Postgres y registrar el runner self-hosted (pasos en el runbook).
3. Crear el Cloudflare Tunnel + Access y el ingress wildcard; cargar credenciales como secrets.
4. Levantar `staging` desde `develop` y validar el flujo completo (deploy, routing, seed, teardown) en un PR de prueba.
5. Una vez validado, migrar `prod` apuntando el DNS productivo al túnel.
6. **Rollback**: hasta el paso 6, prod sigue en el deploy actual; no migrar el DNS = no impacto en prod. Los ambientes staging/pr-N son descartables, se destruyen sin consecuencia.

## Decisiones confirmadas por el usuario (gate sección 1, 2026-06-12)

Las cuatro confirmaciones previas de `tasks.md §1` quedaron resueltas antes de escribir provisioning:

- **D1 — App host: VM.** Confirmado VM dedicada para Docker en Proxmox (rechazado LXC). El runbook targetea VM; no se documentan workarounds de nesting.
- **Dominio: parametrizado.** El dominio real no se hornea en los artefactos versionados. Todo usa la variable `${DOMINIO}` (placeholder `example.net` en docs/ejemplos); el valor real vive solo en el `.env` de runtime y se documenta en el runbook.
- **D7 — Aislamiento de DB: base completa por ambiente.** `CREATE/DROP DATABASE` por ambiente (`arsdocendi_<env>`), no schema. La app mantiene "un schema por módulo" _dentro_ de la base de cada ambiente. El teardown hace `DROP DATABASE` total.
- **Reaper: 7 días, sin límite concurrente.** Umbral default `REAPER_MAX_DIAS=7`; sin tope de ambientes `pr-N` concurrentes (parametrizable luego vía env si el nodo lo necesita).

## Open Questions (remanentes, se acotan en runtime)

- Estrategia concreta del seed anonimizado: ¿snapshot anonimizado de prod periódico o fixtures sintéticas? — definir en el runbook.
- Mecanismo exacto de runners efímeros (self-hosted ephemeral vía `--ephemeral`, o un orquestador como actions-runner-controller adaptado a Docker) — acotar al implementar.
