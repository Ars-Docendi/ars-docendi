# Infraestructura

Plataforma de deploy de Ars Docendi: **producción exclusivamente en la PC
Debian principal**, en el dominio raíz; **staging y previews pr-N únicamente
en la VM Proxmox secundaria**, en sus subdominios. No hay LB, Worker DNS,
deploy dual ni failover; tampoco se comparten datos, objetos o sesiones.
Una caída de Debian deja producción indisponible.

> **Estado:** esta es la topología objetivo del cambio
> `deploy-dual-con-failover-temporal` (nombre histórico, alcance revisado),
> no una afirmación de corte operativo. El operador configura Cloudflare y
> los runners de GitHub; el traslado de producción requiere decisión explícita
> sobre los datos previos, ventana de mantenimiento y aceptación verificable.

> Artefactos versionados en [`infra/`](../../infra/) (compose, traefik, cloudflared,
> scripts, Makefile) y [`.github/workflows/`](../../.github/workflows/).
> El runbook de provisioning (lo no automatizable) vive en [infra/README.md](../../infra/README.md).

## Ambientes

| Ambiente | Rama / trigger     | Hostname público    | Hosts        | Base local           | Datos           |
| -------- | ------------------ | ------------------- | ------------ | -------------------- | --------------- |
| Local    | dev                | localhost           | desarrollo   | `docker-compose.yml` | dev             |
| prod     | push a `main`      | `<DOMINIO>`         | sólo Debian  | `arsdocendi_prod`    | institucionales |
| staging  | push a `develop`   | `staging.<DOMINIO>` | sólo Proxmox | `arsdocendi_staging` | sintéticos      |
| pr-N     | PR abierto (gated) | `pr-<N>.<dominio>`  | sólo Proxmox | `arsdocendi_pr_<N>`  | sintéticos      |

Cada host contiene Compose projects independientes (`docker compose -p <id>`).
Debian aloja SeaweedFS prod dedicado; Proxmox aloja SeaweedFS no-prod
compartido por staging y previews. ClamAV es local a cada host. No hay enlaces
entre las redes Docker de ambas máquinas. El dominio real se parametriza por
`${DOMINIO}`; el repo usa `example.net` como placeholder.

## Topología

```text
Internet HTTPS → Cloudflare (TLS; Access según política institucional)
  ├─ DNS apex <DOMINIO> ───────────────→ túnel PC Debian
  ├─ DNS staging.<DOMINIO> ────────────→ túnel VM Proxmox
  └─ DNS *.<DOMINIO> (pr-N) ──────────→ túnel VM Proxmox

PC Debian:      cloudflared → Traefik → prod → PG + SeaweedFS prod + ClamAV
VM Proxmox:     cloudflared → Traefik → staging + pr-N
                                      → PG + SeaweedFS no-prod + ClamAV
                prod.<DOMINIO> → 404 antes del wildcard, tras el corte
               Redes, bases y objetos estrictamente locales a cada host.
```

### Modelo propio del asistente (opcional)

Con `ASISTENTE_PROVEEDOR=local`, el backend habla con un servidor OpenAI-compatible
**compartido por los ambientes**, igual que PostgreSQL: su propio Compose project
(`infra/compose/compose.llm.yml`, contenedor `arsdocendi-llm`) en la red
`arsdocendi-datos`, **sin puerto publicado** y con `--api-key`. El host necesita la GPU
(RTX 5070, 12 GB), driver NVIDIA ≥ 580 y `nvidia-container-toolkit`. `spin-up.sh` suma
`compose.asistente-local.yml` al ambiente para apuntarlo ahí con el perfil de valores
del change `asistente-proveedor-local`.

La compuerta de concurrencia es por proceso: con varios ambientes contra la misma
GPU, el límite real es la suma de sus `ASISTENTE_MAX_LLAMADAS_CONCURRENTES`, y tiene
que igualar el `--max-num-seqs` del servidor. Dimensionamiento, modelo y piloto en
[modelo-local.md](modelo-local.md). El perfil también prende las optimizaciones de
`asistente-optimizaciones-modelo-local` (esquema compacto, reparación, plantillas,
caché de consultas, streaming de la redacción, etc.); para medir una con el
evaluador se la apaga ahí.

`infra/compose/compose.llm-3070.yml` **no es infraestructura de ambientes**: es un
`llama-server` suelto, publicado sólo en `127.0.0.1:8000`, para probar el asistente
desde una PC de desarrollo con una RTX 3070 y el backend en `dotnet run`. La guía está
en [modelo-local.md §8](modelo-local.md#8-probar-en-una-rtx-3070).

## Enrutamiento (Traefik por labels)

Traefik descubre contenedores por el Docker provider leyendo labels. Dar de alta
un preview **no** requiere tocar Traefik ni Cloudflare tras configurar el
wildcard. `HOST_PUBLICO` vale `<DOMINIO>` para prod en Debian y
`<env>.<DOMINIO>` para staging/pr-N en Proxmox:

- Frontend: `Host(\`<HOST_PUBLICO>\`)` → contenedor frontend (puerto 80).
- API: `Host(\`<HOST_PUBLICO>\`) && PathPrefix(\`/api\`)` → backend (puerto 8080), prioridad mayor.

Detalle en [infra/traefik/README.md](../../infra/traefik/README.md).

## Ingreso público y TLS

- **Dos Cloudflare Tunnels**, uno por host: apex a Debian y wildcard/staging
  a Proxmox. Reutilizar el túnel existente de la VM. Debian admite sólo raíz
  y catchall 404; Proxmox rechaza `prod.<DOMINIO>` antes de staging/wildcard.
  Retirar sólo el CNAME antiguo no evita que el wildcard sirva prod.
- El operador materializa `/etc/cloudflared/config.yml` fuera del repo para
  túneles locales, o configura rutas equivalentes en el panel para túneles
  remotos. Un YAML local no configura automáticamente un túnel remoto.
- **TLS lo termina Cloudflare**; cloudflared y Traefik hablan HTTP interno. Traefik confía los forwarded headers solo del upstream interno y no gestiona ACME.
- Un smoke check `/api/designaciones/ping` comprueba API, **no** salud integral
  de DB/S3 ni failover. Las alertas no cambian el destino del tráfico.
- Detalle en [infra/cloudflared/README.md](../../infra/cloudflared/README.md).

## Fronteras de red (qué NO se expone)

- **PostgreSQL**: instancia independiente por host, sólo alcanzable por su red interna `arsdocendi-datos`. Nunca publicado al túnel.
- **SeaweedFS y ClamAV**: instancias locales por host, sólo alcanzables por `arsdocendi-datos`; SeaweedFS no tiene labels de Traefik, puertos publicados ni consola pública. En Debian prod usa `seaweedfs-prod`; en Proxmox staging y `pr-N` usan `seaweedfs-nonprod`. ClamAV usa el alias local `clamav-shared`.
- **Dashboard de Traefik / socket de Docker / puertos de admin**: solo loopback / red de administración (Tailscale), nunca por el wildcard público.
- Cada túnel expone **un solo origin por ambiente** (el frontend); la API solo bajo `/api`.

## Base de datos

Una instancia PostgreSQL independiente **en cada host**; dentro de cada una,
una base por ambiente local (D7), aislada y nombrada determinísticamente
(`arsdocendi_<env>`). La app mantiene un schema por módulo dentro de cada base.
`infra/scripts/` aprovisiona sólo el host donde corre el job. No hay replicación
de bases ni objetos; los no-prod usan datos sintéticos — **nunca** copia de prod.

### Dataset sintético y autenticación de desarrollo

`spin-up.sh` reconstruye `staging` y cada `pr-N` sólo en Proxmox: detiene el Compose project local, purga únicamente el bucket y la identidad SeaweedFS del ambiente, elimina la base local con `drop-db.sh`, aprovisiona o reutiliza la instancia SeaweedFS no-prod y ClamAV del mismo host, crea la base, corre las migraciones, ejecuta `seed.sh` y publica los servicios sólo después de completar esos pasos. Un lock **local por ambiente** serializa reintentos o ejecuciones manuales concurrentes en ese host. Después de las migraciones, `infra/scripts/seed.sh <staging|pr-N|local>` ejecuta el dataset SQL versionado `2026.09.1`. La ejecución es transaccional, serializada con advisory lock e idempotente por UUIDs reservados y upserts; reejecutarla restaura sólo sus fixtures y preserva filas ajenas. El script aborta antes de escribir si el destino es `prod` o si `SEED_FROM_DB` señala la base productiva. `SEED_SQL` permite probar otra versión explícita sin cambiar la protección.

Una falla de `down`, reset, migración o seed detiene `spin-up.sh` por
`set -euo pipefail` y evita `up -d`; la recuperación de un ambiente descartable
es corregir la versión y repetir el comando. En `prod` no se ejecutan `down`,
`drop-db.sh` ni `seed.sh`; el rollback se hace con el backup y el despliegue
conjunto de la versión anterior.

La autenticación por `X-Dev-User-Id`/`X-Dev-Role-Code` exige simultáneamente ambiente backend no productivo y `DevelopmentAuthentication__Enabled=true`. Sólo acepta usuarios presentes en `public.seed_identities`, activos y con el rol solicitado vigente. El frontend usa el servidor Vite de desarrollo o el opt-in de build `VITE_DEVELOPMENT_AUTH_ENABLED=true`; ambos lados deben estar habilitados para completar el flujo.

| Ambiente             | Frontend             | Backend                          | Resultado                                     |
| -------------------- | -------------------- | -------------------------------- | --------------------------------------------- |
| Local con `vite dev` | habilitado por `DEV` | no productivo + opt-in requerido | selector disponible si el Host fue habilitado |
| Staging              | build arg `true`     | `Staging` + opt-in `true`        | selector y headers disponibles                |
| Preview `pr-N`       | build arg `true`     | `Staging` + opt-in `true`        | selector y headers disponibles                |
| Producción           | build arg `false`    | `Production` + opt-in `false`    | ruta, esquema, selector y headers ausentes    |

Los defaults del Dockerfile, Compose y `spin-up.sh` son `false`. Además, el Host conserva la guarda independiente `!IsProduction()`: configurar el opt-in accidentalmente en Production no registra la superficie.

## Proceso de despliegue (CI/CD)

GitHub Actions construye las imágenes permanentes una vez por SHA en un runner
GitHub-hosted y despliega una única vez por ambiente: prod en el confiable
`principal` de Debian; staging en el confiable `secundaria` de Proxmox.
Los previews mantienen su runner **efímero** `secundaria` sólo en Proxmox;
su teardown usa el confiable `secundaria`:

| Workflow          | Trigger                     | Acción                                                 |
| ----------------- | --------------------------- | ------------------------------------------------------ |
| `deploy-prod`     | push a `main`               | build+push una vez y spin-up `prod` sólo en Debian     |
| `deploy-staging`  | push a `develop`            | build+push una vez y spin-up `staging` sólo en Proxmox |
| `pr-env-deploy`   | PR open/synchronize (gated) | build+push del PR + spin-up `pr-N` sólo en Proxmox     |
| `pr-env-teardown` | PR closed                   | teardown `pr-N` sólo en Proxmox                        |

Los filtros de CI separan las áreas ejecutables: Backend se selecciona por
`backend/**`, `database/**` o `global.json`, excluyendo Markdown dentro de
`backend/` y `database/`; los archivos de pnpm sólo seleccionan Frontend.
Staging y `pr-N` aplican las mismas exclusiones Markdown a sus rutas positivas
de código, infraestructura y base de datos, por lo que un cambio únicamente
documental no construye ni despliega.

Cuando Backend se selecciona, el job conserva el flujo completo de restore,
build y suite de tests. Reutiliza de forma best-effort `~/.nuget/packages` con
una clave que incluye runner, SDK, proyectos `.csproj` y lockfiles; un miss o
fallo del cache no impide el restore normal. No se usa cache remoto Docker en
estos workflows.

Los filtros no cambian el aislamiento: cada deploy construye las imágenes una
vez, las etiqueta por SHA y ejecuta `spin-up` sólo en el destino asignado.
No hay matriz de ubicaciones ni un segundo intento de deploy en otro host. Los
gates de maintainer y secretos siguen vigentes para `pr-N`, que conserva su
Compose project, base y storage únicamente en la VM.

**Seguridad del flujo pr-N** (D8):

- Trigger `pull_request` (nunca `pull_request_target`).
- Doble gate de maintainer: label `deploy-preview` + GitHub Environment `pr-preview` con required reviewers. El código del PR no corre con secrets hasta pasar el gate.
- Runners efímeros: estado limpio por job, descartado tras el job.

## Gestión de secretos

- **Local**: `dotnet user-secrets` (backend) + `.env.local` (frontend, gitignored).
- **Deploy/CI**: `PGHOST`, `PGPORT`, `PGUSER`, `PGPASSWORD` y credenciales
  administrativas SeaweedFS provienen de GitHub repository secrets; las claves
  de app de `prod`, `staging` y `pr-preview` provienen de sus respectivos
  Environments. `REGISTRO`/`DOMINIO` son GitHub repository variables. Los jobs
  inyectan secrets en runtime; nunca se versionan ni se hornean en imágenes.
- **Servicios permanentes**: los runners y `cloudflared` requieren credenciales
  protegidas en cada máquina para funcionar fuera de un job; son distintas de
  las credenciales de deploy de la aplicación.
- Lista documentada en [`.env.example`](../../.env.example) (raíz) y el runbook.

## Estrategia de backup

A definir SLA con UNLaM. Respaldar producción en Debian como una unidad de
base **y objetos**. Conservar por separado el backup y datos del prod anterior
en Proxmox hasta aceptar el corte y autorizar su tratamiento.

- `infra/scripts/backup-storage.sh prod` en Debian (dump PostgreSQL + objetos
  S3), cifrado antes de salir del nodo e identificado por host de origen.
- Retención: 7 diarios + 4 semanales + 6 mensuales.
- Prueba mensual de restore en un entorno aislado y descartable autorizado;
  no copiar datos productivos a staging/previews públicos o compartidos con PRs.
- Los ambientes staging/pr-N **no** se respaldan (son descartables, datos sintéticos).

## Checklist de seguridad (ambos hosts)

- [ ] Firewall: cerrar todo entrante salvo SSH/administración; el ingreso público es solo por el túnel (saliente).
- [ ] SSH: solo keys, sin password auth.
- [ ] `unattended-upgrades` para parches de seguridad.
- [ ] Postgres: bind a la red interna de Docker, nunca al host público.
- [ ] Docker socket: no expuesto; dashboard de Traefik solo loopback.
- [ ] Deploy permanente en runners confiables por ubicación; código de PR
      sólo en efímeros `secundaria` con estado limpio por job.
- [ ] Logrotate / límites de logs de Docker para no llenar disco.
- [ ] Healthcheck externo apuntando a `https://<DOMINIO>/api/designaciones/ping`.

## Monitoreo (mínimo viable)

- **Logs**: backend con Serilog → stdout → `docker logs` / `journalctl`. Scripts de infra con logging estructurado (`ts=… nivel=… clave=valor`).
- **Métricas**: endpoints `/api/<modulo>/ping` por ambiente.
- **Alertas**: uptime monitor externo sobre el ping de prod.

## Runbook

El provisioning manual (PC Debian, VM Proxmox, Postgres, Cloudflare Tunnel +
Access, runners y seed) está en [infra/README.md](../../infra/README.md).
La operación de SeaweedFS, backup, restore y recuperación está en
[docs/operations/storage-runbook.md](../operations/storage-runbook.md).

### Corte de producción y retiro futuro de Proxmox

El operador decide transferencia verificable de PostgreSQL/objetos o inicio
vacío aprobado **antes** del corte. Planificar ventana de mantenimiento y
controlar escrituras; revisar callbacks SSO, allowed origins y enlaces reales,
sin inventar URLs de callback. Conservar `prod`, `arsdocendi_prod` y el
GitHub Environment `prod` como identificadores internos. Tras validar raíz,
API, login y adjuntos en Debian, bloquear la URL antigua en Proxmox y retirar
su runtime sin borrar datos hasta autorización. Rollback: restaurar DNS/ingress
y workflow anteriores, resolviendo primero cualquier escritura nueva en Debian.
Ver el [runbook de corte](../operations/storage-runbook.md#corte-de-producción-de-proxmox-a-debian).

El retiro futuro de la VM requiere decidir el destino de staging/previews y el
tratamiento de datos. No los traslada a Debian. Sólo entonces retirar wildcard,
túnel y runners secundarios; producción en Debian no depende de ellos.
