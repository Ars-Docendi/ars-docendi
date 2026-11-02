# Traefik — reverse proxy interno

Cada host ejecuta su propio Traefik, conectado únicamente a su túnel y su
Docker local. La PC Debian rutea sólo `prod` en el dominio raíz; la VM Proxmox
rutea sólo `staging` y `pr-N` en sus subdominios. Traefik descubre los
ambientes por **labels de los contenedores**. Cloudflare termina TLS; Traefik habla HTTP interno (D4).
Los nombres de router pueden repetirse entre hosts porque no comparten Docker.

## Archivos

| Archivo                         | Rol                                                                |
| ------------------------------- | ------------------------------------------------------------------ |
| `traefik.yml`                   | Config estática: entrypoints, providers, API/dashboard, sin ACME.  |
| `dynamic/headers-seguridad.yml` | Config dinámica: middleware de headers aplicado a todo router web. |

## Cómo se rutea un ambiente (sin tocar Traefik)

Dar de alta un ambiente **no requiere editar este directorio**. El ruteo sale de
las labels que `compose.base.yml` pone en los contenedores del ambiente:

```
# frontend del ambiente -> todo el Host
traefik.http.routers.<AMBIENTE>-frontend.rule = Host(`<HOST_PUBLICO>`)
traefik.http.routers.<AMBIENTE>-frontend.priority = 1
traefik.http.services.<AMBIENTE>-frontend.loadbalancer.server.port = 80

# backend del ambiente -> mismo Host, solo /api, prioridad mayor
traefik.http.routers.<AMBIENTE>-backend.rule = Host(`<HOST_PUBLICO>`) && PathPrefix(`/api`)
traefik.http.routers.<AMBIENTE>-backend.priority = 10
traefik.http.services.<AMBIENTE>-backend.loadbalancer.server.port = 8080
```

Valores de `HOST_PUBLICO`: `prod` → `<DOMINIO>` en Debian; `staging` →
`staging.<DOMINIO>` y `pr-N` → `pr-N.<DOMINIO>` en Proxmox. El proyecto
Compose `prod`, la base `arsdocendi_prod` y el GitHub Environment `prod` no
se renombran. El rechazo de `prod.<DOMINIO>` se configura en el ingress de
Proxmox antes del wildcard, no mediante una redirección de Traefik.

Convenciones:

- **Nombre de router único por ambiente**: prefijo `<AMBIENTE>-` (`prod-`, `staging-`, `pr-123-`). Evita colisiones entre ambientes.
- **Prioridad**: el backend (`/api`) gana al frontend (regla más específica) con `priority` mayor.
- **`traefik.enable=true`** es obligatorio: el provider corre con `exposedByDefault=false`, así nada se publica por accidente.
- **`traefik.docker.network=traefik`**: fija la red por la que Traefik alcanza al contenedor (cada ambiente se une a la red externa compartida `traefik`).
- **Sin labels de router público** en el backend directo ni en puertos de administración: la API solo es alcanzable bajo `/api`.

## Dashboard (solo administración)

El dashboard se sirve en el entrypoint `traefik` **bindeado a `127.0.0.1:8080`**.
Nunca se expone por el ingress del túnel (3.3). Para verlo, túnel SSH sobre la red
de administración (Tailscale):

```bash
ssh -L 8080:127.0.0.1:8080 <usuario>@<app-host>
# luego abrir http://127.0.0.1:8080/dashboard/ en la máquina local
```

## Lo que Traefik NO hace

- **No gestiona TLS/ACME**: Cloudflare termina TLS para los hostnames exactos
  del dominio raíz productivo, `staging` y el wildcard de previews.
- **No balancea entre hosts ni hace failover**: cada ambiente tiene un único
  destino; una caída de Debian deja producción indisponible.
- **No se expone directo a internet**: su único upstream es `cloudflared` en la red interna; por eso confía los forwarded headers de ese rango (`forwardedHeaders.trustedIPs` en `traefik.yml`).
- **No expone el socket de Docker ni Postgres** por el túnel (ver runbook, fronteras de red).
