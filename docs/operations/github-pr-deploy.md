# Despliegues temporales desde GitHub

`deploy-prod.yml` y `deploy-staging.yml` publican una sola pareja de imágenes
por SHA con **un único destino por ambiente**: producción sólo en la PC
Debian (`https://<DOMINIO>`) y staging sólo en la VM Proxmox
(`https://staging.<DOMINIO>`). `pr-env-deploy.yml` y `pr-env-teardown.yml`
operan sólo en Proxmox (`https://pr-N.<DOMINIO>`).
No hay matriz dual, LB ni failover; tampoco se comparten bases, buckets o
sesiones. El proyecto `prod`, la base `arsdocendi_prod`, la rama `main` y el
GitHub Environment `prod` conservan sus nombres. Este es el alcance objetivo,
no evidencia de despliegues reales ni de configuración externa aplicada.

## Configuración de GitHub

### Fuentes de variables y secretos

Los jobs de aplicación conservan **GitHub Actions** como fuente; no hay un
segundo inventario de credenciales de deploy en archivos locales:

| Scope                    | Variables / secretos consumidos                                                                      |
| ------------------------ | ---------------------------------------------------------------------------------------------------- |
| Repository variables     | `REGISTRO`, `DOMINIO`                                                                                |
| Repository secrets       | `PGHOST`, `PGPORT`, `PGUSER`, `PGPASSWORD`, `SEAWEEDFS_ROOT_ACCESS_KEY`, `SEAWEEDFS_ROOT_SECRET_KEY` |
| Environment `prod`       | `APP_DB_PASSWORD_PROD`                                                                               |
| Environment `staging`    | `APP_DB_PASSWORD_STAGING`                                                                            |
| Environment `pr-preview` | `APP_DB_PASSWORD_PREVIEW` (sólo en el job aprobado del Environment)                                  |

`PGHOST=arsdocendi-postgres` puede tener el mismo nombre en ambos hosts porque
resuelve dentro de su propia red Docker. **Antes de activar cada destino**,
comprobar en una ejecución controlada que las instancias PostgreSQL y SeaweedFS
de **cada** máquina aceptan las credenciales inyectadas por esos scopes. No
imprimir valores ni copiar las contraseñas entre máquinas o al repositorio.
GitHub protege la disponibilidad de los secrets del Environment con sus
required reviewers; el teardown, que no usa ese Environment, consume los
secrets compartidos de repositorio.

Las credenciales de registro del runner y del proceso permanente `cloudflared`
son de bootstrap/servicio, no contraseñas de aplicación: deben provisionarse
por un canal seguro para que esos daemons funcionen aun sin jobs de Actions.

### Environment `pr-preview`

Crear `Settings → Environments → pr-preview` y configurar **required
reviewers**. El workflow no recibe secrets del ambiente hasta que un reviewer
aprueba el deployment.

El secret de **infraestructura de previews** propio de este Environment es
`APP_DB_PASSWORD_PREVIEW` (puede haber otros de funciones ajenas). El job
también lee `REGISTRO`/`DOMINIO` como variables **del repositorio** y
`PGHOST`, `PGPORT`, `PGUSER`, `PGPASSWORD`, `SEAWEEDFS_ROOT_ACCESS_KEY` y
`SEAWEEDFS_ROOT_SECRET_KEY` como secrets **del repositorio**. No duplicarlos en
el Environment salvo que se pretenda una sustitución deliberada: si un nombre
coincide, el valor del Environment tiene precedencia en ese job.

### Secrets compartidos de repositorio

Las credenciales administrativas pertenecen al plano de infraestructura SeaweedFS.
Las dos credenciales raíz S3 se administran actualmente a nivel de repositorio;
el mismo par debe autenticar contra las instancias **independientes** de ambos
hosts. Las aplicaciones nunca reciben esta identidad:

| Nombre                      | Uso                                   |
| --------------------------- | ------------------------------------- |
| `SEAWEEDFS_ROOT_ACCESS_KEY` | identidad administrativa de SeaweedFS |
| `SEAWEEDFS_ROOT_SECRET_KEY` | secreto de esa identidad              |

El workflow de teardown usa los mismos nombres para eliminar sólo el bucket y la
identidad lógica del `pr-N`. No baja el proyecto/volumen SeaweedFS compartido ni
necesita permisos sobre otros buckets.

## Secrets legados de MinIO

Los workflows versionados consumen los nombres nuevos. Si aún aparecen los
anteriores en GitHub, mantenerlos sólo hasta verificar que no exista ningún
consumidor activo y luego retirarlos desde el repositorio; no reutilizar un
nombre legado en los jobs nuevos:

| Nombre anterior       | Nombre nuevo                |
| --------------------- | --------------------------- |
| `MINIO_ROOT_USER`     | `SEAWEEDFS_ROOT_ACCESS_KEY` |
| `MINIO_ROOT_PASSWORD` | `SEAWEEDFS_ROOT_SECRET_KEY` |

Los workflows no consumen los nombres `MINIO_*`. Una rotación de los valores
`SEAWEEDFS_ROOT_*` exige coordinar **ambos hosts** y los environments; cambiar
sólo GitHub sin actualizar las identidades S3 existentes rompe el deploy.

## Environments `staging` y `prod`

No necesitan secrets de aplicación SeaweedFS adicionales. Los scripts derivan
de forma estable, usando la credencial administrativa y el nombre del ambiente:

| Ambiente  | Access key derivada |
| --------- | ------------------- |
| `staging` | `app_staging`       |
| `prod`    | `app_prod`          |

El secret derivado se calcula con SHA-256 a partir de la credencial raíz y del
ambiente, por lo que las reejecuciones conservan la misma identidad. Una
rotación de `SEAWEEDFS_ROOT_SECRET_KEY` requiere redeploy coordinado del
ambiente para regenerar la configuración S3.

Las siguientes variables/secrets ya existían en esos workflows y no cambian:
`REGISTRO`, `DOMINIO`, `PGHOST`, `PGPORT`, `PGUSER`, `PGPASSWORD` y el password
de la base de aplicación correspondiente.

Las credenciales de aplicación son independientes por ambiente. `prod` registra
su identidad en `seaweedfs-prod`; `staging` y cada `pr-N` registran dinámicamente
la suya en el SeaweedFS compartido `seaweedfs-nonprod`. ClamAV no requiere un
secret por ambiente: todos consumen el servicio interno `clamav-shared`.

## Credencial aislada por PR

`pr-env-deploy.yml` genera una credencial de aplicación efímera para cada PR y
la publica en `GITHUB_ENV` con los nombres que espera el provisionamiento:

```text
SEAWEEDFS_APP_ACCESS_KEY_PR_<N>
SEAWEEDFS_APP_SECRET_KEY_PR_<N>
```

No agregar una credencial de aplicación compartida al Environment
`pr-preview`: cada `pr-N` tiene su propio bucket e identidad S3, aunque use el
servicio y volumen no-prod compartidos.

## Etiqueta y flujo

1. Crear la etiqueta `deploy-preview` si todavía no existe.
2. Un maintainer aplica esa etiqueta al PR.
3. El workflow recibe el evento `labeled` y pasa el primer gate.
4. Un reviewer aprueba el deployment en `pr-preview`.
5. El runner efímero construye y publica las imágenes en el registry.
6. `spin-up.sh pr-<N>` registra la identidad del PR en el SeaweedFS no-prod
   compartido, crea el bucket, la base y las fixtures, y publica
   `https://pr-<N>.<DOMINIO>`.

Los PRs que solo modifican documentación no disparan este workflow debido al
filtro de paths.

`prod` requiere el runner persistente de Debian y `staging` el de Proxmox,
con etiquetas específicas de ubicación:
`self-hosted, arsdocendi, confiable, principal` en Debian y
`self-hosted, arsdocendi, confiable, secundaria` en Proxmox. En Proxmox también
deben existir runners efímeros con `self-hosted, arsdocendi, efimero, secundaria`.
El build permanente corre en GitHub-hosted con `packages: write`; cada job de
deploy usa `packages: read` y los secrets de su Environment. Cada workflow permanente despliega únicamente en su host asignado; una
falla no dispara un deploy ni una conmutación hacia el otro host.

| Workflow          | Destino y runner                                          |
| ----------------- | --------------------------------------------------------- |
| `deploy-prod`     | Debian: `self-hosted, arsdocendi, confiable, principal`   |
| `deploy-staging`  | Proxmox: `self-hosted, arsdocendi, confiable, secundaria` |
| `pr-env-deploy`   | Proxmox: `self-hosted, arsdocendi, efimero, secundaria`   |
| `pr-env-teardown` | Proxmox: `self-hosted, arsdocendi, confiable, secundaria` |

### Activación manual de runners (operador)

1. En **Settings → Actions → Runners**, comprobar físicamente qué instalación
   corresponde a cada runner. Registrar en la PC un runner persistente con
   `arsdocendi,confiable,principal`. En Proxmox conservar el confiable con
   `arsdocendi,confiable,secundaria`; no añadir `principal` a ese runner.
2. En Proxmox, fijar `RUNNER_LABELS=arsdocendi,efimero,secundaria` en la
   configuración **persistente** del servicio que registra runners efímeros.
   La etiqueta aplicada desde la API a un runner efímero existente puede
   desaparecer cuando éste termina un job y se registra con otro ID. Confirmar
   el label en el runner **nuevo** después de un ciclo completo.
3. Verificar sólo metadatos, nunca valores de secretos:

   ```bash
   gh api repos/Ars-Docendi/ars-docendi/actions/runners \
     --jq '[.runners[] | {name,status,labels:[.labels[].name]}]'
   gh api repos/Ars-Docendi/ars-docendi/actions/secrets \
     --jq '[.secrets[].name]'
   for ambiente in prod staging pr-preview; do
     gh api "repos/Ars-Docendi/ars-docendi/environments/$ambiente/secrets" \
       --jq '[.secrets[].name]'
   done
   ```

4. Tras publicar el cambio por el proceso normal del repositorio, verificar en
   deploys controlados que `deploy-prod` ejecuta únicamente en Debian y
   `deploy-staging` únicamente en Proxmox, y que cada contenedor usa el SHA
   publicado por su respectivo build.
   Una prueba `pr-N` aprobada debe construir/desplegar en el efímero de Proxmox
   y su teardown debe ejecutarse en el confiable de Proxmox. Hasta ese ensayo,
   que las etiquetas aparezcan en GitHub **no** demuestra que el deploy funcione.

Antes de activar producción, el operador aprueba una ventana de mantenimiento
y decide transferencia verificable de base/objetos o inicio vacío autorizado.
Conservar los datos productivos anteriores de Proxmox hasta aceptar el corte;
ver [storage-runbook.md](storage-runbook.md#corte-de-producción-de-proxmox-a-debian).
Cloudflare y el registro de runners corresponden sólo al operador; no se
certifican con una validación local del repo.

## Requisitos de Proxmox para previews, fuera de GitHub

El workflow presupone que ya existen:

- runner efímero con labels `self-hosted, arsdocendi, efimero, secundaria`;
- runner confiable con labels `self-hosted, arsdocendi, confiable, secundaria` para teardown;
- Docker/Compose y las redes externas `traefik` y `arsdocendi-datos`;
- PostgreSQL accesible como `PGHOST` dentro de `arsdocendi-datos`;
- Traefik y Cloudflare Tunnel con wildcard DNS para `<pr-N>.<DOMINIO>`;
- acceso a los registros públicos de las imágenes fijadas de SeaweedFS y AWS CLI.

El workflow usa `GITHUB_TOKEN` con `packages: write` para GHCR; no hace falta
crear un PAT para publicar imágenes en el registry de GitHub.

## Seguridad

El trigger es `pull_request`, no `pull_request_target`. No aplicar
`deploy-preview` a código no revisado: después de la aprobación del Environment
el código del PR corre en el runner efímero con acceso a credenciales de
infraestructura del preview.
