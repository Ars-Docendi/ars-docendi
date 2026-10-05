# Infra — plataforma de ambientes efímeros

Plataforma temporal de dos hosts aislados: la PC Debian **principal** y una VM
Proxmox **secundaria**. `prod` (desde `main`) corre **sólo en Debian**, en
`https://<DOMINIO>`; `staging` (desde `develop`) y `pr-N` (uno por PR) corren
**sólo en Proxmox**, en `staging.<DOMINIO>` y `pr-N.<DOMINIO>`. Cada host tiene
su propio PostgreSQL, SeaweedFS, ClamAV y redes Docker. No hay LB, deploy dual,
replicación ni failover: una caída de Debian deja producción indisponible.
El proyecto Compose `prod`, la base `arsdocendi_prod` y el Environment `prod`
conservan sus nombres. Esta es la topología objetivo, no evidencia de activación.

Stack: **Docker Compose + Traefik + Cloudflare Tunnel**. Topología completa en
[docs/architecture/infrastructure.md](../docs/architecture/infrastructure.md).

## Layout

```
infra/
├── compose/
│   ├── compose.base.yml      # definición de servicios (frontend+backend), parametrizada
│   ├── compose.storage.yml   # SeaweedFS prod o pool compartido no-prod
│   ├── compose.antivirus.yml # ClamAV compartido por los ambientes de cada host
│   └── .env.example          # variables de un ambiente
├── traefik/
│   ├── traefik.yml           # config estática (entrypoints, docker provider, sin ACME)
│   ├── dynamic/headers-seguridad.yml
│   └── README.md             # convención de labels de routing
├── cloudflared/
│   ├── config.yml            # ingress de Proxmox (rechazo prod, staging y previews)
│   ├── config-principal.yml  # ingress de Debian (sólo dominio raíz productivo)
│   └── README.md             # crear túnel + credenciales
├── scripts/
│   ├── _comun.sh             # helpers (logging, validación, nombres de base)
│   ├── provision-db.sh       # crea base + rol del ambiente (idempotente)
│   ├── seed.sh               # siembra datos sintéticos (aborta si datos de prod)
│   ├── drop-db.sh            # DROP DATABASE (solo staging/pr-N, nunca prod)
│   ├── spin-up.sh <env>      # reconstruye descartables, migra, siembra y levanta
│   ├── teardown.sh <env>     # down -v + drop-db (idempotente)
│   ├── backup-storage.sh <env> <dir> # backup PostgreSQL + objetos S3 verificable
│   ├── restore-storage.sh <env> <dir> # restore descartable con hashes
│   └── seed-data/sintetico.sql
├── runners/
│   ├── respawn-efimero.sh                  # registra+corre 1 runner efímero (token auto)
│   └── arsdocendi-runner-efimero@.service  # unit template systemd (N instancias)
├── Makefile                  # up / down / logs / ps
└── README.md                 # este archivo + runbook
```

CI relacionada en `.github/workflows/`: `deploy-prod`, `deploy-staging`,
`pr-env-deploy`, `pr-env-teardown`.

La matriz de variables, secrets y gates de GitHub está en
[`docs/operations/github-pr-deploy.md`](../docs/operations/github-pr-deploy.md).

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

Pasos **manuales** para dejar cada host listo para que CI despliegue. Se hacen
una vez por host, en orden; los pasos de previews se hacen sólo en Proxmox.
La configuración de Cloudflare DNS/túneles y el registro de runners son
responsabilidad exclusiva del operador, fuera del repositorio. El cambio temporal está descrito en
`openspec/changes/deploy-dual-con-failover-temporal/`.

Orden de dependencias: redes → Postgres → Traefik → cloudflared → runners.
Traefik debe estar arriba **antes** que cualquier ambiente (los rutea); y
cloudflared **después** de Traefik (lo tiene como upstream).

## 1. Hosts de aplicación

La PC principal usa Debian nativo; la secundaria corre en una **VM Proxmox**
(no LXC). Debian requiere recursos para producción (PostgreSQL, SeaweedFS
prod y ClamAV); Proxmox para staging/previews (PostgreSQL, SeaweedFS no-prod
y ClamAV). No aprovisionar staging/previews en Debian ni prod nuevo en la VM.

1. Preparar ambos hosts con CPU/RAM y Docker Engine + Compose plugin; dimensionar
   Proxmox también para los `pr-N` esperados.
2. Crear **en cada host** las redes locales que comparten sus contenedores:
   ```bash
   docker network create traefik
   docker network create arsdocendi-datos
   ```
3. Clonar el repo en `/opt/ars-docendi/repo` en la rama **`main`** (línea estable)
   en ambos hosts.
   De este checkout salen los configs montados de Traefik, las plantillas de
   cloudflared (materializadas fuera de Git) y las unidades de los runners → debe ser código revisado de `main`, nunca
   una rama de trabajo. La CI **no** usa este checkout (cada job hace su propio
   `checkout`).

   Re-sincronizar tras cambios de infra:

   ```bash
   git -C /opt/ars-docendi/repo pull --ff-only origin main
   ```

   Si cambian las unidades de runners, el operador recarga/reinicia los servicios
   conforme a su política de administración; no ejecutar tareas privilegiadas
   desde esta aplicación del cambio.

## 2. PostgreSQL por host

En **cada host**, una instancia local para sus ambientes, en la red
`arsdocendi-datos`, nombre `arsdocendi-postgres` (= `PGHOST`) y volumen
persistente **local**. Sólo Proxmox aloja las bases `pr-N`. El usuario **admin**
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

Si `docker port arsdocendi-postgres` muestra `0.0.0.0:5432` o `[::]:5432`, el
host **no** cumple la frontera de red: corregir la publicación del contenedor
en una ventana de mantenimiento, preservando el volumen y verificando antes
un backup/restauración. No confundir un firewall externo con la ausencia de un
puerto Docker publicado ni recrear PostgreSQL sin plan de recuperación.

Credenciales admin para scripts/CI: `PGHOST=arsdocendi-postgres`, `PGPORT=5432`,
`PGUSER=postgres`, `PGPASSWORD=<el de arriba>` (secret de GitHub). Aislamiento:
**una base por ambiente y por host** (D7), `arsdocendi_<env>`; las crea/borra
`provision-db.sh` / `drop-db.sh`. La misma contraseña de GitHub debe ser válida
en ambas instancias independientes antes de activar cada destino. Debian usa
SeaweedFS `prod`; Proxmox usa `nonprod`. Sus volúmenes son locales: **no
montarlos mediante una ruta al otro host**. Conservar el storage productivo
anterior de la VM hasta la aceptación del corte y autorización de su tratamiento.

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

Un túnel por host: `principal` sólo admite el dominio raíz de producción;
`secundaria` admite staging y wildcard de previews, con rechazo exacto
`prod.<DOMINIO> -> http_status:404` **antes del wildcard**. Apex CNAME `@`
apunta al UUID de Debian; `*` y, opcionalmente, `staging` al UUID de Proxmox,
con proxy y flattening del apex. No crear LB ni Worker DNS. Detalle, secuencia
de corte y rollback en [cloudflared/README.md](cloudflared/README.md).

1. Identificar el UUID del túnel que ya sirve Proxmox y **reutilizarlo como
   secundario** si está operativo. Crear un UUID distinto para Debian; no
   cambiar el DNS público hasta validar ambos orígenes. En Debian partir de
   `config-principal.yml`; en Proxmox, de `config.yml`. Reemplazar `example.net`
   y cada UUID en copias protegidas **fuera del repositorio**, materializadas
   como `/etc/cloudflared/config.yml` en cada host, si los túneles son locales.
   No montar el YAML de Git con placeholders. Su `credentials-file` debe ser
   `/etc/cloudflared/credentials.json`, coincidiendo con el montaje siguiente.
   Si el existente es gestionado remotamente con token, configurar las mismas
   rutas en el panel: los YAML son referencia, no se cargan allí.
2. Levantar `cloudflared` en la red `traefik` de **cada host**, con sus
   credenciales de servicio respectivas (ejemplo para un host nuevo; adaptar
   la instalación existente de Proxmox sin reemplazarla a ciegas, **sólo para
   un túnel local con JSON de credenciales**):
   ```bash
   docker run -d \
     --name cloudflared \
     --network traefik \
     --restart unless-stopped \
     -v /etc/cloudflared/config.yml:/etc/cloudflared/config.yml:ro \
     -v /etc/cloudflared/credentials.json:/etc/cloudflared/credentials.json:ro \
     cloudflare/cloudflared:latest tunnel --config /etc/cloudflared/config.yml run
   ```
   Este runbook usa ingress **local**: no sustituir el archivo de configuración
   por un simple `tunnel run --token` salvo que las rutas estén gestionadas y
   comprobadas en el panel de Cloudflare.
3. Verificar si **Cloudflare Access** ya protege staging y `pr-N`; si no, definir
   la restricción al equipo según la política institucional (panel Zero Trust).
   Los smoke checks deben usar acceso autorizado, sin abrir staging/previews
   para sortear la política institucional.

Verificar rutas y conectividad de cada túnel, DNS público y el hostname de
cada ambiente. La respuesta HTTP por sí sola no demuestra el destino físico:
correlacionarla con logs y contenedores del host previsto. Debian debe rechazar
staging/previews; Proxmox debe rechazar raíz y, tras el corte, `prod.<DOMINIO>`.
No hay simulacro de failover ni conmutación automática.

## 5. Runners self-hosted

Los jobs de deploy corren comandos locales (`docker compose`, `spin-up.sh`, el
Postgres interno), así que cada host tiene un runner confiable. GitHub debe
seleccionar **todas** las etiquetas de la fila correspondiente:

| Host / pool         | Labels requeridas                                | Workflows                         |
| ------------------- | ------------------------------------------------ | --------------------------------- |
| Debian / confiable  | `self-hosted, arsdocendi, confiable, principal`  | deploy `prod`                     |
| Proxmox / confiable | `self-hosted, arsdocendi, confiable, secundaria` | deploy `staging`, teardown `pr-N` |
| Proxmox / efímero   | `self-hosted, arsdocendi, efimero, secundaria`   | build y deploy `pr-N`             |

El build/push de `prod` y `staging` corre **una sola vez por SHA** en un runner
GitHub-hosted. Cada workflow permanente tiene un único deploy local: prod
en principal, staging en secundaria, sin matriz de ubicaciones. Las imágenes
por SHA y los scopes de secretos GitHub existentes se conservan. No dejar un
runner viejo con sólo etiquetas genéricas como destino accidental del teardown.

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

### 5a. Runners confiables por host (una vez)

```bash
# Ejecutar sobre la PC Debian:
cd /opt/actions-runner-confiable     # paquete ya descomprimido
./config.sh --url https://github.com/<org>/<repo> --token <RUNNER_TOKEN> \
            --name arsdocendi-confiable-principal \
            --labels arsdocendi,confiable,principal --unattended
# El operador instala/inicia el servicio según su política de administración.

# En la VM Proxmox, usar su propia instalación del runner:
./config.sh --url https://github.com/<org>/<repo> --token <RUNNER_TOKEN> \
            --name arsdocendi-confiable-secundaria \
            --labels arsdocendi,confiable,secundaria --unattended
# El operador instala/inicia el servicio según su política de administración.
```

Si un runner ya está registrado, actualizarlo según el procedimiento oficial
de GitHub (no ejecutar `config.sh` dos veces en la misma instalación). Verificar
en **Settings → Actions → Runners** cuál es cada host antes de cambiar las
etiquetas. No inferirlo únicamente del nombre anterior.

### 5b. Pool efímero (auto-respawn, sin tocar nada por PR)

[`respawn-efimero.sh`](runners/respawn-efimero.sh) pide un token fresco, registra
con `--ephemeral`, corre 1 job y sale; el unit template
[`arsdocendi-runner-efimero@.service`](runners/arsdocendi-runner-efimero@.service)
(`Restart=always`) lo relanza. El `@` permite N instancias en paralelo
(`@1`, `@2`, …) → la concurrencia de pr-N es cuántas instancias activás.

Configuración **a cargo del operador, sólo en Proxmox**:

1. Provisionar `/etc/ars-docendi/runner.env` protegido (modo 600, fuera del
   repo), con `GH_OWNER`, `GH_REPO`, `GH_PAT` y
   `RUNNER_LABELS=arsdocendi,efimero,secundaria`; no imprimir sus valores.
2. Preparar un directorio del paquete por instancia, por ejemplo
   `/opt/actions-runners/efimero-1`, con permisos para `arsdocendi-runner`.
3. Instalar la unidad revisada y activar la cantidad de instancias aprobada
   mediante el procedimiento de administración del host. Verificar estado
   del servicio y runners online sin ejecutar trabajos privilegiados aquí.
4. Después de un job, verificar que el runner nuevo conserva `secundaria`: una
   etiqueta aplicada sólo al ID anterior no garantiza persistencia.

> **Aislamiento**: estos runners corren código de PRs en la misma VM que staging,
> no que producción tras aceptar el corte y retirar el runtime antiguo. Lo
> efímero evita el estado residual entre jobs pero no aísla a nivel host; si el riesgo
> lo amerita, mover el pool efímero a una VM aparte.

### 5c. Gates de seguridad pr-N (en GitHub, no en el host)

- Label `deploy-preview` (la aplica un maintainer) — primer gate.
- Environment `pr-preview` con **required reviewers** — segundo gate (aprobación manual).
- Nunca `pull_request_target` (evita filtrar secrets a código de forks).

El cierre del PR dispara `pr-env-teardown` **sólo en Proxmox**. Si el evento
de cierre se pierde, un operador debe ejecutar `infra/scripts/teardown.sh pr-N`
en esa VM tras comprobar el estado del PR; no existe limpieza periódica.

## 6. Verificación end-to-end

Con todo preparado, verificar un deploy productivo autorizado **sólo en Debian**,
y staging más un ciclo `pr-N`/teardown **sólo en Proxmox**. Esto requiere evidencia
del operador; una validación local no lo certifica. Comprobar por host:

```bash
docker network inspect traefik arsdocendi-datos >/dev/null
docker inspect -f '{{.State.Health.Status}}' arsdocendi-postgres
docker port arsdocendi-postgres # sin puertos publicados
docker compose ls --all        # Debian sólo prod; Proxmox staging/pr-N tras el corte
```

Luego comprobar el camino HTTP, respetando Access:

```bash
# Sólo en Proxmox, o disparar deploy-staging desde CI:
make up AMBIENTE=staging
curl -I https://staging.<dominio>                    # frontend vía túnel
curl -fsS https://staging.<dominio>/api/designaciones/ping # GET del backend (200)
```

> Seed: `spin-up.sh` siembra automáticamente con `seed.sh`
> (`scripts/seed-data/sintetico.sql`) en todo ambiente **no-prod**. Regla dura:
> `seed.sh` aborta si se le pide copiar la base de prod a un ambiente no-prod.

### Corte productivo reversible (operador)

Antes de cambiar DNS, registrar decisión explícita de **transferir base y
objetos con backup/restore verificable** o **iniciar vacío con aprobación**.
Acordar ventana de mantenimiento, controlar escrituras del origen y revisar
callbacks SSO/allowed origins/enlaces reales, sin inventar callbacks. Verificar
frontend, API, login, escrituras y adjuntos de Debian antes de aceptar el corte.

Aceptado el corte, bloquear `prod.<DOMINIO>` antes del wildcard de Proxmox y
retirar su runtime, **preservando base, volúmenes, objetos y backups hasta
autorización**. No usar teardown/reset de previews para retirar producción.
Rollback: restaurar DNS/ingress y workflow anteriores de forma coordinada;
si Debian recibió escrituras, resolver su recuperación antes de reabrir el
prod anterior. No hay sincronización ni failover que resuelva ese problema.
El procedimiento está en el [runbook de storage](../docs/operations/storage-runbook.md#corte-de-producción-de-proxmox-a-debian).

El retiro futuro de Proxmox requiere decidir el destino de staging/previews
y tratar sus datos antes de apagar wildcard/túnel/runners/VM. No se trasladan
implícitamente a Debian ni afectan la independencia de producción.

### Reconstrucción y recuperación

`spin-up.sh` ejecuta el siguiente orden en `staging` y `pr-N`: toma el lock del
ambiente, detiene el Compose project, ejecuta `drop-db.sh`, aprovisiona una base
nueva, corre `docker compose ... run --rm backend ... --migrate`, aplica
`seed.sh` y recién entonces publica con `docker compose ... up -d`. Una falla
interrumpe el script por `set -euo pipefail`, por lo que no se publica una
versión cuya migración o seed no terminó.

```bash
infra/scripts/spin-up.sh staging
docker compose -p staging -f infra/compose/compose.base.yml ps
```

Para recuperar un ambiente descartable después de una falla se corrige la
imagen o migración y se repite el mismo comando; la base se vuelve a crear
desde cero. El rollback de `prod` requiere restaurar el backup y desplegar la
versión conjunta anterior de backend y frontend. `spin-up.sh prod` no ejecuta
`down`, `drop-db.sh` ni `seed.sh`: sólo aprovisiona de forma idempotente,
migra y publica.

### Backup y restore de storage

El backup institucional se ejecuta con `infra/scripts/backup-storage.sh` y
produce `postgres.dump`, `objects/`, `manifest.json` y `checksums.sha256` en un
directorio cifrado. El restore de prueba se ejecuta con
`infra/scripts/restore-storage.sh <staging|pr-N> <backup>`; verifica los hashes,
recrea la base descartable, restaura PostgreSQL y repone los objetos mediante
S3 verificando tamaño y SHA-256. El script rechaza `prod`. El procedimiento
completo y el mapeo de secretos están en
[docs/operations/storage-runbook.md](../docs/operations/storage-runbook.md).

### Operar y reejecutar el dataset sintético

El SQL declara la versión `2026.09.1` en `public.seed_metadata` y usa UUIDs reservados, una transacción y un advisory lock. Puede ejecutarse nuevamente para restaurar las filas de ejemplo sin duplicarlas ni borrar registros ajenos:

```bash
infra/scripts/seed.sh staging
# o, dentro del flujo normal: infra/scripts/spin-up.sh staging
```

Para verificar dos despliegues consecutivos sin tocar producción, se puede
usar `staging` y una base vecina `pr-123` **sólo en Proxmox**:

```bash
infra/scripts/spin-up.sh staging
infra/scripts/spin-up.sh pr-123
docker run --rm --network arsdocendi-datos \
  -e PGPASSWORD="$PGPASSWORD" postgres:18-alpine psql \
  -h "$PGHOST" -U "$PGUSER" -d arsdocendi_staging \
  -c "INSERT INTO identity.personas (id, documento, nombre, apellido) VALUES ('eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee', 'TEST-RESET', 'Fila', 'Temporal')"
infra/scripts/spin-up.sh staging
docker run --rm --network arsdocendi-datos \
  -e PGPASSWORD="$PGPASSWORD" postgres:18-alpine psql \
  -h "$PGHOST" -U "$PGUSER" -d arsdocendi_staging \
  -c "SELECT count(*) FROM identity.personas WHERE id = 'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee'"
```

Resultado esperado: la consulta devuelve `0`, la versión `2026.09.1` y la
numeración sintética vuelven a su estado inicial, mientras la base `pr-123`
mantiene sus propias fixtures. `spin-up.sh` rechaza `prod` en los caminos de
reset y seed.

`SEED_SQL=/ruta/version.sql` selecciona explícitamente otro archivo. Nunca se debe invocar con `prod`; el script lo rechaza antes de abrir `psql`. Para usar las identidades sembradas en un Host local no productivo hay que optar además por `DevelopmentAuthentication__Enabled=true` (equivale a `DevelopmentAuthentication:Enabled` en configuración). En Compose se configura mediante `DEVELOPMENT_AUTHENTICATION_ENABLED=true`. Los bundles optimizados de staging/preview requieren también `--build-arg VITE_DEVELOPMENT_AUTH_ENABLED=true`; los workflows no productivos fijan ambos valores. Production conserva ambos opt-ins en `false`, ignora los headers de desarrollo y no publica `/api/desarrollo/identidades`.

Smoke check después de desplegar staging o un preview:

1. Abrir `/login` y pulsar **Iniciar sesión con cuenta institucional**; debe abrirse el selector sembrado.
2. Confirmar que `GET /api/desarrollo/identidades` responde `200` y lista sólo identidades elegibles.
3. Elegir una identidad y verificar que una llamada protegida envía `X-Dev-User-Id` y `X-Dev-Role-Code` y responde `200`.
4. Cambiar de rol y verificar que la solicitud siguiente usa el nuevo código.
5. En producción, confirmar que `/api/desarrollo/identidades` responde `404` y que el selector no está disponible.

## Empaquetado de la app (resuelto en el change `containerizar-app`)

El empaquetado que esta plataforma consume vive en el repo:

- `backend/Dockerfile` (+ `.dockerignore`) → imagen `arsdocendi-backend`, escucha en `8080`.
- `frontend/Dockerfile` (+ `.dockerignore` + `nginx.conf`) → imagen `arsdocendi-frontend`, sirve el SPA en `80`.
- El backend soporta el comando de migraciones one-shot que invoca `spin-up.sh`
  (`COMANDO_MIGRACIONES`, default `dotnet ArsDocendi.Host.dll --migrate`): aplica
  las migraciones de los 4 módulos y termina sin levantar el web server.

Detalle del contrato app↔infra (clave de connection string `ArsDocendi`, mecanismo
`--migrate`) en [docs/architecture/data-model.md](../docs/architecture/data-model.md).
