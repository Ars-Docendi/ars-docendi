# Infra — plataforma de ambientes efímeros

Plataforma de deploy estilo Bunnyshell sobre **un único nodo Proxmox**. Produce
tres clases de ambiente público: **prod** (desde `main`), **staging** (desde
`develop`) y **pr-N** efímeros (uno por PR, destruidos al cerrarlo).

Stack: **Docker Compose + Traefik + Cloudflare Tunnel**. Reemplaza el modelo viejo
(nginx + certbot + systemd + Kestrel), ya retirado. Topología completa en
[docs/architecture/infrastructure.md](../docs/architecture/infrastructure.md).

## Layout

```
infra/
├── compose/
│   ├── compose.base.yml      # definición de servicios (frontend+backend), parametrizada
│   └── .env.example          # variables de UN ambiente
├── traefik/
│   ├── traefik.yml           # config estática (entrypoints, docker provider, sin ACME)
│   ├── dynamic/headers-seguridad.yml
│   └── README.md             # convención de labels de routing
├── cloudflared/
│   ├── config.yml            # ingress wildcard único
│   └── README.md             # crear túnel + credenciales
├── scripts/
│   ├── _comun.sh             # helpers (logging, validación, nombres de base)
│   ├── provision-db.sh       # crea base + rol del ambiente (idempotente)
│   ├── seed.sh               # siembra datos sintéticos (aborta si datos de prod)
│   ├── drop-db.sh            # DROP DATABASE (solo staging/pr-N, nunca prod)
│   ├── spin-up.sh <env>      # provisiona + levanta + migra + siembra
│   ├── teardown.sh <env>     # down -v + drop-db (idempotente)
│   └── seed-data/sintetico.sql
├── reaper/
│   ├── reap-pr-envs.sh       # destruye pr-N > N días
│   ├── reap-pr-envs.service
│   └── reap-pr-envs.timer
├── runners/
│   ├── respawn-efimero.sh                  # registra+corre 1 runner efímero (token auto)
│   └── arsdocendi-runner-efimero@.service  # unit template systemd (N instancias)
├── Makefile                  # up / down / logs / ps
└── README.md                 # este archivo + runbook
```

CI relacionada en `.github/workflows/`: `deploy-prod`, `deploy-staging`,
`pr-env-deploy`, `pr-env-teardown`.

## Operación manual

```bash
cd infra
# (exportar antes las variables de .env.example raíz)
make up   AMBIENTE=pr-123
make logs AMBIENTE=pr-123
make ps   AMBIENTE=pr-123
make down AMBIENTE=pr-123
```

---

# Runbook — provisioning del host (una sola vez)

Pasos **manuales** que dejan el nodo listo para que la CI (o `make up`) deploye
ambientes. Se hacen una vez, en orden. Decisiones registradas en el change
`ephemeral-environments` (design.md).

Orden de dependencias: redes → Postgres → Traefik → cloudflared → runners →
reaper. Traefik debe estar arriba **antes** que cualquier ambiente (los rutea); y
cloudflared **después** de Traefik (lo tiene como upstream).

## 1. App host: VM en Proxmox (D1)

Docker corre en una **VM dedicada** (no LXC: el nesting de Docker-in-LXC es frágil).

1. Crear la VM (Debian/Ubuntu LTS), CPU/RAM acorde a los pr-N esperados.
2. Instalar Docker Engine + Compose plugin.
3. Crear las redes externas compartidas (las consumen todos los servicios):
   ```bash
   docker network create traefik
   docker network create arsdocendi-datos
   ```
4. Clonar el repo en `/opt/ars-docendi/repo` en la rama **`main`** (línea estable).
   De este checkout salen los configs montados (Traefik, cloudflared), los units
   de systemd (reaper/runners) y el script del reaper, que corre `DROP DATABASE`
   con credenciales admin → debe ser código revisado de `main`, nunca una rama de
   trabajo. La CI **no** usa este checkout (cada job hace su propio `checkout`).

   Re-sincronizar tras cambios de infra:

   ```bash
   git -C /opt/ars-docendi/repo pull --ff-only origin main
   sudo systemctl daemon-reload   # si cambiaron units de reaper/runners
   ```

## 2. Postgres compartido

Una sola instancia para prod/staging/pr-N, en la red `arsdocendi-datos`, nombre
`arsdocendi-postgres` (= `PGHOST`), volumen persistente. El usuario **admin**
(`CREATE/DROP DATABASE`) es distinto del de la app; su password es secret.

> **Frontera de red**: NO se publica 5432 (sin `-p`). Postgres solo es alcanzable
> por la red interna de Docker, nunca por el host público ni el túnel.

```bash
read -rs PGADMIN_PASSWORD && export PGADMIN_PASSWORD   # no queda en el historial

docker run -d \
  --name arsdocendi-postgres \
  --network arsdocendi-datos \
  --restart unless-stopped \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD="${PGADMIN_PASSWORD}" \
  -e POSTGRES_DB=postgres \
  -v arsdocendi-pgdata:/var/lib/postgresql \
  --health-cmd='pg_isready -U postgres' \
  --health-interval=10s --health-timeout=5s --health-retries=5 \
  postgres:18-alpine

docker inspect -f '{{.State.Health.Status}}' arsdocendi-postgres   # -> healthy
docker port arsdocendi-postgres                                    # -> (vacío)
```

Credenciales admin para scripts/CI: `PGHOST=arsdocendi-postgres`, `PGPORT=5432`,
`PGUSER=postgres`, `PGPASSWORD=<el de arriba>` (secret de CI). Aislamiento: **una
base por ambiente** (D7), `arsdocendi_<env>`; las crea/borra `provision-db.sh` /
`drop-db.sh`.

> **psql vía contenedor**: como 5432 no se publica y `arsdocendi-postgres` solo
> resuelve dentro de `arsdocendi-datos`, los scripts NO usan un `psql` del host —
> corren el cliente en un contenedor efímero adjunto a esa red (`psql_en_docker`
> en `scripts/_comun.sh`). El host del runner solo necesita Docker, no
> `postgresql-client`. Override: `RED_DATOS`, `IMAGEN_PSQL`.

## 3. Traefik (reverse proxy interno)

Rutea el túnel hacia el contenedor de cada ambiente leyendo sus labels vía el
Docker provider. Corre como su propio contenedor en la red `traefik`. No gestiona
TLS (lo termina Cloudflare). Detalle en [traefik/README.md](traefik/README.md).

```bash
docker run -d \
  --name traefik \
  --network traefik \
  --restart unless-stopped \
  -p 127.0.0.1:8080:8080 \
  -v /var/run/docker.sock:/var/run/docker.sock:ro \
  -v /opt/ars-docendi/repo/infra/traefik/traefik.yml:/etc/traefik/traefik.yml:ro \
  -v /opt/ars-docendi/repo/infra/traefik/dynamic:/etc/traefik/dynamic:ro \
  traefik:v3
```

> El entrypoint `web` (:80) **no** se publica al host: su único upstream es
> cloudflared por la red interna. El dashboard queda en `127.0.0.1:8080` (solo
> loopback); para verlo, SSH port-forward (ver [traefik/README.md](traefik/README.md)).

Verificar: `docker logs traefik` sin errores y el contenedor `healthy`/`Up`.

## 4. Cloudflare Tunnel + cloudflared

Único ingreso público: un túnel con **un ingress wildcard** sirve prod/staging/pr-N.
Detalle en [cloudflared/README.md](cloudflared/README.md).

1. Crear el túnel y el DNS (una vez):
   ```bash
   cloudflared tunnel login
   cloudflared tunnel create arsdocendi          # genera ~/.cloudflared/<ID>.json
   cloudflared tunnel route dns arsdocendi "*.<dominio>"
   ```
2. En `cloudflared/config.yml`: poner el `<TUNNEL_ID>` en `tunnel:` y reemplazar el
   placeholder `example.net` por el dominio real (no se versiona).
3. Levantar cloudflared en la red `traefik` (para alcanzar a Traefik por nombre),
   inyectando las credenciales del túnel (nunca al repo):
   ```bash
   docker run -d \
     --name cloudflared \
     --network traefik \
     --restart unless-stopped \
     -v /opt/ars-docendi/repo/infra/cloudflared/config.yml:/etc/cloudflared/config.yml:ro \
     -v /etc/cloudflared/credentials.json:/etc/cloudflared/credentials.json:ro \
     cloudflare/cloudflared:latest tunnel --config /etc/cloudflared/config.yml run
   ```
   Alternativa por token: `... cloudflare/cloudflared:latest tunnel run --token <TOKEN>`
   (token desde secret del host, sin montar config/credenciales).
4. (Recomendado) **Cloudflare Access** para staging/pr-N restringido al equipo
   (panel Zero Trust, sin cambios en el repo).

Verificar: `cloudflared` conectado (`docker logs cloudflared` → "Registered tunnel
connection") y `curl -I https://staging.<dominio>` responde vía Traefik.

## 5. Runners self-hosted

Los jobs de deploy corren comandos locales (`docker compose`, `spin-up.sh`, el
Postgres interno), así que la propia VM actúa de runner. Dos pools separados por
label (GitHub matchea por **todas** las labels pedidas):

| Pool                        | Labels                               | Workflows                                          |
| --------------------------- | ------------------------------------ | -------------------------------------------------- |
| **Persistente** (confiable) | `self-hosted, arsdocendi, confiable` | `deploy-prod`, `deploy-staging`, `pr-env-teardown` |
| **Efímero** (código de PR)  | `self-hosted, arsdocendi, efimero`   | `pr-env-deploy`                                    |

El efímero usa `--ephemeral` (1 job y se desregistra): obligatorio porque
`pr-env-deploy` corre código de PR con secrets, sin reutilizar workspace ni
credenciales entre jobs.

`config.sh`/`run.sh` salen del **paquete del runner** (_repo → Settings → Actions →
Runners → New self-hosted runner_). El **registration-token** (vida ~1h, un uso) se
copia de ahí o se pide a la API con un **PAT fine-grained** (`Administration: RW`):

```bash
curl -fsS -X POST \
  -H "Authorization: Bearer <GH_PAT>" -H "Accept: application/vnd.github+json" \
  "https://api.github.com/repos/<owner>/<repo>/actions/runners/registration-token" \
  | jq -r .token
```

### 5a. Pool persistente (una vez)

```bash
cd /opt/actions-runner-confiable     # paquete ya descomprimido
./config.sh --url https://github.com/<org>/<repo> --token <RUNNER_TOKEN> \
            --name arsdocendi-confiable --labels arsdocendi,confiable --unattended
sudo ./svc.sh install && sudo ./svc.sh start
```

### 5b. Pool efímero (auto-respawn, sin tocar nada por PR)

[`respawn-efimero.sh`](runners/respawn-efimero.sh) pide un token fresco, registra
con `--ephemeral`, corre 1 job y sale; el unit template
[`arsdocendi-runner-efimero@.service`](runners/arsdocendi-runner-efimero@.service)
(`Restart=always`) lo relanza. El `@` permite N instancias en paralelo
(`@1`, `@2`, …) → la concurrencia de pr-N es cuántas instancias activás.

```bash
# 1. Secret del host (600, fuera del repo):
sudo install -m 600 /dev/null /etc/ars-docendi/runner.env
sudo $EDITOR /etc/ars-docendi/runner.env
#   GH_OWNER=Ars-Docendi  GH_REPO=<repo>  GH_PAT=<PAT Administration RW>
#   RUNNER_LABELS=arsdocendi,efimero

# 2. Extraer el paquete en un dir por instancia (p. ej. 3 concurrentes):
for i in 1 2 3; do
  sudo mkdir -p /opt/actions-runners/efimero-$i
  sudo tar xzf actions-runner-linux-x64-*.tar.gz -C /opt/actions-runners/efimero-$i
  sudo chown -R arsdocendi-runner: /opt/actions-runners/efimero-$i
done

# 3. Instalar el unit y levantar las instancias:
sudo cp /opt/ars-docendi/repo/infra/runners/arsdocendi-runner-efimero@.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now arsdocendi-runner-efimero@{1,2,3}
sudo systemctl status 'arsdocendi-runner-efimero@*'   # verificar
```

> **Aislamiento**: estos runners corren código de PRs en la misma VM que prod. Lo
> efímero evita el _state-bleed_ entre jobs pero no aísla a nivel host; si el riesgo
> lo amerita, mover el pool efímero a una VM aparte.

### 5c. Gates de seguridad pr-N (en GitHub, no en el host)

- Label `deploy-preview` (la aplica un maintainer) — primer gate.
- Environment `pr-preview` con **required reviewers** — segundo gate (aprobación manual).
- Nunca `pull_request_target` (evita filtrar secrets a código de forks).

## 6. Reaper (red de seguridad: destruye pr-N huérfanos)

```bash
sudo cp /opt/ars-docendi/repo/infra/reaper/reap-pr-envs.{service,timer} /etc/systemd/system/
sudo install -m 600 /dev/null /etc/ars-docendi/reaper.env
sudo $EDITOR /etc/ars-docendi/reaper.env   # PGHOST PGPORT PGUSER PGPASSWORD REAPER_MAX_DIAS=7
sudo systemctl daemon-reload
sudo systemctl enable --now reap-pr-envs.timer
sudo systemctl list-timers reap-pr-envs.timer   # verificar
```

Logs: `journalctl -u reap-pr-envs`. **Alternativa cron** (host sin systemd):

```cron
# /etc/cron.d/arsdocendi-reaper — diario 04:00
0 4 * * * root . /etc/ars-docendi/reaper.env; /opt/ars-docendi/repo/infra/reaper/reap-pr-envs.sh >> /var/log/arsdocendi-reaper.log 2>&1
```

## 7. Verificación end-to-end

Con todo arriba, deployar un ambiente descartable y comprobar el camino completo:

```bash
make up AMBIENTE=staging        # o disparar deploy-staging desde CI
curl -I https://staging.<dominio>     # 200 vía Cloudflare → Traefik → frontend
curl -I https://staging.<dominio>/api/<modulo>/ping   # 200 (ruteo /api al backend)
```

> Seed: `spin-up.sh` siembra automáticamente con `seed.sh`
> (`scripts/seed-data/sintetico.sql`) en todo ambiente **no-prod**. Regla dura:
> `seed.sh` aborta si se le pide copiar la base de prod a un ambiente no-prod.

## Empaquetado de la app (resuelto en el change `containerizar-app`)

El empaquetado que esta plataforma consume vive en el repo:

- `backend/Dockerfile` (+ `.dockerignore`) → imagen `arsdocendi-backend`, escucha en `8080`.
- `frontend/Dockerfile` (+ `.dockerignore` + `nginx.conf`) → imagen `arsdocendi-frontend`, sirve el SPA en `80`.
- El backend soporta el comando de migraciones one-shot que invoca `spin-up.sh`
  (`COMANDO_MIGRACIONES`, default `dotnet ArsDocendi.Host.dll --migrate`): aplica
  las migraciones de los 4 módulos y termina sin levantar el web server.

Detalle del contrato app↔infra (clave de connection string `ArsDocendi`, mecanismo
`--migrate`) en [docs/architecture/data-model.md](../docs/architecture/data-model.md).
