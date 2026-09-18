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

Las credenciales root pertenecen al servicio MinIO común, no a una base
individual. Crear estos secrets a nivel de repositorio u organización y
restringirlos a este repositorio:

| Nombre                | Uso                             |
| --------------------- | ------------------------------- |
| `MINIO_ROOT_USER`     | usuario root del MinIO privado  |
| `MINIO_ROOT_PASSWORD` | password root del MinIO privado |

El workflow de teardown usa estos mismos secrets porque corre fuera del
Environment `pr-preview` y necesita purgar únicamente el bucket `pr-N`.

### Environments `staging` y `prod`

No necesitan secrets de aplicación MinIO adicionales. Los scripts derivan de
forma estable, usando las credenciales root y el nombre del ambiente:

| Ambiente  | Access key derivada |
| --------- | ------------------- |
| `staging` | `app_staging`       |
| `prod`    | `app_prod`          |

El secret generado se deriva de `MINIO_ROOT_PASSWORD` y del ambiente, por lo
que las reejecuciones conservan la misma credencial. Una rotación de
`MINIO_ROOT_PASSWORD` requiere una rotación coordinada de la credencial de
aplicación en MinIO.

Las siguientes variables/secrets ya existían en esos workflows y no cambian:
`REGISTRO`, `DOMINIO`, `PGHOST`, `PGPORT`, `PGUSER`, `PGPASSWORD` y el password
de la base de aplicación correspondiente.

## Credencial aislada por PR

`pr-env-deploy.yml` genera una credencial de aplicación efímera para cada PR y
la publica en `GITHUB_ENV` con los nombres que espera el provisionamiento:

```text
MINIO_APP_ACCESS_KEY_PR_<N>
MINIO_APP_SECRET_KEY_PR_<N>
```

No agregar una credencial de aplicación compartida al Environment
`pr-preview`: una misma identidad para varios PRs permitiría que la política
del último preview sobrescriba el aislamiento de los anteriores.

## Etiqueta y flujo

1. Crear la etiqueta `deploy-preview` si todavía no existe.
2. Un maintainer aplica esa etiqueta al PR.
3. El workflow recibe el evento `labeled` y pasa el primer gate.
4. Un reviewer aprueba el deployment en `pr-preview`.
5. El runner efímero construye y publica las imágenes en el registry.
6. `spin-up.sh pr-<N>` crea la base, el bucket, las políticas, las fixtures y
   publica `https://pr-<N>.<DOMINIO>`.

Los PRs que solo modifican documentación no disparan este workflow debido al
filtro de paths.

## Requisitos del host, fuera de GitHub

El workflow presupone que ya existen:

- runner efímero con labels `self-hosted, arsdocendi, efimero`;
- runner confiable con labels `self-hosted, arsdocendi, confiable` para teardown;
- Docker/Compose y las redes externas `traefik` y `arsdocendi-datos`;
- PostgreSQL accesible como `PGHOST` dentro de `arsdocendi-datos`;
- Traefik y Cloudflare Tunnel con wildcard DNS para `<pr-N>.<DOMINIO>`;
- reaper de ambientes huérfanos.

El workflow usa `GITHUB_TOKEN` con `packages: write` para GHCR; no hace falta
crear un PAT para publicar imágenes en el registry de GitHub.

## Seguridad

El trigger es `pull_request`, no `pull_request_target`. No aplicar
`deploy-preview` a código no revisado: después de la aprobación del Environment
el código del PR corre en el runner efímero con acceso a credenciales de
infraestructura del preview.
