# Infrastructure

Plataforma de deploy de Ars Docendi. Modelo estilo Bunnyshell sobre **un único
nodo Proxmox**: tres clases de ambiente público (**prod**, **staging**, **pr-N**
efímeros) sobre Docker Compose + Traefik + Cloudflare Tunnel.

> Artefactos versionados en [`infra/`](../../infra/) (compose, traefik, cloudflared,
> scripts, reaper, Makefile) y [`.github/workflows/`](../../.github/workflows/).
> El runbook de provisioning (lo no automatizable) vive en [infra/README.md](../../infra/README.md).

## Ambientes

| Ambiente | Rama / trigger     | Hostname público    | Base de datos        | Datos                   |
| -------- | ------------------ | ------------------- | -------------------- | ----------------------- |
| Local    | dev                | localhost           | `docker-compose.yml` | dev                     |
| prod     | push a `main`      | `prod.<dominio>`    | `arsdocendi_prod`    | reales                  |
| staging  | push a `develop`   | `staging.<dominio>` | `arsdocendi_staging` | sintéticos/anonimizados |
| pr-N     | PR abierto (gated) | `pr-<N>.<dominio>`  | `arsdocendi_pr_<N>`  | sintéticos/anonimizados |

Cada ambiente es un **Compose project independiente** (`docker compose -p <id>`),
aislado de los demás. El dominio real se parametriza por variable (`${DOMINIO}`);
en el repo se usa el placeholder `example.net`.

## Topología

```
                 [Internet]
                     │ HTTPS (TLS terminado en Cloudflare)
                     ▼
            ┌──────────────────┐
            │  Cloudflare      │  Tunnel + Access
            │  (wildcard *.dom)│
            └──────────────────┘
                     │ HTTP (túnel)
                     ▼
   ┌───────────────────── App host (VM Proxmox) ─────────────────────┐
   │   ┌────────────┐   un solo ingress wildcard                     │
   │   │ cloudflared│──────────────┐                                 │
   │   └────────────┘              ▼                                 │
   │                        ┌────────────┐  routing por Host (labels)│
   │                        │  Traefik   │  HTTP interno, sin ACME    │
   │                        └────────────┘                           │
   │            ┌──────────────┬──────────────┬─────────────┐        │
   │            ▼              ▼              ▼              ▼         │
   │     ┌───────────┐  ┌───────────┐  ┌───────────┐  ┌──────────┐   │
   │     │ prod      │  │ staging   │  │ pr-123    │  │  ...      │   │
   │     │ fe + be   │  │ fe + be   │  │ fe + be   │  │          │   │
   │     └─────┬─────┘  └─────┬─────┘  └─────┬─────┘  └────┬─────┘   │
   │           └──────────────┴── red arsdocendi-datos ────┘         │
   │                              ▼                                   │
   │                      ┌───────────────┐                          │
   │                      │  PostgreSQL   │  (NO expuesto al túnel)   │
   │                      │  1 base/amb.  │                          │
   │                      └───────────────┘                          │
   └─────────────────────────────────────────────────────────────────┘
```

## Routing (Traefik por labels)

Traefik descubre contenedores por el Docker provider leyendo labels. Dar de alta
un ambiente **no** requiere tocar Traefik ni Cloudflare:

- Frontend: `Host(\`<env>.<dominio>\`)` → contenedor frontend (puerto 80).
- API: `Host(\`<env>.<dominio>\`) && PathPrefix(\`/api\`)` → backend (puerto 8080), prioridad mayor.

Detalle en [infra/traefik/README.md](../../infra/traefik/README.md).

## Ingreso público y TLS

- **Cloudflare Tunnel** con un único ingress wildcard `*.<dominio> → cloudflared → Traefik`. Cero cambios en Cloudflare por PR.
- **TLS lo termina Cloudflare**; cloudflared y Traefik hablan HTTP interno. Traefik confía los forwarded headers solo del upstream interno y no gestiona ACME.
- Detalle en [infra/cloudflared/README.md](../../infra/cloudflared/README.md).

## Fronteras de red (qué NO se expone)

- **PostgreSQL**: solo alcanzable por la red interna `arsdocendi-datos`. Nunca publicado al túnel.
- **Dashboard de Traefik / socket de Docker / puertos de admin**: solo loopback / red de administración (Tailscale), nunca por el wildcard público.
- El túnel expone **un solo origin por ambiente** (el frontend); la API solo bajo `/api`.

## Base de datos

Una instancia Postgres compartida; **una base por ambiente** (D7), aislada y
nombrada determinísticamente (`arsdocendi_<env>`). La app mantiene "un schema por
módulo" dentro de cada base. Aprovisionamiento/borrado por `infra/scripts/`. Los
ambientes no-prod se siembran con datos sintéticos/anonimizados — **nunca** copia
de prod.

### Dataset sintético y autenticación de desarrollo

Después de las migraciones, `infra/scripts/seed.sh <staging|pr-N|local>` ejecuta el dataset SQL versionado `2026.08.1`. La ejecución es transaccional, serializada con advisory lock e idempotente por UUIDs reservados y upserts; reejecutarla restaura sólo sus fixtures y preserva filas ajenas. El script aborta antes de escribir si el destino es `prod` o si `SEED_FROM_DB` señala la base productiva. `SEED_SQL` permite probar otra versión explícita sin cambiar la protección.

La autenticación por `X-Dev-User-Id`/`X-Dev-Role-Code` exige simultáneamente ambiente no productivo y `DevelopmentAuthentication__Enabled=true`. Sólo acepta usuarios presentes en `public.seed_identities`, activos y con el rol solicitado vigente. La configuración de Production no habilita la opción y el Host ni siquiera registra la ruta o el esquema. El frontend también elimina el selector y el adapter dev de su bundle de producción mediante `import.meta.env.DEV`.

## Deployment process (CI/CD)

GitHub Actions sobre runners self-hosted **efímeros**:

| Workflow          | Trigger                     | Acción                                         |
| ----------------- | --------------------------- | ---------------------------------------------- |
| `deploy-prod`     | push a `main`               | build+push imágenes (tag SHA) + spin-up `prod` |
| `deploy-staging`  | push a `develop`            | build+push + spin-up `staging`                 |
| `pr-env-deploy`   | PR open/synchronize (gated) | build+push del PR + spin-up `pr-N`             |
| `pr-env-teardown` | PR closed                   | teardown `pr-N` (contenedores + DROP DATABASE) |

**Seguridad del flujo pr-N** (D8):

- Trigger `pull_request` (nunca `pull_request_target`).
- Doble gate de maintainer: label `deploy-preview` + GitHub Environment `pr-preview` con required reviewers. El código del PR no corre con secrets hasta pasar el gate.
- Runners efímeros: estado limpio por job, descartado tras el job.
- Reaper (`infra/reaper/`) como red de seguridad: destruye pr-N > N días (default 7) por si se pierde el webhook de cierre.

## Secrets management

- **Local**: `dotnet user-secrets` (backend) + `.env.local` (frontend, gitignored).
- **Deploy/CI**: todos los secrets (Cloudflare Tunnel, Postgres admin, registry, runner token) vía **GitHub Actions secrets**, inyectados en runtime como env vars. **Nunca** al repo ni horneados en capas de imagen.
- Lista documentada en [`.env.example`](../../.env.example) (raíz) y el runbook.

## Backup strategy

A definir SLA con UNLaM. Recomendación mínima para la base de **prod**:

- `pg_dump` diario automatizado, encriptado (GPG) antes de salir del nodo.
- Retención: 7 dailies + 4 weeklies + 6 monthlies.
- Restore drill mensual contra un ambiente descartable (staging/pr).
- Los ambientes staging/pr-N **no** se respaldan (son descartables, datos sintéticos).

## Hardening checklist (app host VM)

- [ ] Firewall: cerrar todo entrante salvo SSH/administración; el ingreso público es solo por el túnel (saliente).
- [ ] SSH: solo keys, sin password auth.
- [ ] `unattended-upgrades` para parches de seguridad.
- [ ] Postgres: bind a la red interna de Docker, nunca al host público.
- [ ] Docker socket: no expuesto; dashboard de Traefik solo loopback.
- [ ] Runners self-hosted **efímeros** (estado limpio por job).
- [ ] Logrotate / límites de logs de Docker para no llenar disco.
- [ ] Healthcheck externo apuntando a `https://prod.<dominio>/api/designaciones/ping`.

## Monitoring (mínimo viable)

- **Logs**: backend con Serilog → stdout → `docker logs` / `journalctl`. Scripts de infra con logging estructurado (`ts=… nivel=… clave=valor`).
- **Métricas**: endpoints `/api/<modulo>/ping` por ambiente.
- **Alertas**: uptime monitor externo sobre el ping de prod.

## Runbook

El procedimiento de provisioning manual (VM Proxmox, Postgres, Cloudflare Tunnel +
Access, runner efímero, reaper, seed) está en [infra/README.md](../../infra/README.md).
