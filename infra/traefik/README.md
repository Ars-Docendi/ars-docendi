# Traefik — reverse proxy interno

Traefik rutea el tráfico del túnel hacia el contenedor correcto de cada ambiente
(`prod`, `staging`, `pr-N`) leyendo **labels de los contenedores** vía el Docker
provider. Cloudflare termina TLS; Traefik habla HTTP interno (D4).

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

- **No gestiona TLS/ACME**: Cloudflare emite y termina los certificados del wildcard.
- **No se expone directo a internet**: su único upstream es `cloudflared` en la red interna; por eso confía los forwarded headers de ese rango (`forwardedHeaders.trustedIPs` en `traefik.yml`).
- **No expone el socket de Docker ni Postgres** por el túnel (ver runbook, fronteras de red).
