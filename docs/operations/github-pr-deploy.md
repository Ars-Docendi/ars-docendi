# Deploy de ambientes efímeros desde GitHub

Esta guía cubre únicamente la configuración de GitHub necesaria para que
`.github/workflows/pr-env-deploy.yml` cree un ambiente `pr-N`.

## Configuración de GitHub

### Environment `pr-preview`

Crear `Settings → Environments → pr-preview` y configurar **required
reviewers**. El workflow no recibe secrets del ambiente hasta que un reviewer
aprueba el deployment.

**Variables del environment**:

| Nombre     | Valor                                                    |
| ---------- | -------------------------------------------------------- |
| `REGISTRO` | `ghcr.io/<organización>` o el registry configurado       |
| `DOMINIO`  | dominio base de los ambientes, por ejemplo `example.net` |

**Secrets del environment**:

| Nombre                    | Uso                                                 |
| ------------------------- | --------------------------------------------------- |
| `PGHOST`                  | normalmente `arsdocendi-postgres`                   |
| `PGPORT`                  | normalmente `5432`                                  |
| `PGUSER`                  | usuario admin que crea y elimina bases de ambientes |
| `PGPASSWORD`              | password del usuario anterior                       |
| `APP_DB_PASSWORD_PREVIEW` | password del usuario `app_pr_N`                     |

### Secrets de repositorio u organización

Las credenciales administrativas pertenecen al plano de infraestructura SeaweedFS.
El mismo root secret puede operar los dos scopes porque prod y no-prod tienen
proyectos y volúmenes distintos; las aplicaciones nunca reciben esta identidad.
Crear estos secrets a nivel de repositorio u organización y restringirlos a este
repositorio:

| Nombre                      | Uso                                   |
| --------------------------- | ------------------------------------- |
| `SEAWEEDFS_ROOT_ACCESS_KEY` | identidad administrativa de SeaweedFS |
| `SEAWEEDFS_ROOT_SECRET_KEY` | secreto de esa identidad              |

El workflow de teardown usa los mismos nombres para eliminar sólo el bucket y la
identidad lógica del `pr-N`. No baja el proyecto/volumen SeaweedFS compartido ni
necesita permisos sobre otros buckets.

## Migración desde los secretos MinIO

Si el repositorio todavía tiene estos secretos, renombrarlos en cada scope
relevante —`prod`, `staging`, `pr-preview` o repository/org— antes del primer
deploy SeaweedFS:

| Nombre anterior       | Nombre nuevo                |
| --------------------- | --------------------------- |
| `MINIO_ROOT_USER`     | `SEAWEEDFS_ROOT_ACCESS_KEY` |
| `MINIO_ROOT_PASSWORD` | `SEAWEEDFS_ROOT_SECRET_KEY` |

Los workflows no consumen los nombres `MINIO_*`. El valor puede reutilizarse
como credencial inicial si cumple la política de SeaweedFS; la rotación debe
hacerse coordinadamente con un nuevo provisionamiento de los ambientes que
usen esas credenciales.

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

## Requisitos del host, fuera de GitHub

El workflow presupone que ya existen:

- runner efímero con labels `self-hosted, arsdocendi, efimero`;
- runner confiable con labels `self-hosted, arsdocendi, confiable` para teardown;
- Docker/Compose y las redes externas `traefik` y `arsdocendi-datos`;
- PostgreSQL accesible como `PGHOST` dentro de `arsdocendi-datos`;
- Traefik y Cloudflare Tunnel con wildcard DNS para `<pr-N>.<DOMINIO>`;
- reaper de ambientes huérfanos;
- acceso a los registros públicos de las imágenes fijadas de SeaweedFS y AWS CLI.

El workflow usa `GITHUB_TOKEN` con `packages: write` para GHCR; no hace falta
crear un PAT para publicar imágenes en el registry de GitHub.

## Seguridad

El trigger es `pull_request`, no `pull_request_target`. No aplicar
`deploy-preview` a código no revisado: después de la aprobación del Environment
el código del PR corre en el runner efímero con acceso a credenciales de
infraestructura del preview.
